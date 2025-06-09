
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Responses;
using PaylKoyn.Node.Data;
using PaylKoyn.Node.Services;

namespace PaylKoyn.Node.Workers;

public class TxWorker(
    IDbContextFactory<WalletDbContext> dbContextFactory,
    FileService fileService
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using WalletDbContext dbContext = await dbContextFactory.CreateDbContextAsync(stoppingToken);

                List<Wallet> pendingTxBuilds = await fileService.GetActiveWalletsWithCleanupAsync(UploadStatus.Paid, 10, stoppingToken);

                if (pendingTxBuilds.Count == 0)
                {
                    await Task.Delay(5000, stoppingToken);
                    continue;
                }

                Task[] tasks = [.. pendingTxBuilds.Select(request =>
                    fileService.PrepareTransactionsAsync(request.Address!)
                )];

                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(10000, stoppingToken);
            }
        }
    }
}