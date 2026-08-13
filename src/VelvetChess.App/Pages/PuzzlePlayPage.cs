using VelvetChess.App.Controls;
using VelvetChess.App.Services;
using VelvetChess.Core.Model;
using VelvetChess.Core.Puzzles;

namespace VelvetChess.App.Pages;

public sealed class PuzzlePlayPage : ContentPage, IQueryAttributable
{
    private readonly PuzzleRepository _repository;
    private readonly ChessBoardView _board = new();
    private readonly Label _title = new() { FontSize = 22, FontFamily = "OpenSansSemibold" };
    private readonly Label _message = new() { FontSize = 14, TextColor = Color.FromArgb("#9DA7BE") };
    private readonly Button _hint = new() { Text = "Подсказка", HeightRequest = 46, BackgroundColor = Color.FromArgb("#202841"), TextColor = Colors.White };
    private PuzzleSession? _session;

    public PuzzlePlayPage(PuzzleRepository repository)
    {
        _repository = repository; Title = "Решение задачи";
        _board.MoveRequested += OnMove;
        _hint.Clicked += async (_, _) => { if (_session is not null) await DisplayAlert("Подсказка", _session.Puzzle.Hint, "Понятно"); };
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 16, Spacing = 14, Children =
        {
            _title,
            new Label { Text = "Найдите лучший ход", TextColor = Color.FromArgb("#D6AE68"), CharacterSpacing = 1.4, FontSize = 12 },
            new Border { StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 }, Content = _board },
            _message, _hint
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
        _session = new PuzzleSession(puzzle); _title.Text = puzzle.Title; _message.Text = $"Сложность: {puzzle.Rating}";
        _board.Flipped = puzzle.SideToMove == PieceColor.Black; _board.SetBoard(_session.Board);
    }

    private async void OnMove(object? sender, Move move)
    {
        if (_session is null) return;
        var result = _session.TryMove(move.Uci); _board.SetBoard(_session.Board);
        if (result == PuzzleMoveResult.Wrong) { _message.Text = "Не совсем. Посмотрите на форсированные ответы и попробуйте ещё."; }
        else if (result == PuzzleMoveResult.Complete)
        {
            _board.InputEnabled = false; _message.Text = _session.Puzzle.Explanation;
            await DisplayAlert("Верно!", "Задача решена. Отличная точность.", "Продолжить");
        }
        else _message.Text = "Точно! Продолжайте вариант.";
    }
}
