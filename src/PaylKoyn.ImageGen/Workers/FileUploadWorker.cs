
using Microsoft.EntityFrameworkCore;
using PaylKoyn.ImageGen.Data;
using PaylKoyn.ImageGen.Services;

namespace Paylkoyn.ImageGen.Workers;

public class FileUploadWorker(
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

                List<MintRequest> pendingUploads = await dbContext.MintRequests
                    .OrderBy(p => p.UpdatedAt)
                    .Where(p => p.Status == MintStatus.UploadPaymentSent)
                    .Take(5)
                    .ToListAsync(stoppingToken);

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