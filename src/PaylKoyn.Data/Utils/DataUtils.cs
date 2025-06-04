using System.Text;
using Chrysalis.Cbor.Serialization;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using PaylKoyn.Data.Models.Entity;

namespace PaylKoyn.Data.Utils;

public static class DataUtils
{
    public static string? GetPayloadFromTransactionMetadatum(TransactionMetadatum? txMetadatum)
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
    
    public static byte[]? GetMetadataValueBytes(
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

    public static MetadatumMap? GetMetadataMapBytes(
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

    public static string? GetMetadataValueString(
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

    public static int? GetMetadataValueLong(
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
    
    public static string? GetPayloadFromTransactionRaw(TransactionBySlot transaction)
    {
        TransactionMetadatum? txMetadatum = CborSerializer.Deserialize<TransactionMetadatum>(transaction.Metadata);
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
}