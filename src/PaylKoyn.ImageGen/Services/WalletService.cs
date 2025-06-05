using Chrysalis.Wallet.Models.Enums;
using Chrysalis.Wallet.Models.Keys;
using Chrysalis.Wallet.Words;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.ImageGen.Data;
using WalletAddress = Chrysalis.Wallet.Models.Addresses.Address;

namespace PaylKoyn.ImageGen.Services;

public class WalletService(
    IConfiguration configuration,
    IDbContextFactory<MintDbContext> dbContextFactory
)
{
    private const int MainnetMagic = 764824073;
    private const int AccountIndex = 0;
    private const int StakeIndex = 0;

    private readonly string _defaultSeed = configuration.GetValue("Seed", string.Empty);
    private readonly NetworkType _networkType = DetermineNetworkType(configuration);

    public WalletAddress GetWalletAddress(string seed, int walletIndex = 0)
    {
        (PublicKey PaymentPublicKey, PublicKey StakePublicKey) = DeriveKeyPair(seed, walletIndex);
        return CreateWalletAddress(PaymentPublicKey, StakePublicKey);
    }

    public WalletAddress GetWalletAddress(int walletIndex = 0) =>
        GetWalletAddress(_defaultSeed, walletIndex);

    public PrivateKey GetPaymentPrivateKey(string seed, int walletIndex = 0)
    {
        PrivateKey accountKey = DeriveAccountKey(seed);
        return DerivePaymentKey(accountKey, walletIndex);
    }

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

    private static NetworkType DetermineNetworkType(IConfiguration configuration)
    {
        int networkMagic = configuration.GetValue("CardanoNodeConnection:NetworkMagic", 2);
        return networkMagic == MainnetMagic ? NetworkType.Mainnet : NetworkType.Testnet;
    }

    private static PrivateKey DeriveAccountKey(string seed)
    {
        Mnemonic mnemonic = Mnemonic.Restore(seed, English.Words);
        return mnemonic
            .GetRootKey()
            .Derive(PurposeType.Shelley, DerivationType.HARD)
            .Derive(CoinType.Ada, DerivationType.HARD)
            .Derive(AccountIndex, DerivationType.HARD);
    }

    private static PrivateKey DerivePaymentKey(PrivateKey accountKey, int walletIndex)
    {
        return accountKey
            .Derive(RoleType.ExternalChain)
            .Derive(walletIndex);
    }

    private static PrivateKey DeriveStakeKey(PrivateKey accountKey)
    {
        return accountKey
            .Derive(RoleType.Staking)
            .Derive(StakeIndex);
    }

    private static (PublicKey PaymentPublicKey, PublicKey StakePublicKey) DeriveKeyPair(string seed, int walletIndex)
    {
        PrivateKey accountKey = DeriveAccountKey(seed);
        PrivateKey paymentKey = DerivePaymentKey(accountKey, walletIndex);
        PrivateKey stakeKey = DeriveStakeKey(accountKey);

        return (paymentKey.GetPublicKey(), stakeKey.GetPublicKey());
    }

    private WalletAddress CreateWalletAddress(PublicKey paymentPublicKey, PublicKey stakePublicKey)
        => WalletAddress.FromPublicKeys(_networkType, AddressType.Base, paymentPublicKey, stakePublicKey);


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