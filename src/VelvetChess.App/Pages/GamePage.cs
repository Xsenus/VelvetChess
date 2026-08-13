using VelvetChess.App.Controls;
using VelvetChess.Core.AI;
using VelvetChess.Core.Model;

namespace VelvetChess.App.Pages;

public sealed class GamePage : ContentPage
{
    private readonly ChessAi _ai;
    private readonly ChessBoardView _boardView = new();
    private readonly Label _status = new() { FontSize = 15, HorizontalTextAlignment = TextAlignment.Center };
    private readonly Picker _difficulty = new() { Title = "Сложность", TextColor = Color.FromArgb("#F3E9D6"), TitleColor = Color.FromArgb("#9DA7BE") };
    private ChessBoard _board = new();
    private CancellationTokenSource? _thinking;

    public GamePage(ChessAi ai)
    {
        _ai = ai; Title = "Локальная партия";
        foreach (var level in Enum.GetValues<Difficulty>()) _difficulty.Items.Add(DifficultyProfile.For(level).DisplayName);
        _difficulty.SelectedIndex = 1; _boardView.MoveRequested += OnMoveRequested;
        var restart = new Button { Text = "Новая партия", HeightRequest = 48, FontSize = 14 };
        restart.Clicked += (_, _) => NewGame();
        var flip = new Button { Text = "↻", HeightRequest = 48, WidthRequest = 54, FontSize = 22, BackgroundColor = Color.FromArgb("#202841"), TextColor = Colors.White };
        flip.Clicked += (_, _) => _boardView.Flipped = !_boardView.Flipped;
        var grid = new Grid { Padding = new Thickness(16,14,16,24), RowSpacing = 14 };
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); grid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        var pickerCard = new Border { Padding = new Thickness(16,8), BackgroundColor = Color.FromArgb("#151B2E"), StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 }, Content = _difficulty };
        var boardCard = new Border { StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 }, Content = _boardView };
        grid.Add(pickerCard, 0, 0); grid.Add(boardCard, 0, 1); grid.Add(_status, 0, 2);
        grid.Add(new HorizontalStackLayout { Spacing = 10, HorizontalOptions = LayoutOptions.Center, Children = { restart, flip } }, 0, 3);
        Content = grid; UpdateStatus();
    }

    private async void OnMoveRequested(object? sender, Move move)
    {
        if (!_board.TryMove(move, out _)) return;
        _boardView.SetBoard(_board); UpdateStatus();
        if (_board.GetStatus().IsFinished) return;
        _boardView.InputEnabled = false; _status.Text = "Компьютер думает…";
        _thinking = new CancellationTokenSource();
        try
        {
            var difficulty = (Difficulty)Math.Max(0, _difficulty.SelectedIndex);
            var reply = await _ai.FindMoveAsync(_board.Clone(), difficulty, _thinking.Token);
            if (reply.HasValue) _board.ApplyLegalMove(reply.Value);
        }
        catch (OperationCanceledException) { }
        finally { _boardView.InputEnabled = true; _boardView.SetBoard(_board); UpdateStatus(); }
    }

    private void NewGame() { _thinking?.Cancel(); _board = new ChessBoard(); _boardView.SetBoard(_board); _boardView.InputEnabled = true; UpdateStatus(); }
    private void UpdateStatus()
    {
        var status = _board.GetStatus();
        _status.Text = status.Outcome switch
        {
            GameOutcome.Checkmate => status.Winner == PieceColor.White ? "Мат. Вы победили!" : "Мат. Победил компьютер.",
            GameOutcome.Stalemate => "Пат — ничья", GameOutcome.DrawFiftyMove or GameOutcome.DrawThreefoldRepetition or GameOutcome.DrawInsufficientMaterial => "Ничья",
            _ when _board.IsInCheck(_board.SideToMove) => "Шах!", _ => _board.SideToMove == PieceColor.White ? "Ваш ход" : "Ход компьютера"
        };
    }
}
