using Argus.Sync.Reducers;
using Chrysalis.Cbor.Extensions.Cardano.Core;
using Chrysalis.Cbor.Extensions.Cardano.Core.Header;
using Chrysalis.Cbor.Extensions.Cardano.Core.Transaction;
using Chrysalis.Cbor.Serialization;
using Chrysalis.Cbor.Types.Cardano.Core;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Models;
using PaylKoyn.Data.Models.Entity;

namespace PaylKoyn.Sync.Reducers;

public class TransactionBySlotReducer(
    IDbContextFactory<PaylKoynDbContext> dbContextFactory
) : IReducer<TransactionBySlot>
{
    private readonly ulong _transactionMetadatumKey = 6673;

    public async Task RollBackwardAsync(ulong slot)
    {
        await using PaylKoynDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
        await dbContext.TransactionsBySlot.Where(x => x.Slot >= slot).ExecuteDeleteAsync();
    }

    public async Task RollForwardAsync(Block block)
    {
        await using PaylKoynDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        IEnumerable<TransactionBody> transactions = block.TransactionBodies();
        Dictionary<int, AuxiliaryData> auxiliaryData = block.AuxiliaryDataSet();
        ulong currentSlot = block.Header().HeaderBody().Slot();

        IEnumerable<TransactionBySlot> newEntries = transactions.Select((transaction, index) =>
        {
            if (auxiliaryData.TryGetValue(index, out AuxiliaryData? auxData))
            {
                Metadata metadata = auxData.Metadata()!;
                if (metadata.Value().TryGetValue(_transactionMetadatumKey, out TransactionMetadatum? txMetadatum))
                {
                    return new TransactionBySlot(
                        Hash: transaction.Hash(),
                        Slot: currentSlot,
                        Metadata: CborSerializer.Serialize(txMetadatum),
                        Body: CborSerializer.Serialize(transaction)
                    );
                }
            }

            return null;
        }).Where(x => x is not null)!;

        dbContext.TransactionsBySlot.AddRange(newEntries);
        await dbContext.SaveChangesAsync();
    }
}
