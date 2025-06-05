
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Models;
using PaylKoyn.Node.Data;
using PaylKoyn.Node.Services;

namespace PaylKoyn.Node.Workers;

public class PaymentWorker(
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

                List<Wallet> pendingPayments = await dbContext.Wallets
                    .OrderBy(p => p.UpdatedAt)
                    .Where(p => p.Status == UploadStatus.Waiting)
                    .Take(5)
                    .ToListAsync(stoppingToken);

                if (pendingPayments.Count == 0)
                {
                    await Task.Delay(5000, stoppingToken);
                    continue;
                }

                Task<Wallet?>[] tasks = [.. pendingPayments.Select(request =>
                    fileService.WaitForPaymentAsync(request.Address)
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