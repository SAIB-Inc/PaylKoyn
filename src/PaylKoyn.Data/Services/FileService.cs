using System.Diagnostics;
using Chrysalis.Cbor.Extensions.Cardano.Core.Common;
using Chrysalis.Cbor.Extensions.Cardano.Core.Transaction;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using Chrysalis.Tx.Extensions;
using Chrysalis.Tx.Models;
using Chrysalis.Tx.Models.Cbor;
using Chrysalis.Wallet.Models.Keys;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PaylKoyn.Data.Models;

namespace PaylKoyn.Data.Services;

public class FileService(
    IConfiguration configuration,
    TransactionService transactionService,
    ICardanoDataProvider cardanoDataProvider,
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
        logger.LogInformation("Saving file to temporary path: {TempFilePath}", _tempFilePath);
        string tempFilePath = Path.Combine(_tempFilePath, address);
        await File.WriteAllBytesAsync(tempFilePath, file);
        logger.LogInformation("File saved to: {FilePath}", tempFilePath);

        logger.LogInformation("Checking for UTXOs for address: {Address}", address);
        IEnumerable<ResolvedInput> utxos = await WaitForUtxosAsync(address);
        ulong amount = utxos.Aggregate(0UL, (sum, utxo) => sum + utxo.Output.Amount().Lovelace());
        logger.LogInformation("Found {UtxoCount} UTXOs with total amount: {TotalAmount} lovelace", utxos.Count(), amount);

        Chrysalis.Network.Cbor.LocalStateQuery.ProtocolParams protocolParams = await cardanoDataProvider.GetParametersAsync();
        // Calculate required fee for the file upload
        ulong requiredFee = transactionService.CalculateFee(file.Length, _revenueFee, protocolParams.MaxTransactionSize ?? 16384);
        logger.LogInformation("Calculated required fee: {RequiredFee} lovelace for file size: {FileSize} bytes", requiredFee, file.Length);

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

        ulong totalFee = 0;
        string adaFsTxHash = string.Empty;
        foreach (Transaction tx in txs)
        {
            logger.LogInformation("Signing transaction");
            Transaction signedTx = tx.Sign(paymentPrivateKey);
            logger.LogInformation("Transaction signed successfully");

            totalFee += ((PostMaryTransaction)signedTx).TransactionBody.Fee();

            logger.LogInformation("Submitting transaction to the network");
            int retriesRemaining = _submissionRetries;
            while (true)
            {
                try
                {
                    string txHash = await cardanoDataProvider.SubmitTransactionAsync(signedTx);
                    adaFsTxHash = txHash;
                    logger.LogInformation("Transaction submitted successfully: {TransactionId}", txHash);
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to submit transaction. Retrying...");
                    retriesRemaining--;
                    if (_submissionRetries <= 0) throw;
                    await Task.Delay(_getUtxosInterval); // Wait before retrying
                    continue;
                }
            }
        }

        logger.LogInformation("Total transaction fee: {TotalFee} ADA", totalFee / 1_000_000m);
        logger.LogInformation("Total transactions created: {TransactionCount}", txs.Count);

        // DELETE the temporary file after upload
        logger.LogInformation("Deleting temporary file: {FilePath}", tempFilePath);
        if (File.Exists(tempFilePath)) File.Delete(tempFilePath);

        return adaFsTxHash;
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
            var utxos = await cardanoDataProvider.GetUtxosAsync([address]);
            return (utxos.Count != 0, utxos);
        }
        catch
        {
            return (false, Enumerable.Empty<ResolvedInput>());
        }
    }
}