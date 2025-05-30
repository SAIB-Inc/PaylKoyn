using System.Text;
using Chrysalis.Cbor.Serialization;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Models;
using PaylKoyn.Data.Models.Entity;
using MetadatumMap = Chrysalis.Cbor.Types.Cardano.Core.Transaction.MetadatumMap;

namespace PaylKoyn.API.Endpoints;

public class GetPayloadByTxHash(IDbContextFactory<PaylKoynDbContext> dbContextFactory) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/payload");
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

        TransactionBySlot? payLoad = await dbContext.TransactionsBySlot
            .AsNoTracking()
            .Where(x => x.Hash == txHash)
            .FirstOrDefaultAsync(ct);

        if (payLoad is null)
        {
            AddError("No payloads found.");
            await SendNotFoundAsync(ct);
            return;
        }

        await SendOkAsync(GetPayloadFromTransactionRaw(payLoad), cancellation: ct);
    }

    private static string? GetPayloadFromTransactionRaw(TransactionBySlot transaction)
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