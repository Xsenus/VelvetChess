using VelvetChess.App.Pages;

namespace VelvetChess.App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(GamePage), typeof(GamePage));
        Routing.RegisterRoute(nameof(PuzzlesPage), typeof(PuzzlesPage));
        Routing.RegisterRoute(nameof(PuzzlePlayPage), typeof(PuzzlePlayPage));
        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
        Routing.RegisterRoute(nameof(ProfilePage), typeof(ProfilePage));
    }
}
