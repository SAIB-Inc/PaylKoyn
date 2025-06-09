using Chrysalis.Wallet.Models.Keys;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Services;
using PaylKoyn.ImageGen.Data;
using PaylKoyn.ImageGen.Services;
using WalletAddress = Chrysalis.Wallet.Models.Addresses.Address;

namespace PaylKoyn.ImageGen.Workers;

public partial class AirdropWorker(
    IDbContextFactory<MintDbContext> dbContextFactory,
    IConfiguration configuration,
    WalletService walletService,
    AssetTransferService assetTransferService,
    MintingService mintingService,
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
        WalletAddress airdropAddress = walletService.GetWalletAddress(_airdropSeed, AirdropWalletIndex);
        string airdropAddressBech32 = airdropAddress.ToBech32();
        PrivateKey privateKey = walletService.GetPaymentPrivateKey(_airdropSeed, AirdropWalletIndex);
        Dictionary<string, Dictionary<string, ulong>> assetMap =
            AssetTransferService.CreateAssetMap(_policyId, _assetName, _airdropAmount, _minUtxoAda);

        logger.LogInformation("Airdrop Worker started. Address: {Address}", airdropAddressBech32);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using MintDbContext dbContext = await dbContextFactory.CreateDbContextAsync(stoppingToken);

                IEnumerable<MintRequest> pendingAirdrops = await mintingService.GetActiveRequestsWithCleanupAsync(MintStatus.NftSent, 1, stoppingToken);
                MintRequest? pendingAirdrop = pendingAirdrops.FirstOrDefault();

                if (pendingAirdrop is null)
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

                await ExecuteAirdropAsync(dbContext, pendingAirdrop, airdropAddressBech32, privateKey, assetMap, stoppingToken);
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
        MintDbContext dbContext,
        MintRequest pendingAirdrop,
        string airdropAddress,
        PrivateKey privateKey,
        Dictionary<string, Dictionary<string, ulong>> assetMap,
        CancellationToken stoppingToken)
    {
        try
        {
            string txHash = await assetTransferService.SendAssetTransferAsync(
                airdropAddress,
                pendingAirdrop.UserAddress,
                assetMap,
                privateKey);

            await UpdateRequestAsCompletedAsync(dbContext, pendingAirdrop, txHash, stoppingToken);

            logger.LogInformation("Airdrop completed for request {RequestId}. TxHash: {TxHash}", pendingAirdrop.Id, txHash);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Airdrop failed for request {RequestId} to address {Address}",
                pendingAirdrop.Id, pendingAirdrop.UserAddress);

            await UpdateRequestTimestampAsync(dbContext, pendingAirdrop, stoppingToken);
            throw;
        }
    }

    private static async Task UpdateRequestAsCompletedAsync(
        MintDbContext dbContext,
        MintRequest request,
        string txHash,
        CancellationToken stoppingToken)
    {
        request.Status = MintStatus.TokenSent;
        request.AirdropTxHash = txHash;
        request.UpdatedAt = DateTime.UtcNow;

        dbContext.MintRequests.Update(request);
        await dbContext.SaveChangesAsync(stoppingToken);
    }

    private static async Task UpdateRequestTimestampAsync(
        MintDbContext dbContext,
        MintRequest request,
        CancellationToken stoppingToken)
    {
        request.UpdatedAt = DateTime.UtcNow;
        dbContext.MintRequests.Update(request);
        await dbContext.SaveChangesAsync(stoppingToken);
    }
}