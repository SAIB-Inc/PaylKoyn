using System.Diagnostics;
using Chrysalis.Cbor.Extensions.Cardano.Core.Common;
using Chrysalis.Cbor.Extensions.Cardano.Core.Transaction;
using Chrysalis.Tx.Models.Cbor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PaylKoyn.Data.Models;

namespace PaylKoyn.Data.Services;

public class FileService(
    IConfiguration configuration,
    WalletService walletService,
    ILogger<FileService> logger
)
{
    private readonly TimeSpan _expirationTime =
        TimeSpan.FromMinutes(int.TryParse(configuration["File:ExpirationMinutes"], out int minutes) ? minutes : 5);
    private readonly TimeSpan _getUtxosInterval =
        TimeSpan.FromSeconds(int.TryParse(configuration["File:GetUtxosIntervalSeconds"], out int seconds) ? seconds : 10);
    private readonly string _tempFilePath = configuration["File:TempFilePath"] ?? "/tmp";

    public async Task<Wallet> RequestUploadAsync() => await walletService.GenerateWalletAsync();

    public async Task<bool> UploadAsync(string address, byte[] file, string contentType, string fileName)
    {
        logger.LogInformation("Starting file upload for address: {Address}", address);
        Wallet? wallet = await walletService.GetWalletAsync(address)
            ?? throw new ArgumentException("Wallet not found");
        logger.LogInformation("Wallet found: {WalletAddress}", wallet.Address);

        logger.LogInformation("Saving file to temporary path: {TempFilePath}", _tempFilePath);
        string tempFilePath = Path.Combine(_tempFilePath, address);
        await File.WriteAllBytesAsync(tempFilePath, file);
        logger.LogInformation("File saved to: {FilePath}", tempFilePath);

        logger.LogInformation("Checking for UTXOs for address: {Address}", address);
        IEnumerable<ResolvedInput> utxos = await WaitForUtxosAsync(address);
        ulong amount = utxos.Aggregate(0UL, (sum, utxo) => sum + utxo.Output.Amount().Lovelace());
        logger.LogInformation("Found {UtxoCount} UTXOs with total amount: {TotalAmount} lovelace", utxos.Count(), amount);

        // @TODO: Call Upload

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
            (bool isSuccess, IEnumerable<ResolvedInput> utxos) = await walletService.TryGetUtxosAsync(address);
            if (isSuccess) return utxos;

            logger.LogInformation("No UTXOs found for address: {Address}. Retrying in {Interval} seconds...", address, _getUtxosInterval.TotalSeconds);
            await Task.Delay(_getUtxosInterval);
        }

        throw new TimeoutException("Upload request has expired, no UTXOs found for the address within the specified time.");
    }
}