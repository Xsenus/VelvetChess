namespace VelvetChess.Core.AI;

public enum Difficulty { Beginner, Casual, Advanced, Expert }

public sealed record DifficultyProfile(
    Difficulty Level,
    string DisplayName,
    string Description,
    int SearchDepth,
    int TimeLimitMs,
    double Randomness)
{
    public static DifficultyProfile For(Difficulty level) => level switch
    {
        Difficulty.Beginner => new(level, "Новичок", "Играет быстро и иногда зевает фигуры", 1, 120, 0.55),
        Difficulty.Casual => new(level, "Любитель", "Видит простые угрозы на 2 полухода", 2, 350, 0.18),
        Difficulty.Advanced => new(level, "Клубный", "Считает тактику и ценит позицию", 3, 900, 0.04),
        _ => new(level, "Эксперт", "Глубокий поиск с жёстким лимитом времени", 4, 1800, 0)
    };
}
