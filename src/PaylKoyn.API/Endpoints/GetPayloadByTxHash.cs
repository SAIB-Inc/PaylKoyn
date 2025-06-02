using System.Text;
using Chrysalis.Cbor.Serialization;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Models;
using PaylKoyn.Data.Models.Entity;
using PaylKoyn.Data.Utils;
using MetadatumMap = Chrysalis.Cbor.Types.Cardano.Core.Transaction.MetadatumMap;

namespace PaylKoyn.API.Endpoints;

public class GetPayloadByTxHash(IDbContextFactory<PaylKoynDbContext> dbContextFactory) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/txs/{txHash}/payload");
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

        await SendOkAsync(DataUtils.GetPayloadFromTransactionRaw(payLoad), cancellation: ct);
    }
}