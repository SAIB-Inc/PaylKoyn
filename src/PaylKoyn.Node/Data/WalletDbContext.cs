using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Models;

namespace PaylKoyn.Node.Data;

public class WalletDbContext(DbContextOptions Options) : DbContext(Options)
{
    public DbSet<Wallet> Wallets { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Wallet>(e =>
        {
            e.HasKey(w => w.Address);
        });
    }
}
