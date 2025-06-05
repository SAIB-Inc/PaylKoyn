using Chrysalis.Wallet.Models.Enums;
using Chrysalis.Wallet.Models.Keys;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Utils;
using PaylKoyn.ImageGen.Data;
using WalletAddress = Chrysalis.Wallet.Models.Addresses.Address;

namespace PaylKoyn.ImageGen.Services;

public class WalletService(
    IConfiguration configuration,
    IDbContextFactory<MintDbContext> dbContextFactory
)
{
    private readonly string _defaultSeed = configuration.GetValue("Seed", string.Empty);
    private readonly NetworkType _networkType = WalletUtils.DetermineNetworkType(configuration);

    public WalletAddress GetWalletAddress(string seed, int walletIndex = 0) =>
        WalletUtils.GetWalletAddress(seed, walletIndex, _networkType);

    public WalletAddress GetWalletAddress(int walletIndex = 0) =>
        GetWalletAddress(_defaultSeed, walletIndex);

    public PrivateKey GetPaymentPrivateKey(string seed, int walletIndex = 0) =>
        WalletUtils.GetPaymentPrivateKey(seed, walletIndex);

    public PrivateKey GetPaymentPrivateKey(int walletIndex = 0) =>
        GetPaymentPrivateKey(_defaultSeed, walletIndex);

    public async Task<MintRequest> GenerateMintRequestAsync(string requesterAddress)
    {
        using MintDbContext dbContext = dbContextFactory.CreateDbContext();

        int nextWalletIndex = await GetNextWalletIndexAsync(dbContext);
        WalletAddress walletAddress = GetWalletAddress(nextWalletIndex);
        MintRequest mintRequest = CreateMintRequest(walletAddress.ToBech32(), nextWalletIndex, requesterAddress);

        dbContext.MintRequests.Add(mintRequest);
        await dbContext.SaveChangesAsync();

        return mintRequest;
    }

    public async Task<MintRequest?> GetRequestAsync(string requestId)
    {
        using MintDbContext dbContext = dbContextFactory.CreateDbContext();
        return await dbContext.MintRequests.FirstOrDefaultAsync(request => request.Id == requestId);
    }

    public async Task<PrivateKey?> GetPrivateKeyByAddressAsync(string address)
    {
        MintRequest? request = await GetRequestAsync(address);
        return request?.WalletIndex == null ? null : GetPaymentPrivateKey(request.WalletIndex);
    }

    private static async Task<int> GetNextWalletIndexAsync(MintDbContext dbContext)
    {
        int lastWalletIndex = await dbContext.MintRequests
            .Select(request => request.WalletIndex)
            .OrderByDescending(index => index)
            .FirstOrDefaultAsync();

        return lastWalletIndex + 1;
    }

    private static MintRequest CreateMintRequest(string walletAddress, int walletIndex, string requesterAddress)
    {
        DateTime now = DateTime.UtcNow;

        return new MintRequest(
            Id: walletAddress,
            WalletIndex: walletIndex,
            UserAddress: requesterAddress,
            NftNumber: null,
            UploadPaymentAmount: 0,
            UploadPaymentAddress: null,
            AdaFsId: null,
            NftMetadata: null,
            Image: null,
            AssetName: null,
            PolicyId: null,
            MintTxHash: null,
            Traits: null,
            AirdropTxHash: null,
            Status: MintStatus.Waiting,
            CreatedAt: now,
            UpdatedAt: now
        );
    }
}