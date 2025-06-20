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
            .Produces<GetUtxoByAddressResponse[]>(StatusCodes.Status200OK)
            .ProducesProblemFE(StatusCodes.Status400BadRequest)
            .ProducesProblemFE(StatusCodes.Status500InternalServerError)
            .WithName("GetUtxosByAddress")
        );
    }

    public override async Task HandleAsync(
        CancellationToken cancellationToken
    )
    {
        string? address = Route<string>("Address", isRequired: true);
        int count = Query<int?>("count", isRequired: false) ?? 20;
        int page = Query<int?>("page", isRequired: false) ?? 1;

        await using PaylKoynDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        IEnumerable<OutputBySlot> resolvedOutRefs = await dbContext.OutputsBySlot
            .AsNoTracking()
            .Where(e => e.Address == address)
            .Where(e => string.IsNullOrEmpty(e.SpentTxHash))
            .OrderBy(e => e.Slot)
            .ThenBy(e => e.OutRef)
            .Skip((page - 1) * count)
            .Take(count)
            .ToListAsync(cancellationToken: cancellationToken);

        if (!resolvedOutRefs.Any())
        {
            await SendAsync(new List<GetUtxoByAddressResponse>(), cancellation: cancellationToken);
            return;
        }

        IEnumerable<GetUtxoByAddressResponse> unspentOutputs = [.. resolvedOutRefs
            .Select(outputBySlot =>
            {
                TransactionOutput txOutput = CborSerializer.Deserialize<TransactionOutput>(outputBySlot.Raw);
                
                string[] outRefParts = outputBySlot.OutRef.Split('#');
                string txHash = outRefParts[0];
                ulong index = ulong.Parse(outRefParts[1]);

                List<Amount> amounts = [];

                amounts.Add(new Amount(
                    Unit: "lovelace",
                    Quantity: txOutput.Amount().Lovelace().ToString()
                ));

                Dictionary<byte[], TokenBundleOutput> multiAssets = txOutput.Amount().MultiAsset();
                if (multiAssets?.Any() == true)
                {
                    IEnumerable<Amount> assetAmounts = multiAssets
                        .SelectMany(policyAssets => policyAssets.Value.Value.Select(asset => new Amount(
                            Unit: Convert.ToHexStringLower(policyAssets.Key) + Convert.ToHexStringLower(asset.Key),
                            Quantity: asset.Value.ToString()
                        )));
                    
                    amounts.AddRange(assetAmounts);
                }

                return new GetUtxoByAddressResponse(
                    Address: address!,
                    TxHash: txHash,
                    TxIndex: index,
                    OutputIndex: index,
                    Amount: amounts,
                    Block: outputBySlot.BlockHash,
                    DataHash: outputBySlot.ScriptDataHash,
                    InlineDatum: txOutput.DatumOption()?.Data() is not null
                        ? Convert.ToHexStringLower(txOutput.DatumOption()?.Data() ?? [])
                        : null,
                    ReferenceScriptHash: outputBySlot.ScriptHash
                );
            })];

        await SendAsync(unspentOutputs, cancellation: cancellationToken);
    }
}