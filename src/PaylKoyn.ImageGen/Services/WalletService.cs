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
        using var dbContext = dbContextFactory.CreateDbContext();

        // Step 1: Create mint request with temporary null address
        var mintRequest = CreateMintRequestWithoutAddress(requesterAddress);

        // Step 2: Save to get auto-generated ID
        dbContext.MintRequests.Add(mintRequest);
        await dbContext.SaveChangesAsync();

        // Step 3: Generate address using the auto-generated ID as wallet index
        var walletAddress = GetWalletAddress(mintRequest.Id);
        mintRequest.Address = walletAddress.ToBech32();

        // Step 4: Update with the generated address
        await dbContext.SaveChangesAsync();

        return mintRequest;
    }

    public async Task<MintRequest?> GetRequestAsync(string requestId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();
        return await dbContext.MintRequests.FirstOrDefaultAsync(request => request.Id.ToString() == requestId);
    }

    public async Task<MintRequest?> GetRequestByAddressAsync(string address)
    {
        using var dbContext = dbContextFactory.CreateDbContext();
        return await dbContext.MintRequests.FirstOrDefaultAsync(request => request.Address == address);
    }

    public async Task<PrivateKey?> GetPrivateKeyByAddressAsync(string address)
    {
        var request = await GetRequestByAddressAsync(address);
        return request is null ? null : GetPaymentPrivateKey(request.Id);
    }

    public PrivateKey? GetPrivateKeyById(int id)
    {
        return GetPaymentPrivateKey(id);
    }

    private static MintRequest CreateMintRequestWithoutAddress(string requesterAddress)
    {
        var now = DateTime.UtcNow;

        return new MintRequest(
            Address: null, // Will be set after getting auto-generated ID
            WalletIndex: 0, // Will be same as Id
            UserAddress: requesterAddress,
            UploadPaymentAddress: null,
            UploadPaymentAmount: 0,
            PolicyId: null,
            AssetName: null,
            NftMetadata: null,
            AdaFsId: null,
            MintTxHash: null,
            AirdropTxHash: null,
            Traits: null,
            Image: null,
            Status: MintStatus.Waiting,
            NftNumber: null,
            CreatedAt: now,
            UpdatedAt: now
        );
    }
}