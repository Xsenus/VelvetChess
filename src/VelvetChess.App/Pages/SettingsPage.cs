using VelvetChess.App.Services;

namespace VelvetChess.App.Pages;

public sealed class SettingsPage : ContentPage
{
    private readonly AppStateService _state;

    public SettingsPage(AppStateService state)
    {
        _state = state; Title = "Настройки";
        var coordinates = SettingSwitch("Координаты доски", "Показывать буквы и цифры у полей", state.ShowCoordinates, value => state.ShowCoordinates = value);
        var haptics = SettingSwitch("Тактильный отклик", "Короткий отклик после хода", state.HapticsEnabled, value => state.HapticsEnabled = value);
        var confirmation = SettingSwitch("Подтверждать новую партию", "Защита от случайной потери текущей позиции", state.ConfirmNewGame, value => state.ConfirmNewGame = value);
        var reset = new Button { Text = "Сбросить прогресс", BackgroundColor = Color.FromArgb("#6E183E"), TextColor = Colors.White };
        reset.Clicked += async (_, _) =>
        {
            if (!await DisplayAlert("Сбросить прогресс?", "Будут удалены сохранённая партия, статистика и отметки решённых задач. Настройки останутся.", "Сбросить", "Отмена")) return;
            _state.ResetProgress(); await DisplayAlert("Готово", "Локальный прогресс удалён.", "OK");
        };
        var privacy = new Button { Text = "Политика конфиденциальности", BackgroundColor = Color.FromArgb("#202841"), TextColor = Colors.White, IsVisible = ReleaseOwnerInfo.IsConfigured };
        privacy.Clicked += async (_, _) => await OpenExternalAsync(ReleaseOwnerInfo.PrivacyPolicyUrl);
        var support = new Button { Text = "Связаться с поддержкой", BackgroundColor = Color.FromArgb("#202841"), TextColor = Colors.White, IsVisible = ReleaseOwnerInfo.IsConfigured };
        support.Clicked += async (_, _) => await OpenExternalAsync($"mailto:{ReleaseOwnerInfo.SupportEmail}");
        var version = AppInfo.Current.VersionString;
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 20, Spacing = 14, Children =
        {
            new Label { Text = "Игра", FontSize = 25, FontFamily = "OpenSansSemibold" },
            coordinates, haptics, confirmation,
            new Label { Text = "Данные", FontSize = 25, FontFamily = "OpenSansSemibold", Margin = new Thickness(0,12,0,0) },
            new Border { Padding = 16, BackgroundColor = Color.FromArgb("#151B2E"), StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 }, Content = new Label { Text = "Версия 1.0 работает офлайн. Персональные данные, реклама и аналитические идентификаторы не собираются. Партии и прогресс хранятся только на этом устройстве.", TextColor = Color.FromArgb("#9DA7BE"), LineHeight = 1.35 } },
            new Label { Text = ReleaseOwnerInfo.IsConfigured ? $"Разработчик: {ReleaseOwnerInfo.DeveloperName}" : "", IsVisible = ReleaseOwnerInfo.IsConfigured, TextColor = Color.FromArgb("#9DA7BE"), HorizontalTextAlignment = TextAlignment.Center },
            privacy, support,
            reset,
            new Label { Text = $"Шахматы Velvet · версия {version}\nШахматные задачи: Lichess CC0\nФигуры: Noto Sans Symbols 2 (OFL 1.1)", FontSize = 12, TextColor = Color.FromArgb("#69738A"), HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(0,18) }
        }}};
    }

    private async Task OpenExternalAsync(string uri)
    {
        try { await Launcher.Default.OpenAsync(uri); }
        catch (Exception) { await DisplayAlert("Не удалось открыть ссылку", "Проверьте, что на устройстве доступно подходящее приложение.", "OK"); }
    }

    private static Border SettingSwitch(string title, string description, bool value, Action<bool> changed)
    {
        var toggle = new Switch { IsToggled = value, OnColor = Color.FromArgb("#D6AE68"), VerticalOptions = LayoutOptions.Center };
        toggle.Toggled += (_, args) => changed(args.Value);
        var text = new VerticalStackLayout { Spacing = 3, Children =
        {
            new Label { Text = title, FontSize = 16, FontFamily = "OpenSansSemibold" },
            new Label { Text = description, FontSize = 12, TextColor = Color.FromArgb("#9DA7BE") }
        }};
        var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
        grid.Add(text, 0, 0); grid.Add(toggle, 1, 0);
        return new Border { Padding = 16, BackgroundColor = Color.FromArgb("#151B2E"), StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 }, Content = grid };
    }
}
