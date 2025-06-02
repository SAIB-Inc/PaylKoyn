using Chrysalis.Cbor.Extensions.Cardano.Core.Common;
using Chrysalis.Cbor.Extensions.Cardano.Core.Transaction;
using Chrysalis.Cbor.Serialization;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Models;
using PaylKoyn.Data.Models.Api.Response.Data;
using PaylKoyn.Data.Models.Entity;

namespace PaylKoyn.API.Endpoints.Address;

public class Balance(
    IDbContextFactory<PaylKoynDbContext> dbContextFactory
) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/{Address}/balance");
        AllowAnonymous();

        Description(d => d
            .WithTags("Address")
            .Produces<BalanceByAddressResponse>(StatusCodes.Status200OK)
            .ProducesProblemFE(StatusCodes.Status400BadRequest)
            .ProducesProblemFE(StatusCodes.Status500InternalServerError)
            .WithName("GetBalanceByAddress")
        );
    }

    public override async Task HandleAsync(
        CancellationToken cancellationToken
    )
    {
        string? address = Route<string>("Address", isRequired: true);

        await using PaylKoynDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        IEnumerable<OutputBySlot> resolvedOutRefs = await dbContext.OutputsBySlot
            .AsNoTracking()
            .Where(e => e.Address == address)
            .Where(e => string.IsNullOrEmpty(e.SpentTxHash))
            .ToListAsync(cancellationToken: cancellationToken);

        if (!resolvedOutRefs.Any())
        {
            await SendAsync(new BalanceByAddressResponse(0, []), cancellation: cancellationToken);
            return;
        }

        IEnumerable<TransactionOutput> resolvedTxOutputs = resolvedOutRefs
            .Select(e => CborSerializer.Deserialize<TransactionOutput>(e.OutputRaw));

        ulong totalLovelace = resolvedTxOutputs
            .Select(output => output.Amount().Lovelace())
            .Aggregate((acc, value) => acc + value);

        Dictionary<string, Dictionary<string, ulong>> multiAssets = resolvedTxOutputs
            .SelectMany(output => output.Amount().MultiAsset())
            .GroupBy(ma => Convert.ToHexStringLower(ma.Key))
            .ToDictionary(
                g => g.Key,
                g => g
                    .SelectMany(ma => ma.Value.Value)
                    .GroupBy(tb => Convert.ToHexStringLower(tb.Key))
                    .ToDictionary(
                        tbGroup => tbGroup.Key,
                        tbGroup => tbGroup.Aggregate(0UL, (acc, tb) => acc + tb.Value)
                    )
            );

        BalanceByAddressResponse response = new(totalLovelace, multiAssets);

        await SendAsync(response, cancellation: cancellationToken);
    }
}