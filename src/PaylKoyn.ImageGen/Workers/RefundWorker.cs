using Chrysalis.Cbor.Extensions.Cardano.Core.Common;
using Chrysalis.Cbor.Extensions.Cardano.Core.Transaction;
using Chrysalis.Cbor.Serialization;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using Chrysalis.Network.Cbor.LocalStateQuery;
using Chrysalis.Tx.Extensions;
using Chrysalis.Tx.Models;
using Chrysalis.Tx.Models.Cbor;
using Chrysalis.Wallet.Models.Keys;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Models.Template;
using PaylKoyn.Data.Services;
using PaylKoyn.Data.Utils;
using PaylKoyn.ImageGen.Data;
using PaylKoyn.ImageGen.Services;
using WalletAddress = Chrysalis.Wallet.Models.Addresses.Address;

namespace PaylKoyn.ImageGen.Workers;

public partial class RefundWorker(
    IDbContextFactory<MintDbContext> dbContextFactory,
    IConfiguration configuration,
    WalletService walletService,
    TransactionService txService,
    MintingService mintingService,
    ICardanoDataProvider cardanoDataProvider,
    ILogger<RefundWorker> logger
) : BackgroundService
{
    private const int ErrorRetryDelayMs = 20_000;
    private readonly string _seed = configuration.GetValue<string>("Seed") ?? throw new ArgumentNullException("Seed is not configured");


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using MintDbContext dbContext = await dbContextFactory.CreateDbContextAsync(stoppingToken);

                List<MintRequest> pendingRefunds = await dbContext.MintRequests
                    .Where(r => r.Status == MintStatus.RefundRequested)
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

    private async Task ProcessRefundsAsync(MintRequest request, MintDbContext dbContext)
    {
        try
        {
            WalletAddress tempWalletAddress = walletService.GetWalletAddress(_seed, request.Id);
            PrivateKey tempWalletPrivateKey = walletService.GetPaymentPrivateKey(_seed, request.Id);
            string tempWalletBech32 = tempWalletAddress.ToBech32();
            (bool Success, IEnumerable<ResolvedInput> Utxos) = await mintingService.TryGetUtxosAsync(tempWalletBech32);

            if (Success)
            {
                TransactionTemplate<RefundParams> transferTemplate = txService.Refund(cardanoDataProvider);
                RefundParams transferParams = new(
                    tempWalletBech32,
                    request.UserAddress,
                    Utxos.Aggregate(0UL, (sum, input) => sum + input.Output.Amount().Lovelace())
                );
                Transaction tx = await transferTemplate(transferParams);
                Transaction signedTx = tx.Sign(tempWalletPrivateKey);

                string txHash = await cardanoDataProvider.SubmitTransactionAsync(signedTx);

                logger.LogInformation("Refund transaction submitted for request {RequestId} to {Address}. TxHash: {TxHash}",
                    request.Id, request.UserAddress, txHash);

                request.Status = MintStatus.Refunded;
                request.RefundTxHash = txHash;
            }
            else
            {
                logger.LogWarning("No UTXOs found for refund request {RequestId} at address {Address}",
                    request.Id, tempWalletBech32);
                request.Status = MintStatus.Failed;
            }
        }
        catch
        {
            logger.LogDebug("Failed to process refund for request {RequestId}", request.Id);
            request.Status = MintStatus.Failed;
        }

        request.UpdatedAt = DateTime.UtcNow;
        dbContext.MintRequests.Update(request);
    }
}