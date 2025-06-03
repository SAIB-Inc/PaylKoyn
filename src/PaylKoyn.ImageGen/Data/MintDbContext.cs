using Microsoft.EntityFrameworkCore;

namespace PaylKoyn.ImageGen.Data;

public class MintDbContext(DbContextOptions Options) : DbContext(Options)
{
    public DbSet<MintRequest> MintRequests { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MintRequest>(e =>
        {
            e.HasKey(w => w.Id);
        });
    }
}
