using VelvetChess.App.Controls;
using VelvetChess.App.Services;
using VelvetChess.Core.Model;
using VelvetChess.Core.Puzzles;

namespace VelvetChess.App.Pages;

public sealed class PuzzlePlayPage : ContentPage, IQueryAttributable
{
    private readonly PuzzleRepository _repository;
    private readonly AppStateService _state;
    private readonly ChessBoardView _board = new();
    private readonly Label _title = new() { FontSize = 22, FontFamily = "OpenSansSemibold" };
    private readonly Label _message = new() { FontSize = 14, TextColor = Color.FromArgb("#9DA7BE") };
    private readonly Button _hint = new() { Text = "Подсказка", HeightRequest = 46, BackgroundColor = Color.FromArgb("#202841"), TextColor = Colors.White };
    private readonly Button _next = new() { Text = "Следующая задача", HeightRequest = 46, IsVisible = false };
    private PuzzleSession? _session;

    public PuzzlePlayPage(PuzzleRepository repository, AppStateService state)
    {
        _repository = repository; _state = state; Title = "Решение задачи";
        _board.ShowCoordinates = state.ShowCoordinates;
        _board.MoveRequested += OnMove;
        _hint.Clicked += async (_, _) => { if (_session is not null) await DisplayAlert("Подсказка", _session.Puzzle.Hint, "Понятно"); };
        _next.Clicked += async (_, _) => await OpenNextAsync();
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 16, Spacing = 14, Children =
        {
            _title,
            new Label { Text = "Найдите лучший ход", TextColor = Color.FromArgb("#D6AE68"), CharacterSpacing = 1.4, FontSize = 12 },
            new Border { StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 }, Content = _board },
            _message,
            new HorizontalStackLayout { Spacing = 10, HorizontalOptions = LayoutOptions.Center, Children = { _hint, _next } }
        }}};
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var id)) _ = LoadAsync(Uri.UnescapeDataString(id.ToString() ?? ""));
    }

    private async Task LoadAsync(string id)
    {
        var puzzle = await _repository.GetAsync(id);
        if (puzzle is null) { _message.Text = "Задача не найдена."; return; }
        _session = new PuzzleSession(puzzle); _title.Text = puzzle.Title;
        _message.Text = $"Сложность: {puzzle.Rating} · {puzzle.Theme}"; _next.IsVisible = false; _hint.IsVisible = true;
        _board.InputEnabled = true; _board.ShowCoordinates = _state.ShowCoordinates; _board.Flipped = puzzle.SideToMove == PieceColor.Black; _board.SetBoard(_session.Board);
    }

    private async void OnMove(object? sender, Move move)
    {
        if (_session is null) return;
        var result = _session.TryMove(move.Uci); _board.SetBoard(_session.Board);
        HapticFeedbackIfEnabled(result == PuzzleMoveResult.Wrong ? HapticFeedbackType.LongPress : HapticFeedbackType.Click);
        if (result == PuzzleMoveResult.Wrong)
        {
            _state.RecordPuzzleAttempt(_session.Puzzle.Id);
            _message.Text = "Не совсем. Посмотрите на форсированные ответы и попробуйте ещё.";
        }
        else if (result == PuzzleMoveResult.Complete)
        {
            _board.InputEnabled = false; _state.MarkPuzzleSolved(_session.Puzzle.Id);
            _message.Text = _session.Puzzle.Explanation; _next.IsVisible = true; _hint.IsVisible = false;
            await DisplayAlert("Верно!", "Задача решена. Отличная точность.", "Продолжить");
        }
        else _message.Text = "Точно! Продолжайте вариант.";
    }

    private async Task OpenNextAsync()
    {
        if (_session is null) return;
        var puzzles = await _repository.GetAllAsync(); var current = puzzles.ToList().FindIndex(puzzle => puzzle.Id == _session.Puzzle.Id);
        var next = puzzles[(current + 1 + puzzles.Count) % puzzles.Count];
        await LoadAsync(next.Id);
    }

    private void HapticFeedbackIfEnabled(HapticFeedbackType type)
    {
        if (!_state.HapticsEnabled || !HapticFeedback.Default.IsSupported) return;
        try { HapticFeedback.Default.Perform(type); } catch (FeatureNotSupportedException) { }
    }
}
