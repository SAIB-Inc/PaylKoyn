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

    public DbSet<OutputBySlot> OutputsBySlot => Set<OutputBySlot>();

    public DbSet<TransactionSubmission> TransactionSubmissions => Set<TransactionSubmission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TransactionBySlot>(entity =>
        {
            entity.HasKey(e => e.Hash);
            
            entity.HasIndex(e => e.Slot);
        });

        modelBuilder.Entity<OutputBySlot>(entity =>
        {
            entity.HasKey(e => e.OutRef);

            entity.HasIndex(e => e.Slot);
            entity.HasIndex(e => e.SpentSlot);
            entity.HasIndex(e => e.SpentTxHash);
        });

        modelBuilder.Entity<TransactionSubmission>(entity =>
        {
            entity.HasKey(e => e.Hash);

            entity.HasIndex(e => e.DateSubmitted);
            entity.HasIndex(e => e.Status);
        });
    }
}