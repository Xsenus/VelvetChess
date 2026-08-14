using Microsoft.EntityFrameworkCore;
using VelvetChess.Core.Online;
using VelvetChess.Server.Auth;
using VelvetChess.Server.Data;

namespace VelvetChess.Server.Accounts;

public sealed record ExchangeRequest(string Code, string RedirectUri, string CodeVerifier, string? DeviceId);
public sealed record SessionResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt, ProfileSnapshot Profile);
public sealed record RefreshRequest(string RefreshToken);
public sealed record ClaimGuestProgressRequest(string GuestId, string IdempotencyKey, PlayerProgress Progress, IReadOnlyList<string>? SolvedPuzzleIds = null);
public sealed record LeaderboardItem(int Place, string PlayerId, string DisplayName, int Rating, int Games);
public sealed record PuzzleResultRequest(int Attempts, IReadOnlyList<string> Moves);

public sealed class AccountService(AccountDbContext db, IEnumerable<IExternalIdentityProvider> providers, SessionTokenService tokens, IPuzzleCatalog puzzles)
{
    public async Task<SessionResponse> ExchangeAsync(IdentityProvider provider, ExchangeRequest request, CancellationToken cancellationToken)
    {
        var adapter = ResolveProvider(provider, request);
        var identity = await adapter.ExchangeAsync(new(request.Code, request.RedirectUri, request.CodeVerifier, request.DeviceId), cancellationToken);
        var providerName = provider.ToString().ToLowerInvariant();
        var link = await db.ExternalIdentities.SingleOrDefaultAsync(x => x.Provider == providerName && x.ProviderUserId == identity.ProviderUserId, cancellationToken);
        PlayerEntity player;
        if (link is null)
        {
            player = new PlayerEntity { DisplayName = identity.DisplayName };
            db.Players.Add(player);
            db.ExternalIdentities.Add(new ExternalIdentityEntity { PlayerId = player.Id, Provider = providerName, ProviderUserId = identity.ProviderUserId });
            db.Progress.Add(new ProgressEntity { PlayerId = player.Id });
            foreach (var kind in new[] { RatingKind.Puzzles, RatingKind.OnlineRapid, RatingKind.OnlineBlitz })
                db.Ratings.Add(new RatingEntity { PlayerId = player.Id, Kind = kind.ToString(), Value = 1000 });
            await db.SaveChangesAsync(cancellationToken);
        }
        else player = await db.Players.SingleAsync(x => x.Id == link.PlayerId, cancellationToken);
        return await CreateSessionAsync(player.Id, provider, cancellationToken);
    }

    public async Task<ProfileSnapshot> LinkExternalAsync(string playerId, IdentityProvider provider, ExchangeRequest request, CancellationToken cancellationToken)
    {
        var adapter = ResolveProvider(provider, request);
        var identity = await adapter.ExchangeAsync(new(request.Code, request.RedirectUri, request.CodeVerifier, request.DeviceId), cancellationToken);
        var providerName = provider.ToString().ToLowerInvariant();
        var existing = await db.ExternalIdentities.SingleOrDefaultAsync(x => x.Provider == providerName && x.ProviderUserId == identity.ProviderUserId, cancellationToken);
        if (existing is not null && existing.PlayerId != playerId) throw new InvalidOperationException("Этот внешний аккаунт уже связан с другим профилем Velvet.");
        if (existing is null)
        {
            db.ExternalIdentities.Add(new ExternalIdentityEntity { PlayerId = playerId, Provider = providerName, ProviderUserId = identity.ProviderUserId });
            await db.SaveChangesAsync(cancellationToken);
        }
        return await GetProfileAsync(playerId, provider, cancellationToken);
    }

