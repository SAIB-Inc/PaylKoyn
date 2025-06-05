
using System.Text;
using System.Text.RegularExpressions;
using Chrysalis.Cbor.Types.Cardano.Core.Common;
using Chrysalis.Wallet.Models.Addresses;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.ImageGen.Data;
using PaylKoyn.ImageGen.Services;
using PaylKoyn.ImageGen.Utils;
using WalletAddress = Chrysalis.Wallet.Models.Addresses.Address;

namespace PaylKoyn.ImageGen.Workers;

public partial class NftMintWorker(
    IDbContextFactory<MintDbContext> dbContextFactory,
    IConfiguration configuration,
    MintingService mintingService,
    WalletService walletService
) : BackgroundService
{
    private readonly string _rewardAddress = configuration.GetValue("RewardAddress", "addr_test1qp0wm2cqf3z5qejaeg75g422675ujzfwdxcmqkq83qgj9hmmmsx4jmdrl442mkv2gqh4qecsaws3cw0farcdfh5hehqq5j6wx5");
    private readonly string _nftBaseName = configuration.GetValue("NftBaseName", "Payl Koyn NFT");
    private readonly string _seed = configuration.GetValue<string>("Seed") ?? throw new ArgumentNullException("Seed is not configured");
    private readonly ulong _invalidHereAfter = configuration.GetValue<ulong>("Minting:InvalidAfter", 0);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        WalletAddress mintingAddress = walletService.GetWalletAddress(_seed, 0);
        NativeScript nativeScript = ScriptUtil.GetMintingScript(mintingAddress.ToBech32(), _invalidHereAfter);
        string policyId = ScriptUtil.GetPolicyId(nativeScript);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using MintDbContext dbContext = await dbContextFactory.CreateDbContextAsync(stoppingToken);

                List<MintRequest> pendingMints = await dbContext.MintRequests
                        .OrderBy(p => p.UpdatedAt)
                        .Where(p => p.Status == MintStatus.Uploaded)
                        .Take(3)
                        .ToListAsync(stoppingToken);

                if (pendingMints.Count == 0)
                {
                    await Task.Delay(5000, stoppingToken);
                    continue;
                }

                Task<MintRequest>[] tasks = [.. pendingMints.Select(request => {
                    string asciiAssetName = $"{_nftBaseName} #{request.NftNumber}";
                    string cleanAsciiAssetName = AlphaNumericRegex().Replace(asciiAssetName, "");
                    string assetName = Convert.ToHexString(Encoding.UTF8.GetBytes(cleanAsciiAssetName));
                    return mintingService.MintNftAsync(request.Id, policyId, assetName, asciiAssetName, _rewardAddress);
                })];

                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(10000, stoppingToken);
            }
        }
    }

    [GeneratedRegex(@"[^a-zA-Z0-9]")]
    private static partial Regex AlphaNumericRegex();
}