using Chrysalis.Cbor.Extensions.Cardano.Core.Common;
using Chrysalis.Cbor.Extensions.Cardano.Core.Transaction;
using Chrysalis.Cbor.Types.Cardano.Core.Common;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using Chrysalis.Network.Cbor.LocalStateQuery;
using Chrysalis.Tx.Extensions;
using Chrysalis.Tx.Models;
using Chrysalis.Tx.Models.Cbor;
using Chrysalis.Wallet.Models.Keys;
using Microsoft.Extensions.Logging;
using PaylKoyn.Data.Models.Template;
using PaylKoyn.Data.Services;

namespace PaylKoyn.Data.Services;

public class AssetTransferService(
    ICardanoDataProvider cardanoDataProvider,
    TransactionService transactionService,
    ILogger<AssetTransferService> logger
)
{
    private const ulong MinimumLovelaceBuffer = 500_000UL;

    public async Task<(bool Success, IEnumerable<ResolvedInput> Utxos)> TryGetUtxosAsync(string address)
    {
        try
        {
            List<ResolvedInput> utxos = await cardanoDataProvider.GetUtxosAsync([address]);
            return (utxos.Count != 0, utxos);
        }
        catch
        {
            return (false, Enumerable.Empty<ResolvedInput>());
        }
    }

    public static (ulong LovelaceBalance, ulong AssetBalance) CalculateBalances(
        IEnumerable<ResolvedInput> utxos,
        string policyId,
        string assetName)
    {
        ulong lovelaceBalance = 0UL;
        ulong assetBalance = 0UL;
        string fullAssetId = policyId + assetName;

        foreach (ResolvedInput utxo in utxos)
        {
            Value amount = utxo.Output.Amount();
            lovelaceBalance += amount.Lovelace();
            assetBalance += amount.QuantityOf(fullAssetId) ?? 0UL;
        }

        return (lovelaceBalance, assetBalance);
    }

    public static bool HasSufficientBalance(
        ulong lovelaceBalance,
        ulong assetBalance,
        ulong requiredLovelace,
        ulong requiredAssets)
    {
        return lovelaceBalance >= requiredLovelace && assetBalance >= requiredAssets;
    }

    public async Task<bool> ValidateAirdropBalanceAsync(
        string airdropAddress,
        string policyId,
        string assetName,
        ulong requiredLovelace,
        ulong requiredAssets)
    {
        (bool success, IEnumerable<ResolvedInput> utxos) = await TryGetUtxosAsync(airdropAddress);
        if (!success)
        {
            logger.LogWarning("Failed to retrieve UTXOs for address {Address}", airdropAddress);
            return false;
        }

        (ulong lovelaceBalance, ulong assetBalance) = CalculateBalances(utxos, policyId, assetName);
        bool hasBalance = HasSufficientBalance(lovelaceBalance, assetBalance, requiredLovelace, requiredAssets);

        if (!hasBalance)
        {
            logger.LogWarning("Insufficient balance. Lovelace: {Lovelace}, Asset: {Asset}",
                lovelaceBalance, assetBalance);
        }

        return hasBalance;
    }

    public async Task<Transaction> CreateAssetTransferTransactionAsync(
        string fromAddress,
        List<Recipient> recipients,
        PrivateKey privateKey)
    {
        MultiAssetTransferParams transferParams = new(fromAddress, recipients);

        Transaction unsignedTransaction = await transactionService.MultiAssetTransfer(transferParams, cardanoDataProvider);
        Transaction signedTransaction = unsignedTransaction.Sign(privateKey);

        return signedTransaction;
    }

    public async Task<string> SendAssetTransferAsync(
        Transaction transaction)
    {
        string txHash = await cardanoDataProvider.SubmitTransactionAsync(transaction);
        logger.LogInformation("Asset transfer submitted. TxHash: {TxHash}", txHash);

        return txHash;
    }

    public static Dictionary<string, Dictionary<string, ulong>> CreateAssetMap(
        string policyId,
        string assetName,
        ulong assetAmount,
        ulong lovelaceAmount = 0)
    {
        Dictionary<string, Dictionary<string, ulong>> assetMap = new Dictionary<string, Dictionary<string, ulong>>
        {
            { policyId, new Dictionary<string, ulong> { { assetName, assetAmount } } }
        };

        if (lovelaceAmount > 0)
        {
            assetMap.Add("", new Dictionary<string, ulong> { { "", lovelaceAmount } });
        }

        return assetMap;
    }

    public static ulong CalculateMinimumLovelaceRequired(ulong transferAmount) =>
        transferAmount + MinimumLovelaceBuffer;
}