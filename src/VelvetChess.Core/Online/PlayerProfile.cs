namespace VelvetChess.Core.Online;

public enum IdentityProvider { Guest, Yandex, Vk }
public enum RatingKind { Local, Puzzles, OnlineRapid, OnlineBlitz }

public sealed record PlayerProfile(
    string PlayerId,
    string DisplayName,
    IdentityProvider Provider,
    bool IsGuest,
    DateTimeOffset CreatedAt);

public sealed record RatingEntry(RatingKind Kind, int Value, int Games, DateTimeOffset UpdatedAt);
public sealed record PlayerProgress(int Games, int Wins, int Draws, int Losses, int SolvedPuzzles, int PuzzleAttempts);
public sealed record ProfileSnapshot(PlayerProfile Profile, IReadOnlyList<RatingEntry> Ratings, PlayerProgress Progress, IReadOnlyList<string>? SolvedPuzzleIds = null);

public interface IPlayerAccountService
{
    PlayerProfile Current { get; }
    Task<PlayerProfile> SignInAsync(IdentityProvider provider, CancellationToken cancellationToken = default);
    Task SignOutAsync(CancellationToken cancellationToken = default);
    Task DeleteAccountAsync(CancellationToken cancellationToken = default);
    Task SyncProgressAsync(ProfileSnapshot localSnapshot, CancellationToken cancellationToken = default);
}
