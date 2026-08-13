using VelvetChess.App.Services;
using VelvetChess.Core.Puzzles;

namespace VelvetChess.App.Pages;

public sealed class PuzzlesPage : ContentPage
{
    private readonly PuzzleRepository _repository;
    private readonly CollectionView _list = new() { SelectionMode = SelectionMode.Single };

    public PuzzlesPage(PuzzleRepository repository)
    {
        _repository = repository; Title = "Тактическая коллекция";
        _list.ItemTemplate = new DataTemplate(() =>
        {
            var title = new Label { FontSize = 17, FontFamily = "OpenSansSemibold" };
            title.SetBinding(Label.TextProperty, nameof(ChessPuzzle.Title));
            var details = new Label { FontSize = 13, TextColor = Color.FromArgb("#9DA7BE") };
            details.SetBinding(Label.TextProperty, new Binding(nameof(ChessPuzzle.Rating), stringFormat: "Рейтинг {0} · решите без подсказки"));
            return new Border { Margin = new Thickness(16,6), Padding = 16, BackgroundColor = Color.FromArgb("#151B2E"), StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 }, Content = new VerticalStackLayout { Spacing = 5, Children = { title, details } } };
        });
        _list.SelectionChanged += async (_, args) =>
        {
            if (args.CurrentSelection.FirstOrDefault() is ChessPuzzle puzzle)
                await Shell.Current.GoToAsync($"{nameof(PuzzlePlayPage)}?id={Uri.EscapeDataString(puzzle.Id)}");
            _list.SelectedItem = null;
        };
        Content = new Grid { Children = { _list } };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_list.ItemsSource is null) _list.ItemsSource = await _repository.GetAllAsync();
    }
}
