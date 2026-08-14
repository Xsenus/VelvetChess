using Microsoft.EntityFrameworkCore;

namespace VelvetChess.Server.Data;

public sealed class AccountDbContext(DbContextOptions<AccountDbContext> options) : DbContext(options)
{
    public DbSet<PlayerEntity> Players => Set<PlayerEntity>();
    public DbSet<ExternalIdentityEntity> ExternalIdentities => Set<ExternalIdentityEntity>();
    public DbSet<RatingEntity> Ratings => Set<RatingEntity>();
    public DbSet<ProgressEntity> Progress => Set<ProgressEntity>();
    public DbSet<GuestClaimEntity> GuestClaims => Set<GuestClaimEntity>();
    public DbSet<RefreshSessionEntity> RefreshSessions => Set<RefreshSessionEntity>();
    public DbSet<PuzzleResultEntity> PuzzleResults => Set<PuzzleResultEntity>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<ExternalIdentityEntity>().HasIndex(x => new { x.Provider, x.ProviderUserId }).IsUnique();
        model.Entity<RatingEntity>().HasKey(x => new { x.PlayerId, x.Kind });
        model.Entity<ProgressEntity>().HasKey(x => x.PlayerId);
        model.Entity<GuestClaimEntity>().HasIndex(x => x.GuestId).IsUnique();
        model.Entity<GuestClaimEntity>().HasIndex(x => new { x.PlayerId, x.IdempotencyKey }).IsUnique();
        model.Entity<RefreshSessionEntity>().HasIndex(x => x.TokenHash).IsUnique();
        model.Entity<PuzzleResultEntity>().HasKey(x => new { x.PlayerId, x.PuzzleId });
    }
}

public sealed class PlayerEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DisplayName { get; set; } = "Игрок";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ExternalIdentityEntity
{
    public long Id { get; set; }
    public string PlayerId { get; set; } = "";
    public string Provider { get; set; } = "";
    public string ProviderUserId { get; set; } = "";
}

public sealed class RatingEntity
{
    public string PlayerId { get; set; } = "";
    public string Kind { get; set; } = "";
    public int Value { get; set; } = 1000;
    public int Games { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ProgressEntity
{
    public string PlayerId { get; set; } = "";
    public int Games { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
    public int SolvedPuzzles { get; set; }
    public int PuzzleAttempts { get; set; }
}

public sealed class GuestClaimEntity
{
    public long Id { get; set; }
    public string PlayerId { get; set; } = "";
    public string GuestId { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
    public DateTimeOffset ClaimedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class RefreshSessionEntity
{
    public long Id { get; set; }
    public string PlayerId { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? ReplacedByHash { get; set; }
}

public sealed class PuzzleResultEntity
{
    public string PlayerId { get; set; } = "";
    public string PuzzleId { get; set; } = "";
    public int Attempts { get; set; }
    public DateTimeOffset SolvedAt { get; set; } = DateTimeOffset.UtcNow;
}
