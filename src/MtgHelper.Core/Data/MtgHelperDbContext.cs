using Microsoft.EntityFrameworkCore;
using MtgHelper.Core.Entities;

namespace MtgHelper.Core.Data;

public class MtgHelperDbContext : DbContext
{
    public MtgHelperDbContext(DbContextOptions<MtgHelperDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<BoosterDefinition> BoosterDefinitions => Set<BoosterDefinition>();
    public DbSet<ReserveListCardOwnership> ReserveListCardOwnerships => Set<ReserveListCardOwnership>();
    public DbSet<CardOwnership> CardOwnerships => Set<CardOwnership>();
    public DbSet<CachedSet> CachedSets => Set<CachedSet>();
    public DbSet<CachedCard> CachedCards => Set<CachedCard>();
    public DbSet<DatabaseUpdatePackage> DatabaseUpdatePackages => Set<DatabaseUpdatePackage>();
    public DbSet<UserSubmission> UserSubmissions => Set<UserSubmission>();
    public DbSet<UserSubmissionItem> UserSubmissionItems => Set<UserSubmissionItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasIndex(e => e.UserId);
        });

        modelBuilder.Entity<BoosterDefinition>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.SetCode, e.BoosterType, e.ArtVariant }).IsUnique();
        });

        modelBuilder.Entity<ReserveListCardOwnership>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.ScryfallCardId }).IsUnique();
        });

        modelBuilder.Entity<CardOwnership>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.ScryfallCardId }).IsUnique();
        });

        modelBuilder.Entity<CachedSet>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code).IsUnique();
        });

        modelBuilder.Entity<CachedCard>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SetCode);
            entity.HasIndex(e => e.IsReserved);
        });

        modelBuilder.Entity<DatabaseUpdatePackage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Version).IsUnique();
        });

        modelBuilder.Entity<UserSubmission>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasMany(e => e.Items)
                .WithOne()
                .HasForeignKey(e => e.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserSubmissionItem>(entity =>
        {
            entity.HasKey(e => e.Id);
        });
    }
}
