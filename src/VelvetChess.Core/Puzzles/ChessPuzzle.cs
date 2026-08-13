using VelvetChess.Core.Model;

namespace VelvetChess.Core.Puzzles;

public sealed record ChessPuzzle(
    string Id,
    string Title,
    string Theme,
    int Rating,
    string Fen,
    IReadOnlyList<string> Solution,
    string Hint,
    string Explanation)
{
    public PieceColor SideToMove => new ChessBoard(Fen).SideToMove;
}

public sealed class PuzzleSession(ChessPuzzle puzzle)
{
    public ChessPuzzle Puzzle { get; } = puzzle;
    public ChessBoard Board { get; } = new(puzzle.Fen);
    public int Ply { get; private set; }
    public bool IsComplete => Ply >= Puzzle.Solution.Count;

    public PuzzleMoveResult TryMove(string uci)
    {
        if (IsComplete) return PuzzleMoveResult.Complete;
        if (!string.Equals(Puzzle.Solution[Ply], uci, StringComparison.OrdinalIgnoreCase)) return PuzzleMoveResult.Wrong;
        Board.ApplyLegalMove(Move.ParseUci(uci));
        Ply++;
        while (!IsComplete && Ply % 2 == 1)
        {
            Board.ApplyLegalMove(Move.ParseUci(Puzzle.Solution[Ply]));
            Ply++;
        }
        return IsComplete ? PuzzleMoveResult.Complete : PuzzleMoveResult.Correct;
    }
}

public enum PuzzleMoveResult { Wrong, Correct, Complete }
