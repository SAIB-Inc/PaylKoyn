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
    private readonly string _seed = configuration.GetValue("Seed", string.Empty);
    private readonly NetworkType _networkType = configuration.GetValue("NetworkMagic", 2) switch
    {
        764824073 => NetworkType.Mainnet,
        _ => NetworkType.Testnet
    };

    public WalletAddress GetWalletAddress(string seed, int index = 0)
    {
        Mnemonic mnemonic = Mnemonic.Restore(seed, English.Words);
        PrivateKey accountKey = mnemonic
            .GetRootKey()
            .Derive(PurposeType.Shelley, DerivationType.HARD)
            .Derive(CoinType.Ada, DerivationType.HARD)
            .Derive(0, DerivationType.HARD);
        PrivateKey paymentPrivateKey = accountKey
            .Derive(RoleType.ExternalChain)
            .Derive(index);
        PrivateKey stakePrivateKey = accountKey
            .Derive(RoleType.Staking)
            .Derive(0);

        PublicKey pkPub = paymentPrivateKey.GetPublicKey();
        PublicKey skPub = stakePrivateKey.GetPublicKey();

        WalletAddress address = WalletAddress.FromPublicKeys(_networkType, AddressType.Base, pkPub, skPub);

        return address;
    }

    public WalletAddress GetWalletAddress(int index = 0)
    {
        Mnemonic mnemonic = Mnemonic.Restore(_seed, English.Words);
        PrivateKey accountKey = mnemonic
            .GetRootKey()
            .Derive(PurposeType.Shelley, DerivationType.HARD)
            .Derive(CoinType.Ada, DerivationType.HARD)
            .Derive(0, DerivationType.HARD);
        PrivateKey paymentPrivateKey = accountKey
            .Derive(RoleType.ExternalChain)
            .Derive(index);
        PrivateKey stakePrivateKey = accountKey
            .Derive(RoleType.Staking)
            .Derive(0);

        PublicKey pkPub = paymentPrivateKey.GetPublicKey();
        PublicKey skPub = stakePrivateKey.GetPublicKey();

        WalletAddress address = WalletAddress.FromPublicKeys(_networkType, AddressType.Base, pkPub, skPub);

        return address;
    }

    public PrivateKey GetPaymentPrivateKey(int index = 0)
    {
        Mnemonic mnemonic = Mnemonic.Restore(_seed, English.Words);
        PrivateKey accountKey = mnemonic
            .GetRootKey()
            .Derive(PurposeType.Shelley, DerivationType.HARD)
            .Derive(CoinType.Ada, DerivationType.HARD)
            .Derive(0, DerivationType.HARD);
        PrivateKey paymentPrivateKey = accountKey
            .Derive(RoleType.ExternalChain)
            .Derive(index);

        return paymentPrivateKey;
    }

    public async Task<MintRequest> GenerateMintRequestAsync(string requesterAddress)
    {
        using MintDbContext dbContext = dbContextFactory.CreateDbContext();

        int index = await dbContext.MintRequests
            .Select(w => w.WalletIndex)
            .OrderByDescending(i => i)
            .FirstOrDefaultAsync() + 1;

        WalletAddress address = GetWalletAddress(index);
        string addressBech32 = address.ToBech32();

        MintRequest request = new(
            addressBech32, index, requesterAddress,
            null, 0, null, null, null, null, null, null, null,
            MintStatus.Pending, DateTime.UtcNow, DateTime.UtcNow
        );

        // Save the wallet to the database
        dbContext.MintRequests.Add(request);
        await dbContext.SaveChangesAsync();

        return request;
    }

    public async Task<MintRequest?> GetRequestAsync(string id)
    {
        using MintDbContext dbContext = dbContextFactory.CreateDbContext();
        return await dbContext.MintRequests.FirstOrDefaultAsync(w => w.Id == id);
    }

    public async Task<PrivateKey?> GetPrivateKeyByAddressAsync(string address)
    {
        MintRequest? request = await GetRequestAsync(address);
        if (request == null) return null;

        return GetPaymentPrivateKey(request.WalletIndex);
    }
}