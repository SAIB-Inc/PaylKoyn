
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using PaylKoyn.ImageGen.Data;
using PaylKoyn.ImageGen.Services;

namespace Paylkoyn.ImageGen.Workers;

public class MintWorker(
    IDbContextFactory<MintDbContext> dbContextFactory,
    IConfiguration configuration,
    MintingService mintingService
) : BackgroundService
{
    private readonly string _rewardAddress = configuration.GetValue("RewardAddress", "addr_test1qp0wm2cqf3z5qejaeg75g422675ujzfwdxcmqkq83qgj9hmmmsx4jmdrl442mkv2gqh4qecsaws3cw0farcdfh5hehqq5j6wx5");
    private readonly string _policyId = configuration.GetValue("PolicyId", "b1c0f3d6e8a2f4c5b7e8c9d0e1f2a3b4c5d6e7f8g9h0i1j2k3l4m5n6o7p8q9r");
    private readonly string _nftBaseName = configuration.GetValue("NftBaseName", "Payl Koyn NFT");
    private readonly string _nftBaseDescription = configuration.GetValue("NftBaseDescription", "Payl Koyn NFT Collection");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using MintDbContext dbContext = await dbContextFactory.CreateDbContextAsync(stoppingToken);

                MintRequest? pendingMint = await dbContext.MintRequests
                    .OrderBy(p => p.CreatedAt)
                    .Where(p => p.Status == MintStatus.ImageUploaded)
                    .FirstOrDefaultAsync(stoppingToken);

                if (pendingMint is null)
                {
                    await Task.Delay(5000, stoppingToken);
                    continue;
                }

                int totalMints = await dbContext.MintRequests
                    .CountAsync(p => p.Status == MintStatus.Minted, stoppingToken);

                string asciiAssetName = $"{_nftBaseName} #{totalMints + 1}";
                string assetName = Convert.ToHexString(Encoding.UTF8.GetBytes(asciiAssetName));


                await mintingService.MintNftAsync(pendingMint.Id, _policyId, assetName, asciiAssetName, _rewardAddress, _nftBaseDescription);
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
}