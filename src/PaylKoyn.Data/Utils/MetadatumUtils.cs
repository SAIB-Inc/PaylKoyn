using Chrysalis.Cbor.Types.Cardano.Core.Transaction;

namespace PaylKoyn.Data.Utils;


public static class TransactionMetadatumUtils
{
    public static object? DeserializeMetadatum(TransactionMetadatum? metadatum)
    {
        return metadatum switch
        {
            null => null,

            MetadatumMap map => DeserializeMap(map),
            MetadatumList list => DeserializeList(list),
            MetadatumBytes bytes => bytes.Value,
            MetadataText text => text.Value,
            MetadatumIntLong longInt => longInt.Value,
            MetadatumIntUlong ulongInt => ulongInt.Value,

            _ => throw new ArgumentException($"Unknown TransactionMetadatum type: {metadatum.GetType().Name}")
        };
    }

    private static Dictionary<object, object> DeserializeMap(MetadatumMap map)
    {
        var result = new Dictionary<object, object>();

        foreach (var kvp in map.Value)
        {
            var key = DeserializeMetadatum(kvp.Key);
            var value = DeserializeMetadatum(kvp.Value);

            var finalKey = key ?? "null";
            var finalValue = value ?? "null";

            result[finalKey] = finalValue;
        }

        return result;
    }

    private static List<object> DeserializeList(MetadatumList list)
    {
        return [.. list.Value
            .Select(DeserializeMetadatum)
            .Select(item => item ?? "null")];
    }
}