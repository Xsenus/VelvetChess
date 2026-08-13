using Microsoft.Extensions.Logging;
using VelvetChess.App.Services;
using VelvetChess.Core.AI;

namespace VelvetChess.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>().ConfigureFonts(fonts =>
        {
            fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
        });
        builder.Services.AddSingleton<ChessAi>();
        builder.Services.AddSingleton<PuzzleRepository>();
#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
