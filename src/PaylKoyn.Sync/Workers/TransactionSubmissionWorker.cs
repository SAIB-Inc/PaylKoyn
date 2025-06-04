using Chrysalis.Cbor.Serialization;
using Chrysalis.Cbor.Types;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using Chrysalis.Network.Cbor.LocalTxSubmit;
using Chrysalis.Network.Multiplexer;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Models;
using PaylKoyn.Data.Models.Entity;

namespace PaylKoyn.Sync.Workers;

public class TransactionSubmissionWorker(
    IConfiguration configuration,
    IDbContextFactory<PaylKoynDbContext> dbContextFactory,
    ILogger<TransactionSubmissionWorker> logger
) : BackgroundService
{
    private readonly string _socketPath = configuration.GetValue<string>("CardanoNodeConnection:UnixSocket:Path") ??
        throw new InvalidOperationException("Unix socket path is not configured.");

    private readonly int _networkMagic = configuration.GetValue<int>("CardanoNodeConnection:NetworkMagic");
    private readonly int _processingInterval = configuration.GetValue("Workers:ProcessingDelayMs", 10000);
    private readonly int _batchSize = configuration.GetValue("Workers:BatchSize", 10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Transaction submission worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync(stoppingToken);

                var pendingSubmissions = await dbContext.TransactionSubmissions
                    .Where(ts => ts.Status == TransactionStatus.Pending)
                    .OrderBy(ts => ts.DateSubmitted)
                    .Take(_batchSize)
                    .ToListAsync(stoppingToken);

                if (!pendingSubmissions.Any())
                {
                    logger.LogDebug("No pending transactions to submit.");
                }
                else
                {
                    logger.LogInformation("Submitting {Count} pending transactions.", pendingSubmissions.Count);

                    using var client = await NodeClient.ConnectAsync(_socketPath, stoppingToken);
                    await client.StartAsync((ulong)_networkMagic);

                    var transactionsToUpdate = new List<(TransactionSubmissions original, TransactionSubmissions updated)>();

                    foreach (var submission in pendingSubmissions)
                    {
                        if (stoppingToken.IsCancellationRequested) break;

                        try
                        {
                            var tx = CborSerializer.Deserialize<PostMaryTransaction>(submission.TxRaw);
                            var eraTx = new EraTx(6, new CborEncodedValue(submission.TxRaw));
                            var result = await client.LocalTxSubmit.SubmitTxAsync(new SubmitTx(new Value0(0), eraTx), stoppingToken);

                            switch (result)
                            {
                                case AcceptTx _:
                                    logger.LogInformation("Transaction {Hash} accepted.", submission.Hash);
                                    transactionsToUpdate.Add((submission, submission with { Status = TransactionStatus.Inflight }));
                                    break;

                                case RejectTx rejectTx:
                                    logger.LogError("Transaction {Hash} rejected: {Error}",
                                        submission.Hash, Convert.ToHexString(rejectTx.RejectReason.Value));
                                    break;

                                default:
                                    logger.LogError("Unexpected result for transaction {Hash}: {ResultType}",
                                        submission.Hash, result.GetType().Name);
                                    break;
                            }
                            await Task.Delay(5000, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error submitting transaction {Hash}: {Message}", submission.Hash, ex.Message);
                        }
                    }

                    if (transactionsToUpdate.Any())
                    {
                        foreach (var (original, updated) in transactionsToUpdate)
                        {
                            dbContext.Entry(original).CurrentValues.SetValues(updated);
                        }

                        await dbContext.SaveChangesAsync(stoppingToken);
                        logger.LogInformation("Updated status for {Count} transactions", transactionsToUpdate.Count);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Transaction submission worker is stopping.");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in transaction submission worker");
            }

            try
            {
                await Task.Delay(_processingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Transaction submission worker stopped");
    }
}