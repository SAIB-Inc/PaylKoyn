
using Microsoft.EntityFrameworkCore;
using PaylKoyn.ImageGen.Data;
using PaylKoyn.ImageGen.Services;

namespace PaylKoyn.ImageGen.Workers;

public class PreUploadWorker(
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

                List<MintRequest> pendingUploadPayments = await dbContext.MintRequests
                    .OrderBy(p => p.UpdatedAt)
                    .Where(p => p.Status == MintStatus.Paid)
                    .Take(3)
                    .ToListAsync(stoppingToken);

                if (pendingUploadPayments.Count == 0)
                {
                    await Task.Delay(5000, stoppingToken);
                    continue;
                }

                Task<MintRequest>[] tasks = [.. pendingUploadPayments.Select(request =>
                    mintingService.RequestImageUploadAsync(request.Id)
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