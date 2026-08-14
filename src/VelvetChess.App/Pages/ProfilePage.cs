using VelvetChess.App.Services;
using VelvetChess.Core.Online;

namespace VelvetChess.App.Pages;

public sealed class ProfilePage : ContentPage
{
    private readonly AppStateService _state;
    private readonly PlayerAccountService _account;
    private readonly Label _subtitle = new() { TextColor = Color.FromArgb("#9DA7BE"), FontSize = 14 };
    private readonly Grid _ratings = new() { ColumnSpacing = 10, RowSpacing = 10 };
    private readonly Grid _stats = new() { ColumnSpacing = 10, RowSpacing = 10 };

    public ProfilePage(AppStateService state, PlayerAccountService account)
    {
        _state = state; _account = account; Title = "Профиль"; BackgroundColor = Color.FromArgb("#0B1020");
        var avatar = new Border
        {
            WidthRequest = 72, HeightRequest = 72, BackgroundColor = Color.FromArgb("#D6AE68"), StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 36 },
            Content = new Label { Text = "♚", FontFamily = "ChessPieces", FontSize = 40, TextColor = Color.FromArgb("#111629"), HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center }
        };
        var identity = new VerticalStackLayout { Spacing = 3, VerticalOptions = LayoutOptions.Center, Children =
        {
            new Label { Text = "Гостевой профиль", FontSize = 23, FontFamily = "OpenSansSemibold" }, _subtitle
        }};
        var header = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 16 };
        header.Add(avatar, 0, 0); header.Add(identity, 1, 0);

        ConfigureMetricGrid(_ratings, 3);
        ConfigureMetricGrid(_stats, 2);
        var yandex = ProviderButton("Войти с Яндекс ID", "Я", Color.FromArgb("#FC3F1D"), IdentityProvider.Yandex);
        var vk = ProviderButton("Войти с VK ID", "VK", Color.FromArgb("#0077FF"), IdentityProvider.Vk);

        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 20, Spacing = 16, Children =
        {
            Card(header, 18),
            Section("РЕЙТИНГ"), _ratings,
            new Label { Text = "Локальные показатели хранятся на этом устройстве. Онлайн-рейтинг появится после входа и подключения сервера.", FontSize = 12, TextColor = Color.FromArgb("#9DA7BE"), LineHeight = 1.3 },
            Section("СТАТИСТИКА"), _stats,
            Section("СИНХРОНИЗАЦИЯ"),
            Card(new VerticalStackLayout { Spacing = 11, Children =
            {
                new Label { Text = "Играйте без регистрации или войдите, чтобы в будущем перенести профиль в веб-версию и участвовать в общем рейтинге.", FontSize = 14, TextColor = Color.FromArgb("#C7CDDC"), LineHeight = 1.35 },
                yandex, vk,
                new Label { Text = "OAuth-кнопки подготовлены, но станут активны после регистрации приложений Яндекс/VK и запуска серверного обмена токенов.", FontSize = 11, TextColor = Color.FromArgb("#69738A") }
            }}, 16)
        }}};
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _subtitle.Text = _account.Current.IsGuest ? "Прогресс доступен только на этом устройстве" : "Прогресс синхронизируется";
        _ratings.Children.Clear();
        _ratings.Add(Metric("Локальная игра", _state.LocalRating.ToString(), $"Лучший: {_state.BestLocalRating}"), 0, 0);
        _ratings.Add(Metric("Тактика", _state.PuzzleRating.ToString(), $"Решено: {_state.CompletedPuzzleCount}"), 1, 0);
        _ratings.Add(Metric("Онлайн", "—", "Требуется вход"), 2, 0);
        _stats.Children.Clear();
        _stats.Add(Metric("Партий", _state.GamesPlayed.ToString(), $"Побед: {_state.Wins}"), 0, 0);
        _stats.Add(Metric("Ничьи / поражения", $"{_state.Draws} / {_state.Losses}", "Локальные партии"), 1, 0);
        _stats.Add(Metric("Задач", $"{_state.CompletedPuzzleCount}/50", $"Попыток: {_state.TotalPuzzleAttempts}"), 0, 1);
        _stats.Add(Metric("Результативность", WinRate(), "По завершённым партиям"), 1, 1);
    }

    private string WinRate() => _state.GamesPlayed == 0 ? "—" : $"{Math.Round(_state.Wins * 100d / _state.GamesPlayed)}%";

    private Button ProviderButton(string text, string icon, Color color, IdentityProvider provider)
    {
        var button = new Button { Text = $"{icon}   {text}", BackgroundColor = color, TextColor = Colors.White, HeightRequest = 52 };
        button.Clicked += async (_, _) =>
        {
            try { await _account.SignInAsync(provider); }
            catch (InvalidOperationException exception) { await DisplayAlert("Подключение аккаунта", exception.Message, "Понятно"); }
        };
        return button;
    }

    private static void ConfigureMetricGrid(Grid grid, int columns)
    {
        for (var i = 0; i < columns; i++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
    }

    private static Border Metric(string title, string value, string detail) => Card(new VerticalStackLayout { Spacing = 4, Children =
    {
        new Label { Text = title, FontSize = 12, TextColor = Color.FromArgb("#9DA7BE") },
        new Label { Text = value, FontSize = 24, FontFamily = "OpenSansSemibold", TextColor = Color.FromArgb("#F3E9D6") },
        new Label { Text = detail, FontSize = 10, TextColor = Color.FromArgb("#69738A") }
    }}, 14);

    private static Label Section(string text) => new() { Text = text, FontSize = 12, CharacterSpacing = 1.5, TextColor = Color.FromArgb("#D6AE68"), Margin = new Thickness(2,8,0,0) };
    private static Border Card(View content, double padding) => new() { Padding = padding, BackgroundColor = Color.FromArgb("#151B2E"), StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 }, Content = content };
}
