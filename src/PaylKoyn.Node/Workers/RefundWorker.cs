using Chrysalis.Wallet.Models.Keys;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Responses;
using PaylKoyn.Data.Models.Template;
using PaylKoyn.Data.Services;
using PaylKoyn.Node.Data;
using PaylKoyn.Node.Services;
using Chrysalis.Tx.Models;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using WalletAddress = Chrysalis.Wallet.Models.Addresses.Address;
using Chrysalis.Tx.Models.Cbor;
using Chrysalis.Cbor.Extensions.Cardano.Core.Transaction;
using Chrysalis.Cbor.Extensions.Cardano.Core.Common;
using Chrysalis.Tx.Extensions;

namespace PaylKoyn.Node.Workers;

public partial class RefundWorker(
    IDbContextFactory<WalletDbContext> dbContextFactory,
    IConfiguration configuration,
    WalletService walletService,
    TransactionService txService,
    FileService fileService,
    ICardanoDataProvider cardanoDataProvider,
    ILogger<RefundWorker> logger
) : BackgroundService
{
    private const int ErrorRetryDelayMs = 20_000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using WalletDbContext dbContext = await dbContextFactory.CreateDbContextAsync(stoppingToken);

                List<Wallet> pendingRefunds = await dbContext.Wallets
                    .Where(r => r.Status == UploadStatus.RefundRequested)
                    .ToListAsync(stoppingToken);

                if (pendingRefunds.Count == 0)
                {
                    await Task.Delay(5000, stoppingToken);
                    continue;
                }

                Task[] tasks = [.. pendingRefunds.Select(request =>
                    ProcessRefundsAsync(request, dbContext)
                )];

                await Task.WhenAll(tasks);
                await dbContext.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(ErrorRetryDelayMs, stoppingToken);
            }
        }
    }

    private async Task ProcessRefundsAsync(Wallet request, WalletDbContext dbContext)
    {
        try
        {
            WalletAddress tempWalletAddress = walletService.GetWalletAddress(request.Id);
            PrivateKey tempWalletPrivateKey = walletService.GetPaymentPrivateKey(request.Id);
            string tempWalletBech32 = tempWalletAddress.ToBech32();
            (bool Success, IEnumerable<ResolvedInput> Utxos) = await fileService.TryGetUtxosAsync(tempWalletBech32);

            if (Success)
            {
                TransactionTemplate<RefundParams> transferTemplate = txService.Refund(cardanoDataProvider);
                RefundParams transferParams = new(
                    tempWalletBech32,
                    request.UserAddress!,
                    Utxos.Aggregate(0UL, (sum, input) => sum + input.Output.Amount().Lovelace())
                );
                Transaction tx = await transferTemplate(transferParams);
                Transaction signedTx = tx.Sign(tempWalletPrivateKey);

                string txHash = await cardanoDataProvider.SubmitTransactionAsync(signedTx);

                logger.LogInformation("Refund transaction submitted for request {RequestId} to {Address}. TxHash: {TxHash}",
                    request.Id, request.Address, txHash);

                request.Status = UploadStatus.Refunded;
                request.RefundTxHash = txHash;
            }
            else
            {
                logger.LogWarning("No UTXOs found for refund request {RequestId} at address {Address}",
                    request.Id, tempWalletBech32);
                request.Status = UploadStatus.Failed;
            }
        }
        catch
        {
            logger.LogDebug("Failed to process refund for request {RequestId}", request.Id);
            request.Status = UploadStatus.Failed;
        }

        request.UpdatedAt = DateTime.UtcNow;
        dbContext.Wallets.Update(request);
    }
}