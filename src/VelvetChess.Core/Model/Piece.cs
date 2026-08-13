namespace VelvetChess.Core.Model;

public enum PieceType : byte { None, Pawn, Knight, Bishop, Rook, Queen, King }
public enum PieceColor : byte { White, Black }

public readonly record struct Piece(PieceType Type, PieceColor Color)
{
    public static readonly Piece None = new(PieceType.None, PieceColor.White);
    public bool IsNone => Type == PieceType.None;

    public char ToFen() => (Type, Color) switch
    {
        (PieceType.Pawn, PieceColor.White) => 'P', (PieceType.Knight, PieceColor.White) => 'N',
        (PieceType.Bishop, PieceColor.White) => 'B', (PieceType.Rook, PieceColor.White) => 'R',
        (PieceType.Queen, PieceColor.White) => 'Q', (PieceType.King, PieceColor.White) => 'K',
        (PieceType.Pawn, PieceColor.Black) => 'p', (PieceType.Knight, PieceColor.Black) => 'n',
        (PieceType.Bishop, PieceColor.Black) => 'b', (PieceType.Rook, PieceColor.Black) => 'r',
        (PieceType.Queen, PieceColor.Black) => 'q', (PieceType.King, PieceColor.Black) => 'k',
        _ => ' '
    };

    public static Piece FromFen(char value)
    {
        var color = char.IsUpper(value) ? PieceColor.White : PieceColor.Black;
        var type = char.ToLowerInvariant(value) switch
        {
            'p' => PieceType.Pawn, 'n' => PieceType.Knight, 'b' => PieceType.Bishop,
            'r' => PieceType.Rook, 'q' => PieceType.Queen, 'k' => PieceType.King,
            _ => PieceType.None
        };
        return new(type, color);
    }
}

public static class ChessSquare
{
    public static int FromName(string name)
    {
        if (name.Length != 2 || name[0] is < 'a' or > 'h' || name[1] is < '1' or > '8')
            throw new FormatException($"Invalid square: {name}");
        return name[0] - 'a' + (name[1] - '1') * 8;
    }

    public static string Name(int square) => $"{(char)('a' + square % 8)}{(char)('1' + square / 8)}";
    public static bool IsValid(int file, int rank) => file is >= 0 and < 8 && rank is >= 0 and < 8;
}
