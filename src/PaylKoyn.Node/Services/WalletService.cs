using Chrysalis.Wallet.Models.Enums;
using Chrysalis.Wallet.Models.Keys;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Models;
using PaylKoyn.Data.Responses;
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

        Wallet wallet = CreateWalletWithoutAddress(airdropAddress);

        dbContext.Wallets.Add(wallet);
        await dbContext.SaveChangesAsync();

        WalletAddress walletAddress = GetWalletAddress(wallet.Id);
        wallet.Address = walletAddress.ToBech32();

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
        return wallet is null ? null : GetPaymentPrivateKey(wallet.Id);
    }

    private static Wallet CreateWalletWithoutAddress(string? airdropAddress)
    {
        return new Wallet()
        {
            Address = null!,
            AirdropAddress = airdropAddress,
            Status = UploadStatus.Waiting,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}