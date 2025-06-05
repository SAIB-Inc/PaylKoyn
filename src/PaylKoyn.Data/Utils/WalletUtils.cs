using Chrysalis.Wallet.Models.Enums;
using Chrysalis.Wallet.Models.Keys;
using Chrysalis.Wallet.Words;
using Microsoft.Extensions.Configuration;
using WalletAddress = Chrysalis.Wallet.Models.Addresses.Address;

namespace PaylKoyn.Data.Utils;

public static class WalletUtils
{
    private const int MainnetMagic = 764824073;
    private const int AccountIndex = 0;
    private const int StakeIndex = 0;

    public static NetworkType DetermineNetworkType(IConfiguration configuration)
    {
        int networkMagic = configuration.GetValue("CardanoNodeConnection:NetworkMagic", 2);
        return networkMagic == MainnetMagic ? NetworkType.Mainnet : NetworkType.Testnet;
    }

    public static NetworkType DetermineNetworkType(int networkMagic) =>
        networkMagic == MainnetMagic ? NetworkType.Mainnet : NetworkType.Testnet;

    public static PrivateKey DeriveAccountKey(string seed)
    {
        Mnemonic mnemonic = Mnemonic.Restore(seed, English.Words);
        return mnemonic
            .GetRootKey()
            .Derive(PurposeType.Shelley, DerivationType.HARD)
            .Derive(CoinType.Ada, DerivationType.HARD)
            .Derive(AccountIndex, DerivationType.HARD);
    }

    public static PrivateKey DerivePaymentKey(PrivateKey accountKey, int walletIndex) =>
        accountKey
            .Derive(RoleType.ExternalChain)
            .Derive(walletIndex);

    public static PrivateKey DeriveStakeKey(PrivateKey accountKey) =>
        accountKey
            .Derive(RoleType.Staking)
            .Derive(StakeIndex);

    public static (PublicKey PaymentPublicKey, PublicKey StakePublicKey) DeriveKeyPair(string seed, int walletIndex)
    {
        PrivateKey accountKey = DeriveAccountKey(seed);
        PrivateKey paymentKey = DerivePaymentKey(accountKey, walletIndex);
        PrivateKey stakeKey = DeriveStakeKey(accountKey);

        return (paymentKey.GetPublicKey(), stakeKey.GetPublicKey());
    }

    public static WalletAddress CreateWalletAddress(NetworkType networkType, PublicKey paymentPublicKey, PublicKey stakePublicKey) =>
        WalletAddress.FromPublicKeys(networkType, AddressType.Base, paymentPublicKey, stakePublicKey);

    public static WalletAddress GetWalletAddress(string seed, int walletIndex, NetworkType networkType)
    {
        (PublicKey paymentPublicKey, PublicKey stakePublicKey) = DeriveKeyPair(seed, walletIndex);
        return CreateWalletAddress(networkType, paymentPublicKey, stakePublicKey);
    }

    public static PrivateKey GetPaymentPrivateKey(string seed, int walletIndex)
    {
        PrivateKey accountKey = DeriveAccountKey(seed);
        return DerivePaymentKey(accountKey, walletIndex);
    }
}