using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Chrysalis.Cbor.Extensions.Cardano.Core.Common;
using Chrysalis.Cbor.Extensions.Cardano.Core.Transaction;
using Chrysalis.Cbor.Serialization;
using Chrysalis.Cbor.Types.Cardano.Core;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using Chrysalis.Tx.Extensions;
using Chrysalis.Tx.Models;
using Chrysalis.Tx.Models.Cbor;
using Chrysalis.Wallet.Models.Keys;
using Chrysalis.Wallet.Utils;
using Microsoft.EntityFrameworkCore;
using Paylkoyn.ImageGen.Services;
using PaylKoyn.Data.Responses;
using PaylKoyn.ImageGen.Data;

namespace PaylKoyn.ImageGen.Services;

public class MintingService(
    IConfiguration configuration,
    IDbContextFactory<MintDbContext> dbContextFactory,
    ICardanoDataProvider cardanoDataProvider,
    NftRandomizerService nftRandomizerService,
    WalletService walletService,
    TransactionTemplateService transactionTemplateService,
    IHttpClientFactory httpClientFactory,
    ILogger<MintingService> logger
)
{
    private readonly HttpClient _nodeClient = httpClientFactory.CreateClient("PaylKoynNodeClient");
    private readonly TimeSpan _expirationTime = TimeSpan.FromMinutes(configuration.GetValue<int>("Minting:ExpirationMinutes", 30));
    private readonly TimeSpan _getUtxosInterval = TimeSpan.FromSeconds(configuration.GetValue<int>("Minting:GetUtxosIntervalSeconds", 10));

    public async Task<MintRequest> WaitForPaymentAsync(string id, ulong amount)
    {
        using MintDbContext dbContext = dbContextFactory.CreateDbContext();
        MintRequest? mintRequest = await dbContext.MintRequests.FirstOrDefaultAsync(m => m.Id == id)
            ?? throw new ArgumentException($"Mint request with ID {id} not found.");

        if (mintRequest.Status != MintStatus.Pending)
            throw new InvalidOperationException($"Mint request with ID {id} is not in pending status. Current status: {mintRequest.Status}");

        Stopwatch stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < _expirationTime)
        {
            (bool isSuccess, IEnumerable<ResolvedInput> utxos) = await TryGetUtxosAsync(id);
            if (isSuccess && utxos.Any())
            {
                ulong totalAmount = utxos.Aggregate(0UL, (sum, utxo) => sum + utxo.Output.Amount().Lovelace());
                logger.LogInformation("Found {UtxoCount} UTXOs with total amount: {TotalAmount} lovelace for request ID: {Id}", utxos.Count(), totalAmount, id);

                if (totalAmount >= amount)
                {
                    mintRequest = GenerateMetadata(mintRequest);
                    mintRequest.Status = MintStatus.PaymentReceived;
                    mintRequest.UpdatedAt = DateTime.UtcNow;

                    dbContext.MintRequests.Update(mintRequest);
                    await dbContext.SaveChangesAsync();

                    logger.LogInformation("Payment received for request ID: {Id}. Total amount: {TotalAmount} lovelace", id, totalAmount);
                    return mintRequest;
                }
                else
                {
                    logger.LogWarning("Insufficient payment for request ID: {Id}. Required: {RequiredAmount} lovelace, but received: {ReceivedAmount} lovelace", id, amount, totalAmount);
                }
            }

            logger.LogInformation("No UTXOs found for address: {Address}. Retrying in {Interval} seconds...", id, _getUtxosInterval.TotalSeconds);
            await Task.Delay(_getUtxosInterval);
        }

        logger.LogWarning("Payment for request ID: {Id} not received within the expiration time of {ExpirationTime} minutes.", id, _expirationTime.TotalMinutes);
        mintRequest.Status = MintStatus.Failed;
        mintRequest.UpdatedAt = DateTime.UtcNow;
        dbContext.MintRequests.Update(mintRequest);
        await dbContext.SaveChangesAsync();

        return mintRequest;
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

    public async Task<MintRequest> RequestImageUploadAsync(string id)
    {
        using MintDbContext dbContext = dbContextFactory.CreateDbContext();
        MintRequest? mintRequest = await dbContext.MintRequests.FirstOrDefaultAsync(m => m.Id == id)
            ?? throw new ArgumentException($"Mint request with ID {id} not found.");
        if (mintRequest.Status != MintStatus.PaymentReceived)
            throw new InvalidOperationException($"Mint request with ID {id} is not in metadata generated status. Current status: {mintRequest.Status}");

        HttpResponseMessage response = await _nodeClient.PostAsync("upload/request", null);
        string jsonString = await response.Content.ReadAsStringAsync();
        UploadRequestResponse? uploadResponse = JsonSerializer.Deserialize<UploadRequestResponse>(jsonString);

        // @TODO: Create a send payment transaction to the upload address
        mintRequest.Status = MintStatus.UploadPaymentSent;
        mintRequest.UploadPaymentAddress = uploadResponse?.Id;
        mintRequest.UploadPaymentAmount = 2_000_000UL;
        mintRequest.UpdatedAt = DateTime.UtcNow;
        dbContext.MintRequests.Update(mintRequest);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Image upload fee payment initiated for request ID: {Id}", id);
        return mintRequest;
    }

    public async Task<MintRequest> UploadImageAsync(string id)
    {
        using MintDbContext dbContext = dbContextFactory.CreateDbContext();
        MintRequest? mintRequest = await dbContext.MintRequests.FirstOrDefaultAsync(m => m.Id == id)
            ?? throw new ArgumentException($"Mint request with ID {id} not found.");
        if (mintRequest.Status != MintStatus.UploadPaymentSent)
            throw new InvalidOperationException($"Mint request with ID {id} is not in upload payment sent status. Current status: {mintRequest.Status}");

        // Upload the image to the node
        string fileName = $"{id}.png";
        using MultipartFormDataContent formData = new()
        {
            // Fill in the form fields
            { new StringContent(id), "id" },
            { new StringContent(fileName), "name" },
            { new StringContent("image/png"), "contentType" },

            // Attach the file
            { new ByteArrayContent(mintRequest.Image!), "file", fileName }
        };

        // Send it
        HttpResponseMessage response = await _nodeClient.PostAsync("upload/receive", formData);
        response.EnsureSuccessStatusCode();
        string jsonString = await response.Content.ReadAsStringAsync();

        UploadFileResponse? uploadResponse = JsonSerializer.Deserialize<UploadFileResponse>(jsonString);

        mintRequest.AdaFsId = uploadResponse?.AdaFsId;
        mintRequest.UpdatedAt = DateTime.UtcNow;
        mintRequest.Status = MintStatus.ImageUploaded;
        dbContext.MintRequests.Update(mintRequest);
        await dbContext.SaveChangesAsync();
        logger.LogInformation("Image uploaded successfully for request ID: {Id}", id);
        return mintRequest;
    }

    public async Task<MintRequest> MintNftAsync(
        string id,
        string policyId,
        string assetName,
        string asciiAssetName,
        string rewardAddress,
        string description
    )
    {
        using MintDbContext dbContext = dbContextFactory.CreateDbContext();
        MintRequest? mintRequest = await dbContext.MintRequests.FirstOrDefaultAsync(m => m.Id == id)
            ?? throw new ArgumentException($"Mint request with ID {id} not found.");
        if (mintRequest.Status != MintStatus.ImageUploaded)
            throw new InvalidOperationException($"Mint request with ID {id} is not in image uploaded status. Current status: {mintRequest.Status}");

        Metadata cip25Metadata = CreateNftMetadata(
            policyId,
            assetName,
            mintRequest.AdaFsId!,
            JsonSerializer.Deserialize<List<NftTrait>>(mintRequest.NftMetadata!)!,
            asciiAssetName,
            description
        );

        TransactionTemplate<MintNftParams> nftTemplate = transactionTemplateService.MintNftTemplate();
        MintNftParams mintNftParams = new(
            mintRequest.Id,
            mintRequest.UserAddress,
            rewardAddress,
            cip25Metadata,
            new Dictionary<string, int>
            {
                [assetName] = 1
            }
        );

        PrivateKey? privateKey = await walletService.GetPrivateKeyByAddressAsync(mintRequest.Id);
        Transaction tx = await nftTemplate(mintNftParams);
        Transaction signedTx = tx.Sign(privateKey!);

        // Submit to a node
        string txHash = Convert.ToHexString(HashUtil.Blake2b256(CborSerializer.Serialize(signedTx)));

        // Assign an asset name 
        mintRequest.AssetName = assetName;
        mintRequest.PolicyId = policyId;
        mintRequest.MintTxHash = txHash;
        mintRequest.Status = MintStatus.Minted;
        mintRequest.UpdatedAt = DateTime.UtcNow;
        dbContext.MintRequests.Update(mintRequest);
        await dbContext.SaveChangesAsync();

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
        TransactionMetadatum adaFsUrl = SplitMetadata($"adafs://{adaFsId}");
        // Build the asset metadata
        Dictionary<TransactionMetadatum, TransactionMetadatum> assetMetadata = new Dictionary<TransactionMetadatum, TransactionMetadatum>
        {
            // Required fields
            { new MetadataText("name"), new MetadataText(nftName ?? assetName) },
            { new MetadataText("image"), adaFsUrl},
            { new MetadataText("mediaType"), new MetadataText("image/png") }
        };

        // Add description if provided
        if (!string.IsNullOrEmpty(description))
        {
            assetMetadata.Add(new MetadataText("description"), new MetadataText(description));
        }

        // Add traits directly as key-value pairs
        foreach (NftTrait trait in traits)
        {
            assetMetadata.Add(new MetadataText(trait.Category), new MetadataText(trait.TraitName));
        }

        // Build the policy map: { assetName: { ...metadata } }
        Dictionary<TransactionMetadatum, TransactionMetadatum> policyMap = new Dictionary<TransactionMetadatum, TransactionMetadatum>
        {
            { new MetadataText(assetName), new MetadatumMap(assetMetadata) }
        };

        // Build the root structure: { version: 1, policyId: { ...policyMap } }
        Dictionary<TransactionMetadatum, TransactionMetadatum> rootStructure = new Dictionary<TransactionMetadatum, TransactionMetadatum>
        {
            { new MetadataText(policyId), new MetadatumMap(policyMap) }
        };

        // Wrap in label 721 for CIP-25
        Dictionary<ulong, TransactionMetadatum> labeledMetadata = new Dictionary<ulong, TransactionMetadatum>
        {
            { 721, new MetadatumMap(rootStructure) }
        };

        return new Metadata(labeledMetadata);
    }

    private TransactionMetadatum SplitMetadata(string metadata)
    {
        if (string.IsNullOrEmpty(metadata))
        {
            return new MetadataText("");
        }

        byte[] metadataBytes = Encoding.UTF8.GetBytes(metadata);

        // If it fits in one chunk, return single MetadataText
        if (metadataBytes.Length <= 64)
        {
            return new MetadataText(metadata);
        }

        // Split into multiple chunks
        List<TransactionMetadatum> chunks = [];
        int position = 0;

        while (position < metadataBytes.Length)
        {
            int chunkSize = Math.Min(64, metadataBytes.Length - position);

            // Extract chunk bytes
            byte[] chunkBytes = new byte[chunkSize];
            Array.Copy(metadataBytes, position, chunkBytes, 0, chunkSize);

            // Make sure we don't break UTF-8 characters
            string chunkText;
            try
            {
                chunkText = Encoding.UTF8.GetString(chunkBytes);
            }
            catch (ArgumentException)
            {
                // Broken UTF-8, try smaller chunk
                for (int i = chunkSize - 1; i > 0; i--)
                {
                    try
                    {
                        chunkText = Encoding.UTF8.GetString(chunkBytes, 0, i);
                        chunkSize = i;
                        break;
                    }
                    catch (ArgumentException)
                    {
                        continue;
                    }
                }
                chunkText = "";
            }

            if (string.IsNullOrEmpty(chunkText))
                break;

            chunks.Add(new MetadataText(chunkText));
            position += chunkSize;
        }

        return new MetadatumList(chunks);
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
}