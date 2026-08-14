using VelvetChess.Core.AI;
using VelvetChess.Core.Game;
using VelvetChess.Core.Model;
using VelvetChess.Core.Persistence;
using Xunit;

namespace VelvetChess.Core.Tests;

public sealed class UserStateTests
{
    [Fact]
    public void GameRoundTripsAndCorruptSaveIsDiscarded()
    {
        var memory = new MemoryStore(); var state = new UserStateStore(memory);
        var game = LocalGameSession.Restore("e2e4 e7e5 g1f3");
        state.SaveGame(game);
        Assert.Equal(game.SerializeMoves(), state.LoadGame().SerializeMoves());

        memory.SetString("game.moves.v1", "e2e5 illegal");
        Assert.Empty(state.LoadGame().History);
        Assert.False(state.HasSavedGame);
    }

    [Fact]
    public void PuzzleProgressIsIdempotentAndMalformedJsonRecovers()
    {
        var memory = new MemoryStore(); var state = new UserStateStore(memory);
        Assert.True(state.MarkPuzzleSolved("p1"));
        Assert.False(state.MarkPuzzleSolved("p1"));
        state.RecordPuzzleAttempt("p1"); state.RecordPuzzleAttempt("p1");
        Assert.Single(state.CompletedPuzzles);
        Assert.Equal(2, state.GetPuzzleAttempts("p1"));

        memory.SetString("puzzles.completed.v1", "not-json");
        memory.SetString("puzzles.attempts.v1", "not-json");
        Assert.Empty(state.CompletedPuzzles);
        Assert.Equal(0, state.GetPuzzleAttempts("p1"));
    }

    [Fact]
    public void StatisticsAndSettingsPersistAndResetSafely()
    {
        var memory = new MemoryStore(); var state = new UserStateStore(memory)
        {
            Difficulty = Difficulty.Expert,
            ShowCoordinates = false,
            HapticsEnabled = false,
            ConfirmNewGame = false,
            PieceTheme = PieceTheme.Royal,
            BoardTheme = BoardTheme.Forest,
            ShowLegalMoves = false,
            AnimateMoves = false,
            HighlightLastMove = false
        };
        state.RecordFinishedGame(new(GameOutcome.Checkmate, PieceColor.White));
        state.RecordFinishedGame(new(GameOutcome.Stalemate));
        state.MarkPuzzleSolved("p1");
        Assert.Equal((2, 1, 1), (state.GamesPlayed, state.Wins, state.Draws));

        state.ResetProgress();
        Assert.Equal((0, 0, 0), (state.GamesPlayed, state.Wins, state.Draws));
        Assert.Empty(state.CompletedPuzzles);
        Assert.Equal(1000, state.LocalRating); Assert.Equal(1000, state.PuzzleRating);
        Assert.Equal(Difficulty.Expert, state.Difficulty);
        Assert.False(state.ShowCoordinates); Assert.False(state.HapticsEnabled); Assert.False(state.ConfirmNewGame);
        Assert.Equal(PieceTheme.Royal, state.PieceTheme); Assert.Equal(BoardTheme.Forest, state.BoardTheme);
        Assert.False(state.ShowLegalMoves); Assert.False(state.AnimateMoves); Assert.False(state.HighlightLastMove);
    }

    [Fact]
    public void InvalidStoredNumbersAreClamped()
    {
        var memory = new MemoryStore();
        memory.SetInt("stats.games.v1", -3); memory.SetInt("stats.wins.v1", 99); memory.SetInt("stats.draws.v1", 99);
        memory.SetInt("game.difficulty.v1", 99);
        memory.SetInt("settings.pieceTheme.v1", 99); memory.SetInt("settings.boardTheme.v1", -4);
        var state = new UserStateStore(memory);
        Assert.Equal((0, 0, 0), (state.GamesPlayed, state.Wins, state.Draws));
        Assert.Equal(Difficulty.Expert, state.Difficulty);
        Assert.Equal(PieceTheme.Minimal, state.PieceTheme); Assert.Equal(BoardTheme.Velvet, state.BoardTheme);
    }

    [Fact]
    public void RatingsReactToResultsAndPuzzleProgress()
    {
        var state = new UserStateStore(new MemoryStore());
        var initialGame = state.LocalRating; var initialPuzzle = state.PuzzleRating;
        state.RecordFinishedGame(new(GameOutcome.Checkmate, PieceColor.White), Difficulty.Expert);
        Assert.True(state.LocalRating > initialGame);
        Assert.Equal(state.LocalRating, state.BestLocalRating);

        Assert.True(state.MarkPuzzleSolved("rated", 1600));
        Assert.True(state.PuzzleRating > initialPuzzle);
        var afterFirstSolve = state.PuzzleRating;
        Assert.False(state.MarkPuzzleSolved("rated", 1600));
        Assert.Equal(afterFirstSolve, state.PuzzleRating);
    }

    private sealed class MemoryStore : IKeyValueStore
    {
        private readonly Dictionary<string, object> _values = [];
        public string GetString(string key, string defaultValue = "") => _values.TryGetValue(key, out var value) && value is string text ? text : defaultValue;
        public int GetInt(string key, int defaultValue = 0) => _values.TryGetValue(key, out var value) && value is int number ? number : defaultValue;
        public bool GetBool(string key, bool defaultValue) => _values.TryGetValue(key, out var value) && value is bool flag ? flag : defaultValue;
        public void SetString(string key, string value) => _values[key] = value;
        public void SetInt(string key, int value) => _values[key] = value;
        public void SetBool(string key, bool value) => _values[key] = value;
        public void Remove(string key) => _values.Remove(key);
    }
}
