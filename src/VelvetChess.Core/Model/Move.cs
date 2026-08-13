namespace VelvetChess.Core.Model;

[Flags]
public enum MoveFlags : byte
{
    None = 0, Capture = 1, EnPassant = 2, Castle = 4, DoublePawn = 8, Promotion = 16
}

public readonly record struct Move(int From, int To, PieceType Promotion = PieceType.None, MoveFlags Flags = MoveFlags.None)
{
    public string Uci => ChessSquare.Name(From) + ChessSquare.Name(To) + (Promotion switch
    {
        PieceType.Queen => "q", PieceType.Rook => "r", PieceType.Bishop => "b", PieceType.Knight => "n", _ => ""
    });

    public static Move ParseUci(string uci)
    {
        if (uci.Length is < 4 or > 5) throw new FormatException($"Invalid UCI move: {uci}");
        var promotion = uci.Length == 5 ? char.ToLowerInvariant(uci[4]) switch
        {
            'q' => PieceType.Queen, 'r' => PieceType.Rook, 'b' => PieceType.Bishop,
            'n' => PieceType.Knight, _ => throw new FormatException($"Invalid promotion: {uci}")
        } : PieceType.None;
        return new(ChessSquare.FromName(uci[..2]), ChessSquare.FromName(uci.Substring(2, 2)), promotion);
    }

    public override string ToString() => Uci;
}

public enum GameOutcome { Ongoing, Checkmate, Stalemate, DrawFiftyMove, DrawThreefoldRepetition, DrawInsufficientMaterial }

public readonly record struct GameStatus(GameOutcome Outcome, PieceColor? Winner = null)
{
    public bool IsFinished => Outcome != GameOutcome.Ongoing;
}
