using VelvetChess.App.Pages;

namespace VelvetChess.App;

public sealed class MainPage : ContentPage
{
    public MainPage()
    {
        Title = "Шахматы Velvet";
        var play = new Button { Text = "Играть против компьютера" };
        play.Clicked += async (_, _) => await Shell.Current.GoToAsync(nameof(GamePage));
        var puzzles = new Button { Text = "50 тактических задач", BackgroundColor = Color.FromArgb("#6E183E"), TextColor = Colors.White };
        puzzles.Clicked += async (_, _) => await Shell.Current.GoToAsync(nameof(PuzzlesPage));
        Content = new ScrollView { Content = new VerticalStackLayout { Spacing = 18, Children =
        {
            new Border { StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(0,0,28,28) }, Content = new Image { Source = "brand_key_art.png", Aspect = Aspect.AspectFill, HeightRequest = 310 } },
            new VerticalStackLayout { Padding = new Thickness(24,4,24,28), Spacing = 14, Children =
            {
                new Label { Text = "ВАША ПАРТИЯ. ВАШ ТЕМП.", TextColor = Color.FromArgb("#D6AE68"), FontSize = 12, CharacterSpacing = 2.2 },
                new Label { Text = "Красивые шахматы, которые всегда рядом", FontSize = 30, FontFamily = "OpenSansSemibold", LineHeight = 1.05 },
                new Label { Text = "Четыре уровня сложности, честные правила и коллекция задач — полностью офлайн.", FontSize = 15, TextColor = Color.FromArgb("#9DA7BE"), LineHeight = 1.35 },
                play, puzzles,
                new Border { Margin = new Thickness(0,8,0,0), Padding = 16, BackgroundColor = Color.FromArgb("#151B2E"), StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 }, Content = new Label { Text = "Онлайн-матчи появятся в следующей большой версии. Архитектура уже готова к подключению сервера.", FontSize = 13, TextColor = Color.FromArgb("#9DA7BE") } }
            }}
        }}};
    }
}
