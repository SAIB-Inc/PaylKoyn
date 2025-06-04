using FastEndpoints;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using Chrysalis.Tx.Models;
using PaylKoyn.Node.Services;

namespace PaylKoyn.Node.Endpoints;

public class RetrieveFileRequest
{
    public string TxHash { get; set; } = string.Empty;
}


public class RetrieveFile(ICardanoDataProvider blockfrost, FileCacheService cacheService) : Endpoint<RetrieveFileRequest>
{
    public override void Configure()
    {
        Get("adafs/{TxHash}");
        AllowAnonymous();
        Description(x => x
            .WithTags("AdaFS")
            .WithSummary("Retrieve file from AdaFS")
            .WithDescription("Retrieves a file from AdaFS using a transaction hash as the starting point."));
    }

    public override async Task HandleAsync(RetrieveFileRequest req, CancellationToken ct)
    {
        // Input validation
        if (string.IsNullOrWhiteSpace(req.TxHash) || req.TxHash.Length != 64)
        {
            await SendErrorsAsync(400, cancellation: ct);
            return;
        }

        try
        {
            // Check cache first
            (byte[] fileBytes, string contentType, string fileName)? cachedFile = await cacheService.GetCachedFileAsync(req.TxHash);
            if (cachedFile.HasValue)
            {
                (byte[] fileBytes, string contentType, string fileName) = cachedFile.Value;
                HttpContext.Response.Headers.Append("Content-Disposition", $"inline; filename=\"{fileName}\"");
                HttpContext.Response.ContentType = contentType;
                await HttpContext.Response.Body.WriteAsync(fileBytes, ct);
                return;
            }

            // Retrieve from blockchain
            (byte[] retrievedBytes, string retrievedContentType, string retrievedFileName) = await RetrieveFileFromChainAsync(req.TxHash, ct);

            if (retrievedBytes.Length == 0)
            {
                await SendErrorsAsync(404, cancellation: ct);
                return;
            }

            // Cache the file
            await cacheService.CacheFileAsync(req.TxHash, retrievedBytes, retrievedContentType, retrievedFileName);

            HttpContext.Response.Headers.Append("Content-Disposition", $"inline; filename=\"{retrievedFileName}\"");
            HttpContext.Response.ContentType = retrievedContentType;
            await HttpContext.Response.Body.WriteAsync(retrievedBytes, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in RetrieveFile: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            if (!HttpContext.Response.HasStarted)
            {
                await SendErrorsAsync(500, cancellation: ct);
            }
        }
    }

    private async Task<(byte[] fileBytes, string contentType, string fileName)> RetrieveFileFromChainAsync(string startTxHash, CancellationToken ct)
    {
        List<byte> allPayloadBytes = new List<byte>();
        string? currentTxHash = startTxHash;
        string contentType = "application/octet-stream";
        string fileName = $"file_{startTxHash}";
        HashSet<string> processedTransactions = new HashSet<string>();

        while (!string.IsNullOrEmpty(currentTxHash))
        {
            // Prevent infinite loops
            if (processedTransactions.Contains(currentTxHash))
                break;

            processedTransactions.Add(currentTxHash);

            // Safety limit
            if (processedTransactions.Count > 1000)
                break;

            Chrysalis.Cbor.Types.Cardano.Core.Metadata? metadata = await blockfrost.GetTransactionMetadataAsync(currentTxHash);
            if (metadata?.Value == null)
                break;

            string? nextHash = ProcessTransactionMetadata(metadata.Value, allPayloadBytes, ref contentType, ref fileName);
            currentTxHash = nextHash;
        }

        return (allPayloadBytes.ToArray(), contentType, fileName);
    }

    private static string? ProcessTransactionMetadata(
        Dictionary<ulong, TransactionMetadatum> metadata,
        List<byte> payloadBytes,
        ref string contentType,
        ref string fileName)
    {
        string? nextHash = null;

        foreach ((ulong _, TransactionMetadatum metadatum) in metadata)
        {
            if (metadatum is not MetadatumMap map) continue;

            foreach ((TransactionMetadatum key, TransactionMetadatum value) in map.Value)
            {
                if (key is not MetadataText keyText) continue;

                switch (keyText.Value)
                {
                    case "next" when value is MetadataText nextText:
                        nextHash = nextText.Value.StartsWith("0x") ? nextText.Value[2..] : nextText.Value;
                        break;

                    case "payload" when value is MetadatumList payloadList:
                        ExtractPayloadChunks(payloadList, payloadBytes);
                        break;

                    case "metadata" when value is MetadatumMap nestedMetadata:
                        ExtractFileMetadata(nestedMetadata, ref contentType, ref fileName);
                        break;
                }
            }
        }

        return string.IsNullOrEmpty(nextHash) ? null : nextHash;
    }

    private static void ExtractPayloadChunks(MetadatumList payloadList, List<byte> payloadBytes)
    {
        foreach (TransactionMetadatum chunk in payloadList.Value)
        {
            if (chunk is not MetadataText chunkText) continue;

            string hexData = chunkText.Value.StartsWith("0x") ? chunkText.Value[2..] : chunkText.Value;

            try
            {
                byte[] chunkBytes = Convert.FromHexString(hexData);
                payloadBytes.AddRange(chunkBytes);
            }
            catch
            {
                // Skip invalid hex chunks
            }
        }
    }

    private static void ExtractFileMetadata(MetadatumMap metadataMap, ref string contentType, ref string fileName)
    {
        foreach ((TransactionMetadatum key, TransactionMetadatum value) in metadataMap.Value)
        {
            if (key is not MetadataText keyText || value is not MetadataText valueText) continue;

            switch (keyText.Value)
            {
                case "contentType":
                    contentType = valueText.Value;
                    break;
                case "fileName" or "filename":
                    fileName = valueText.Value;
                    break;
            }
        }
    }
}