using System.Threading.Tasks;
using Chrysalis.Cbor.Types.Cardano.Core.Common;
using Chrysalis.Tx.Models;
using Chrysalis.Tx.Models.Cbor;
using Chrysalis.Wallet.Models.Enums;
using Chrysalis.Wallet.Models.Keys;
using Chrysalis.Wallet.Words;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PaylKoyn.Data.Models;
using WalletAddress = Chrysalis.Wallet.Models.Addresses.Address;

namespace PaylKoyn.Data.Services;

public class WalletService(
    IConfiguration configuration,
    IDbContextFactory<WalletDbContext> dbContextFactory,
    ICardanoDataProvider cardanoDataProvider
)
{
    private readonly string _seed = configuration["Seed"] ?? throw new ArgumentNullException("Seed is not configured");
    private readonly NetworkType _networkType = int.Parse(configuration["CardanoNodeConnection:NetworkMagic"] ?? "2") switch
    {
        764824073 => NetworkType.Mainnet,
        _ => NetworkType.Testnet
    };

    public async Task<Wallet> GenerateWalletAsync()
    {
        using WalletDbContext dbContext = dbContextFactory.CreateDbContext();

        int index = await dbContext.Wallets
            .Select(w => w.Index)
            .OrderByDescending(i => i)
            .FirstOrDefaultAsync() + 1;

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
        string addressBech32 = address.ToBech32();
        Wallet wallet = new(addressBech32, index, paymentPrivateKey.Key, paymentPrivateKey.Chaincode);

        // Save the wallet to the database
        dbContext.Wallets.Add(wallet);
        await dbContext.SaveChangesAsync();

        return wallet;
    }

    public async Task<IEnumerable<ResolvedInput>> GetUtxosAsync(Wallet wallet)
    {
        try
        {
            return await cardanoDataProvider.GetUtxosAsync([wallet.Address]);
        }
        catch
        {
            return [];
        }
    }

    public async Task<IEnumerable<ResolvedInput>> GetUtxosAsync(string address)
    {
        try
        {
            return await cardanoDataProvider.GetUtxosAsync([address]);
        }
        catch
        {
            return [];
        }
    }
}