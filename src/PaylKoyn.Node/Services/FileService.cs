using System.Diagnostics;
using System.Text.Json;
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
using PaylKoyn.Data.Responses;
using PaylKoyn.Data.Services;
using PaylKoyn.Node.Data;

namespace PaylKoyn.Node.Services;

public class FileService(
    IConfiguration configuration,
    TransactionService transactionService,
    ICardanoDataProvider cardanoDataProvider,
    WalletService walletService,
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
    private readonly TimeSpan _requestExpirationTime = TimeSpan.FromMinutes(
        configuration.GetValue("RequestExpirationMinutes", 30));

    public async Task<ulong> UploadAsync(string address, byte[] file, string contentType, string fileName)
    {
        ValidateUploadParameters(address, file);

        await SaveFileWithMetadataAsync(address, file, fileName, contentType);

        return await CalculateRequiredFeeAsync(file.Length);
    }

    public async Task<List<Wallet>> GetActiveWalletsByStatusAsync(
        UploadStatus status,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        using WalletDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        DateTime cutoffTime = DateTime.UtcNow - _requestExpirationTime;

        IQueryable<Wallet> query = dbContext.Wallets
            .Where(wallet => wallet.Status == status)
            .Where(wallet => wallet.CreatedAt >= cutoffTime) // Only non-expired
            .OrderBy(wallet => wallet.UpdatedAt)
            .Take(limit);

        // Add special condition for SubmitWorker (Queued status)
        if (status == UploadStatus.Queued)
        {
            query = (IOrderedQueryable<Wallet>)query
                .Where(wallet => wallet.TransactionsRaw != null && wallet.TransactionsRaw != "[]");
        }

        return await query.ToListAsync(cancellationToken);
    }

    // Method to clean up expired wallets for a specific status
    public async Task<int> MarkExpiredWalletsAsFailedAsync(
        UploadStatus status,
        CancellationToken cancellationToken = default)
    {
        using WalletDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        DateTime cutoffTime = DateTime.UtcNow - _requestExpirationTime;

        List<Wallet> expiredWallets = await dbContext.Wallets
            .Where(wallet => wallet.Status == status)
            .Where(wallet => wallet.CreatedAt < cutoffTime)
            .ToListAsync(cancellationToken);

        if (expiredWallets.Count > 0)
        {
            logger.LogWarning("Marking {Count} expired {Status} wallets as failed (older than {Minutes} minutes)",
                expiredWallets.Count, status.ToString().ToLowerInvariant(), _requestExpirationTime.TotalMinutes);

            foreach (Wallet? expiredWallet in expiredWallets)
            {
                expiredWallet.LastValidStatus = expiredWallet.Status;
                expiredWallet.Status = UploadStatus.Failed;
                expiredWallet.UpdatedAt = DateTime.UtcNow;
            }

            dbContext.Wallets.UpdateRange(expiredWallets);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // Clean up temporary files for expired wallets if they exist
        foreach (Wallet expiredWallet in expiredWallets)
        {
            CleanupFiles(expiredWallet.Address!);
        }

        return expiredWallets.Count;
    }

    // Combined method that does both cleanup and retrieval
    public async Task<List<Wallet>> GetActiveWalletsWithCleanupAsync(
        UploadStatus status,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        // First clean up expired wallets
        await MarkExpiredWalletsAsFailedAsync(status, cancellationToken);

        // Then get active wallets
        return await GetActiveWalletsByStatusAsync(status, limit, cancellationToken);
    }

    public async Task<Wallet?> WaitForPaymentAsync(string address)
    {
        ValidateAddress(address);

        using WalletDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
        Wallet? wallet = await dbContext.Wallets.FirstOrDefaultAsync(w => w.Address == address, cancellationToken: default)
            ?? throw new InvalidOperationException($"Wallet not found for address: {address}");

        try
        {
            IEnumerable<ResolvedInput> utxos = await WaitForUtxosAsync(address);
            ulong requiredFee = await CalculateRequiredFeeAsync(wallet.FileSize);

            ulong totalAmount = CalculateTotalAmount(utxos);
            wallet.Status = totalAmount >= requiredFee ? UploadStatus.Paid : wallet.Status;

            if (wallet.Status != UploadStatus.Paid)
            {
                logger.LogInformation("Insufficient payment for {Address}. Required: {Required}, Received: {Received}",
                    address, requiredFee, totalAmount);
            }
        }
        catch (TimeoutException ex)
        {
            logger.LogInformation(ex, "Payment timeout for {Address}", address);
            wallet.LastValidStatus = wallet.Status;
            wallet.Status = UploadStatus.Failed;
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Payment error for {Address}", address);
        }

        return await UpdateWalletAsync(dbContext, wallet, address);
    }

    public async Task<Wallet> SubmitTransactionsAsync(string address)
    {
        using WalletDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
        Wallet? wallet = await dbContext.Wallets.FirstOrDefaultAsync(w => w.Address == address, cancellationToken: default)
            ?? throw new InvalidOperationException($"Wallet not found for address: {address}");

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

    public async Task<ulong> CalculateRequiredFeeAsync(int fileSize)
    {
        ProtocolParams protocolParams = await cardanoDataProvider.GetParametersAsync();
        ulong fee = transactionService.CalculateFee(fileSize, _revenueFee, protocolParams.MaxTransactionSize ?? DefaultMaxTransactionSize);

        logger.LogInformation("Required fee: {Fee} lovelace for {Size} bytes", fee, fileSize);
        return fee;
    }

    private async Task SaveFileWithMetadataAsync(string address, byte[] file, string fileName, string contentType)
    {
        if (!Directory.Exists(_tempFilePath))
            Directory.CreateDirectory(_tempFilePath);

        // Save the actual file
        string tempFilePath = Path.Combine(_tempFilePath, $"{address}.tmp");
        await File.WriteAllBytesAsync(tempFilePath, file);

        // Save metadata
        var metadata = new FileMetadata { FileName = fileName, ContentType = contentType };
        string metadataJson = JsonSerializer.Serialize(metadata);
        string metadataPath = Path.Combine(_tempFilePath, $"{address}.meta");
        await File.WriteAllTextAsync(metadataPath, metadataJson);

        logger.LogInformation("Saved file and metadata for {Address}", address);
    }

    private async Task<FileMetadata?> GetFileMetadataAsync(string address)
    {
        string metadataPath = Path.Combine(_tempFilePath, $"{address}.meta");

        if (!File.Exists(metadataPath))
            return null;

        try
        {
            string json = await File.ReadAllTextAsync(metadataPath);
            return JsonSerializer.Deserialize<FileMetadata>(json);
        }
        catch
        {
            return null;
        }
    }

    private void CleanupFiles(string address)
    {
        try
        {
            string tempFile = Path.Combine(_tempFilePath, $"{address}.tmp");
            string metaFile = Path.Combine(_tempFilePath, $"{address}.meta");

            if (File.Exists(tempFile)) File.Delete(tempFile);
            if (File.Exists(metaFile)) File.Delete(metaFile);

            logger.LogInformation("Cleaned up files for {Address}", address);
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Failed to cleanup files for {Address}", address);
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
            logger.LogInformation("Insufficient payment. Required: {Required}, Received: {Received}", requiredFee, totalAmount);
            throw new InvalidOperationException($"Insufficient payment. Required: {requiredFee} lovelace, but received: {totalAmount} lovelace");
        }

        logger.LogInformation("Payment validation successful");
    }

    public async Task PrepareTransactionsAsync(string address)
    {
        using WalletDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
        Wallet wallet = await dbContext.Wallets.FirstOrDefaultAsync(w => w.Address == address, cancellationToken: default)
            ?? throw new InvalidOperationException($"Wallet not found for address: {address}");

        // fetch temporary file
        FileMetadata? metadata = await GetFileMetadataAsync(address);

        if (metadata is not null)
        {
            string tempFilePath = Path.Combine(_tempFilePath, $"{address}.tmp");
            byte[] file = await File.ReadAllBytesAsync(tempFilePath);

            (_, IEnumerable<ResolvedInput> Utxos) = await TryGetUtxosAsync(address);
            ProtocolParams protocolParams = await cardanoDataProvider.GetParametersAsync();
            PrivateKey paymentPrivateKey = walletService.GetPaymentPrivateKey(wallet.Id);
            List<Transaction> transactions = transactionService.UploadFile(
                address, file,
                metadata!.FileName,
                metadata!.ContentType,
                [.. Utxos],
                protocolParams,
                _rewardAddress
            );
            List<Transaction> signedTransactions = [.. transactions.Select(tx => tx.Sign(paymentPrivateKey))];

            PostMaryTransaction lastTransaction = (PostMaryTransaction)signedTransactions.Last();
            string adaFsId = Convert.ToHexString(HashUtil.ToBlake2b256(CborSerializer.Serialize(lastTransaction.TransactionBody))).ToLowerInvariant();

            wallet.AdaFsId = adaFsId;
            wallet.Transactions = [.. signedTransactions.Select(tx => new TxStatus(CborSerializer.Serialize(tx), false, false))];
            wallet.Status = UploadStatus.Queued;


            // Clean up the temporary file after preparing transactions
            CleanupFiles(wallet.Address!);

            logger.LogInformation("Prepared transactions for address: {Address}, AdaFsId: {AdaFsId}", address, adaFsId);

        }
        else
        {
            logger.LogInformation("No metadata found for address: {Address}", address);
        }

        wallet.UpdatedAt = DateTime.UtcNow;
        dbContext.Wallets.Update(wallet);
        await dbContext.SaveChangesAsync();
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
            catch
            {
                logger.LogInformation("Transaction submission failed, retries remaining: {Retries}", retriesRemaining);
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