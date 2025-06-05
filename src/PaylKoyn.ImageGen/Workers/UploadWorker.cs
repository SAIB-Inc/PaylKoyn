
using Microsoft.EntityFrameworkCore;
using PaylKoyn.ImageGen.Data;
using PaylKoyn.ImageGen.Services;

namespace PaylKoyn.ImageGen.Workers;

public class UploadWorker(
    IDbContextFactory<MintDbContext> dbContextFactory,
    MintingService mintingService
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using MintDbContext dbContext = await dbContextFactory.CreateDbContextAsync(stoppingToken);

                List<MintRequest> pendingUploads = await mintingService.GetActiveRequestsWithCleanupAsync(MintStatus.Processing, 5, stoppingToken);

                if (pendingUploads.Count == 0)
                {
                    await Task.Delay(5000, stoppingToken);
                    continue;
                }

                Task<MintRequest>[] tasks = [.. pendingUploads.Select(request =>
                    mintingService.UploadImageAsync(request.Id)
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