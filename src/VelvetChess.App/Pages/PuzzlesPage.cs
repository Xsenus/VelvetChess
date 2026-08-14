using VelvetChess.App.Services;
using VelvetChess.Core.Puzzles;

namespace VelvetChess.App.Pages;

public sealed class PuzzlesPage : ContentPage
{
    private readonly PuzzleRepository _repository;
    private readonly AppStateService _state;
    private readonly CollectionView _list = new() { SelectionMode = SelectionMode.Single };
    private readonly Label _progress = new() { FontSize = 14, TextColor = Color.FromArgb("#D6AE68"), Margin = new Thickness(18,10,18,4) };
    private readonly ProgressBar _progressBar = new() { ProgressColor = Color.FromArgb("#D6AE68"), BackgroundColor = Color.FromArgb("#202841"), HeightRequest = 6 };
    private IReadOnlyList<ChessPuzzle> _puzzles = [];
    private bool _onlyUnsolved;
    private Button? _allFilter;
    private Button? _unsolvedFilter;

    public PuzzlesPage(PuzzleRepository repository, AppStateService state)
    {
        BackgroundColor = Color.FromArgb("#0B1020");
        _repository = repository; _state = state; Title = "Тактическая коллекция";
        _list.ItemTemplate = new DataTemplate(() =>
        {
            var title = new Label { FontSize = 17, FontFamily = "OpenSansSemibold" };
            title.SetBinding(Label.TextProperty, nameof(PuzzleListItem.DisplayTitle));
            var details = new Label { FontSize = 13, TextColor = Color.FromArgb("#9DA7BE") };
            details.SetBinding(Label.TextProperty, nameof(PuzzleListItem.Details));
            var arrow = new Label { Text = "›", FontSize = 28, TextColor = Color.FromArgb("#69738A"), VerticalTextAlignment = TextAlignment.Center };
            var text = new VerticalStackLayout { Spacing = 5, Children = { title, details } };
            var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
            row.Add(text, 0, 0); row.Add(arrow, 1, 0);
            return new Border { Margin = new Thickness(16,6), Padding = 16, BackgroundColor = Color.FromArgb("#151B2E"), StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 }, Content = row };
        });
        _list.SelectionChanged += async (_, args) =>
        {
            if (args.CurrentSelection.FirstOrDefault() is PuzzleListItem item)
                await Shell.Current.GoToAsync($"{nameof(PuzzlePlayPage)}?id={Uri.EscapeDataString(item.Puzzle.Id)}");
            _list.SelectedItem = null;
        };
        _allFilter = FilterButton("Все", false); _unsolvedFilter = FilterButton("Нерешённые", true); UpdateFilterStyles();
        var filters = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(16,6,16,8), Children = { _allFilter, _unsolvedFilter } };
        var header = new VerticalStackLayout { Spacing = 7, Children = { _progress, new Border { Margin = new Thickness(18,0), Content = _progressBar, StrokeThickness = 0 }, filters } };
        var layout = new Grid(); layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); layout.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        layout.Add(header, 0, 0); layout.Add(_list, 0, 1); Content = layout;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _puzzles = await _repository.GetAllAsync(); RefreshList();
    }

    private Button FilterButton(string text, bool unsolved)
    {
        var button = new Button { Text = text, HeightRequest = 40, Padding = new Thickness(16,4), FontSize = 13, BackgroundColor = Color.FromArgb("#202841"), TextColor = Color.FromArgb("#F3E9D6") };
        button.Clicked += (_, _) => { _onlyUnsolved = unsolved; UpdateFilterStyles(); RefreshList(); };
        return button;
    }

    private void UpdateFilterStyles()
    {
        if (_allFilter is null || _unsolvedFilter is null) return;
        _allFilter.BackgroundColor = Color.FromArgb(!_onlyUnsolved ? "#D6AE68" : "#202841");
        _allFilter.TextColor = Color.FromArgb(!_onlyUnsolved ? "#101522" : "#F3E9D6");
        _unsolvedFilter.BackgroundColor = Color.FromArgb(_onlyUnsolved ? "#D6AE68" : "#202841");
        _unsolvedFilter.TextColor = Color.FromArgb(_onlyUnsolved ? "#101522" : "#F3E9D6");
    }

    private void RefreshList()
    {
        var completed = _state.CompletedPuzzles;
        _progress.Text = $"Решено {completed.Count} из {_puzzles.Count}  ·  Рейтинг {_state.PuzzleRating}";
        _progressBar.Progress = _puzzles.Count == 0 ? 0 : completed.Count / (double)_puzzles.Count;
        var visible = _onlyUnsolved ? _puzzles.Where(puzzle => !completed.Contains(puzzle.Id)) : _puzzles;
        _list.ItemsSource = visible.Select(puzzle => new PuzzleListItem(
            puzzle,
            completed.Contains(puzzle.Id) ? $"✓  {puzzle.Title}" : puzzle.Title,
            $"Рейтинг {puzzle.Rating} · {puzzle.Theme}{(_state.GetPuzzleAttempts(puzzle.Id) > 0 ? $" · попыток: {_state.GetPuzzleAttempts(puzzle.Id)}" : "")}"));
    }

    private sealed record PuzzleListItem(ChessPuzzle Puzzle, string DisplayTitle, string Details);
}
