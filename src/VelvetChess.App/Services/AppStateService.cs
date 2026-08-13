using System.Text.Json;
using VelvetChess.Core.AI;
using VelvetChess.Core.Game;
using VelvetChess.Core.Model;

namespace VelvetChess.App.Services;

public sealed class AppStateService
{
    private const string SavedMovesKey = "game.moves.v1";
    private const string DifficultyKey = "game.difficulty.v1";
    private const string CompletedPuzzlesKey = "puzzles.completed.v1";
    private const string PuzzleAttemptsKey = "puzzles.attempts.v1";
    private const string GamesKey = "stats.games.v1";
    private const string WinsKey = "stats.wins.v1";
    private const string DrawsKey = "stats.draws.v1";

    public Difficulty Difficulty
    {
        get => (Difficulty)Math.Clamp(Preferences.Default.Get(DifficultyKey, (int)Difficulty.Casual), 0, 3);
        set => Preferences.Default.Set(DifficultyKey, (int)value);
    }

    public bool HasSavedGame => !string.IsNullOrWhiteSpace(Preferences.Default.Get(SavedMovesKey, ""));
    public int GamesPlayed => Preferences.Default.Get(GamesKey, 0);
    public int Wins => Preferences.Default.Get(WinsKey, 0);
    public int Draws => Preferences.Default.Get(DrawsKey, 0);
    public int CompletedPuzzleCount => CompletedPuzzles.Count;

    public LocalGameSession LoadGame()
    {
        try { return LocalGameSession.Restore(Preferences.Default.Get(SavedMovesKey, "")); }
        catch (FormatException) { ClearGame(); return new LocalGameSession(); }
    }

    public void SaveGame(LocalGameSession session)
    {
        var moves = session.SerializeMoves();
        if (string.IsNullOrEmpty(moves)) ClearGame(); else Preferences.Default.Set(SavedMovesKey, moves);
    }

    public void ClearGame() => Preferences.Default.Remove(SavedMovesKey);

    public IReadOnlySet<string> CompletedPuzzles => ReadSet(CompletedPuzzlesKey);

    public int GetPuzzleAttempts(string id)
    {
        var attempts = ReadDictionary(PuzzleAttemptsKey);
        return attempts.GetValueOrDefault(id);
    }

    public void RecordPuzzleAttempt(string id)
    {
        var attempts = ReadDictionary(PuzzleAttemptsKey);
        attempts[id] = attempts.GetValueOrDefault(id) + 1;
        Preferences.Default.Set(PuzzleAttemptsKey, JsonSerializer.Serialize(attempts));
    }

    public bool MarkPuzzleSolved(string id)
    {
        var completed = ReadSet(CompletedPuzzlesKey);
        if (!completed.Add(id)) return false;
        Preferences.Default.Set(CompletedPuzzlesKey, JsonSerializer.Serialize(completed));
        return true;
    }

    public void RecordFinishedGame(GameStatus status)
    {
        Preferences.Default.Set(GamesKey, GamesPlayed + 1);
        if (status.Winner == PieceColor.White) Preferences.Default.Set(WinsKey, Wins + 1);
        else if (status.Winner is null) Preferences.Default.Set(DrawsKey, Draws + 1);
        ClearGame();
    }

    private static HashSet<string> ReadSet(string key)
    {
        try { return JsonSerializer.Deserialize<HashSet<string>>(Preferences.Default.Get(key, "[]")) ?? []; }
        catch (JsonException) { return []; }
    }

    private static Dictionary<string, int> ReadDictionary(string key)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, int>>(Preferences.Default.Get(key, "{}")) ?? []; }
        catch (JsonException) { return []; }
    }
}
