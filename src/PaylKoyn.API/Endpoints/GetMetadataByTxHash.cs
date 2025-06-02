using System.Text;
using System.Text.Json;
using Chrysalis.Cbor.Extensions.Cardano.Core.Transaction;
using Chrysalis.Cbor.Serialization;
using Chrysalis.Cbor.Types.Cardano.Core;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Models;
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
        var response = metadataDict
            .Select(kv =>
            {
                TransactionMetadatum value = kv.Value;
                return new TransactionMetadatumResponse
                {
                    Key = kv.Key,
                    Value = value switch
                    {
                        MetadatumMap map => map.Value.ToDictionary(m => m.Key, m => m.Value),
                        MetadatumList list => list.Value.Select(m => m).ToList(),
                        _ => value
                    }
                };
            })
            .ToList();

        await SendOkAsync(metadata, cancellation: ct);
    }
}

public class TransactionMetadatumResponse
{
    public ulong Key { get; set; }
    public object Value { get; set; } = null!;
}