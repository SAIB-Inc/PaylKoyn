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
    private readonly TimeSpan _expirationTime =
        TimeSpan.FromMinutes(int.TryParse(configuration["File:ExpirationMinutes"], out int minutes) ? minutes : 5);
    private readonly TimeSpan _getUtxosInterval =
        TimeSpan.FromSeconds(int.TryParse(configuration["File:GetUtxosIntervalSeconds"], out int seconds) ? seconds : 10);
    private readonly string _rewardAddress = configuration["RewardAddress"]
        ?? throw new ArgumentException("Reward Address cannot be null or empty.", nameof(configuration));
    private readonly string _tempFilePath = configuration["File:TempFilePath"] ?? "/tmp";
    private readonly int _submissionRetries =
        int.TryParse(configuration["File:SubmissionRetries"], out int retries) ? retries : 3;
    private readonly ulong _revenueFee =
        ulong.TryParse(configuration["File:RevenueFee"], out ulong revenueFee) ? revenueFee : 2_000_000UL;


    public async Task<string> UploadAsync(string address, byte[] file, string contentType, string fileName, PrivateKey paymentPrivateKey)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("Address cannot be null or empty.", nameof(address));

        using WalletDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
        Wallet? wallet = await dbContext.Wallets
            .Where(w => w.Address == address)
            .FirstOrDefaultAsync();

        if (wallet == null)
        {
            logger.LogError("Wallet not found for address: {Address}", address);
            throw new InvalidOperationException($"Wallet not found for address: {address}");
        }

        ProtocolParams protocolParams = await cardanoDataProvider.GetParametersAsync();
        ulong requiredFee = transactionService.CalculateFee(file.Length, _revenueFee, protocolParams.MaxTransactionSize ?? 16384);
        decimal requiredFeeAda = requiredFee / 1_000_000m;
        logger.LogInformation("Calculated required fee: {RequiredFee} lovelace for file size: {FileSize} bytes", requiredFee, file.Length);

        if (wallet.Status != UploadStatus.PaymentReceived)
        {
            logger.LogError("Wallet status is not PaymentReceived for address: {Address}", address);

            if (wallet.Status == UploadStatus.Pending)
            {
                throw new InvalidOperationException($"Please pay the required fee of {requiredFeeAda} $ADA to the address: {address} before uploading the file.");
            }
            else
            {
                throw new InvalidOperationException($"File upload is either already in progress or completed for address: {address}. Current status: {wallet.Status}. AdaFsId: {wallet.AdaFsId}");
            }
        }

        logger.LogInformation("Saving file to temporary path: {TempFilePath}", _tempFilePath);
        string tempFilePath = Path.Combine(_tempFilePath, address);
        await File.WriteAllBytesAsync(tempFilePath, file);
        logger.LogInformation("File saved to: {FilePath}", tempFilePath);
        logger.LogInformation("Checking for UTXOs for address: {Address}", address);

        IEnumerable<ResolvedInput> utxos = await WaitForUtxosAsync(address);
        ulong amount = utxos.Aggregate(0UL, (sum, utxo) => sum + utxo.Output.Amount().Lovelace());
        logger.LogInformation("Found {UtxoCount} UTXOs with total amount: {TotalAmount} lovelace", utxos.Count(), amount);

        // Validate that payment is sufficient
        if (amount < requiredFee)
        {
            logger.LogError("Insufficient payment. Required: {RequiredFee} lovelace, but received: {ReceivedAmount} lovelace", requiredFee, amount);
            throw new InvalidOperationException($"Insufficient payment. Required: {requiredFee} lovelace, but received: {amount} lovelace");
        }

        logger.LogInformation("Payment validation successful. Proceeding with upload.");

        logger.LogInformation("Preparing transaction to upload file: {FileName}", fileName);
        List<Transaction> txs = transactionService.UploadFile(
            address,
            file,
            fileName,
            contentType,
            [.. utxos],
            protocolParams,
            _rewardAddress
        );

        txs = [.. txs.Select(tx => tx.Sign(paymentPrivateKey))];

        PostMaryTransaction lastTx = (PostMaryTransaction)txs.Last();
        string adaFsId = Convert.ToHexString(HashUtil.ToBlake2b256(CborSerializer.Serialize(lastTx.TransactionBody))).ToLowerInvariant();
        wallet.AdaFsId = adaFsId;
        wallet.Transactions = [.. txs.Select(tx => new TxStatus(CborSerializer.Serialize(tx), false, false))];
        wallet.FileSize = file.Length;
        wallet.Status = UploadStatus.QueudForSubmission;
        wallet.UpdatedAt = DateTime.UtcNow;

        dbContext.Wallets.Update(wallet);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Deleting temporary file: {FilePath}", tempFilePath);
        if (File.Exists(tempFilePath)) File.Delete(tempFilePath);

        return adaFsId;
    }

    public async Task<Wallet?> WaitForPaymentAsync(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("Address cannot be null or empty.", nameof(address));

        using WalletDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
        Wallet? wallet = await dbContext.Wallets
            .Where(w => w.Address == address)
            .FirstOrDefaultAsync();

        if (wallet is null)
        {
            logger.LogError("Wallet not found for address: {Address}", address);
            throw new InvalidOperationException($"Wallet not found for address: {address}");
        }

        try
        {
            logger.LogInformation("Checking for UTXOs for address: {Address}", address);
            IEnumerable<ResolvedInput> utxos = await WaitForUtxosAsync(address);
            ulong amount = utxos.Aggregate(0UL, (sum, utxo) => sum + utxo.Output.Amount().Lovelace());
            logger.LogInformation("Found {UtxoCount} UTXOs with total amount: {TotalAmount} lovelace", utxos.Count(), amount);

            ProtocolParams protocolParams = await cardanoDataProvider.GetParametersAsync();
            // Calculate required fee for the file upload
            ulong requiredFee = transactionService.CalculateFee(wallet!.FileSize, _revenueFee, protocolParams.MaxTransactionSize ?? 16384);

            // Validate that payment is sufficient
            if (amount >= requiredFee)
            {
                wallet.Status = UploadStatus.PaymentReceived;
            }
            else
            {
                logger.LogError("Insufficient payment. Required: {RequiredFee} lovelace, but received: {ReceivedAmount} lovelace", requiredFee, amount);
            }
        }
        catch (TimeoutException ex)
        {
            logger.LogError(ex, "Timeout while waiting for UTXOs for address: {Address}", address);
            wallet.Status = UploadStatus.Failed;
            wallet.TransactionsRaw = null;
            wallet.Transactions = null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while waiting for payment for address: {Address}", address);
        }

        wallet.UpdatedAt = DateTime.UtcNow;
        dbContext.Wallets.Update(wallet);
        await dbContext.SaveChangesAsync();
        logger.LogInformation("Wallet updated with payment status for address: {Address}", address);
        return wallet;
    }

    public async Task<Wallet> SubmitTransactionsAsync(string address)
    {
        using WalletDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
        Wallet? wallet = await dbContext.Wallets.Where(w => w.Address == address).FirstOrDefaultAsync();

        if (wallet == null || wallet.Transactions is null || wallet.Transactions.Count == 0)
        {
            logger.LogError("Wallet not found or no transactions to submit for address: {Address}", address);
            throw new InvalidOperationException($"Wallet not found for address: {address}");
        }

        List<TxStatus> updatedTransactions = new List<TxStatus>();
        bool anyChanges = false;

        foreach (TxStatus txStatus in wallet.Transactions)
        {
            if (txStatus.IsSent)
            {
                // Already sent, keep as-is
                updatedTransactions.Add(txStatus);
                continue;
            }

            Transaction tx = Transaction.Read(txStatus.TxRaw);
            logger.LogInformation("Submitting transaction to the network");

            int retriesRemaining = _submissionRetries;
            bool wasSubmitted = false;

            while (retriesRemaining > 0)
            {
                try
                {
                    string txHash = await cardanoDataProvider.SubmitTransactionAsync(tx);
                    logger.LogInformation("Transaction submitted successfully: {TransactionId}", txHash);

                    // ✅ Create new record with IsSent = true
                    TxStatus updatedTxStatus = txStatus with { IsSent = true };
                    updatedTransactions.Add(updatedTxStatus);
                    wasSubmitted = true;
                    anyChanges = true;
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to submit transaction. Retrying...");
                    retriesRemaining--;
                    if (retriesRemaining <= 0) break;
                    await Task.Delay(_getUtxosInterval);
                }
            }

            if (!wasSubmitted)
            {
                updatedTransactions.Add(txStatus);
            }
        }

        if (anyChanges)
        {
            wallet.Transactions = updatedTransactions;
            wallet.UpdatedAt = DateTime.UtcNow;

            if (wallet.Transactions.All(t => t.IsSent))
            {
                wallet.Status = UploadStatus.Completed;
                logger.LogInformation("All transactions submitted successfully for address: {Address}", address);
            }

            dbContext.Wallets.Update(wallet);
            await dbContext.SaveChangesAsync();
        }

        return wallet;
    }

    private async Task<IEnumerable<ResolvedInput>> WaitForUtxosAsync(string address)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < _expirationTime)
        {
            (bool isSuccess, IEnumerable<ResolvedInput> utxos) = await TryGetUtxosAsync(address);
            if (isSuccess) return utxos;

            logger.LogInformation("No UTXOs found for address: {Address}. Retrying in {Interval} seconds...", address, _getUtxosInterval.TotalSeconds);
            await Task.Delay(_getUtxosInterval);
        }

        throw new TimeoutException("Upload request has expired, no UTXOs found for the address within the specified time.");
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
}