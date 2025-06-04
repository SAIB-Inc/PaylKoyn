
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.ImageGen.Data;
using PaylKoyn.ImageGen.Services;

namespace Paylkoyn.ImageGen.Workers;

public partial class MintWorker(
    IDbContextFactory<MintDbContext> dbContextFactory,
    IConfiguration configuration,
    MintingService mintingService
) : BackgroundService
{
    private readonly string _rewardAddress = configuration.GetValue("RewardAddress", "addr_test1qp0wm2cqf3z5qejaeg75g422675ujzfwdxcmqkq83qgj9hmmmsx4jmdrl442mkv2gqh4qecsaws3cw0farcdfh5hehqq5j6wx5");
    private readonly string _policyId = configuration.GetValue("PolicyId", "b1c0f3d6e8a2f4c5b7e8c9d0e1f2a3b4c5d6e7f8g9h0i1j2k3l4m5n6o7p8q9r");
    private readonly string _nftBaseName = configuration.GetValue("NftBaseName", "Payl Koyn NFT");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using MintDbContext dbContext = await dbContextFactory.CreateDbContextAsync(stoppingToken);

                List<MintRequest> pendingMints = await dbContext.MintRequests
                        .OrderBy(p => p.UpdatedAt)
                        .Where(p => p.Status == MintStatus.ImageUploaded)
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
                    return mintingService.MintNftAsync(request.Id, _policyId, assetName, asciiAssetName, _rewardAddress);
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