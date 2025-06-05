
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Models;
using PaylKoyn.Node.Data;
using PaylKoyn.Node.Services;

namespace PaylKoyn.Node.Workers;

public class SubmitWorker(
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

                List<Wallet> pendingUploads = await fileService.GetActiveWalletsWithCleanupAsync(UploadStatus.Queued, limit: 3, stoppingToken);

                if (pendingUploads.Count == 0)
                {
                    await Task.Delay(5000, stoppingToken);
                    continue;
                }

                Task<Wallet>[] tasks = [.. pendingUploads.Select(request =>
                    fileService.SubmitTransactionsAsync(request.Address!)
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