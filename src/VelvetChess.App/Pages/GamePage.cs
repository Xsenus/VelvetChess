using VelvetChess.App.Controls;
using VelvetChess.App.Services;
using VelvetChess.Core.AI;
using VelvetChess.Core.Game;
using VelvetChess.Core.Model;

namespace VelvetChess.App.Pages;

public sealed class GamePage : ContentPage
{
    private readonly ChessAi _ai;
    private readonly AppStateService _state;
    private readonly ChessBoardView _boardView = new();
    private readonly Label _status = new() { FontSize = 15, HorizontalTextAlignment = TextAlignment.Center };
    private readonly Label _history = new() { FontSize = 12, TextColor = Color.FromArgb("#9DA7BE"), HorizontalTextAlignment = TextAlignment.Center, LineBreakMode = LineBreakMode.TailTruncation };
    private readonly Picker _difficulty = new() { Title = "Сложность", TextColor = Color.FromArgb("#F3E9D6"), TitleColor = Color.FromArgb("#9DA7BE") };
    private readonly Button _undo = new() { Text = "Отменить ход", HeightRequest = 48, FontSize = 13, BackgroundColor = Color.FromArgb("#202841"), TextColor = Colors.White };
    private LocalGameSession _session;
    private CancellationTokenSource? _thinking;
    private bool _outcomeRecorded;

    public GamePage(ChessAi ai, AppStateService state)
    {
        _ai = ai; _state = state; _session = state.LoadGame(); Title = "Локальная партия";
        foreach (var level in Enum.GetValues<Difficulty>()) _difficulty.Items.Add(DifficultyProfile.For(level).DisplayName);
        _difficulty.SelectedIndex = (int)state.Difficulty;
        _difficulty.SelectedIndexChanged += (_, _) => { if (_difficulty.SelectedIndex >= 0) _state.Difficulty = (Difficulty)_difficulty.SelectedIndex; };
        _boardView.SetBoard(_session.Board); _boardView.MoveRequested += OnMoveRequested;
        var restart = new Button { Text = "Новая партия", HeightRequest = 48, FontSize = 13 };
        restart.Clicked += async (_, _) => await ConfirmNewGameAsync();
        _undo.Clicked += (_, _) => UndoTurn();
        var flip = new Button { Text = "↻", HeightRequest = 48, WidthRequest = 54, FontSize = 22, BackgroundColor = Color.FromArgb("#202841"), TextColor = Colors.White };
        SemanticProperties.SetDescription(flip, "Перевернуть доску");
        flip.Clicked += (_, _) => _boardView.Flipped = !_boardView.Flipped;

        var grid = new Grid { Padding = new Thickness(16,14,16,24), RowSpacing = 10 };
        foreach (var height in new[] { GridLength.Auto, GridLength.Star, GridLength.Auto, GridLength.Auto, GridLength.Auto }) grid.RowDefinitions.Add(new RowDefinition(height));
        var pickerCard = new Border { Padding = new Thickness(16,8), BackgroundColor = Color.FromArgb("#151B2E"), StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 }, Content = _difficulty };
        var boardCard = new Border { StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 }, Content = _boardView };
        grid.Add(pickerCard, 0, 0); grid.Add(boardCard, 0, 1); grid.Add(_status, 0, 2); grid.Add(_history, 0, 3);
        grid.Add(new HorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.Center, Children = { restart, _undo, flip } }, 0, 4);
        Content = grid; Refresh();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_session.Board.SideToMove == PieceColor.Black && !_session.Board.GetStatus().IsFinished) _ = RunAiAsync();
    }

    protected override void OnDisappearing()
    {
        _thinking?.Cancel();
        base.OnDisappearing();
    }

    private async void OnMoveRequested(object? sender, Move move)
    {
        if (move.Promotion != PieceType.None)
        {
            var selected = await DisplayActionSheet("Превратить пешку", "Отмена", null, "Ферзь", "Ладья", "Слон", "Конь");
            var promotion = selected switch { "Ладья" => PieceType.Rook, "Слон" => PieceType.Bishop, "Конь" => PieceType.Knight, "Ферзь" => PieceType.Queen, _ => PieceType.None };
            if (promotion == PieceType.None) return;
            move = move with { Promotion = promotion };
        }
        if (!_session.TryMove(move, out _)) return;
        _state.SaveGame(_session); Refresh();
        if (_session.Board.GetStatus().IsFinished) { FinishGame(); return; }
        await RunAiAsync();
    }

    private async Task RunAiAsync()
    {
        if (_thinking is not null || _session.Board.SideToMove != PieceColor.Black) return;
        _boardView.InputEnabled = false; _status.Text = "Компьютер думает…"; _undo.IsEnabled = false;
        using var cancellation = new CancellationTokenSource(); _thinking = cancellation;
        try
        {
            var reply = await _ai.FindMoveAsync(_session.Board.Clone(), _state.Difficulty, cancellation.Token);
            if (reply.HasValue && !cancellation.IsCancellationRequested) _session.TryMove(reply.Value, out _);
            _state.SaveGame(_session);
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (ReferenceEquals(_thinking, cancellation)) _thinking = null;
            _boardView.InputEnabled = true; Refresh();
            if (_session.Board.GetStatus().IsFinished) FinishGame();
        }
    }

    private async Task ConfirmNewGameAsync()
    {
        if (_session.History.Count > 0 && !_session.Board.GetStatus().IsFinished &&
            !await DisplayAlert("Новая партия", "Текущая партия будет заменена. Начать заново?", "Начать", "Отмена")) return;
        _thinking?.Cancel(); _session.NewGame(); _outcomeRecorded = false; _state.ClearGame(); _boardView.InputEnabled = true; Refresh();
    }

    private void UndoTurn()
    {
        _thinking?.Cancel();
        if (!_session.UndoPlayerTurn()) return;
        _outcomeRecorded = false; _state.SaveGame(_session); _boardView.InputEnabled = true; Refresh();
    }

    private void FinishGame()
    {
        if (_outcomeRecorded) return;
        _outcomeRecorded = true; _state.RecordFinishedGame(_session.Board.GetStatus()); Refresh();
    }

    private void Refresh()
    {
        _boardView.SetBoard(_session.Board);
        var status = _session.Board.GetStatus();
        _status.Text = status.Outcome switch
        {
            GameOutcome.Checkmate => status.Winner == PieceColor.White ? "Мат. Вы победили!" : "Мат. Победил компьютер.",
            GameOutcome.Stalemate => "Пат — ничья", GameOutcome.DrawFiftyMove => "Ничья по правилу 50 ходов",
            GameOutcome.DrawThreefoldRepetition => "Ничья: троекратное повторение", GameOutcome.DrawInsufficientMaterial => "Ничья: недостаточно материала",
            _ when _session.Board.IsInCheck(_session.Board.SideToMove) => "Шах!", _ => _session.Board.SideToMove == PieceColor.White ? "Ваш ход" : "Ход компьютера"
        };
        _history.Text = FormatRecentHistory();
        _undo.IsEnabled = _session.CanUndo && _thinking is null && !status.IsFinished;
    }

    private string FormatRecentHistory()
    {
        if (_session.History.Count == 0) return "История ходов появится здесь";
        return string.Join("   ", _session.History.TakeLast(8).Select(move => move.Color == PieceColor.White ? $"{move.MoveNumber}. {move.San}" : move.San));
    }
}
