using Microsoft.EntityFrameworkCore;
using LicensingService.Models.Entities;

namespace LicensingService.Data;

public class LicensingDbContext : DbContext
{
    public LicensingDbContext(DbContextOptions<LicensingDbContext> options) : base(options) { }

    public DbSet<License> Licenses => Set<License>();
    public DbSet<App> Apps => Set<App>();
    public DbSet<Job> Jobs => Set<Job>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<License>(e =>
        {
            e.HasKey(l => l.Id);
            e.HasIndex(l => l.TenantId).IsUnique();
            e.Property(l => l.Status).HasConversion<string>();
            e.Property(l => l.CreatedAt).HasDefaultValueSql("NOW()");
            e.Property(l => l.UpdatedAt).HasDefaultValueSql("NOW()");
        });

        modelBuilder.Entity<App>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasOne(a => a.License)
             .WithMany(l => l.Apps)
             .HasForeignKey(a => a.LicenseId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Job>(e =>
        {
            e.HasKey(j => j.Id);
            e.HasOne(j => j.App)
             .WithMany(a => a.Jobs)
             .HasForeignKey(j => j.AppId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
