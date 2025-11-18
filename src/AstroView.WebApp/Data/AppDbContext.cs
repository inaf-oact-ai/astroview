using AstroView.WebApp.Data.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AstroView.WebApp.Data;

public class AppDbContext : IdentityDbContext<UserDbe>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        Database.SetCommandTimeout(120);
    }

    public DbSet<DatasetDbe> Datasets { get; set; }
    public DbSet<ImageDbe> Images { get; set; }
    public DbSet<ImageLabelDbe> ImageLabels { get; set; }
    public DbSet<ChangeDbe> Changes { get; set; }
    public DbSet<DatasetOptionDbe> DatasetOptions { get; set; }
    public DbSet<LabelDbe> Labels { get; set; }
    public DbSet<CaesarJobDbe> CaesarJobs { get; set; }
    public DbSet<OutlierDbe> Outliers { get; set; }
    public DbSet<SimilarDbe> Similars { get; set; }
    public DbSet<IndividualSimilarDbe> IndividualSimilars { get; set; }
    public DbSet<ClusterDbe> Clusters { get; set; }
    public DbSet<ClusterItemDbe> ClusterItems { get; set; }
    public DbSet<DisplayModeDbe> DisplayModes { get; set; }
    public DbSet<ExportDbe> Exports { get; set; }
    public DbSet<PredictionDbe> Predictions { get; set; }
    public DbSet<DatasetJobDbe> DatasetJobs { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<UserDbe>()
            .HasOne(a => a.Dataset)
            .WithMany(a => a.Users)
            .HasForeignKey(r => r.LastDatasetId);

        builder.Entity<DatasetDbe>()
            .HasOne(a => a.User)
            .WithMany(a => a.Datasets)
            .HasForeignKey(r => r.UserId);

        builder.Entity<UserDbe>()
            .Property(r => r.DisplayName)
            .HasDefaultValue("");

        builder.Entity<UserDbe>()
            .Property(r => r.NotedFiles)
            .HasDefaultValue("");

        base.OnModelCreating(builder);
    }
}
