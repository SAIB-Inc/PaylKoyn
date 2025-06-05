using System.Diagnostics;
using Argus.Sync.Utils;
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
using PaylKoyn.Data.Models;
using PaylKoyn.Data.Services;
using PaylKoyn.Node.Data;

namespace PaylKoyn.Node.Services;

public class FileService(
    IConfiguration configuration,
    TransactionService transactionService,
    ICardanoDataProvider cardanoDataProvider,
    IDbContextFactory<WalletDbContext> dbContextFactory,
    ILogger<FileService> logger
)
{
    private const decimal LovelaceToAdaRatio = 1_000_000m;
    private const uint DefaultMaxTransactionSize = 16384;
    private const int DefaultExpirationMinutes = 5;
    private const int DefaultUtxoCheckIntervalSeconds = 10;
    private const int DefaultSubmissionRetries = 3;
    private const ulong DefaultRevenueFee = 2_000_000UL;

    private readonly TimeSpan _paymentExpirationTime =
        TimeSpan.FromMinutes(int.TryParse(configuration["File:ExpirationMinutes"], out int minutes) ? minutes : DefaultExpirationMinutes);
    private readonly TimeSpan _utxoCheckInterval =
        TimeSpan.FromSeconds(int.TryParse(configuration["File:GetUtxosIntervalSeconds"], out int seconds) ? seconds : DefaultUtxoCheckIntervalSeconds);
    private readonly string _rewardAddress = configuration["RewardAddress"]
        ?? throw new ArgumentException("Reward Address cannot be null or empty.", nameof(configuration));
    private readonly string _tempFilePath = configuration["File:TempFilePath"] ?? "/tmp";
    private readonly int _submissionRetries =
        int.TryParse(configuration["File:SubmissionRetries"], out int retries) ? retries : DefaultSubmissionRetries;
    private readonly ulong _revenueFee =
        ulong.TryParse(configuration["File:RevenueFee"], out ulong revenueFee) ? revenueFee : DefaultRevenueFee;

    public async Task<string> UploadAsync(string address, byte[] file, string contentType, string fileName, PrivateKey paymentPrivateKey)
    {
        ValidateUploadParameters(address, file);

        using WalletDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
        Wallet wallet = await GetWalletForUploadAsync(dbContext, address);

        ulong requiredFee = await CalculateRequiredFeeAsync(file.Length);
        ValidateWalletStatusForUpload(wallet, address, requiredFee);

        string tempFilePath = await SaveFileTemporarilyAsync(address, file);
        try
        {
            IEnumerable<ResolvedInput> utxos = await WaitForUtxosAsync(address);
            ValidatePaymentAmount(utxos, requiredFee);

            string adaFsId = await PrepareTransactionsAsync(dbContext, wallet, address, file, fileName, contentType, utxos, paymentPrivateKey);

            logger.LogInformation("Upload prepared for {Address}, AdaFsId: {AdaFsId}", address, adaFsId);
            return adaFsId;
        }
        finally
        {
            CleanupTempFile(tempFilePath);
        }
    }

    public async Task<Wallet?> WaitForPaymentAsync(string address)
    {
        ValidateAddress(address);

        using WalletDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
        Wallet wallet = await GetWalletAsync(dbContext, address);

        try
        {
            IEnumerable<ResolvedInput> utxos = await WaitForUtxosAsync(address);
            ulong requiredFee = await CalculateRequiredFeeAsync(wallet.FileSize);

            ulong totalAmount = CalculateTotalAmount(utxos);
            wallet.Status = totalAmount >= requiredFee ? UploadStatus.Paid : wallet.Status;

            if (wallet.Status != UploadStatus.Paid)
            {
                logger.LogError("Insufficient payment for {Address}. Required: {Required}, Received: {Received}",
                    address, requiredFee, totalAmount);
            }
        }
        catch (TimeoutException ex)
        {
            logger.LogError(ex, "Payment timeout for {Address}", address);
            wallet.Status = UploadStatus.Failed;
            wallet.TransactionsRaw = null;
            wallet.Transactions = null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Payment error for {Address}", address);
        }

        return await UpdateWalletAsync(dbContext, wallet, address);
    }

    public async Task<Wallet> SubmitTransactionsAsync(string address)
    {
        using WalletDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
        Wallet wallet = await GetWalletWithTransactionsAsync(dbContext, address);

        (List<TxStatus> updatedTransactions, bool hasChanges) = await ProcessTransactionSubmissionsAsync(wallet.Transactions);

        if (hasChanges)
        {
            wallet.Transactions = updatedTransactions;
            wallet.UpdatedAt = DateTime.UtcNow;

            if (wallet.Transactions.All(tx => tx.IsSent))
            {
                wallet.Status = UploadStatus.Uploaded;
                logger.LogInformation("All transactions submitted for {Address}", address);
            }

            dbContext.Wallets.Update(wallet);
            await dbContext.SaveChangesAsync();
        }

        return wallet;
    }

    public async Task<(bool success, IEnumerable<ResolvedInput> utxos)> TryGetUtxosAsync(string address)
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

    private static void ValidateUploadParameters(string address, byte[] file)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("Address cannot be null or empty.", nameof(address));
        if (file?.Length == 0)
            throw new ArgumentException("File cannot be null or empty.", nameof(file));
    }

    private static void ValidateAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("Address cannot be null or empty.", nameof(address));
    }

    private async Task<Wallet> GetWalletForUploadAsync(WalletDbContext dbContext, string address)
    {
        Wallet? wallet = await dbContext.Wallets.FirstOrDefaultAsync(w => w.Address == address);
        if (wallet is null)
        {
            logger.LogError("Wallet not found: {Address}", address);
            throw new InvalidOperationException($"Wallet not found for address: {address}");
        }
        return wallet;
    }

    private async Task<Wallet> GetWalletAsync(WalletDbContext dbContext, string address)
    {
        Wallet? wallet = await dbContext.Wallets.FirstOrDefaultAsync(w => w.Address == address);
        if (wallet is null)
        {
            logger.LogError("Wallet not found: {Address}", address);
            throw new InvalidOperationException($"Wallet not found for address: {address}");
        }
        return wallet;
    }

    private async Task<Wallet> GetWalletWithTransactionsAsync(WalletDbContext dbContext, string address)
    {
        Wallet? wallet = await dbContext.Wallets.FirstOrDefaultAsync(w => w.Address == address);
        if (wallet?.Transactions is null || wallet.Transactions.Count == 0)
        {
            logger.LogError("No transactions to submit for {Address}", address);
            throw new InvalidOperationException($"Wallet not found for address: {address}");
        }
        return wallet;
    }

    private async Task<ulong> CalculateRequiredFeeAsync(int fileSize)
    {
        ProtocolParams protocolParams = await cardanoDataProvider.GetParametersAsync();
        ulong fee = transactionService.CalculateFee(fileSize, _revenueFee, protocolParams.MaxTransactionSize ?? DefaultMaxTransactionSize);

        logger.LogInformation("Required fee: {Fee} lovelace for {Size} bytes", fee, fileSize);
        return fee;
    }

    private void ValidateWalletStatusForUpload(Wallet wallet, string address, ulong requiredFee)
    {
        if (wallet.Status == UploadStatus.Waiting)
        {
            decimal requiredFeeAda = requiredFee / LovelaceToAdaRatio;
            throw new InvalidOperationException($"Please pay the required fee of {requiredFeeAda} $ADA to the address: {address} before uploading the file.");
        }

        if (wallet.Status != UploadStatus.Paid)
        {
            throw new InvalidOperationException($"File upload is either already in progress or completed for address: {address}. Current status: {wallet.Status}. AdaFsId: {wallet.AdaFsId}");
        }
    }

    private async Task<string> SaveFileTemporarilyAsync(string address, byte[] file)
    {
        string tempFilePath = Path.Combine(_tempFilePath, address);
        await File.WriteAllBytesAsync(tempFilePath, file);
        logger.LogInformation("File saved temporarily: {Path}", tempFilePath);
        return tempFilePath;
    }

    private void CleanupTempFile(string tempFilePath)
    {
        if (File.Exists(tempFilePath))
        {
            File.Delete(tempFilePath);
            logger.LogInformation("Temp file cleaned up: {Path}", tempFilePath);
        }
    }

    private static ulong CalculateTotalAmount(IEnumerable<ResolvedInput> utxos)
    {
        return utxos.Aggregate(0UL, (sum, utxo) => sum + utxo.Output.Amount().Lovelace());
    }

    private void ValidatePaymentAmount(IEnumerable<ResolvedInput> utxos, ulong requiredFee)
    {
        ulong totalAmount = CalculateTotalAmount(utxos);
        logger.LogInformation("Found {Count} UTXOs, total: {Amount} lovelace", utxos.Count(), totalAmount);

        if (totalAmount < requiredFee)
        {
            logger.LogError("Insufficient payment. Required: {Required}, Received: {Received}", requiredFee, totalAmount);
            throw new InvalidOperationException($"Insufficient payment. Required: {requiredFee} lovelace, but received: {totalAmount} lovelace");
        }

        logger.LogInformation("Payment validation successful");
    }

    private async Task<string> PrepareTransactionsAsync(
        WalletDbContext dbContext,
        Wallet wallet,
        string address,
        byte[] file,
        string fileName,
        string contentType,
        IEnumerable<ResolvedInput> utxos,
        PrivateKey paymentPrivateKey)
    {
        logger.LogInformation("Preparing upload transaction: {FileName}", fileName);

        ProtocolParams protocolParams = await cardanoDataProvider.GetParametersAsync();
        List<Transaction> transactions = transactionService.UploadFile(address, file, fileName, contentType, [.. utxos], protocolParams, _rewardAddress);
        List<Transaction> signedTransactions = transactions.Select(tx => tx.Sign(paymentPrivateKey)).ToList();

        PostMaryTransaction lastTransaction = (PostMaryTransaction)signedTransactions.Last();
        string adaFsId = Convert.ToHexString(HashUtil.ToBlake2b256(CborSerializer.Serialize(lastTransaction.TransactionBody))).ToLowerInvariant();

        wallet.AdaFsId = adaFsId;
        wallet.Transactions = [.. signedTransactions.Select(tx => new TxStatus(CborSerializer.Serialize(tx), false, false))];
        wallet.FileSize = file.Length;
        wallet.Status = UploadStatus.Queued;
        wallet.UpdatedAt = DateTime.UtcNow;

        dbContext.Wallets.Update(wallet);
        await dbContext.SaveChangesAsync();

        return adaFsId;
    }

    private async Task<Wallet> UpdateWalletAsync(WalletDbContext dbContext, Wallet wallet, string address)
    {
        wallet.UpdatedAt = DateTime.UtcNow;
        dbContext.Wallets.Update(wallet);
        await dbContext.SaveChangesAsync();
        logger.LogInformation("Wallet updated: {Address}", address);
        return wallet;
    }

    private async Task<(List<TxStatus> UpdatedTransactions, bool HasChanges)> ProcessTransactionSubmissionsAsync(List<TxStatus>? transactions)
    {
        List<TxStatus> updatedTransactions = [];
        bool hasChanges = false;

        foreach (TxStatus txStatus in transactions ?? [])
        {
            if (txStatus.IsSent)
            {
                updatedTransactions.Add(txStatus);
                continue;
            }

            Transaction transaction = Transaction.Read(txStatus.TxRaw);
            (bool wasSubmitted, TxStatus updatedStatus) = await TrySubmitTransactionWithRetriesAsync(transaction, txStatus);

            updatedTransactions.Add(updatedStatus);
            if (wasSubmitted) hasChanges = true;
        }

        return (updatedTransactions, hasChanges);
    }

    private async Task<(bool WasSubmitted, TxStatus UpdatedStatus)> TrySubmitTransactionWithRetriesAsync(Transaction transaction, TxStatus originalStatus)
    {
        int retriesRemaining = _submissionRetries;

        while (retriesRemaining > 0)
        {
            try
            {
                string txHash = await cardanoDataProvider.SubmitTransactionAsync(transaction);
                logger.LogInformation("Transaction submitted: {TxHash}", txHash);
                return (true, originalStatus with { IsSent = true });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Transaction submission failed, retrying...");
                retriesRemaining--;
                if (retriesRemaining > 0)
                    await Task.Delay(_utxoCheckInterval);
            }
        }

        return (false, originalStatus);
    }

    private async Task<IEnumerable<ResolvedInput>> WaitForUtxosAsync(string address)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < _paymentExpirationTime)
        {
            (bool isSuccess, IEnumerable<ResolvedInput> utxos) = await TryGetUtxosAsync(address);
            if (isSuccess) return utxos;

            logger.LogInformation("No UTXOs found for {Address}, retrying in {Interval}s...",
                address, _utxoCheckInterval.TotalSeconds);
            await Task.Delay(_utxoCheckInterval);
        }

        throw new TimeoutException("Upload request has expired, no UTXOs found for the address within the specified time.");
    }
}