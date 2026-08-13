using VelvetChess.Core.AI;
using VelvetChess.Core.Game;
using VelvetChess.Core.Model;
using VelvetChess.Core.Persistence;

namespace VelvetChess.App.Services;

public sealed class AppStateService
{
    private readonly UserStateStore _state = new(new MauiPreferencesStore());

    public Difficulty Difficulty { get => _state.Difficulty; set => _state.Difficulty = value; }
    public bool ShowCoordinates { get => _state.ShowCoordinates; set => _state.ShowCoordinates = value; }
    public bool HapticsEnabled { get => _state.HapticsEnabled; set => _state.HapticsEnabled = value; }
    public bool ConfirmNewGame { get => _state.ConfirmNewGame; set => _state.ConfirmNewGame = value; }
    public bool HasSavedGame => _state.HasSavedGame;
    public int GamesPlayed => _state.GamesPlayed;
    public int Wins => _state.Wins;
    public int Draws => _state.Draws;
    public int CompletedPuzzleCount => _state.CompletedPuzzles.Count;
    public IReadOnlySet<string> CompletedPuzzles => _state.CompletedPuzzles;

    public LocalGameSession LoadGame() => _state.LoadGame();
    public void SaveGame(LocalGameSession session) => _state.SaveGame(session);
    public void ClearGame() => _state.ClearGame();
    public int GetPuzzleAttempts(string id) => _state.GetPuzzleAttempts(id);
    public void RecordPuzzleAttempt(string id) => _state.RecordPuzzleAttempt(id);
    public bool MarkPuzzleSolved(string id) => _state.MarkPuzzleSolved(id);
    public void RecordFinishedGame(GameStatus status) => _state.RecordFinishedGame(status);
    public void ResetProgress() => _state.ResetProgress();

    private sealed class MauiPreferencesStore : IKeyValueStore
    {
        public string GetString(string key, string defaultValue = "") => Preferences.Default.Get(key, defaultValue);
        public int GetInt(string key, int defaultValue = 0) => Preferences.Default.Get(key, defaultValue);
        public bool GetBool(string key, bool defaultValue) => Preferences.Default.Get(key, defaultValue);
        public void SetString(string key, string value) => Preferences.Default.Set(key, value);
        public void SetInt(string key, int value) => Preferences.Default.Set(key, value);
        public void SetBool(string key, bool value) => Preferences.Default.Set(key, value);
        public void Remove(string key) => Preferences.Default.Remove(key);
    }
}
