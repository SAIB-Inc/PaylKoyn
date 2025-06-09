using Chrysalis.Wallet.Models.Keys;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Models;
using PaylKoyn.Data.Responses;
using PaylKoyn.Data.Models.Template;
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
    private readonly int _maxAirdropCount = configuration.GetValue<int>("Airdrop:MaxCount", 15);

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

                List<Wallet> pendingWallets = await fileService.GetActiveWalletsWithCleanupAsync(UploadStatus.Uploaded, _maxAirdropCount, cancellationToken: stoppingToken);

                if (pendingWallets.Count == 0)
                {
                    await Task.Delay(NormalRetryDelayMs, stoppingToken);
                    continue;
                }

                bool hasBalance = await assetTransferService.ValidateAirdropBalanceAsync(
                    airdropAddressBech32, _policyId, _assetName, MinimumLovelaceBalance, _airdropAmount);

                if (!hasBalance)
                {
                    await Task.Delay(NormalRetryDelayMs, stoppingToken);
                    continue;
                }

                foreach (Wallet pendingWallet in pendingWallets)
                {
                    if (pendingWallet.AirdropAddress is null)
                    {
                        logger.LogWarning("Pending wallet {Address} does not have an airdrop address set.", pendingWallet.Address);
                        pendingWallet.UpdatedAt = DateTime.UtcNow;
                        pendingWallet.Status = UploadStatus.Airdropped;
                        dbContext.Wallets.Update(pendingWallet);
                        continue;
                    }
                    await dbContext.SaveChangesAsync(stoppingToken);
                }


                await ExecuteAirdropAsync(dbContext, pendingWallets, airdropAddressBech32, privateKey, assetMap, stoppingToken);
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
        List<Wallet> pendingWallets,
        string airdropAddress,
        PrivateKey privateKey,
        Dictionary<string, Dictionary<string, ulong>> assetMap,
        CancellationToken stoppingToken)
    {
        try
        {
            List<Recipient> recipients = [.. pendingWallets.Select(wallet => new Recipient(wallet.AirdropAddress!, assetMap))];
            string txHash = await assetTransferService.SendAssetTransferAsync(
                airdropAddress,
                recipients,
                privateKey);

            await UpdateWalletAsCompletedAsync(dbContext, pendingWallets, txHash, stoppingToken);

            logger.LogInformation("Airdrop completed for {WalletCount} wallets. TxHash: {TxHash}. Addresses: [{Addresses}]",
                pendingWallets.Count,
                txHash,
                string.Join(", ", pendingWallets.Select(w => w.Address)));
        }
        catch
        {
            logger.LogInformation("Airdrop failed for {WalletCount} wallets. Addresses: [{Addresses}]",
                pendingWallets.Count,
                string.Join(", ", pendingWallets.Select(w => w.Address)));

            await UpdateWalletTimestampAsync(dbContext, pendingWallets, stoppingToken);
            throw;
        }
    }

    private static async Task UpdateWalletAsCompletedAsync(
        WalletDbContext dbContext,
        List<Wallet> wallets,
        string txHash,
        CancellationToken stoppingToken)
    {
        foreach (var wallet in wallets)
        {
            wallet.Status = UploadStatus.Airdropped;
            wallet.AirdropTxHash = txHash;
            wallet.UpdatedAt = DateTime.UtcNow;
            dbContext.Wallets.Update(wallet);
        }

        await dbContext.SaveChangesAsync(stoppingToken);
    }

    private static async Task UpdateWalletTimestampAsync(
        WalletDbContext dbContext,
        List<Wallet> wallets,
        CancellationToken stoppingToken)
    {
        foreach (var wallet in wallets)
        {
            wallet.UpdatedAt = DateTime.UtcNow;
            dbContext.Wallets.Update(wallet);
        }
        await dbContext.SaveChangesAsync(stoppingToken);
    }
}