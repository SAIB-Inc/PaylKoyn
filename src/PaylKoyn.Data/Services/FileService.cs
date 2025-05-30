using System.Diagnostics;
using Chrysalis.Cbor.Extensions.Cardano.Core.Common;
using Chrysalis.Cbor.Extensions.Cardano.Core.Transaction;
using Chrysalis.Cbor.Serialization;
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
    private readonly string _tempFilePath = configuration["File:TempFilePath"] ?? "/tmp";
    private readonly int _submissionRetries =
        int.TryParse(configuration["File:SubmissionRetries"], out int retries) ? retries : 3;


    public async Task<bool> UploadAsync(string address, byte[] file, string contentType, string fileName, PrivateKey paymentPrivateKey)
    {

        logger.LogInformation("Saving file to temporary path: {TempFilePath}", _tempFilePath);
        string tempFilePath = Path.Combine(_tempFilePath, address);
        await File.WriteAllBytesAsync(tempFilePath, file);
        logger.LogInformation("File saved to: {FilePath}", tempFilePath);

        logger.LogInformation("Checking for UTXOs for address: {Address}", address);
        IEnumerable<ResolvedInput> utxos = await WaitForUtxosAsync(address);
        ulong amount = utxos.Aggregate(0UL, (sum, utxo) => sum + utxo.Output.Amount().Lovelace());
        logger.LogInformation("Found {UtxoCount} UTXOs with total amount: {TotalAmount} lovelace", utxos.Count(), amount);

        logger.LogInformation("Preparing transaction to upload file: {FileName}", fileName);
        Chrysalis.Network.Cbor.LocalStateQuery.ProtocolParams protocolParams = await cardanoDataProvider.GetParametersAsync();
        List<Transaction> txs = transactionService.UploadFile(
            address,
            file,
            fileName,
            contentType,
            [.. utxos],
            protocolParams
        );

        ulong totalFee = 0;
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
                    logger.LogInformation("Transaction submitted successfully: {TransactionId}", txHash);
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

        return true;
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