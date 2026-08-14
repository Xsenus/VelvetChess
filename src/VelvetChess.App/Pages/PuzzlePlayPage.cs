using VelvetChess.App.Controls;
using VelvetChess.App.Services;
using VelvetChess.Core.Model;
using VelvetChess.Core.Puzzles;

namespace VelvetChess.App.Pages;

public sealed class PuzzlePlayPage : ContentPage, IQueryAttributable
{
    private readonly PuzzleRepository _repository;
    private readonly AppStateService _state;
    private readonly PlayerAccountService _account;
    private readonly ChessBoardView _board = new();
    private readonly Label _title = new() { FontSize = 22, FontFamily = "OpenSansSemibold" };
    private readonly Label _message = new() { FontSize = 14, TextColor = Color.FromArgb("#9DA7BE") };
    private readonly Button _hint = new() { Text = "Подсказка", HeightRequest = 46, Margin = 4, BackgroundColor = Color.FromArgb("#202841"), TextColor = Colors.White };
    private readonly Button _solution = new() { Text = "Показать решение", HeightRequest = 46, Margin = 4, BackgroundColor = Color.FromArgb("#202841"), TextColor = Colors.White };
    private readonly Button _next = new() { Text = "Следующая задача", HeightRequest = 46, Margin = 4, IsVisible = false };
    private PuzzleSession? _session;

    public PuzzlePlayPage(PuzzleRepository repository, AppStateService state, PlayerAccountService account)
    {
        BackgroundColor = Color.FromArgb("#0B1020");
        _repository = repository; _state = state; _account = account; Title = "Решение задачи";
        _board.ShowCoordinates = state.ShowCoordinates;
        _board.ShowLegalMoves = state.ShowLegalMoves; _board.HighlightLastMove = state.HighlightLastMove;
        _board.SetAppearance(state.PieceTheme, state.BoardTheme);
        _board.MoveRequested += OnMove;
        _hint.Clicked += async (_, _) => { if (_session is not null) await DisplayAlert("Подсказка", _session.Puzzle.Hint, "Понятно"); };
        _solution.Clicked += async (_, _) => await RevealSolutionAsync();
        _next.Clicked += async (_, _) => await OpenNextAsync();
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 16, Spacing = 14, Children =
        {
            _title,
            new Label { Text = "Найдите лучший ход", TextColor = Color.FromArgb("#D6AE68"), CharacterSpacing = 1.4, FontSize = 12 },
            new Border { StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 }, Content = _board },
            _message,
            new FlexLayout { Direction = Microsoft.Maui.Layouts.FlexDirection.Row, Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Center, AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Center, Children = { _hint, _solution, _next } }
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
        _message.Text = $"Сложность: {puzzle.Rating} · {puzzle.Theme}"; _next.IsVisible = false; _hint.IsVisible = true; _solution.IsVisible = true;
        _board.InputEnabled = true; _board.ShowCoordinates = _state.ShowCoordinates; _board.ShowLegalMoves = _state.ShowLegalMoves; _board.HighlightLastMove = _state.HighlightLastMove;
        _board.SetAppearance(_state.PieceTheme, _state.BoardTheme); _board.Flipped = puzzle.SideToMove == PieceColor.Black; _board.SetBoard(_session.Board);
    }

    private async void OnMove(object? sender, Move move)
    {
        if (_session is null) return;
        var movingPiece = _session.Board[move.From];
        var result = _session.TrySolverMove(move.Uci);
        HapticFeedbackIfEnabled(result == PuzzleMoveResult.Wrong ? HapticFeedbackType.LongPress : HapticFeedbackType.Click);
        if (result == PuzzleMoveResult.Wrong)
        {
            _state.RecordPuzzleAttempt(_session.Puzzle.Id);
            _message.Text = "Этот ход не входит в лучший вариант. Позиция не изменилась — попробуйте найти форсированное продолжение.";
            _board.ClearSelection();
        }
        else
        {
            _board.InputEnabled = false;
            await _board.AnimateMoveAsync(move, movingPiece, _session.Board, _state.AnimateMoves);
            if (result == PuzzleMoveResult.Complete) { await CompletePuzzleAsync(); return; }

            _message.Text = "Верно. Соперник отвечает…";
            await Task.Delay(_state.AnimateMoves ? 260 : 80);
            var replyPiece = _session.Board[Move.ParseUci(_session.Puzzle.Solution[_session.Ply]).From];
            var reply = _session.ApplyOpponentMove();
            await _board.AnimateMoveAsync(reply, replyPiece, _session.Board, _state.AnimateMoves);
            if (_session.IsComplete) { await CompletePuzzleAsync(); return; }
            _message.Text = "Ответ соперника сделан автоматически. Теперь найдите следующий лучший ход.";
            _board.InputEnabled = true;
        }
    }

    private async Task RevealSolutionAsync()
    {
        if (_session is null || !await DisplayAlert("Показать решение?", "Ответ будет открыт, но задача не будет отмечена как решённая.", "Показать", "Отмена")) return;
        _state.RecordPuzzleAttempt(_session.Puzzle.Id); _board.InputEnabled = false; _hint.IsVisible = false; _solution.IsVisible = false;
        _message.Text = "Показываем вариант ход за ходом…";
        while (!_session.IsComplete)
        {
            var next = Move.ParseUci(_session.Puzzle.Solution[_session.Ply]);
            var piece = _session.Board[next.From];
            if (_session.HasPendingOpponentMove) _session.ApplyOpponentMove(); else _session.TrySolverMove(next.Uci);
            await _board.AnimateMoveAsync(next, piece, _session.Board, _state.AnimateMoves, 260);
            await Task.Delay(_state.AnimateMoves ? 180 : 30);
        }
        _message.Text = $"{_session.Puzzle.Explanation}\n\nРазбор варианта: {_session.SolutionText}";
        _next.IsVisible = true;
    }

    private async Task CompletePuzzleAsync()
    {
        if (_session is null) return;
        _board.InputEnabled = false; _state.MarkPuzzleSolved(_session.Puzzle.Id, _session.Puzzle.Rating);
        try { await _account.RecordPuzzleResultAsync(_session.Puzzle.Id, _state.GetPuzzleAttempts(_session.Puzzle.Id), _session.Puzzle.Solution); } catch (Exception) { /* Local completion must work offline. */ }
        _message.Text = $"{_session.Puzzle.Explanation}\n\nВариант: {_session.SolutionText}";
        _next.IsVisible = true; _hint.IsVisible = false; _solution.IsVisible = false;
        await DisplayAlert("Задача решена", "Отлично! Все ответы соперника были показаны автоматически.", "Продолжить");
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
        try { HapticFeedback.Default.Perform(type); } catch (Exception) { /* Optional feedback must never interrupt a puzzle. */ }
    }
}
