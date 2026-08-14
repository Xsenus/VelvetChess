using VelvetChess.App.Pages;
using VelvetChess.App.Services;

namespace VelvetChess.App;

public sealed class MainPage : ContentPage
{
    private readonly AppStateService _state = new();
    private readonly Button _play = new();
    private readonly Label _progress = new() { FontSize = 14, TextColor = Color.FromArgb("#9DA7BE") };

    public MainPage()
    {
        BackgroundColor = Color.FromArgb("#0B1020");
        Title = "Шахматы Velvet";
        _play.Clicked += async (_, _) => await Shell.Current.GoToAsync(nameof(GamePage));
        var puzzles = new Button { Text = "Тактические задачи", BackgroundColor = Color.FromArgb("#6E183E"), TextColor = Colors.White };
        puzzles.Clicked += async (_, _) => await Shell.Current.GoToAsync(nameof(PuzzlesPage));
        var settings = new Button { Text = "Настройки и о приложении", HeightRequest = 48, FontSize = 14, BackgroundColor = Color.FromArgb("#202841"), TextColor = Colors.White };
        settings.Clicked += async (_, _) => await Shell.Current.GoToAsync(nameof(SettingsPage));
        Content = new ScrollView { Content = new VerticalStackLayout { Spacing = 18, Children =
        {
            new Border { StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(0,0,28,28) }, Content = new Image { Source = "brand_key_art.png", Aspect = Aspect.AspectFill, HeightRequest = 310 } },
            new VerticalStackLayout { Padding = new Thickness(24,4,24,28), Spacing = 14, Children =
            {
                new Label { Text = "ВАША ПАРТИЯ. ВАШ ТЕМП.", TextColor = Color.FromArgb("#D6AE68"), FontSize = 12, CharacterSpacing = 2.2 },
                new Label { Text = "Красивые шахматы, которые всегда рядом", FontSize = 30, FontFamily = "OpenSansSemibold", LineHeight = 1.05 },
                new Label { Text = "Четыре уровня сложности, честные правила и коллекция задач — полностью офлайн.", FontSize = 15, TextColor = Color.FromArgb("#9DA7BE"), LineHeight = 1.35 },
                _play, puzzles, settings,
                new Border { Margin = new Thickness(0,8,0,0), Padding = 16, BackgroundColor = Color.FromArgb("#151B2E"), StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 }, Content = new VerticalStackLayout { Spacing = 6, Children =
                {
                    new Label { Text = "ВАШ ПРОГРЕСС", FontSize = 12, CharacterSpacing = 1.4, TextColor = Color.FromArgb("#D6AE68") },
                    _progress
                }}},
                new Label { Text = "Онлайн-матчи появятся в следующей большой версии. Архитектура уже готова к подключению сервера.", FontSize = 12, TextColor = Color.FromArgb("#69738A"), Margin = new Thickness(2,6) }
            }}
        }}};
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _play.Text = _state.HasSavedGame ? "Продолжить партию" : "Играть против компьютера";
        _progress.Text = $"Решено задач: {_state.CompletedPuzzleCount}/50   •   Партий: {_state.GamesPlayed}   •   Побед: {_state.Wins}";
    }
}
