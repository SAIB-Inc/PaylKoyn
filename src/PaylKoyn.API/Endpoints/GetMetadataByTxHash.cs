using System.Text;
using Chrysalis.Cbor.Serialization;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Models;
using PaylKoyn.Data.Models.Common;
using PaylKoyn.Data.Models.Entity;
using MetadatumMap = Chrysalis.Cbor.Types.Cardano.Core.Transaction.MetadatumMap;

namespace PaylKoyn.API.Endpoints;

public class GetMetadataByTxHash(IDbContextFactory<PaylKoynDbContext> dbContextFactory) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/metadata");
        AllowAnonymous();
        Description(x => x
            .WithTags("Transaction")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await using PaylKoynDbContext dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        string? txHash = Query<string>("txHash", isRequired: true);

        TransactionBySlot? tx = await dbContext.TransactionsBySlot
            .AsNoTracking()
            .Where(x => x.Hash == txHash)
            .FirstOrDefaultAsync(ct);

        if (tx is null)
        {
            AddError("No payloads found.");
            await SendNotFoundAsync(ct);
            return;
        }

        TransactionMetadatum? txMetadatum = CborSerializer.Deserialize<TransactionMetadatum>(tx.Metadata);
        MetadatumMap? metadataMap = GetMetadataMapBytes(txMetadatum, "metadata");
        string fileName = GetMetadataValueString(metadataMap?.Value, "filename") ?? "unknown.txt";
        string contentType = GetMetadataValueString(metadataMap?.Value, "contentType") ?? "application/octet-stream";
        PaylKoynMetadata metadata = new(
            Version: GetMetadataValueLong(txMetadatum, "version") ?? 0,
            Payload: GetPayloadFromTransactionRaw(txMetadatum)!,
            Metadata: new (
                FileName: fileName,
                ContentType: contentType
            ),
            Next: Convert.ToHexStringLower(GetMetadataValueBytes(txMetadatum, "next") ?? [])
        );

        await SendOkAsync(metadata, cancellation: ct);
    }

    private string? GetPayloadFromTransactionRaw(TransactionMetadatum? txMetadatum)
    {
        if (txMetadatum is not MetadatumMap metadatumMap) return null;

        string? payloadStr = metadatumMap.Value
            .Where(kvp => kvp is { Key: MetadataText { Value: "payload" } })
            .FirstOrDefault()
            .Value switch
            {
                MetadatumList arr => arr.Value
                    .Select(m => m switch
                    {
                        MetadatumBytes bytes => Convert.ToHexStringLower(bytes.Value),
                        MetadataText text => text.Value,
                        _ => string.Empty
                    })
                    .Aggregate(new StringBuilder(), (sb, str) => sb.Append(str)).ToString(),
                MetadataText text => text.Value,
                _ => string.Empty
            };

        return payloadStr;
    }
    
    private static byte[]? GetMetadataValueBytes(
        TransactionMetadatum? metadatum,
        string key)
    {
        if (metadatum is null || metadatum is not MetadatumMap metadatumMap) return null;
    
        return metadatumMap.Value
            .Where(kv => kv.Key is MetadataText text && text.Value == key)
            .Select(kv => kv.Value)
            .OfType<MetadatumBytes>()
            .Select(bytes => bytes.Value)
            .FirstOrDefault();
    }

    private static MetadatumMap? GetMetadataMapBytes(
        TransactionMetadatum? metadatum,
        string key)
    {
        if (metadatum is null || metadatum is not MetadatumMap metadatumMap) return null;
    
        return metadatumMap.Value
            .Where(kv => kv.Key is MetadataText text && text.Value == key)
            .Select(kv => kv.Value)
            .OfType<MetadatumMap>()
            .Select(bytes => bytes)
            .FirstOrDefault();
    }

    private static string? GetMetadataValueString(
        IEnumerable<KeyValuePair<TransactionMetadatum, TransactionMetadatum>>? metadatumMapList,
        string key)
    {
        return metadatumMapList?
            .Where(kv => kv.Key is MetadataText text && text.Value == key)
            .Select(kv => kv.Value)
            .OfType<MetadataText>()
            .Select(str => str.Value)
            .FirstOrDefault() ?? string.Empty;
    }

    private static int? GetMetadataValueLong(
        TransactionMetadatum? metadatum,
        string key)
    {
        if (metadatum is null || metadatum is not MetadatumMap metadatumMap) return null;

        return (int?)metadatumMap.Value
            .Where(kv => kv.Key is MetadataText text && text.Value == key)
            .Select(kv => kv.Value)
            .OfType<MetadatumIntLong>()
            .Select(str => str.Value)
            .FirstOrDefault();
    }
}