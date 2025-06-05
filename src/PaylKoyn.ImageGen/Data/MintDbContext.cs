using Microsoft.EntityFrameworkCore;

namespace PaylKoyn.ImageGen.Data;

public class MintDbContext(DbContextOptions Options) : DbContext(Options)
{
    private static readonly Lock _nftNumberLock = new();

    public DbSet<MintRequest> MintRequests { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MintRequest>(e =>
        {
            e.HasKey(w => w.Id);
            e.Property(e => e.Id).ValueGeneratedOnAdd();
            e.Property(w => w.NftNumber).IsRequired(false);
            e.HasIndex(w => w.NftNumber).IsUnique();
        });
    }

    public override int SaveChanges()
    {
        AssignNftNumbers();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        AssignNftNumbers();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void AssignNftNumbers()
    {
        List<MintRequest> newlyPaidEntities = [.. ChangeTracker.Entries<MintRequest>()
            .Where(e => e.State == EntityState.Modified &&
                       e.Entity.Status == MintStatus.Paid &&
                       e.Entity.NftNumber == null &&
                       e.Property(nameof(MintRequest.Status)).OriginalValue?.Equals(MintStatus.Waiting) == true)
            .Select(e => e.Entity)];

        if (newlyPaidEntities.Count == 0) return;

        lock (_nftNumberLock)
        {
            int maxNftNumber = MintRequests
                .Where(x => x.NftNumber.HasValue)
                .Max(x => x.NftNumber) ?? 0;

            foreach (MintRequest? entity in newlyPaidEntities)
            {
                entity.NftNumber = ++maxNftNumber;
            }
        }
    }
}