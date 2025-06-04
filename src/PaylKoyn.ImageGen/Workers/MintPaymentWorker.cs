
using Microsoft.EntityFrameworkCore;
using PaylKoyn.ImageGen.Data;
using PaylKoyn.ImageGen.Services;

namespace Paylkoyn.ImageGen.Workers;

public class MintPaymentWorker(
    IDbContextFactory<MintDbContext> dbContextFactory,
    IConfiguration configuration,
    MintingService mintingService
) : BackgroundService
{
    private readonly ulong _mintingFee = configuration.GetValue<ulong>("Minting:MintingFee", 100_000_000UL);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using MintDbContext dbContext = await dbContextFactory.CreateDbContextAsync(stoppingToken);

                List<MintRequest> pendingPayments = await dbContext.MintRequests
                    .OrderBy(p => p.UpdatedAt)
                    .Where(p => p.Status == MintStatus.Pending)
                    .Take(5)
                    .ToListAsync(stoppingToken);

                if (pendingPayments.Count == 0)
                {
                    await Task.Delay(5000, stoppingToken); // Wait 5 seconds
                    continue;
                }

                Task<MintRequest>[] tasks = [.. pendingPayments.Select(request =>
                    mintingService.WaitForPaymentAsync(request.Id, _mintingFee)
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