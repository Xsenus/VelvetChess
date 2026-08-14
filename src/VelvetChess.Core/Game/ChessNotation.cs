using System.Text;
using VelvetChess.Core.Model;

namespace VelvetChess.Core.Game;

public static class ChessNotation
{
    public static string ToSan(ChessBoard board, Move move)
    {
        var legal = board.GenerateLegalMoves();
        var actual = legal.FirstOrDefault(candidate => candidate.From == move.From && candidate.To == move.To &&
            move.Promotion == candidate.Promotion);
        if (actual == default) throw new InvalidOperationException($"Illegal move: {move.Uci}");

        var piece = board[actual.From];
        if (actual.Flags.HasFlag(MoveFlags.Castle)) return WithSuffix(board, actual, actual.To > actual.From ? "O-O" : "O-O-O");

        var san = new StringBuilder();
        if (piece.Type != PieceType.Pawn) san.Append(PieceLetter(piece.Type));
        AppendDisambiguation(board, legal, actual, piece, san);
        var capture = actual.Flags.HasFlag(MoveFlags.Capture);
        if (piece.Type == PieceType.Pawn && capture) san.Append((char)('a' + actual.From % 8));
        if (capture) san.Append('x');
        san.Append(ChessSquare.Name(actual.To));
        if (actual.Promotion != PieceType.None) san.Append('=').Append(PieceLetter(actual.Promotion));
        return WithSuffix(board, actual, san.ToString());
    }

    private static void AppendDisambiguation(ChessBoard board, IReadOnlyList<Move> legal, Move move, Piece piece, StringBuilder san)
    {
        if (piece.Type is PieceType.Pawn or PieceType.King) return;
        var alternatives = legal.Where(candidate => candidate.To == move.To && candidate.From != move.From &&
            board[candidate.From].Type == piece.Type).ToArray();
        if (alternatives.Length == 0) return;
        var file = move.From % 8; var rank = move.From / 8;
        if (alternatives.All(candidate => candidate.From % 8 != file)) san.Append((char)('a' + file));
        else if (alternatives.All(candidate => candidate.From / 8 != rank)) san.Append(rank + 1);
        else san.Append((char)('a' + file)).Append(rank + 1);
    }

    private static string WithSuffix(ChessBoard board, Move move, string san)
    {
        var next = board.Clone(); next.ApplyLegalMove(move);
        var status = next.GetStatus();
        if (status.Outcome == GameOutcome.Checkmate) return san + "#";
        return next.IsInCheck(next.SideToMove) ? san + "+" : san;
    }

    private static char PieceLetter(PieceType type) => type switch
    {
        PieceType.Knight => 'N', PieceType.Bishop => 'B', PieceType.Rook => 'R',
        PieceType.Queen => 'Q', PieceType.King => 'K', _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
