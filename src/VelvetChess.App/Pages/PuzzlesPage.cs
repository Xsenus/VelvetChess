using VelvetChess.App.Services;
using VelvetChess.Core.Puzzles;

namespace VelvetChess.App.Pages;

public sealed class PuzzlesPage : ContentPage
{
    private readonly PuzzleRepository _repository;
    private readonly AppStateService _state;
    private readonly CollectionView _list = new() { SelectionMode = SelectionMode.Single };
    private readonly Label _progress = new() { FontSize = 14, TextColor = Color.FromArgb("#D6AE68"), Margin = new Thickness(18,10,18,4) };

    public PuzzlesPage(PuzzleRepository repository, AppStateService state)
    {
        _repository = repository; _state = state; Title = "Тактическая коллекция";
        _list.ItemTemplate = new DataTemplate(() =>
        {
            var title = new Label { FontSize = 17, FontFamily = "OpenSansSemibold" };
            title.SetBinding(Label.TextProperty, nameof(PuzzleListItem.DisplayTitle));
            var details = new Label { FontSize = 13, TextColor = Color.FromArgb("#9DA7BE") };
            details.SetBinding(Label.TextProperty, nameof(PuzzleListItem.Details));
            return new Border { Margin = new Thickness(16,6), Padding = 16, BackgroundColor = Color.FromArgb("#151B2E"), StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 }, Content = new VerticalStackLayout { Spacing = 5, Children = { title, details } } };
        });
        _list.SelectionChanged += async (_, args) =>
        {
            if (args.CurrentSelection.FirstOrDefault() is PuzzleListItem item)
                await Shell.Current.GoToAsync($"{nameof(PuzzlePlayPage)}?id={Uri.EscapeDataString(item.Puzzle.Id)}");
            _list.SelectedItem = null;
        };
        var layout = new Grid(); layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); layout.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        layout.Add(_progress, 0, 0); layout.Add(_list, 0, 1); Content = layout;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var puzzles = await _repository.GetAllAsync(); var completed = _state.CompletedPuzzles;
        _progress.Text = $"Решено {completed.Count} из {puzzles.Count}";
        _list.ItemsSource = puzzles.Select(puzzle => new PuzzleListItem(
            puzzle,
            completed.Contains(puzzle.Id) ? $"✓  {puzzle.Title}" : puzzle.Title,
            $"Рейтинг {puzzle.Rating} · {puzzle.Theme}{(_state.GetPuzzleAttempts(puzzle.Id) > 0 ? $" · попыток: {_state.GetPuzzleAttempts(puzzle.Id)}" : "")}"));
    }

    private sealed record PuzzleListItem(ChessPuzzle Puzzle, string DisplayTitle, string Details);
}
