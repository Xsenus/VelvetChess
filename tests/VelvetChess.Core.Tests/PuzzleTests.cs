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
}
