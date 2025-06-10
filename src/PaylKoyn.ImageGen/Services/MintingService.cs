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
using Chrysalis.Wallet.Models.Keys;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Models.Template;
using PaylKoyn.Data.Responses;
using PaylKoyn.Data.Services;
using PaylKoyn.ImageGen.Data;
using WalletAddress = Chrysalis.Wallet.Models.Addresses.Address;

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
    private readonly TimeSpan _requestExpirationTime = TimeSpan.FromMinutes(
        configuration.GetValue("RequestExpirationMinutes", 30));


    public async Task<List<MintRequest>> GetActiveRequestsByStatusAsync(
        MintStatus status,
        int limit = 10,
    CancellationToken cancellationToken = default)
    {
        using MintDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        DateTime cutoffTime = DateTime.UtcNow - _requestExpirationTime;

        return await dbContext.MintRequests
            .Where(request => request.Status == status)
            .Where(request => request.CreatedAt >= cutoffTime)
            .OrderBy(request => request.UpdatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> MarkExpiredRequestsAsFailedAsync(
        MintStatus status,
        CancellationToken cancellationToken = default)
    {
        using MintDbContext dbContext = dbContextFactory.CreateDbContext();

        DateTime cutoffTime = DateTime.UtcNow - _requestExpirationTime;

        List<MintRequest> expiredRequests = await dbContext.MintRequests
            .Where(request => request.Status == status)
            .Where(request => request.CreatedAt < cutoffTime)
            .ToListAsync(cancellationToken);

        if (expiredRequests.Count > 0)
        {
            logger.LogWarning("Marking {Count} expired {Status} requests as failed (older than {Minutes} minutes)",
                expiredRequests.Count, status, _requestExpirationTime.TotalMinutes);

            foreach (MintRequest? expiredRequest in expiredRequests)
            {
                expiredRequest.LastValidStatus = expiredRequest.Status;
                expiredRequest.Status = MintStatus.Failed;
                expiredRequest.UpdatedAt = DateTime.UtcNow;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return expiredRequests.Count;
    }

    public async Task<List<MintRequest>> GetActiveRequestsWithCleanupAsync(
        MintStatus status,
        int limit = 10,
    CancellationToken cancellationToken = default)
    {
        await MarkExpiredRequestsAsFailedAsync(status, cancellationToken);
        return await GetActiveRequestsByStatusAsync(status, limit, cancellationToken);
    }

    public async Task<MintRequest> WaitForPaymentAsync(int id, ulong requiredAmount)
    {
        using MintDbContext dbContext = dbContextFactory.CreateDbContext();
        MintRequest mintRequest = await GetMintRequestAsync(dbContext, id);

        ValidateRequestStatus(mintRequest, MintStatus.Waiting, id);

        Stopwatch stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < _paymentExpirationTime)
        {
            (bool IsReceived, ulong ReceivedAmount) = await CheckForPaymentAsync(mintRequest.Address!, requiredAmount);
            if (IsReceived)
            {
                return await ProcessPaymentReceivedAsync(dbContext, mintRequest);
            }

            LogPaymentStatus(mintRequest.Address!, requiredAmount, ReceivedAmount);
            await Task.Delay(_utxoCheckInterval);
        }

        return await HandlePaymentTimeoutAsync(dbContext, mintRequest, id);
    }

    public async Task<MintRequest> RequestImageUploadAsync(int id)
    {
        using MintDbContext dbContext = dbContextFactory.CreateDbContext();
        MintRequest mintRequest = await GetMintRequestAsync(dbContext, id);

        ValidateRequestStatus(mintRequest, MintStatus.Paid, id);

        string uploadRequestId = await RequestUploadSlotAsync(mintRequest.UserAddress);
        ulong uploadFee = await CalculateUploadFeeAsync(mintRequest.Image!.Length);

        await SendUploadPaymentAsync(mintRequest.Address!, uploadRequestId, uploadFee);

        return await UpdateRequestWithUploadInfoAsync(dbContext, mintRequest, uploadRequestId, uploadFee);
    }

    public async Task<MintRequest> UploadImageAsync(int id)
    {
        using MintDbContext dbContext = dbContextFactory.CreateDbContext();
        MintRequest mintRequest = await GetMintRequestAsync(dbContext, id);

        ValidateRequestStatus(mintRequest, MintStatus.Processing, id);

        try
        {
            UploadFileResponse? uploadResponse = await PerformImageUploadAsync(mintRequest);
            mintRequest.UpdatedAt = DateTime.UtcNow;
            mintRequest.Status = MintStatus.Uploading;

            logger.LogInformation("Image uploaded successfully for request ID: {Id}", mintRequest.Id);

            dbContext.MintRequests.Update(mintRequest);
            await dbContext.SaveChangesAsync();
            return mintRequest;
        }
        catch
        {
            logger.LogInformation("Failed to upload image for request ID: {Id}", id);
            return await UpdateRequestStatusAsync(dbContext, mintRequest);
        }
    }

    public async Task<MintRequest> UpdateUploadStatusAsync(int id)
    {
        using MintDbContext dbContext = dbContextFactory.CreateDbContext();
        MintRequest mintRequest = await GetMintRequestAsync(dbContext, id);

        try
        {
            UploadDetailsResponse? uploadResponse =
                await _nodeClient.GetFromJsonAsync<UploadDetailsResponse>($"upload/details/{mintRequest.UploadPaymentAddress}");

            if (uploadResponse?.AdaFsId is not null)
            {
                mintRequest.AdaFsId = uploadResponse.AdaFsId;
                mintRequest.Status = MintStatus.Uploaded;
                logger.LogInformation("Image uploaded successfully for request ID: {Id}", mintRequest.Id);
            }
            else
            {
                logger.LogInformation("Image upload not completed for request ID: {Id}", id);
            }
        }
        catch
        {
            logger.LogInformation("Failed to upload image for request ID: {Id}", id);
        }

        mintRequest.UpdatedAt = DateTime.UtcNow;
        dbContext.MintRequests.Update(mintRequest);
        await dbContext.SaveChangesAsync();

        return mintRequest;
    }

    public async Task<MintRequest> MintNftAsync(
        int id,
        string policyId,
        string assetName,
        string asciiAssetName,
        string rewardAddress
    )
    {
        using MintDbContext dbContext = dbContextFactory.CreateDbContext();
        MintRequest mintRequest = await GetMintRequestAsync(dbContext, id);

        ValidateRequestStatus(mintRequest, MintStatus.Uploaded, id);

        Metadata metadata = CreateNftMetadata(
            policyId,
            assetName,
            mintRequest.AdaFsId!,
            DeserializeNftTraits(mintRequest.NftMetadata!),
            asciiAssetName
        );

        try
        {
            string txHash = await ExecuteMintTransactionAsync(mintRequest, rewardAddress, metadata, assetName);
            return await UpdateRequestAfterMintingAsync(dbContext, mintRequest, assetName, policyId, txHash);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to mint NFT for request ID: {Id}", id);
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

    private static async Task<MintRequest> GetMintRequestAsync(MintDbContext dbContext, int id)
    {
        return await dbContext.MintRequests.FirstOrDefaultAsync(m => m.Id == id)
            ?? throw new ArgumentException($"Mint request with ID {id} not found.");
    }

    private static void ValidateRequestStatus(MintRequest mintRequest, MintStatus expectedStatus, int id)
    {
        if (mintRequest.Status != expectedStatus)
            throw new InvalidOperationException($"Mint request with ID {id} is not in {expectedStatus} status. Current status: {mintRequest.Status}");
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

    private async Task<MintRequest> HandlePaymentTimeoutAsync(MintDbContext dbContext, MintRequest mintRequest, int id)
    {
        logger.LogWarning("Payment for request ID: {Id} not received within the expiration time of {ExpirationTime} minutes.",
            id, _paymentExpirationTime.TotalMinutes);

        mintRequest.LastValidStatus = mintRequest.Status;
        mintRequest.Status = MintStatus.Failed;
        mintRequest.UpdatedAt = DateTime.UtcNow;
        dbContext.MintRequests.Update(mintRequest);
        await dbContext.SaveChangesAsync();

        return mintRequest;
    }

    private void LogPaymentStatus(string address, ulong requiredAmount, ulong receivedAmount)
    {
        if (receivedAmount > 0)
        {
            logger.LogWarning("Insufficient payment for request ID: {Id}. Required: {RequiredAmount} lovelace, but received: {ReceivedAmount} lovelace",
                address, requiredAmount, receivedAmount);
        }
        else
        {
            logger.LogInformation("No UTXOs found for address: {Address}. Retrying in {Interval} seconds...",
                address, _utxoCheckInterval.TotalSeconds);
        }
    }

    private async Task<string> RequestUploadSlotAsync(string airdropAddress)
    {
        HttpResponseMessage response = await _nodeClient.PostAsync($"upload/request/{airdropAddress}", null);
        UploadRequestResponse? uploadResponse = await response.Content.ReadFromJsonAsync<UploadRequestResponse>();
        return uploadResponse!.Id;
    }

    private async Task<ulong> CalculateUploadFeeAsync(int imageLength)
    {
        Chrysalis.Network.Cbor.LocalStateQuery.ProtocolParams protocolParams = await cardanoDataProvider.GetParametersAsync();
        return transactionService.CalculateFee(imageLength, _uploadRevenueFee, (ulong)protocolParams.MaxTransactionSize!);
    }

    private async Task SendUploadPaymentAsync(string requestAddress, string uploadAddress, ulong uploadFee)
    {
        PrivateKey? privateKey = await walletService.GetPrivateKeyByAddressAsync(requestAddress);
        TransactionTemplate<TransferParams> transferTemplate = transactionService.Transfer(cardanoDataProvider);
        TransferParams transferParams = new(requestAddress, uploadAddress, uploadFee);

        Transaction transaction = await transferTemplate(transferParams);
        Transaction signedTransaction = transaction.Sign(privateKey!);

        string txHash = await cardanoDataProvider.SubmitTransactionAsync(signedTransaction);
        logger.LogInformation("Transaction submitted successfully for request ID: {Id}. TxHash: {TxHash}", requestAddress, txHash);
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
        MintRequest mintRequest,
        string rewardAddress,
        Metadata metadata,
        string assetName)
    {
        TransactionTemplate<MintNftParams> nftTemplate = transactionTemplateService.MintNftTemplate();
        WalletAddress mintingAddress = walletService.GetWalletAddress(_mintingSeed, 0);

        MintNftParams mintNftParams = new(
            mintRequest.Address!,
            mintRequest.UserAddress,
            rewardAddress,
            mintingAddress.ToBech32(),
            _invalidHereafter,
            metadata,
            new Dictionary<string, int> { [assetName] = 1 }
        );

        Transaction transaction = await nftTemplate(mintNftParams);
        Transaction signedTransaction = await SignTransactionWithBothKeysAsync(transaction, mintRequest.Address!);

        return await cardanoDataProvider.SubmitTransactionAsync(signedTransaction);
    }

    private string GetUserAddressAsync(int id)
    {
        WalletAddress address = walletService.GetWalletAddress(id);
        return address.ToBech32();
    }

    private async Task<Transaction> SignTransactionWithBothKeysAsync(Transaction transaction, string address)
    {
        PrivateKey? userPrivateKey = await walletService.GetPrivateKeyByAddressAsync(address);
        PrivateKey mintingPrivateKey = walletService.GetPaymentPrivateKey(_mintingSeed, 0);

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