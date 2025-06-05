using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Chrysalis.Cbor.Extensions.Cardano.Core.Common;
using Chrysalis.Cbor.Extensions.Cardano.Core.Transaction;
using Chrysalis.Cbor.Types.Cardano.Core;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using Chrysalis.Tx.Extensions;
using Chrysalis.Tx.Models;
using Chrysalis.Tx.Models.Cbor;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Models.Template;
using PaylKoyn.Data.Responses;
using PaylKoyn.Data.Services;
using PaylKoyn.ImageGen.Data;

namespace PaylKoyn.ImageGen.Services;

public class MintingService(
    IConfiguration configuration,
    IDbContextFactory<MintDbContext> dbContextFactory,
    ICardanoDataProvider cardanoDataProvider,
    NftRandomizerService nftRandomizerService,
    WalletService walletService,
    TransactionService transactionService,
    TransactionTemplateService transactionTemplateService,
    IHttpClientFactory httpClientFactory,
    ILogger<MintingService> logger
)
{
    private const int MetadataChunkSize = 64;
    private const ulong Cip25MetadataLabel = 721;
    private const int MaxUploadRetries = 5;
    private const int BaseRetryDelayMs = 1000;
    private const string ImageContentType = "image/png";
    private const string AdaFsProtocol = "adafs://";

    private readonly HttpClient _nodeClient = httpClientFactory.CreateClient("PaylKoynNodeClient");
    private readonly TimeSpan _paymentExpirationTime = TimeSpan.FromMinutes(configuration.GetValue<int>("Minting:ExpirationMinutes", 30));
    private readonly TimeSpan _utxoCheckInterval = TimeSpan.FromSeconds(configuration.GetValue<int>("Minting:GetUtxosIntervalSeconds", 10));
    private readonly ulong _uploadRevenueFee = configuration.GetValue<ulong>("Minting:UploadRevenueFee", 2_000_000UL);
    private readonly string _nftBaseName = configuration.GetValue("NftBaseName", "Payl Koyn NFT");
    private readonly string _mintingSeed = configuration.GetValue<string>("Seed")
        ?? throw new ArgumentNullException("Minting seed is not configured");
    private readonly ulong _invalidHereafter = configuration.GetValue<ulong?>("Minting:InvalidHereafter")
        ?? throw new ArgumentNullException("Invalid hereafter is not configured");

    public async Task<MintRequest> WaitForPaymentAsync(string requestId, ulong requiredAmount)
    {
        using MintDbContext dbContext = dbContextFactory.CreateDbContext();
        MintRequest mintRequest = await GetMintRequestAsync(dbContext, requestId);

        ValidateRequestStatus(mintRequest, MintStatus.Waiting, requestId);

        Stopwatch stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < _paymentExpirationTime)
        {
            (bool IsReceived, ulong ReceivedAmount) = await CheckForPaymentAsync(requestId, requiredAmount);
            if (IsReceived)
            {
                return await ProcessPaymentReceivedAsync(dbContext, mintRequest);
            }

            LogPaymentStatus(requestId, requiredAmount, ReceivedAmount);
            await Task.Delay(_utxoCheckInterval);
        }

        return await HandlePaymentTimeoutAsync(dbContext, mintRequest, requestId);
    }

    public async Task<MintRequest> RequestImageUploadAsync(string requestId)
    {
        using MintDbContext dbContext = dbContextFactory.CreateDbContext();
        MintRequest mintRequest = await GetMintRequestAsync(dbContext, requestId);

        ValidateRequestStatus(mintRequest, MintStatus.Paid, requestId);

        string uploadRequestId = await RequestUploadSlotAsync();
        ulong uploadFee = await CalculateUploadFeeAsync(mintRequest.Image!.Length);

        await SendUploadPaymentAsync(requestId, uploadRequestId, uploadFee);

        return await UpdateRequestWithUploadInfoAsync(dbContext, mintRequest, uploadRequestId, uploadFee);
    }

    public async Task<MintRequest> UploadImageAsync(string requestId)
    {
        using MintDbContext dbContext = dbContextFactory.CreateDbContext();
        MintRequest mintRequest = await GetMintRequestAsync(dbContext, requestId);

        ValidateRequestStatus(mintRequest, MintStatus.Processing, requestId);

        try
        {
            UploadFileResponse? uploadResponse = await PerformImageUploadAsync(mintRequest);
            return await UpdateRequestAfterUploadAsync(dbContext, mintRequest, uploadResponse?.AdaFsId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upload image for request ID: {Id}", requestId);
            return await UpdateRequestStatusAsync(dbContext, mintRequest);
        }
    }

    public async Task<MintRequest> MintNftAsync(
        string requestId,
        string policyId,
        string assetName,
        string asciiAssetName,
        string rewardAddress
    )
    {
        using MintDbContext dbContext = dbContextFactory.CreateDbContext();
        MintRequest mintRequest = await GetMintRequestAsync(dbContext, requestId);

        ValidateRequestStatus(mintRequest, MintStatus.Uploaded, requestId);

        Metadata metadata = CreateNftMetadata(
            policyId,
            assetName,
            mintRequest.AdaFsId!,
            DeserializeNftTraits(mintRequest.NftMetadata!),
            asciiAssetName
        );

        try
        {
            string txHash = await ExecuteMintTransactionAsync(requestId, rewardAddress, metadata, assetName);
            return await UpdateRequestAfterMintingAsync(dbContext, mintRequest, assetName, policyId, txHash);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to mint NFT for request ID: {Id}", requestId);
            return await UpdateRequestStatusAsync(dbContext, mintRequest);
        }
    }

    public MintRequest GenerateMetadata(MintRequest mintRequest)
    {
        IEnumerable<NftTrait> randomTraits = nftRandomizerService.GenerateRandomTraits();
        byte[] image = nftRandomizerService.GenerateNftImage(randomTraits);

        mintRequest.NftMetadata = JsonSerializer.Serialize(randomTraits);
        mintRequest.Image = image;

        logger.LogInformation("Metadata generated for request ID: {Id}", mintRequest.Id);
        return mintRequest;
    }

    public Metadata CreateNftMetadata(
        string policyId,
        string assetName,
        string adaFsId,
        List<NftTrait> traits,
        string? nftName = null,
        string? description = null
    )
    {
        TransactionMetadatum imageUrl = CreateAdaFsMetadata($"{AdaFsProtocol}{adaFsId}");
        Dictionary<TransactionMetadatum, TransactionMetadatum> assetMetadata = BuildAssetMetadata(nftName ?? assetName, imageUrl, description, traits);
        Dictionary<TransactionMetadatum, TransactionMetadatum> policyMap = new()
        {
            { new MetadataText(assetName), new MetadatumMap(assetMetadata) }
        };

        Dictionary<TransactionMetadatum, TransactionMetadatum> rootStructure = new()
        {
            { new MetadataText(policyId), new MetadatumMap(policyMap) }
        };

        Dictionary<ulong, TransactionMetadatum> labeledMetadata = new()
        {
            { Cip25MetadataLabel, new MetadatumMap(rootStructure) }
        };

        return new Metadata(labeledMetadata);
    }

    public async Task<(bool success, IEnumerable<ResolvedInput> utxos)> TryGetUtxosAsync(string address)
    {
        try
        {
            List<ResolvedInput> utxos = await cardanoDataProvider.GetUtxosAsync([address]);
            return (utxos.Count != 0, utxos);
        }
        catch
        {
            return (false, Enumerable.Empty<ResolvedInput>());
        }
    }

    private static async Task<MintRequest> GetMintRequestAsync(MintDbContext dbContext, string requestId)
    {
        return await dbContext.MintRequests.FirstOrDefaultAsync(m => m.Id == requestId)
            ?? throw new ArgumentException($"Mint request with ID {requestId} not found.");
    }

    private static void ValidateRequestStatus(MintRequest mintRequest, MintStatus expectedStatus, string requestId)
    {
        if (mintRequest.Status != expectedStatus)
            throw new InvalidOperationException($"Mint request with ID {requestId} is not in {expectedStatus} status. Current status: {mintRequest.Status}");
    }

    private async Task<(bool IsReceived, ulong ReceivedAmount)> CheckForPaymentAsync(string requestId, ulong requiredAmount)
    {
        (bool isSuccess, IEnumerable<ResolvedInput> utxos) = await TryGetUtxosAsync(requestId);
        if (!isSuccess || !utxos.Any())
            return (false, 0);

        ulong totalAmount = utxos.Aggregate(0UL, (sum, utxo) => sum + utxo.Output.Amount().Lovelace());
        logger.LogInformation("Found {UtxoCount} UTXOs with total amount: {TotalAmount} lovelace for request ID: {Id}",
            utxos.Count(), totalAmount, requestId);

        return (totalAmount >= requiredAmount, totalAmount);
    }

    private async Task<MintRequest> ProcessPaymentReceivedAsync(MintDbContext dbContext, MintRequest mintRequest)
    {
        mintRequest = GenerateMetadata(mintRequest);
        mintRequest.Status = MintStatus.Paid;
        mintRequest.UpdatedAt = DateTime.UtcNow;

        dbContext.MintRequests.Update(mintRequest);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Payment received for request ID: {Id}", mintRequest.Id);
        return mintRequest;
    }

    private async Task<MintRequest> HandlePaymentTimeoutAsync(MintDbContext dbContext, MintRequest mintRequest, string requestId)
    {
        logger.LogWarning("Payment for request ID: {Id} not received within the expiration time of {ExpirationTime} minutes.",
            requestId, _paymentExpirationTime.TotalMinutes);

        mintRequest.Status = MintStatus.Failed;
        mintRequest.UpdatedAt = DateTime.UtcNow;
        dbContext.MintRequests.Update(mintRequest);
        await dbContext.SaveChangesAsync();

        return mintRequest;
    }

    private void LogPaymentStatus(string requestId, ulong requiredAmount, ulong receivedAmount)
    {
        if (receivedAmount > 0)
        {
            logger.LogWarning("Insufficient payment for request ID: {Id}. Required: {RequiredAmount} lovelace, but received: {ReceivedAmount} lovelace",
                requestId, requiredAmount, receivedAmount);
        }
        else
        {
            logger.LogInformation("No UTXOs found for address: {Address}. Retrying in {Interval} seconds...",
                requestId, _utxoCheckInterval.TotalSeconds);
        }
    }

    private async Task<string> RequestUploadSlotAsync()
    {
        HttpResponseMessage response = await _nodeClient.PostAsync("upload/request", null);
        UploadRequestResponse? uploadResponse = await response.Content.ReadFromJsonAsync<UploadRequestResponse>();
        return uploadResponse!.Id;
    }

    private async Task<ulong> CalculateUploadFeeAsync(int imageLength)
    {
        Chrysalis.Network.Cbor.LocalStateQuery.ProtocolParams protocolParams = await cardanoDataProvider.GetParametersAsync();
        return transactionService.CalculateFee(imageLength, _uploadRevenueFee, (ulong)protocolParams.MaxTransactionSize!);
    }

    private async Task SendUploadPaymentAsync(string requestId, string uploadRequestId, ulong uploadFee)
    {
        Chrysalis.Wallet.Models.Keys.PrivateKey? privateKey = await walletService.GetPrivateKeyByAddressAsync(requestId);
        TransactionTemplate<TransferParams> transferTemplate = transactionService.Transfer(cardanoDataProvider);
        TransferParams transferParams = new(requestId, uploadRequestId, uploadFee);

        Transaction transaction = await transferTemplate(transferParams);
        Transaction signedTransaction = transaction.Sign(privateKey!);

        string txHash = await cardanoDataProvider.SubmitTransactionAsync(signedTransaction);
        logger.LogInformation("Transaction submitted successfully for request ID: {Id}. TxHash: {TxHash}", requestId, txHash);
    }

    private async Task<MintRequest> UpdateRequestWithUploadInfoAsync(
        MintDbContext dbContext,
        MintRequest mintRequest,
        string uploadRequestId,
        ulong uploadFee)
    {
        mintRequest.Status = MintStatus.Processing;
        mintRequest.UploadPaymentAddress = uploadRequestId;
        mintRequest.UploadPaymentAmount = uploadFee;
        mintRequest.UpdatedAt = DateTime.UtcNow;

        logger.LogInformation("Image upload fee payment initiated for request ID: {Id}", mintRequest.Id);

        dbContext.MintRequests.Update(mintRequest);
        await dbContext.SaveChangesAsync();
        return mintRequest;
    }

    private async Task<UploadFileResponse?> PerformImageUploadAsync(MintRequest mintRequest)
    {
        string fileName = $"{_nftBaseName.ToLowerInvariant()}-{mintRequest.NftNumber}.png";
        using MultipartFormDataContent formData = new()
        {
            { new StringContent(mintRequest.UploadPaymentAddress!), "id" },
            { new StringContent(fileName), "name" },
            { new StringContent(ImageContentType), "contentType" },
            { new ByteArrayContent(mintRequest.Image!), "file", fileName }
        };

        return await UploadWithRetryAsync(formData);
    }

    private async Task<UploadFileResponse?> UploadWithRetryAsync(MultipartFormDataContent formData)
    {
        for (int attempt = 0; attempt <= MaxUploadRetries; attempt++)
        {
            try
            {
                HttpResponseMessage response = await _nodeClient.PostAsync("upload/receive", formData);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<UploadFileResponse>();
            }
            catch (HttpRequestException ex) when (attempt < MaxUploadRetries)
            {
                int delayMs = BaseRetryDelayMs * (int)Math.Pow(2, attempt);
                logger.LogWarning("Upload attempt {Attempt} failed, retrying in {DelayMs}ms. Error: {Error}",
                    attempt + 1, delayMs, ex.Message);
                await Task.Delay(delayMs);
            }
        }

        throw new InvalidOperationException($"Failed to upload after {MaxUploadRetries + 1} attempts");
    }

    private async Task<MintRequest> UpdateRequestAfterUploadAsync(MintDbContext dbContext, MintRequest mintRequest, string? adaFsId)
    {
        mintRequest.AdaFsId = adaFsId;
        mintRequest.UpdatedAt = DateTime.UtcNow;
        mintRequest.Status = MintStatus.Uploaded;

        logger.LogInformation("Image uploaded successfully for request ID: {Id}", mintRequest.Id);

        dbContext.MintRequests.Update(mintRequest);
        await dbContext.SaveChangesAsync();
        return mintRequest;
    }

    private static async Task<MintRequest> UpdateRequestStatusAsync(MintDbContext dbContext, MintRequest mintRequest)
    {
        mintRequest.UpdatedAt = DateTime.UtcNow;
        dbContext.MintRequests.Update(mintRequest);
        await dbContext.SaveChangesAsync();
        return mintRequest;
    }

    private static List<NftTrait> DeserializeNftTraits(string nftMetadata)
    {
        return JsonSerializer.Deserialize<List<NftTrait>>(nftMetadata)!;
    }

    private async Task<string> ExecuteMintTransactionAsync(
        string requestId,
        string rewardAddress,
        Metadata metadata,
        string assetName)
    {
        TransactionTemplate<MintNftParams> nftTemplate = transactionTemplateService.MintNftTemplate();
        Chrysalis.Wallet.Models.Addresses.Address mintingAddress = walletService.GetWalletAddress(_mintingSeed, 0);

        MintNftParams mintNftParams = new(
            requestId,
            await GetUserAddressAsync(requestId),
            rewardAddress,
            mintingAddress.ToBech32(),
            _invalidHereafter,
            metadata,
            new Dictionary<string, int> { [assetName] = 1 }
        );

        Transaction transaction = await nftTemplate(mintNftParams);
        Transaction signedTransaction = await SignTransactionWithBothKeysAsync(transaction, requestId);

        return await cardanoDataProvider.SubmitTransactionAsync(signedTransaction);
    }

    private async Task<string> GetUserAddressAsync(string requestId)
    {
        using MintDbContext dbContext = dbContextFactory.CreateDbContext();
        MintRequest mintRequest = await GetMintRequestAsync(dbContext, requestId);
        return mintRequest.UserAddress;
    }

    private async Task<Transaction> SignTransactionWithBothKeysAsync(Transaction transaction, string requestId)
    {
        Chrysalis.Wallet.Models.Keys.PrivateKey? userPrivateKey = await walletService.GetPrivateKeyByAddressAsync(requestId);
        Chrysalis.Wallet.Models.Keys.PrivateKey mintingPrivateKey = walletService.GetPaymentPrivateKey(_mintingSeed, 0);

        Transaction signedByUser = transaction.Sign(userPrivateKey!);
        return signedByUser.Sign(mintingPrivateKey);
    }

    private static async Task<MintRequest> UpdateRequestAfterMintingAsync(
        MintDbContext dbContext,
        MintRequest mintRequest,
        string assetName,
        string policyId,
        string txHash)
    {
        mintRequest.AssetName = assetName;
        mintRequest.PolicyId = policyId;
        mintRequest.MintTxHash = txHash;
        mintRequest.Status = MintStatus.NftSent;
        mintRequest.UpdatedAt = DateTime.UtcNow;

        dbContext.MintRequests.Update(mintRequest);
        await dbContext.SaveChangesAsync();
        return mintRequest;
    }

    private static Dictionary<TransactionMetadatum, TransactionMetadatum> BuildAssetMetadata(
        string nftName,
        TransactionMetadatum imageUrl,
        string? description,
        List<NftTrait> traits)
    {
        Dictionary<TransactionMetadatum, TransactionMetadatum> assetMetadata = new()
        {
            { new MetadataText("name"), new MetadataText(nftName) },
            { new MetadataText("image"), imageUrl },
            { new MetadataText("mediaType"), new MetadataText(ImageContentType) }
        };

        if (!string.IsNullOrEmpty(description))
            assetMetadata.Add(new MetadataText("description"), new MetadataText(description));

        traits.ForEach(trait => assetMetadata.Add(new MetadataText(trait.Category), new MetadataText(trait.TraitName)));

        return assetMetadata;
    }

    private static TransactionMetadatum CreateAdaFsMetadata(string url)
    {
        if (string.IsNullOrEmpty(url))
            return new MetadataText("");

        byte[] urlBytes = Encoding.UTF8.GetBytes(url);

        if (urlBytes.Length <= MetadataChunkSize)
            return new MetadataText(url);

        return SplitIntoChunks(url, urlBytes);
    }

    private static TransactionMetadatum SplitIntoChunks(string originalText, byte[] textBytes)
    {
        List<TransactionMetadatum> chunks = new();
        int position = 0;

        while (position < textBytes.Length)
        {
            int chunkSize = Math.Min(MetadataChunkSize, textBytes.Length - position);
            string chunkText = ExtractValidUtf8Chunk(textBytes, position, chunkSize);

            if (string.IsNullOrEmpty(chunkText))
                break;

            chunks.Add(new MetadataText(chunkText));
            position += Encoding.UTF8.GetByteCount(chunkText);
        }

        return new MetadatumList(chunks);
    }

    private static string ExtractValidUtf8Chunk(byte[] textBytes, int position, int maxChunkSize)
    {
        for (int chunkSize = maxChunkSize; chunkSize > 0; chunkSize--)
        {
            try
            {
                return Encoding.UTF8.GetString(textBytes, position, chunkSize);
            }
            catch (ArgumentException)
            {
                continue;
            }
        }
        return "";
    }
}