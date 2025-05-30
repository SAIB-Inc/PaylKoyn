using Argus.Sync.Reducers;
using Chrysalis.Cbor.Extensions.Cardano.Core;
using Chrysalis.Cbor.Extensions.Cardano.Core.Common;
using Chrysalis.Cbor.Extensions.Cardano.Core.Header;
using Chrysalis.Cbor.Extensions.Cardano.Core.Transaction;
using Chrysalis.Cbor.Serialization;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Models;
using PaylKoyn.Data.Models.Entity;
using PaylKoyn.Data.Utils;
using Block = Chrysalis.Cbor.Types.Cardano.Core.Block;

namespace PaylKoyn.Sync.Reducers;

public class OutputBySlotReducer(
    IDbContextFactory<PaylKoynDbContext> dbContextFactory
) : IReducer<OutputBySlot>
{
    public async Task RollBackwardAsync(ulong slot)
    {
        await using PaylKoynDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
        await dbContext.OutputsBySlot.Where(e => e.Slot >= slot).ExecuteDeleteAsync();
    }

    public async Task RollForwardAsync(Block block)
    {
        await using PaylKoynDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        IEnumerable<TransactionBody> transactions = block.TransactionBodies();

        if (!transactions.Any()) return;

        ulong currentSlot = block.Header().HeaderBody().Slot();

        IEnumerable<(string txHash, IEnumerable<TransactionOutput> outputs)> outputsByTx = transactions
            .Select(tx => (tx.Hash(), tx.Outputs()));

        IEnumerable<string> inputs = transactions.SelectMany(tx =>
            tx.Inputs().Select(input => $"{Convert.ToHexStringLower(input.TransactionId)}#{input.Index}"));
        
        IEnumerable<OutputBySlot> resolvedInputs = await dbContext.OutputsBySlot
            .Where(e => inputs.Contains(e.OutRef))
            .Where(obs => string.IsNullOrEmpty(obs.SpentTxHash))
            .ToListAsync();

        ProcessOutputs(outputsByTx, currentSlot, dbContext);
        ProcessInputs(resolvedInputs, transactions, dbContext);

        await dbContext.SaveChangesAsync();
    }

    private static void ProcessOutputs(
        IEnumerable<(string txHash, IEnumerable<TransactionOutput> outputs)> outputsByTx,
        ulong currentSlot,
        PaylKoynDbContext dbContext
    )
    {
        IEnumerable<OutputBySlot> newOutputs = outputsByTx
            .SelectMany(obtx =>
                obtx.outputs
                .Select((Output, Index) =>
                {
                    if (!ReducerUtils.IsRelevantAddress(Output, out string bech32Address)) return null;

                    OutputBySlot newEntry = new(
                        Slot: currentSlot,
                        OutRef: obtx.txHash + "#" + Index,
                        SpentTxHash: string.Empty,
                        Address: bech32Address,
                        OutputRaw: Output.Raw.HasValue ? Output.Raw.Value.ToArray() : CborSerializer.Serialize(Output)
                    );

                    return newEntry;
                })
            )
            .Where(e => e != null)!;

        dbContext.AddRange(newOutputs);
    }

    private static void ProcessInputs(
        IEnumerable<OutputBySlot> resolvedInputs,
        IEnumerable<TransactionBody> transactions,
        PaylKoynDbContext dbContext
    )
    {
        Dictionary<string, OutputBySlot> tracked = dbContext.OutputsBySlot.Local.ToDictionary(e => e.OutRef);

        IEnumerable<(string spentTxHash, OutputBySlot resolvedInput)> resolvedInputsByTx = transactions
            .SelectMany(tx =>
            {
                IEnumerable<string> txInputs = tx.Inputs().Select(input => $"{Convert.ToHexStringLower(input.TransactionId)}#{input.Index}");
                IEnumerable<OutputBySlot> resolvedInputsByTx = resolvedInputs.Where(ri => txInputs.Contains(ri.OutRef));
                return resolvedInputsByTx.Select(ribtx => (tx.Hash(), ribtx));
            });

        IEnumerable<OutputBySlot> updatedOutputs = resolvedInputsByTx.Select(resolvedInputByTx =>
        {
            if (!tracked.TryGetValue(resolvedInputByTx.resolvedInput.OutRef, out OutputBySlot? outputBySlot))
            {
                return null;
            }
            if (!string.IsNullOrEmpty(outputBySlot.SpentTxHash))
            {
                return null;
            }

            return resolvedInputByTx.resolvedInput with { SpentTxHash = resolvedInputByTx.spentTxHash };
        })
        .Where(e => e != null)!;
        
        dbContext.UpdateRange(updatedOutputs);
    }
}