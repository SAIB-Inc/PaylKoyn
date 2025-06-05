using Chrysalis.Cbor.Serialization;
using Chrysalis.Cbor.Types;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using Chrysalis.Network.Cbor.LocalTxSubmit;
using Chrysalis.Network.Multiplexer;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Models;

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
    private readonly int _processingInterval = configuration.GetValue("Workers:ProcessingDelayMs", 20000);

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

                    foreach (var submission in pendingSubmissions)
                    {
                        if (stoppingToken.IsCancellationRequested) break;

                        try
                        {
                            logger.LogInformation("About to submit transaction {Hash}", submission.Hash);

                            PostMaryTransaction tx = CborSerializer.Deserialize<PostMaryTransaction>(submission.TxRaw);
                            EraTx eraTx = new(6, new CborEncodedValue(submission.TxRaw));
                            LocalTxSubmissionMessage result = await client.LocalTxSubmit.SubmitTxAsync(new SubmitTx(new Value0(0), eraTx), stoppingToken);

                            switch (result)
                            {
                                case AcceptTx _:
                                    logger.LogInformation("Transaction {Hash} accepted. Updating to Inflight...", submission.Hash);

                                    await dbContext.TransactionSubmissions
                                        .Where(ts => ts.Hash == submission.Hash)
                                        .ExecuteUpdateAsync(ts => ts.SetProperty(t => t.Status, TransactionStatus.Inflight), stoppingToken);

                                    logger.LogInformation("Updated transaction with hash {Hash} to Inflight", submission.Hash);
                                    break;

                                case RejectTx rejectTx:
                                    
                                    await dbContext.TransactionSubmissions
                                        .Where(ts => ts.Hash == submission.Hash)
                                        .ExecuteUpdateAsync(ts => ts.SetProperty(t => t.Status, TransactionStatus.Rejected), stoppingToken);

                                    logger.LogError("Transaction {Hash} rejected: {Error}",
                                        submission.Hash, Convert.ToHexString(rejectTx.RejectReason.Value));
                                    break;

                                default:
                                    logger.LogError("Unexpected result for transaction {Hash}: {ResultType}",
                                        submission.Hash, result.GetType().Name);
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error submitting transaction {Hash}: {Message}", submission.Hash, ex.Message);
                        }
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

            await Task.Delay(_processingInterval, stoppingToken);
        }

        logger.LogInformation("Transaction submission worker stopped");
    }
}