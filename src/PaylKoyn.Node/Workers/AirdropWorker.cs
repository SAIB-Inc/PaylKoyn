using Chrysalis.Wallet.Models.Keys;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Models;
using PaylKoyn.Data.Responses;
using PaylKoyn.Data.Services;
using PaylKoyn.Data.Utils;
using PaylKoyn.Node.Data;
using PaylKoyn.Node.Services;

namespace PaylKoyn.Node.Workers;

public partial class AirdropWorker(
    IDbContextFactory<WalletDbContext> dbContextFactory,
    IConfiguration configuration,
    AssetTransferService assetTransferService,
    FileService fileService,
    ILogger<AirdropWorker> logger
) : BackgroundService
{
    private const int NormalRetryDelayMs = 5_000;
    private const int ErrorRetryDelayMs = 20_000;
    private const ulong MinimumLovelaceBalance = 2_500_000UL;
    private const int AirdropWalletIndex = 0;

    private readonly ulong _minUtxoAda = configuration.GetValue("Airdrop:MinUtxoAda", 2_000_000UL);
    private readonly string _airdropSeed = configuration.GetValue<string>("Airdrop:Seed")
        ?? throw new ArgumentNullException("Airdrop Seed is not configured");
    private readonly string _policyId = configuration.GetValue<string>("Airdrop:PolicyId")
        ?? throw new ArgumentNullException("Airdrop PolicyId is not configured");
    private readonly string _assetName = configuration.GetValue<string>("Airdrop:AssetName")
        ?? throw new ArgumentNullException("Airdrop AssetName is not configured");
    private readonly ulong _airdropAmount = configuration.GetValue<ulong?>("Airdrop:Amount")
        ?? throw new ArgumentNullException("Airdrop Amount is not configured");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Chrysalis.Wallet.Models.Enums.NetworkType networkType = WalletUtils.DetermineNetworkType(configuration);
        Chrysalis.Wallet.Models.Addresses.Address airdropAddress = WalletUtils.GetWalletAddress(_airdropSeed, AirdropWalletIndex, networkType);
        string airdropAddressBech32 = airdropAddress.ToBech32();
        PrivateKey privateKey = WalletUtils.GetPaymentPrivateKey(_airdropSeed, AirdropWalletIndex);
        Dictionary<string, Dictionary<string, ulong>> assetMap =
             AssetTransferService.CreateAssetMap(_policyId, _assetName, _airdropAmount, _minUtxoAda);

        logger.LogInformation("Node Airdrop Worker started. Address: {Address}", airdropAddressBech32);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using WalletDbContext dbContext = await dbContextFactory.CreateDbContextAsync(stoppingToken);

                List<Wallet> pendingWallets = await fileService.GetActiveWalletsWithCleanupAsync(UploadStatus.Uploaded, limit: 1, cancellationToken: stoppingToken);

                if (pendingWallets.Count == 0)
                {
                    await Task.Delay(NormalRetryDelayMs, stoppingToken);
                    continue;
                }

                Wallet pendingWallet = pendingWallets.First();

                bool hasBalance = await assetTransferService.ValidateAirdropBalanceAsync(
                    airdropAddressBech32, _policyId, _assetName, MinimumLovelaceBalance, _airdropAmount);

                if (!hasBalance)
                {
                    await Task.Delay(NormalRetryDelayMs, stoppingToken);
                    continue;
                }

                if (pendingWallet.AirdropAddress is null)
                {
                    logger.LogWarning("Pending wallet {Address} does not have an airdrop address set.", pendingWallet.Address);
                    pendingWallet.UpdatedAt = DateTime.UtcNow;
                    pendingWallet.Status = UploadStatus.Airdropped;
                    dbContext.Wallets.Update(pendingWallet);
                    await dbContext.SaveChangesAsync(stoppingToken);

                    continue;
                }

                await ExecuteAirdropAsync(dbContext, pendingWallet, airdropAddressBech32, privateKey, assetMap, stoppingToken);
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

    private async Task ExecuteAirdropAsync(
        WalletDbContext dbContext,
        Wallet pendingWallet,
        string airdropAddress,
        PrivateKey privateKey,
        Dictionary<string, Dictionary<string, ulong>> assetMap,
        CancellationToken stoppingToken)
    {
        try
        {
            string txHash = await assetTransferService.SendAssetTransferAsync(
                airdropAddress,
                pendingWallet.AirdropAddress!,
                assetMap,
                privateKey);

            await UpdateWalletAsCompletedAsync(dbContext, pendingWallet, txHash, stoppingToken);

            logger.LogInformation("Airdrop completed for wallet {Address}. TxHash: {TxHash}",
                pendingWallet.Address, txHash);
        }
        catch
        {
            logger.LogInformation("Airdrop failed for wallet {Address}", pendingWallet.Address);

            await UpdateWalletTimestampAsync(dbContext, pendingWallet, stoppingToken);
            throw;
        }
    }

    private static async Task UpdateWalletAsCompletedAsync(
        WalletDbContext dbContext,
        Wallet wallet,
        string txHash,
        CancellationToken stoppingToken)
    {
        wallet.Status = UploadStatus.Airdropped;
        wallet.AirdropTxHash = txHash;
        wallet.UpdatedAt = DateTime.UtcNow;

        dbContext.Wallets.Update(wallet);
        await dbContext.SaveChangesAsync(stoppingToken);
    }

    private static async Task UpdateWalletTimestampAsync(
        WalletDbContext dbContext,
        Wallet wallet,
        CancellationToken stoppingToken)
    {
        wallet.UpdatedAt = DateTime.UtcNow;
        dbContext.Wallets.Update(wallet);
        await dbContext.SaveChangesAsync(stoppingToken);
    }
}