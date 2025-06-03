using Chrysalis.Cbor.Extensions.Cardano.Core.Common;
using Chrysalis.Cbor.Extensions.Cardano.Core.Transaction;
using Chrysalis.Cbor.Serialization;
using Chrysalis.Cbor.Types.Cardano.Core.Common;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Models;
using PaylKoyn.Data.Models.Api.Response.Data;
using PaylKoyn.Data.Models.Entity;

namespace PaylKoyn.API.Endpoints.Address;

public class GetUtxoByAddress(
    IDbContextFactory<PaylKoynDbContext> dbContextFactory
) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("addresses/{address}/utxos");
        AllowAnonymous();

        Description(d => d
            .WithTags("Address")
            .Produces<BalanceByAddressResponse>(StatusCodes.Status200OK)
            .ProducesProblemFE(StatusCodes.Status400BadRequest)
            .ProducesProblemFE(StatusCodes.Status500InternalServerError)
            .WithName("GetUtxosByAddress")
            .WithSummary("Get unspent transaction outputs (UTXOs) for a specific address")
        );
    }

    public override async Task HandleAsync(
        CancellationToken cancellationToken
    )
    {
        string? address = Route<string>("Address", isRequired: true);
        int limit = Query<int?>("count", isRequired: false) ?? 20;
        int offset = Query<int?>("page", isRequired: false) ?? 0;

        await using PaylKoynDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        IEnumerable<OutputBySlot> resolvedOutRefs = await dbContext.OutputsBySlot
            .AsNoTracking()
            .Where(e => e.Address == address)
            .Where(e => string.IsNullOrEmpty(e.SpentTxHash))
            .OrderBy(e => e.Slot)
            .ThenBy(e => e.OutRef)
            .Take(limit)
            .Skip(offset)
            .ToListAsync(cancellationToken: cancellationToken);

        if (!resolvedOutRefs.Any())
        {
            await SendAsync(new BalanceByAddressResponse(0, []), cancellation: cancellationToken);
            return;
        }

        IEnumerable<UnspentOutput> unspentOutputs = [.. resolvedOutRefs
            .Select(outputBySlot =>
            {
                TransactionOutput txOutput = CborSerializer.Deserialize<TransactionOutput>(outputBySlot.Raw);
                
                string[] outRefParts = outputBySlot.OutRef.Split('#');
                string txHash = outRefParts[0];
                ulong index = ulong.Parse(outRefParts[1]);

                List<Amount> amounts = [];

                amounts.Add(new Amount(
                    Unit: "lovelace",
                    Quantity: txOutput.Amount().Lovelace()
                ));

                Dictionary<byte[], TokenBundleOutput> multiAssets = txOutput.Amount().MultiAsset();
                if (multiAssets?.Any() == true)
                {
                    IEnumerable<Amount> assetAmounts = multiAssets
                        .SelectMany(policyAssets => policyAssets.Value.Value.Select(asset => new Amount(
                            Unit: Convert.ToHexStringLower(policyAssets.Key) + Convert.ToHexStringLower(asset.Key),
                            Quantity: asset.Value
                        )));
                    
                    amounts.AddRange(assetAmounts);
                }

                return new UnspentOutput(
                    Address: address!,
                    TxHash: txHash,
                    Index: index,
                    Amount: amounts,
                    Block: outputBySlot.BlockHash,
                    DataHash: outputBySlot.ScriptHash,
                    InlineDatum: txOutput.DatumOption()?.Data() is not null
                        ? Convert.ToHexStringLower(txOutput.DatumOption()?.Data() ?? [])
                        : null,
                    ReferenceScriptHash: outputBySlot.ScriptDataHash
                );
            })];

        await SendAsync(unspentOutputs, cancellation: cancellationToken);
    }
}