using Argus.Sync.Reducers;
using Chrysalis.Cbor.Extensions.Cardano.Core;
using Chrysalis.Cbor.Extensions.Cardano.Core.Header;
using Chrysalis.Cbor.Extensions.Cardano.Core.Transaction;
using Chrysalis.Cbor.Types.Cardano.Core;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Models;
using PaylKoyn.Data.Models.Entity;

namespace PaylKoyn.Sync.Reducers;

public class TransactionSubmissionReducer(
    IDbContextFactory<PaylKoynDbContext> dbContextFactory
) : IReducer<TransactionBySlot>
{
    public async Task RollBackwardAsync(ulong slot)
    {
        await using PaylKoynDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        await dbContext.TransactionSubmissions
            .Where(ts => ts.Status == TransactionStatus.Confirmed && ts.ConfirmedSlot >= slot)
            .ExecuteUpdateAsync(ts => ts
                .SetProperty(t => t.Status, TransactionStatus.Pending)
                .SetProperty(t => t.ConfirmedSlot, (ulong?)null));
    }

    public async Task RollForwardAsync(Block block)
    {
        await using PaylKoynDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        IEnumerable<TransactionBody> transactions = block.TransactionBodies();
        ulong currentSlot = block.Header().HeaderBody().Slot();

        IEnumerable<string> transactionHashes = [.. transactions.Select(tx => tx.Hash())];

        await dbContext.TransactionSubmissions
            .Where(ts => ts.Status == TransactionStatus.Inflight && transactionHashes.Contains(ts.Hash))
            .ExecuteUpdateAsync(ts => ts
                .SetProperty(t => t.Status, TransactionStatus.Confirmed)
                .SetProperty(t => t.ConfirmedSlot, currentSlot));
    }
}