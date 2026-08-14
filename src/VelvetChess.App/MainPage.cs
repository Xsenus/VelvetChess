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
        var profile = new Button { Text = "Профиль и рейтинг", HeightRequest = 48, FontSize = 14, BackgroundColor = Color.FromArgb("#202841"), TextColor = Colors.White };
        profile.Clicked += async (_, _) => await Shell.Current.GoToAsync(nameof(ProfilePage));
        var secondaryActions = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 10 };
        secondaryActions.Add(settings, 0, 0); secondaryActions.Add(profile, 1, 0);
        Content = new ScrollView { Content = new VerticalStackLayout { Spacing = 12, Children =
        {
            new Border { StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(0,0,24,24) }, Content = new Image { Source = "brand_key_art.png", Aspect = Aspect.AspectFill, HeightRequest = 205 } },
            new VerticalStackLayout { Padding = new Thickness(24,2,24,24), Spacing = 11, Children =
            {
                new Label { Text = "ВАША ПАРТИЯ. ВАШ ТЕМП.", TextColor = Color.FromArgb("#D6AE68"), FontSize = 12, CharacterSpacing = 2.2 },
                new Label { Text = "Шахматы в вашем темпе", FontSize = 28, FontFamily = "OpenSansSemibold", LineHeight = 1.05 },
                new Label { Text = "Играйте с компьютером, тренируйте тактику и отслеживайте прогресс — полностью офлайн.", FontSize = 14, TextColor = Color.FromArgb("#9DA7BE"), LineHeight = 1.3 },
                _play, puzzles,
                secondaryActions,
                new Border { Margin = new Thickness(0,8,0,0), Padding = 16, BackgroundColor = Color.FromArgb("#151B2E"), StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 }, Content = new VerticalStackLayout { Spacing = 6, Children =
                {
                    new Label { Text = "ВАШ ПРОГРЕСС", FontSize = 12, CharacterSpacing = 1.4, TextColor = Color.FromArgb("#D6AE68") },
                    _progress
                }}},
                new Label { Text = "Весь прогресс сохраняется локально на этом устройстве.", FontSize = 12, TextColor = Color.FromArgb("#69738A"), Margin = new Thickness(2,6) }
            }}
        }}};
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _play.Text = _state.HasSavedGame ? "Продолжить партию" : "Играть против компьютера";
        _progress.Text = $"Рейтинг: {_state.LocalRating}   •   Задачи: {_state.CompletedPuzzleCount}/50   •   Победы: {_state.Wins}/{_state.GamesPlayed}";
    }
}
