using VelvetChess.Core.Game;
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

public sealed class PuzzleSession
{
    public ChessPuzzle Puzzle { get; }
    public ChessBoard Board { get; }
    public string SolutionText { get; }
    public int Ply { get; private set; }
    public bool IsComplete => Ply >= Puzzle.Solution.Count;

    public PuzzleSession(ChessPuzzle puzzle)
    {
        Puzzle = puzzle;
        Board = new ChessBoard(puzzle.Fen);
        SolutionText = FormatSolution(puzzle);
    }

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

    public void RevealSolution()
    {
        while (!IsComplete)
        {
            Board.ApplyLegalMove(Move.ParseUci(Puzzle.Solution[Ply]));
            Ply++;
        }
    }

    private static string FormatSolution(ChessPuzzle puzzle)
    {
        var board = new ChessBoard(puzzle.Fen);
        var tokens = new List<string>(puzzle.Solution.Count);
        foreach (var uci in puzzle.Solution)
        {
            var move = Move.ParseUci(uci);
            var color = board.SideToMove;
            var moveNumber = board.FullmoveNumber;
            var san = ChessNotation.ToSan(board, move);
            tokens.Add(color == PieceColor.White ? $"{moveNumber}. {san}" : tokens.Count == 0 ? $"{moveNumber}... {san}" : san);
            board.ApplyLegalMove(move);
        }
        return string.Join(' ', tokens);
    }
}

public enum PuzzleMoveResult { Wrong, Correct, Complete }
