using Microsoft.EntityFrameworkCore;
using CountOrSell.Core.Entities;

namespace CountOrSell.Core.Data;

public class CountOrSellDbContext : DbContext
{
    public CountOrSellDbContext(DbContextOptions<CountOrSellDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<BoosterDefinition> BoosterDefinitions => Set<BoosterDefinition>();
    public DbSet<ReserveListCardOwnership> ReserveListCardOwnerships => Set<ReserveListCardOwnership>();
    public DbSet<CardOwnership> CardOwnerships => Set<CardOwnership>();
    public DbSet<CachedSet> CachedSets => Set<CachedSet>();
    public DbSet<CachedCard> CachedCards => Set<CachedCard>();
    public DbSet<SetTag> SetTags => Set<SetTag>();
    public DbSet<DatabaseUpdatePackage> DatabaseUpdatePackages => Set<DatabaseUpdatePackage>();
    public DbSet<UserSubmission> UserSubmissions => Set<UserSubmission>();
    public DbSet<UserSubmissionItem> UserSubmissionItems => Set<UserSubmissionItem>();
    public DbSet<SlabbedCard> SlabbedCards => Set<SlabbedCard>();

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

        modelBuilder.Entity<SetTag>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.SetCode, e.Tag }).IsUnique();
            entity.HasOne(e => e.Set)
                  .WithMany(s => s.Tags)
                  .HasForeignKey(e => e.SetCode)
                  .HasPrincipalKey(s => s.Code)
                  .OnDelete(DeleteBehavior.Cascade);
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

        modelBuilder.Entity<SlabbedCard>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.GradingCompany, e.CertificationNumber }).IsUnique();
        });
    }

    /// <summary>
    /// Applies incremental schema changes that EnsureCreated() won't add to an existing database.
    /// Call this once after EnsureCreated() on every startup.
    /// </summary>
    public void EnsureSchemaUpToDate()
    {
        Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "SetTags" (
                "Id"      INTEGER PRIMARY KEY AUTOINCREMENT,
                "SetCode" TEXT NOT NULL,
                "Tag"     TEXT NOT NULL,
                UNIQUE("SetCode", "Tag"),
                FOREIGN KEY("SetCode") REFERENCES "CachedSets"("Code") ON DELETE CASCADE
            )
            """);
        Database.ExecuteSqlRaw("""
            CREATE INDEX IF NOT EXISTS "IX_SetTags_SetCode" ON "SetTags" ("SetCode")
            """);
        Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "SlabbedCards" (
                "Id"                  INTEGER PRIMARY KEY AUTOINCREMENT,
                "UserId"              TEXT NOT NULL,
                "ScryfallCardId"      TEXT NOT NULL,
                "CardName"            TEXT NOT NULL,
                "SetCode"             TEXT NOT NULL,
                "SetName"             TEXT NOT NULL,
                "CollectorNumber"     TEXT NOT NULL,
                "CardVariant"         TEXT NOT NULL,
                "GradingCompany"      TEXT NOT NULL,
                "Grade"               TEXT NOT NULL,
                "CertificationNumber" TEXT NOT NULL,
                "PurchaseDate"        TEXT,
                "PurchasedFrom"       TEXT,
                "PurchaseCost"        REAL,
                "Notes"               TEXT,
                "CreatedAt"           TEXT NOT NULL,
                UNIQUE("UserId", "GradingCompany", "CertificationNumber")
            )
            """);
        Database.ExecuteSqlRaw("""
            CREATE INDEX IF NOT EXISTS "IX_SlabbedCards_UserId" ON "SlabbedCards" ("UserId")
            """);

        // Add IsAdmin / IsDisabled columns to existing databases
        try { Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN IsAdmin INTEGER NOT NULL DEFAULT 0"); } catch { }
        try { Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN IsDisabled INTEGER NOT NULL DEFAULT 0"); } catch { }
    }
}
