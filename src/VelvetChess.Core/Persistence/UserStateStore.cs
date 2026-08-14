using System.Text.Json;
using VelvetChess.Core.AI;
using VelvetChess.Core.Game;
using VelvetChess.Core.Model;

namespace VelvetChess.Core.Persistence;

public sealed class UserStateStore(IKeyValueStore storage)
{
    private const string SavedMovesKey = "game.moves.v1";
    private const string DifficultyKey = "game.difficulty.v1";
    private const string CompletedPuzzlesKey = "puzzles.completed.v1";
    private const string PuzzleAttemptsKey = "puzzles.attempts.v1";
    private const string GamesKey = "stats.games.v1";
    private const string WinsKey = "stats.wins.v1";
    private const string DrawsKey = "stats.draws.v1";
    private const string CoordinatesKey = "settings.coordinates.v1";
    private const string HapticsKey = "settings.haptics.v1";
    private const string ConfirmNewGameKey = "settings.confirmNewGame.v1";
    private const string PieceThemeKey = "settings.pieceTheme.v1";
    private const string BoardThemeKey = "settings.boardTheme.v1";

    public Difficulty Difficulty
    {
        get => (Difficulty)Math.Clamp(storage.GetInt(DifficultyKey, (int)Difficulty.Casual), 0, 3);
        set => storage.SetInt(DifficultyKey, (int)value);
    }

    public bool ShowCoordinates { get => storage.GetBool(CoordinatesKey, true); set => storage.SetBool(CoordinatesKey, value); }
    public bool HapticsEnabled { get => storage.GetBool(HapticsKey, true); set => storage.SetBool(HapticsKey, value); }
    public bool ConfirmNewGame { get => storage.GetBool(ConfirmNewGameKey, true); set => storage.SetBool(ConfirmNewGameKey, value); }
    public PieceTheme PieceTheme
    {
        get => (PieceTheme)Math.Clamp(storage.GetInt(PieceThemeKey, (int)PieceTheme.Tournament), 0, 4);
        set => storage.SetInt(PieceThemeKey, Math.Clamp((int)value, 0, 4));
    }
    public BoardTheme BoardTheme
    {
        get => (BoardTheme)Math.Clamp(storage.GetInt(BoardThemeKey, (int)BoardTheme.Velvet), 0, 4);
        set => storage.SetInt(BoardThemeKey, Math.Clamp((int)value, 0, 4));
    }
    public bool HasSavedGame => !string.IsNullOrWhiteSpace(storage.GetString(SavedMovesKey));
    public int GamesPlayed => Math.Max(0, storage.GetInt(GamesKey));
    public int Wins => Math.Clamp(storage.GetInt(WinsKey), 0, GamesPlayed);
    public int Draws => Math.Clamp(storage.GetInt(DrawsKey), 0, GamesPlayed - Wins);
    public HashSet<string> CompletedPuzzles => ReadSet(CompletedPuzzlesKey);

    public LocalGameSession LoadGame()
    {
        try { return LocalGameSession.Restore(storage.GetString(SavedMovesKey)); }
        catch (FormatException) { ClearGame(); return new LocalGameSession(); }
    }

    public void SaveGame(LocalGameSession session)
    {
        var moves = session.SerializeMoves();
        if (string.IsNullOrEmpty(moves)) ClearGame(); else storage.SetString(SavedMovesKey, moves);
    }

    public void ClearGame() => storage.Remove(SavedMovesKey);
    public int GetPuzzleAttempts(string id) => Math.Max(0, ReadDictionary(PuzzleAttemptsKey).GetValueOrDefault(id));

    public void RecordPuzzleAttempt(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var attempts = ReadDictionary(PuzzleAttemptsKey);
        attempts[id] = Math.Max(0, attempts.GetValueOrDefault(id)) + 1;
        storage.SetString(PuzzleAttemptsKey, JsonSerializer.Serialize(attempts));
    }

    public bool MarkPuzzleSolved(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var completed = ReadSet(CompletedPuzzlesKey);
        if (!completed.Add(id)) return false;
        storage.SetString(CompletedPuzzlesKey, JsonSerializer.Serialize(completed));
        return true;
    }

    public void RecordFinishedGame(GameStatus status)
    {
        storage.SetInt(GamesKey, GamesPlayed + 1);
        if (status.Winner == PieceColor.White) storage.SetInt(WinsKey, Wins + 1);
        else if (status.Winner is null) storage.SetInt(DrawsKey, Draws + 1);
        ClearGame();
    }

    public void ResetProgress()
    {
        foreach (var key in new[] { SavedMovesKey, CompletedPuzzlesKey, PuzzleAttemptsKey, GamesKey, WinsKey, DrawsKey }) storage.Remove(key);
    }

    private HashSet<string> ReadSet(string key)
    {
        try { return JsonSerializer.Deserialize<HashSet<string>>(storage.GetString(key, "[]")) ?? []; }
        catch (JsonException) { storage.Remove(key); return []; }
    }

    private Dictionary<string, int> ReadDictionary(string key)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, int>>(storage.GetString(key, "{}")) ?? []; }
        catch (JsonException) { storage.Remove(key); return []; }
    }
}