    public async Task<ProfileSnapshot> GetProfileAsync(string playerId, IdentityProvider provider, CancellationToken cancellationToken)
    {
        var player = await db.Players.AsNoTracking().SingleAsync(x => x.Id == playerId, cancellationToken);
        var progress = await db.Progress.AsNoTracking().SingleAsync(x => x.PlayerId == playerId, cancellationToken);
        var ratings = await db.Ratings.AsNoTracking().Where(x => x.PlayerId == playerId).OrderBy(x => x.Kind).ToListAsync(cancellationToken);
        var solvedPuzzleIds = await db.PuzzleResults.AsNoTracking().Where(x => x.PlayerId == playerId).OrderBy(x => x.PuzzleId).Select(x => x.PuzzleId).ToListAsync(cancellationToken);
        if (provider == IdentityProvider.Guest)
        {
            var providerName = await db.ExternalIdentities.AsNoTracking().Where(x => x.PlayerId == playerId).Select(x => x.Provider).FirstOrDefaultAsync(cancellationToken);
            if (providerName is not null) Enum.TryParse(providerName, true, out provider);
        }
        return new(new(player.Id, player.DisplayName, provider, false, player.CreatedAt), ratings.Select(x => new RatingEntry(Enum.Parse<RatingKind>(x.Kind), x.Value, x.Games, x.UpdatedAt)).ToArray(),
            new(progress.Games, progress.Wins, progress.Draws, progress.Losses, progress.SolvedPuzzles, progress.PuzzleAttempts), solvedPuzzleIds);
    }

