using Chrysalis.Wallet.Models.Enums;
using Chrysalis.Wallet.Models.Keys;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Models;
using PaylKoyn.Data.Utils;
using PaylKoyn.Node.Data;
using WalletAddress = Chrysalis.Wallet.Models.Addresses.Address;

namespace PaylKoyn.Node.Services;

public class WalletService(
    IConfiguration configuration,
    IDbContextFactory<WalletDbContext> dbContextFactory
)
{
    private readonly string _defaultSeed = configuration["Seed"]
        ?? throw new ArgumentNullException("Seed is not configured");
    private readonly NetworkType _networkType = WalletUtils.DetermineNetworkType(
        int.Parse(configuration["CardanoNodeConnection:NetworkMagic"] ?? "2"));

    public WalletAddress GetWalletAddress(int walletIndex = 0) =>
        WalletUtils.GetWalletAddress(_defaultSeed, walletIndex, _networkType);

    public PrivateKey GetPaymentPrivateKey(int walletIndex = 0) =>
        WalletUtils.GetPaymentPrivateKey(_defaultSeed, walletIndex);

    public async Task<Wallet> GenerateWalletAsync(string? airdropAddress = null)
    {
        using WalletDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        int nextWalletIndex = await GetNextWalletIndexAsync(dbContext);
        WalletAddress walletAddress = GetWalletAddress(nextWalletIndex);
        Wallet wallet = CreateWallet(walletAddress.ToBech32(), nextWalletIndex, airdropAddress);

        dbContext.Wallets.Add(wallet);
        await dbContext.SaveChangesAsync();

        return wallet;
    }

    public async Task<Wallet?> GetWalletAsync(string address)
    {
        using WalletDbContext dbContext = dbContextFactory.CreateDbContext();
        return await dbContext.Wallets.FirstOrDefaultAsync(wallet => wallet.Address == address);
    }

    public async Task<PrivateKey?> GetPrivateKeyByAddressAsync(string address)
    {
        Wallet? wallet = await GetWalletAsync(address);
        return wallet?.Index == null ? null : GetPaymentPrivateKey(wallet.Index);
    }

    private static async Task<int> GetNextWalletIndexAsync(WalletDbContext dbContext)
    {
        int lastWalletIndex = await dbContext.Wallets
            .Select(wallet => wallet.Index)
            .OrderByDescending(index => index)
            .FirstOrDefaultAsync();

        return lastWalletIndex + 1;
    }

    private static Wallet CreateWallet(string walletAddress, int walletIndex, string? airdropAddress)
    {
        return new Wallet(walletAddress, walletIndex)
        {
            AirdropAddress = airdropAddress
        };
    }
}