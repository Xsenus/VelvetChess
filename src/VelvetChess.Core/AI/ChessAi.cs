using System.Diagnostics;
using VelvetChess.Core.Model;

namespace VelvetChess.Core.AI;

public sealed class ChessAi(int? randomSeed = null)
{
    private const int MateScore = 1_000_000;
    private readonly Random _random = randomSeed.HasValue ? new Random(randomSeed.Value) : Random.Shared;
    private readonly Dictionary<string, (int depth, int score)> _table = new(32_768);

    public Task<Move?> FindMoveAsync(ChessBoard board, Difficulty difficulty, CancellationToken cancellationToken = default) =>
        Task.Run(() => FindMove(board, DifficultyProfile.For(difficulty), cancellationToken), cancellationToken);

    public Move? FindMove(ChessBoard board, DifficultyProfile profile, CancellationToken cancellationToken = default)
    {
        var legal = board.GenerateLegalMoves();
        if (legal.Count == 0) return null;
        if (legal.Count == 1) return legal[0];

        _table.Clear();
        var watch = Stopwatch.StartNew();
        var deadline = profile.TimeLimitMs;
        var completed = new List<(Move move, int score)>();

        for (var depth = 1; depth <= profile.SearchDepth; depth++)
        {
            var iteration = new List<(Move move, int score)>(legal.Count);
            foreach (var move in OrderMoves(board, legal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (watch.ElapsedMilliseconds >= deadline && iteration.Count > 0) break;
                var next = board.Clone(); next.ApplyLegalMove(move);
                var score = -Negamax(next, depth - 1, -MateScore, MateScore, watch, deadline, cancellationToken, 1);
                iteration.Add((move, score));
            }
            if (iteration.Count == legal.Count) completed = iteration;
            if (watch.ElapsedMilliseconds >= deadline) break;
        }

        if (completed.Count == 0) completed = legal.Select(m => (m, 0)).ToList();
        completed.Sort((a, b) => b.score.CompareTo(a.score));
        if (profile.Randomness <= 0) return completed[0].move;

        var candidateCount = Math.Min(completed.Count, profile.Level == Difficulty.Beginner ? 6 : 3);
        if (_random.NextDouble() > profile.Randomness) candidateCount = Math.Min(candidateCount, 2);
        var weights = Enumerable.Range(0, candidateCount).Select(i => 1d / (i + 1)).ToArray();
        var roll = _random.NextDouble() * weights.Sum();
        for (var i = 0; i < candidateCount; i++) { roll -= weights[i]; if (roll <= 0) return completed[i].move; }
        return completed[0].move;
    }

    private int Negamax(ChessBoard board, int depth, int alpha, int beta, Stopwatch watch, int deadline, CancellationToken token, int ply)
    {
        token.ThrowIfCancellationRequested();
        var status = board.GetStatus();
        if (status.Outcome == GameOutcome.Checkmate) return -MateScore + ply;
        if (status.IsFinished) return 0;
        if (depth <= 0 || watch.ElapsedMilliseconds >= deadline) return EvaluateForSide(board);

        var key = board.ToFen();
        if (_table.TryGetValue(key, out var cached) && cached.depth >= depth) return cached.score;
        var best = -MateScore;
        foreach (var move in OrderMoves(board, board.GenerateLegalMoves()))
        {
            var next = board.Clone(); next.ApplyLegalMove(move);
            var score = -Negamax(next, depth - 1, -beta, -alpha, watch, deadline, token, ply + 1);
            if (score > best) best = score;
            if (score > alpha) alpha = score;
            if (alpha >= beta || watch.ElapsedMilliseconds >= deadline) break;
        }
        if (watch.ElapsedMilliseconds < deadline) _table[key] = (depth, best);
        return best;
    }

    private static IEnumerable<Move> OrderMoves(ChessBoard board, IReadOnlyList<Move> moves) => moves
        .OrderByDescending(m => MoveOrderScore(board, m));

    private static int MoveOrderScore(ChessBoard board, Move move)
    {
        var captured = board[move.To];
        var mover = board[move.From];
        var capture = captured.IsNone ? 0 : Values[captured.Type] * 10 - Values[mover.Type];
        var promotion = move.Promotion == PieceType.None ? 0 : Values[move.Promotion];
        return capture + promotion + (move.Flags.HasFlag(MoveFlags.Castle) ? 50 : 0);
    }

    private static int EvaluateForSide(ChessBoard board)
    {
        var white = 0; var black = 0;
        for (var square = 0; square < 64; square++)
        {
            var piece = board[square];
            if (piece.IsNone) continue;
            var file = square % 8; var rank = square / 8;
            var center = 7 - (Math.Abs(file * 2 - 7) + Math.Abs(rank * 2 - 7));
            var development = piece.Type switch
            {
                PieceType.Pawn => (piece.Color == PieceColor.White ? rank : 7 - rank) * 4,
                PieceType.Knight or PieceType.Bishop => center * 3,
                PieceType.Queen => center,
                _ => 0
            };
            var score = Values[piece.Type] + development;
            if (piece.Color == PieceColor.White) white += score; else black += score;
        }
        var evaluation = white - black;
        return board.SideToMove == PieceColor.White ? evaluation : -evaluation;
    }

    private static readonly IReadOnlyDictionary<PieceType, int> Values = new Dictionary<PieceType, int>
    {
        [PieceType.None] = 0, [PieceType.Pawn] = 100, [PieceType.Knight] = 320,
        [PieceType.Bishop] = 330, [PieceType.Rook] = 500, [PieceType.Queen] = 900,
        [PieceType.King] = 20_000
    };
}