    public async Task<SessionResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) || refreshToken.Length > 256) throw new ArgumentException("Некорректный refresh token.");
        var hash = SessionTokenService.HashRefreshToken(refreshToken);
        var session = await db.RefreshSessions.SingleOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (session is null || session.RevokedAt is not null || session.ExpiresAt <= DateTimeOffset.UtcNow) throw new InvalidOperationException("Refresh token недействителен или уже использован.");
        var replacement = SessionTokenService.NewRefreshToken();
        session.RevokedAt = DateTimeOffset.UtcNow;
        session.ReplacedByHash = SessionTokenService.HashRefreshToken(replacement);
        db.RefreshSessions.Add(new RefreshSessionEntity { PlayerId = session.PlayerId, TokenHash = session.ReplacedByHash, ExpiresAt = DateTimeOffset.UtcNow.AddDays(30) });
        await db.SaveChangesAsync(cancellationToken);
        return new(tokens.Issue(session.PlayerId), replacement, DateTimeOffset.UtcNow.AddMinutes(15), await GetProfileAsync(session.PlayerId, IdentityProvider.Guest, cancellationToken));
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;
        var hash = SessionTokenService.HashRefreshToken(refreshToken);
        var session = await db.RefreshSessions.SingleOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (session is null || session.RevokedAt is not null) return;
        session.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProfileSnapshot> ClaimGuestAsync(string playerId, ClaimGuestProgressRequest request, CancellationToken cancellationToken)
    {
        ValidateClaim(request);
        var existing = await db.GuestClaims.AsNoTracking().SingleOrDefaultAsync(x => x.GuestId == request.GuestId, cancellationToken);
        if (existing is not null && existing.PlayerId != playerId) throw new InvalidOperationException("Этот гостевой профиль уже перенесён в другой аккаунт.");
        if (existing is null)
        {
            var progress = await db.Progress.SingleAsync(x => x.PlayerId == playerId, cancellationToken);
            progress.Games += request.Progress.Games; progress.Wins += request.Progress.Wins; progress.Draws += request.Progress.Draws;
            progress.Losses += request.Progress.Losses; progress.SolvedPuzzles += request.Progress.SolvedPuzzles; progress.PuzzleAttempts += request.Progress.PuzzleAttempts;
            foreach (var puzzleId in request.SolvedPuzzleIds ?? []) db.PuzzleResults.Add(new PuzzleResultEntity { PlayerId = playerId, PuzzleId = puzzleId, Attempts = 0 });
            db.GuestClaims.Add(new GuestClaimEntity { PlayerId = playerId, GuestId = request.GuestId, IdempotencyKey = request.IdempotencyKey });
            await db.SaveChangesAsync(cancellationToken);
        }
        return await GetProfileAsync(playerId, IdentityProvider.Guest, cancellationToken);
    }

    public async Task<IReadOnlyList<LeaderboardItem>> LeaderboardAsync(RatingKind kind, int limit, CancellationToken cancellationToken)
    {
        if (kind == RatingKind.Local) throw new ArgumentException("Локальный рейтинг не публикуется.");
        var rows = await db.Ratings.AsNoTracking().Where(x => x.Kind == kind.ToString()).OrderByDescending(x => x.Value).ThenByDescending(x => x.Games)
            .Take(Math.Clamp(limit, 1, 100)).Join(db.Players, rating => rating.PlayerId, player => player.Id, (rating, player) => new { rating, player }).ToListAsync(cancellationToken);
        return rows.Select((x, index) => new LeaderboardItem(index + 1, x.player.Id, x.player.DisplayName, x.rating.Value, x.rating.Games)).ToArray();
    }

    public async Task DeleteAccountAsync(string playerId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.RefreshSessions.Where(x => x.PlayerId == playerId).ExecuteDeleteAsync(cancellationToken);
        await db.GuestClaims.Where(x => x.PlayerId == playerId).ExecuteDeleteAsync(cancellationToken);
        await db.Ratings.Where(x => x.PlayerId == playerId).ExecuteDeleteAsync(cancellationToken);
        await db.Progress.Where(x => x.PlayerId == playerId).ExecuteDeleteAsync(cancellationToken);
        await db.ExternalIdentities.Where(x => x.PlayerId == playerId).ExecuteDeleteAsync(cancellationToken);
        await db.PuzzleResults.Where(x => x.PlayerId == playerId).ExecuteDeleteAsync(cancellationToken);
        await db.Players.Where(x => x.Id == playerId).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<ProfileSnapshot> RecordPuzzleResultAsync(string playerId, RatedPuzzle puzzle, PuzzleResultRequest request, CancellationToken cancellationToken)
    {
        if (request.Attempts is < 1 or > 1000 || !request.Moves.SequenceEqual(puzzle.Solution, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Решение задачи не подтверждено.");
        if (!await db.PuzzleResults.AnyAsync(x => x.PlayerId == playerId && x.PuzzleId == puzzle.Id, cancellationToken))
        {
            db.PuzzleResults.Add(new PuzzleResultEntity { PlayerId = playerId, PuzzleId = puzzle.Id, Attempts = request.Attempts });
            var progress = await db.Progress.SingleAsync(x => x.PlayerId == playerId, cancellationToken);
            progress.SolvedPuzzles++; progress.PuzzleAttempts += request.Attempts;
            var rating = await db.Ratings.SingleAsync(x => x.PlayerId == playerId && x.Kind == RatingKind.Puzzles.ToString(), cancellationToken);
            var expected = 1d / (1d + Math.Pow(10d, (puzzle.Rating - rating.Value) / 400d));
            rating.Value = Math.Clamp((int)Math.Round(rating.Value + 32d * (1d - expected)), 100, 3000);
            rating.Games++; rating.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        return await GetProfileAsync(playerId, IdentityProvider.Guest, cancellationToken);
    }

    private void ValidateClaim(ClaimGuestProgressRequest request)
    {
        if (!request.GuestId.StartsWith("guest-", StringComparison.Ordinal) || request.GuestId.Length > 80 || string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 80)
            throw new ArgumentException("Некорректный гостевой идентификатор.");
        var p = request.Progress;
        if (new[] { p.Games, p.Wins, p.Draws, p.Losses, p.SolvedPuzzles, p.PuzzleAttempts }.Any(x => x < 0) || p.Wins + p.Draws + p.Losses > p.Games || p.SolvedPuzzles > 50)
            throw new ArgumentException("Некорректная локальная статистика.");
        var ids = (request.SolvedPuzzleIds ?? []).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length != p.SolvedPuzzles || ids.Any(id => puzzles.Find(id) is null)) throw new ArgumentException("Список решённых задач не соответствует локальной статистике.");
    }

    private async Task<SessionResponse> CreateSessionAsync(string playerId, IdentityProvider provider, CancellationToken cancellationToken)
    {
        var refresh = SessionTokenService.NewRefreshToken();
        db.RefreshSessions.Add(new RefreshSessionEntity { PlayerId = playerId, TokenHash = SessionTokenService.HashRefreshToken(refresh), ExpiresAt = DateTimeOffset.UtcNow.AddDays(30) });
        await db.SaveChangesAsync(cancellationToken);
        return new(tokens.Issue(playerId), refresh, DateTimeOffset.UtcNow.AddMinutes(15), await GetProfileAsync(playerId, provider, cancellationToken));
    }

    private IExternalIdentityProvider ResolveProvider(IdentityProvider provider, ExchangeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || request.CodeVerifier.Length is < 43 or > 128 || !Uri.TryCreate(request.RedirectUri, UriKind.Absolute, out _))
            throw new ArgumentException("Некорректные параметры OAuth/PKCE.");
        return providers.SingleOrDefault(x => x.Provider == provider) ?? throw new ArgumentException("Неизвестный провайдер.");
    }
}
