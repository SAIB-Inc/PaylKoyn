using Chrysalis.Cbor.Extensions.Cardano.Core.Common;
using Chrysalis.Cbor.Extensions.Cardano.Core.Transaction;
using Chrysalis.Cbor.Types.Cardano.Core.Common;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using Chrysalis.Tx.Extensions;
using Chrysalis.Tx.Models;
using Chrysalis.Tx.Models.Cbor;
using Chrysalis.Wallet.Models.Keys;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Models.Template;
using PaylKoyn.Data.Services;
using PaylKoyn.ImageGen.Data;
using PaylKoyn.ImageGen.Services;
using WalletAddress = Chrysalis.Wallet.Models.Addresses.Address;

namespace PaylKoyn.ImageGen.Workers;

public partial class AirdropWorker(
    IDbContextFactory<MintDbContext> dbContextFactory,
    IConfiguration configuration,
    WalletService walletService,
    MintingService mintingService,
    ICardanoDataProvider cardanoDataProvider,
    TransactionService transactionService,
    ILogger<AirdropWorker> logger
) : BackgroundService
{
    private const int NormalRetryDelayMs = 5_000;
    private const int ErrorRetryDelayMs = 20_000;
    private const ulong MinimumLovelaceBalance = 2_500_000UL;
    private const ulong FixedLovelaceAmount = 2_000_000UL;
    private const int AirdropWalletIndex = 0;

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

        logger.LogInformation("Airdrop Worker started. Airdrop address: {Address}", airdropAddressBech32);

        PrivateKey privateKey = walletService.GetPaymentPrivateKey(_airdropSeed, AirdropWalletIndex);
        Dictionary<string, Dictionary<string, ulong>> assetMap = CreateAssetMap();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using MintDbContext dbContext = await dbContextFactory.CreateDbContextAsync(stoppingToken);

                MintRequest? pendingAirdrop = await GetNextPendingAirdropAsync(dbContext, stoppingToken);
                if (pendingAirdrop is null)
                {
                    await Task.Delay(NormalRetryDelayMs, stoppingToken);
                    continue;
                }

                if (!await HasSufficientBalanceAsync(airdropAddressBech32, stoppingToken))
                {
                    await Task.Delay(NormalRetryDelayMs, stoppingToken);
                    continue;
                }

                await ExecuteAirdropTransactionAsync(dbContext, pendingAirdrop, airdropAddressBech32, privateKey, assetMap, stoppingToken);
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

    private Dictionary<string, Dictionary<string, ulong>> CreateAssetMap() => new()
    {
        { _policyId, new Dictionary<string, ulong> { { _assetName, _airdropAmount } } },
        { "", new Dictionary<string, ulong>() { { "", FixedLovelaceAmount } } }
    };

    private static async Task<MintRequest?> GetNextPendingAirdropAsync(MintDbContext dbContext, CancellationToken stoppingToken)
    {
        return await dbContext.MintRequests
            .Where(request => request.Status == MintStatus.NftSent)
            .OrderBy(request => request.UpdatedAt)
            .FirstOrDefaultAsync(stoppingToken);
    }

    private async Task<bool> HasSufficientBalanceAsync(string airdropAddress, CancellationToken stoppingToken)
    {
        (bool success, IEnumerable<ResolvedInput> utxos) = await mintingService.TryGetUtxosAsync(airdropAddress);

        if (!success)
        {
            logger.LogWarning("Failed to retrieve UTXOs for airdrop address {Address}. Retrying in 5 seconds.", airdropAddress);
            return false;
        }

        (ulong lovelaceBalance, ulong assetBalance) = CalculateBalances(utxos);

        if (lovelaceBalance < MinimumLovelaceBalance || assetBalance < _airdropAmount)
        {
            logger.LogWarning("Insufficient balance for airdrop. Lovelace: {Lovelace}, Asset: {Asset}. Retrying in 5 seconds.",
                lovelaceBalance, assetBalance);
            await Task.Delay(NormalRetryDelayMs, stoppingToken);
            return false;
        }

        return true;
    }

    private (ulong LovelaceBalance, ulong AssetBalance) CalculateBalances(IEnumerable<ResolvedInput> utxos)
    {
        ulong lovelaceBalance = 0UL;
        ulong assetBalance = 0UL;
        string fullAssetId = _policyId + _assetName;

        foreach (ResolvedInput utxo in utxos)
        {
            Value amount = utxo.Output.Amount();
            lovelaceBalance += amount.Lovelace();
            assetBalance += amount.QuantityOf(fullAssetId) ?? 0UL;
        }

        return (lovelaceBalance, assetBalance);
    }

    private async Task ExecuteAirdropTransactionAsync(
        MintDbContext dbContext,
        MintRequest pendingAirdrop,
        string airdropAddress,
        PrivateKey privateKey,
        Dictionary<string, Dictionary<string, ulong>> assetMap,
        CancellationToken stoppingToken)
    {
        try
        {
            TransactionTemplate<MultiAssetTransferParams> transferTemplate = transactionService.MultiAssetTransfer(cardanoDataProvider);
            MultiAssetTransferParams transferParams = new(airdropAddress, pendingAirdrop.UserAddress, assetMap);
            Transaction unsignedTx = await transferTemplate(transferParams);
            Transaction signedTx = unsignedTx.Sign(privateKey);
            string txHash = await cardanoDataProvider.SubmitTransactionAsync(signedTx);

            pendingAirdrop.Status = MintStatus.TokenSent;
            pendingAirdrop.AirdropTxHash = txHash;
            pendingAirdrop.UpdatedAt = DateTime.UtcNow;

            dbContext.MintRequests.Update(pendingAirdrop);
            await dbContext.SaveChangesAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send airdrop for request {RequestId} to address {Address}. Retrying in 20 seconds.",
                pendingAirdrop.Id, pendingAirdrop.UserAddress);

            pendingAirdrop.UpdatedAt = DateTime.UtcNow;
            dbContext.MintRequests.Update(pendingAirdrop);
            await dbContext.SaveChangesAsync(stoppingToken);

            throw;
        }
    }
}