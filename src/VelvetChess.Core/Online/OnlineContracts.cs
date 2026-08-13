using VelvetChess.Core.Model;

namespace VelvetChess.Core.Online;

public enum MatchConnectionState { Offline, Connecting, Searching, Playing, Reconnecting, Finished }

public sealed record MatchPlayer(string Id, string DisplayName, int Rating, PieceColor Color);
public sealed record OnlineMatch(string Id, MatchPlayer White, MatchPlayer Black, string Fen, TimeSpan WhiteTime, TimeSpan BlackTime);
public sealed record MatchEvent(string MatchId, string Type, string? MoveUci, string Fen, DateTimeOffset ServerTime);

public interface IOnlineMatchService
{
    MatchConnectionState State { get; }
    IAsyncEnumerable<MatchEvent> Events(CancellationToken cancellationToken = default);
    Task<OnlineMatch> FindMatchAsync(int? targetRating, CancellationToken cancellationToken = default);
    Task SubmitMoveAsync(string matchId, Move move, CancellationToken cancellationToken = default);
    Task ResignAsync(string matchId, CancellationToken cancellationToken = default);
}
