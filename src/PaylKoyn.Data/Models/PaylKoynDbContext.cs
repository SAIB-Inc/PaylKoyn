using Argus.Sync.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PaylKoyn.Data.Models.Entity;

namespace PaylKoyn.Data.Models;

public class PaylKoynDbContext(
    DbContextOptions<PaylKoynDbContext> options,
    IConfiguration configuration
) : CardanoDbContext(options, configuration)
{
    public DbSet<TransactionBySlot> TransactionsBySlot => Set<TransactionBySlot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TransactionBySlot>(entity =>
        {
            entity.HasKey(e => e.Hash);
        });
    }
}