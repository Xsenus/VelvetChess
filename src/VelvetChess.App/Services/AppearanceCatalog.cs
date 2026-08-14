using VelvetChess.Core.Model;

namespace VelvetChess.App.Services;

public static class AppearanceCatalog
{
    public static IReadOnlyList<string> PieceNames { get; } = ["Турнир", "Классика", "Силуэт", "Королевский", "Минимализм"];
    public static IReadOnlyList<string> BoardNames { get; } = ["Velvet", "Орех", "Лес", "Океан", "Графит"];

    public static string PieceDescription(PieceTheme theme) => theme switch
    {
        PieceTheme.Tournament => "Единые чёткие силуэты с контрастной окантовкой",
        PieceTheme.Classic => "Традиционные контурные белые и цельные чёрные фигуры",
        PieceTheme.Silhouette => "Спокойные цельные формы без лишних деталей",
        PieceTheme.Royal => "Медальоны с золотыми акцентами",
        PieceTheme.Minimal => "Лаконичные фишки с буквенными обозначениями",
        _ => ""
    };
}
