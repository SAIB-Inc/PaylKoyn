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

        IEnumerable<TransactionSubmissions> submissionsToDelete = [.. dbContext.TransactionSubmissions
            .Where(x => x.ConfirmedSlot >= slot)];

        IEnumerable<TransactionBySlot> txsToDelete = [.. dbContext.TransactionsBySlot
            .Where(x => x.Slot >= slot)];

        dbContext.TransactionSubmissions.RemoveRange(submissionsToDelete);
        dbContext.TransactionsBySlot.RemoveRange(txsToDelete);

        await dbContext.SaveChangesAsync();
    }

    public async Task RollForwardAsync(Block block)
    {
        await using PaylKoynDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        IEnumerable<TransactionBody> transactions = block.TransactionBodies();
        Dictionary<int, AuxiliaryData> auxiliaryData = block.AuxiliaryDataSet();
        ulong currentSlot = block.Header().HeaderBody().Slot();

        IEnumerable<TransactionBySlot> newEntries = transactions.Select((transaction, index) =>
        {
            if (!auxiliaryData.TryGetValue(index, out AuxiliaryData? auxData)) return null;

            Metadata? metadata = auxData.Metadata();
            if (metadata is not null && metadata.Value().TryGetValue(_transactionMetadatumKey, out _))
            {
                return new TransactionBySlot(
                    Hash: transaction.Hash(),
                    Slot: currentSlot,
                    Metadata: CborSerializer.Serialize(metadata),
                    Body: CborSerializer.Serialize(transaction)
                );
            }

            return null;
        }).Where(x => x is not null)!;

        dbContext.TransactionsBySlot.AddRange(newEntries);

        IEnumerable<string> transactionHashes = [.. newEntries.Select(x => x.Hash)];

        IEnumerable<TransactionSubmissions> inflightTransactions = [.. dbContext.TransactionSubmissions
            .Where(ts => ts.Status == TransactionStatus.Inflight && transactionHashes.Contains(ts.Hash))];

        foreach (TransactionSubmissions submission in inflightTransactions)
        {
            TransactionSubmissions updatedSubmission = submission with
            {
                Status = TransactionStatus.Confirmed,
                ConfirmedSlot = currentSlot
            };

            dbContext.Entry(submission).CurrentValues.SetValues(updatedSubmission);
        }

        await dbContext.SaveChangesAsync();
    }
}
