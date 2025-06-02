using System.Text;
using System.Text.Json;
using Chrysalis.Cbor.Extensions.Cardano.Core.Transaction;
using Chrysalis.Cbor.Serialization;
using Chrysalis.Cbor.Types.Cardano.Core;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Models;
using PaylKoyn.Data.Models.Api.Response.Data;
using PaylKoyn.Data.Models.Common;
using PaylKoyn.Data.Models.Entity;
using PaylKoyn.Data.Utils;
using CMetadata = Chrysalis.Cbor.Types.Cardano.Core.Metadata;
using MetadatumMap = Chrysalis.Cbor.Types.Cardano.Core.Transaction.MetadatumMap;

namespace PaylKoyn.API.Endpoints;

public class GetMetadataByTxHash(IDbContextFactory<PaylKoynDbContext> dbContextFactory) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/txs/{txHash}/metadata");
        AllowAnonymous();
        Description(x => x
            .WithTags("Transactions")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await using PaylKoynDbContext dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        string? txHash = Route<string>("txHash", isRequired: true);

        TransactionBySlot? tx = await dbContext.TransactionsBySlot
            .AsNoTracking()
            .Where(x => x.Hash == txHash)
            .FirstOrDefaultAsync(ct);

        if (tx is null)
        {
            AddError("No transaction found.");
            await SendNotFoundAsync(ct);
            return;
        }

        CMetadata? metadata = CborSerializer.Deserialize<CMetadata>(tx.Metadata);
        Dictionary<ulong, TransactionMetadatum> metadataDict = metadata.Value();
        IEnumerable<TransactionMetadatumResponse> response = [.. metadataDict
            .Select(kv =>
            {
                TransactionMetadatum value = kv.Value;
                return new TransactionMetadatumResponse
                {
                    Key = kv.Key,
                    Value = DeserializeMetadatum(value) ?? "null"
                };
            })];

        await SendOkAsync(response, cancellation: ct);
    }

    public static object? DeserializeMetadatum(TransactionMetadatum? metadatum)
    {
        return metadatum switch
        {
            null => null,

            MetadatumMap map => DeserializeMap(map),
            MetadatumList list => DeserializeList(list),
            MetadatumBytes bytes => Convert.ToHexStringLower(bytes.Value),
            MetadataText text => text.Value,
            MetadatumIntLong longInt => longInt.Value,
            MetadatumIntUlong ulongInt => ulongInt.Value,

            _ => throw new ArgumentException($"Unknown TransactionMetadatum type: {metadatum.GetType().Name}")
        };
    }

    private static Dictionary<object, object> DeserializeMap(MetadatumMap map)
    {
        Dictionary<object, object> result = [];

        foreach (KeyValuePair<TransactionMetadatum, TransactionMetadatum> kvp in map.Value)
        {
            object? key = DeserializeMetadatum(kvp.Key);
            object? value = DeserializeMetadatum(kvp.Value);

            object finalKey = key ?? "null";
            object finalValue = value ?? "null";

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