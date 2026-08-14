using System.Text.Json;
using VelvetChess.Core.Model;
using VelvetChess.Core.Puzzles;
using Xunit;

namespace VelvetChess.Core.Tests;

public sealed class PuzzleTests
{
    [Fact]
    public void CatalogContainsFiftyFullyLegalSolutions()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "puzzles.json"));
        var puzzles = JsonSerializer.Deserialize<List<ChessPuzzle>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(puzzles); Assert.Equal(50, puzzles.Count);
        foreach (var puzzle in puzzles)
        {
            var board = new ChessBoard(puzzle.Fen);
            foreach (var uci in puzzle.Solution) board.ApplyLegalMove(Move.ParseUci(uci));
        }
    }

    [Fact]
    public void EveryPuzzleSessionAcceptsTheFirstSolverMoveAndFormatsACompleteSolution()
    {
        var puzzles = LoadPuzzles();
        foreach (var puzzle in puzzles)
        {
            var session = new PuzzleSession(puzzle);
            Assert.False(string.IsNullOrWhiteSpace(session.SolutionText));
            var result = session.TryMove(puzzle.Solution[0]);
            Assert.Contains(result, new[] { PuzzleMoveResult.Correct, PuzzleMoveResult.Complete });
            if (!session.IsComplete) Assert.Equal(new ChessBoard(puzzle.Fen).SideToMove, session.Board.SideToMove);
        }
    }

    [Fact]
    public void RevealingSolutionCompletesEveryPuzzleWithoutIllegalMoves()
    {
        foreach (var puzzle in LoadPuzzles())
        {
            var session = new PuzzleSession(puzzle);
            session.RevealSolution();
            Assert.True(session.IsComplete);
            Assert.Equal(puzzle.Solution.Count, session.Ply);
        }
    }

    private static IReadOnlyList<ChessPuzzle> LoadPuzzles()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "puzzles.json"));
        return JsonSerializer.Deserialize<List<ChessPuzzle>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    }
}
