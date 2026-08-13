using System.Text;

namespace VelvetChess.Core.Model;

public sealed class ChessBoard
{
    public const string InitialFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    private readonly Piece[] _squares;
    private readonly Dictionary<string, int> _positionHistory = new(StringComparer.Ordinal);

    public PieceColor SideToMove { get; private set; }
    public bool WhiteCastleKing { get; private set; }
    public bool WhiteCastleQueen { get; private set; }
    public bool BlackCastleKing { get; private set; }
    public bool BlackCastleQueen { get; private set; }
    public int EnPassantSquare { get; private set; } = -1;
    public int HalfmoveClock { get; private set; }
    public int FullmoveNumber { get; private set; } = 1;
    public Piece this[int square] => _squares[square];

    public ChessBoard() : this(InitialFen) { }

    public ChessBoard(string fen)
    {
        _squares = new Piece[64];
        LoadFen(fen);
    }

    private ChessBoard(ChessBoard source)
    {
        _squares = (Piece[])source._squares.Clone();
        SideToMove = source.SideToMove;
        WhiteCastleKing = source.WhiteCastleKing; WhiteCastleQueen = source.WhiteCastleQueen;
        BlackCastleKing = source.BlackCastleKing; BlackCastleQueen = source.BlackCastleQueen;
        EnPassantSquare = source.EnPassantSquare; HalfmoveClock = source.HalfmoveClock;
        FullmoveNumber = source.FullmoveNumber;
        foreach (var entry in source._positionHistory) _positionHistory[entry.Key] = entry.Value;
    }

    public ChessBoard Clone() => new(this);

    public void LoadFen(string fen)
    {
        Array.Fill(_squares, Piece.None);
        var parts = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4) throw new FormatException("FEN must contain at least four fields.");
        var ranks = parts[0].Split('/');
        if (ranks.Length != 8) throw new FormatException("FEN must contain eight ranks.");
        for (var fenRank = 0; fenRank < 8; fenRank++)
        {
            var file = 0;
            foreach (var token in ranks[fenRank])
            {
                if (char.IsDigit(token)) file += token - '0';
                else
                {
                    if (file > 7) throw new FormatException("Too many squares in FEN rank.");
                    _squares[(7 - fenRank) * 8 + file++] = Piece.FromFen(token);
                }
            }
            if (file != 8) throw new FormatException("FEN rank does not contain eight squares.");
        }
        SideToMove = parts[1] == "b" ? PieceColor.Black : PieceColor.White;
        WhiteCastleKing = parts[2].Contains('K'); WhiteCastleQueen = parts[2].Contains('Q');
        BlackCastleKing = parts[2].Contains('k'); BlackCastleQueen = parts[2].Contains('q');
        EnPassantSquare = parts[3] == "-" ? -1 : ChessSquare.FromName(parts[3]);
        HalfmoveClock = parts.Length > 4 ? int.Parse(parts[4]) : 0;
        FullmoveNumber = parts.Length > 5 ? int.Parse(parts[5]) : 1;
        _positionHistory.Clear();
        RecordPosition();
    }

    public string ToFen()
    {
        var board = new StringBuilder();
        for (var rank = 7; rank >= 0; rank--)
        {
            var empty = 0;
            for (var file = 0; file < 8; file++)
            {
                var piece = _squares[rank * 8 + file];
                if (piece.IsNone) empty++;
                else { if (empty > 0) board.Append(empty); empty = 0; board.Append(piece.ToFen()); }
            }
            if (empty > 0) board.Append(empty);
            if (rank > 0) board.Append('/');
        }
        var castle = $"{(WhiteCastleKing ? "K" : "")}{(WhiteCastleQueen ? "Q" : "")}{(BlackCastleKing ? "k" : "")}{(BlackCastleQueen ? "q" : "")}";
        if (castle.Length == 0) castle = "-";
        return $"{board} {(SideToMove == PieceColor.White ? "w" : "b")} {castle} {(EnPassantSquare < 0 ? "-" : ChessSquare.Name(EnPassantSquare))} {HalfmoveClock} {FullmoveNumber}";
    }

    public IReadOnlyList<Move> GenerateLegalMoves()
    {
        var moves = new List<Move>(48);
        foreach (var move in GeneratePseudoLegalMoves())
        {
            var next = Clone();
            next.ApplyUnchecked(move);
            if (!next.IsInCheck(SideToMove)) moves.Add(move);
        }
        return moves;
    }

    public bool TryMove(Move requested, out Move applied)
    {
        foreach (var move in GenerateLegalMoves())
        {
            if (move.From == requested.From && move.To == requested.To &&
                (requested.Promotion == PieceType.None || requested.Promotion == move.Promotion))
            {
                applied = move;
                ApplyUnchecked(move);
                return true;
            }
        }
        applied = default;
        return false;
    }

    public void ApplyLegalMove(Move move)
    {
        if (!TryMove(move, out _)) throw new InvalidOperationException($"Illegal move: {move.Uci}");
    }

    public bool IsInCheck(PieceColor color)
    {
        var king = Array.FindIndex(_squares, p => p.Type == PieceType.King && p.Color == color);
        return king >= 0 && IsSquareAttacked(king, Opposite(color));
    }

    public GameStatus GetStatus()
    {
        if (HalfmoveClock >= 100) return new(GameOutcome.DrawFiftyMove);
        if (_positionHistory.GetValueOrDefault(PositionKey()) >= 3) return new(GameOutcome.DrawThreefoldRepetition);
        if (IsInsufficientMaterial()) return new(GameOutcome.DrawInsufficientMaterial);
        if (GenerateLegalMoves().Count > 0) return new(GameOutcome.Ongoing);
        return IsInCheck(SideToMove)
            ? new(GameOutcome.Checkmate, Opposite(SideToMove))
            : new(GameOutcome.Stalemate);
    }

    private IEnumerable<Move> GeneratePseudoLegalMoves()
    {
        for (var from = 0; from < 64; from++)
        {
            var piece = _squares[from];
            if (piece.IsNone || piece.Color != SideToMove) continue;
            var file = from % 8; var rank = from / 8;
            switch (piece.Type)
            {
                case PieceType.Pawn:
                    foreach (var move in PawnMoves(from, file, rank, piece.Color)) yield return move;
                    break;
                case PieceType.Knight:
                    foreach (var move in JumpMoves(from, file, rank, KnightOffsets)) yield return move;
                    break;
                case PieceType.Bishop:
                    foreach (var move in SlideMoves(from, file, rank, Diagonals)) yield return move;
                    break;
                case PieceType.Rook:
                    foreach (var move in SlideMoves(from, file, rank, Orthogonals)) yield return move;
                    break;
                case PieceType.Queen:
                    foreach (var move in SlideMoves(from, file, rank, QueenDirections)) yield return move;
                    break;
                case PieceType.King:
                    foreach (var move in JumpMoves(from, file, rank, QueenDirections)) yield return move;
                    foreach (var move in CastleMoves(from, piece.Color)) yield return move;
                    break;
            }
        }
    }

    private IEnumerable<Move> PawnMoves(int from, int file, int rank, PieceColor color)
    {
        var direction = color == PieceColor.White ? 1 : -1;
        var startRank = color == PieceColor.White ? 1 : 6;
        var promotionRank = color == PieceColor.White ? 7 : 0;
        var nextRank = rank + direction;
        if (ChessSquare.IsValid(file, nextRank) && _squares[nextRank * 8 + file].IsNone)
        {
            var to = nextRank * 8 + file;
            foreach (var move in AddPawnMove(from, to, nextRank, promotionRank, MoveFlags.None)) yield return move;
            var doubleRank = rank + direction * 2;
            if (rank == startRank && _squares[doubleRank * 8 + file].IsNone)
                yield return new(from, doubleRank * 8 + file, PieceType.None, MoveFlags.DoublePawn);
        }
        foreach (var df in new[] { -1, 1 })
        {
            var targetFile = file + df;
            if (!ChessSquare.IsValid(targetFile, nextRank)) continue;
            var to = nextRank * 8 + targetFile;
            if (!_squares[to].IsNone && _squares[to].Color != color)
                foreach (var move in AddPawnMove(from, to, nextRank, promotionRank, MoveFlags.Capture)) yield return move;
            else if (to == EnPassantSquare)
                yield return new(from, to, PieceType.None, MoveFlags.Capture | MoveFlags.EnPassant);
        }
    }

    private static IEnumerable<Move> AddPawnMove(int from, int to, int rank, int promotionRank, MoveFlags flags)
    {
        if (rank != promotionRank) { yield return new(from, to, PieceType.None, flags); yield break; }
        foreach (var type in new[] { PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight })
            yield return new(from, to, type, flags | MoveFlags.Promotion);
    }

    private IEnumerable<Move> JumpMoves(int from, int file, int rank, (int f, int r)[] offsets)
    {
        foreach (var (df, dr) in offsets)
        {
            var tf = file + df; var tr = rank + dr;
            if (!ChessSquare.IsValid(tf, tr)) continue;
            var target = _squares[tr * 8 + tf];
            if (target.IsNone || target.Color != SideToMove)
                yield return new(from, tr * 8 + tf, PieceType.None, target.IsNone ? MoveFlags.None : MoveFlags.Capture);
        }
    }

    private IEnumerable<Move> SlideMoves(int from, int file, int rank, (int f, int r)[] directions)
    {
        foreach (var (df, dr) in directions)
        {
            for (var distance = 1; distance < 8; distance++)
            {
                var tf = file + df * distance; var tr = rank + dr * distance;
                if (!ChessSquare.IsValid(tf, tr)) break;
                var to = tr * 8 + tf; var target = _squares[to];
                if (target.IsNone) { yield return new(from, to); continue; }
                if (target.Color != SideToMove) yield return new(from, to, PieceType.None, MoveFlags.Capture);
                break;
            }
        }
    }

    private IEnumerable<Move> CastleMoves(int from, PieceColor color)
    {
        if (IsInCheck(color)) yield break;
        if (color == PieceColor.White && from == 4)
        {
            if (WhiteCastleKing && _squares[5].IsNone && _squares[6].IsNone && !IsSquareAttacked(5, PieceColor.Black) && !IsSquareAttacked(6, PieceColor.Black))
                yield return new(4, 6, PieceType.None, MoveFlags.Castle);
            if (WhiteCastleQueen && _squares[1].IsNone && _squares[2].IsNone && _squares[3].IsNone && !IsSquareAttacked(3, PieceColor.Black) && !IsSquareAttacked(2, PieceColor.Black))
                yield return new(4, 2, PieceType.None, MoveFlags.Castle);
        }
        else if (color == PieceColor.Black && from == 60)
        {
            if (BlackCastleKing && _squares[61].IsNone && _squares[62].IsNone && !IsSquareAttacked(61, PieceColor.White) && !IsSquareAttacked(62, PieceColor.White))
                yield return new(60, 62, PieceType.None, MoveFlags.Castle);
            if (BlackCastleQueen && _squares[57].IsNone && _squares[58].IsNone && _squares[59].IsNone && !IsSquareAttacked(59, PieceColor.White) && !IsSquareAttacked(58, PieceColor.White))
                yield return new(60, 58, PieceType.None, MoveFlags.Castle);
        }
    }

    private bool IsSquareAttacked(int square, PieceColor byColor)
    {
        var file = square % 8; var rank = square / 8;
        var pawnRank = rank + (byColor == PieceColor.White ? -1 : 1);
        foreach (var df in new[] { -1, 1 })
            if (ChessSquare.IsValid(file + df, pawnRank))
            {
                var p = _squares[pawnRank * 8 + file + df];
                if (p.Type == PieceType.Pawn && p.Color == byColor) return true;
            }
        if (AttackedByJump(file, rank, byColor, PieceType.Knight, KnightOffsets)) return true;
        if (AttackedByJump(file, rank, byColor, PieceType.King, QueenDirections)) return true;
        return AttackedBySlider(file, rank, byColor, PieceType.Bishop, Diagonals) ||
               AttackedBySlider(file, rank, byColor, PieceType.Rook, Orthogonals);
    }

    private bool AttackedByJump(int file, int rank, PieceColor color, PieceType type, (int f, int r)[] offsets)
    {
        foreach (var (df, dr) in offsets)
        {
            var tf = file + df; var tr = rank + dr;
            if (!ChessSquare.IsValid(tf, tr)) continue;
            var p = _squares[tr * 8 + tf];
            if (p.Color == color && p.Type == type) return true;
        }
        return false;
    }

    private bool AttackedBySlider(int file, int rank, PieceColor color, PieceType type, (int f, int r)[] directions)
    {
        foreach (var (df, dr) in directions)
            for (var distance = 1; distance < 8; distance++)
            {
                var tf = file + df * distance; var tr = rank + dr * distance;
                if (!ChessSquare.IsValid(tf, tr)) break;
                var p = _squares[tr * 8 + tf];
                if (p.IsNone) continue;
                if (p.Color == color && (p.Type == type || p.Type == PieceType.Queen)) return true;
                break;
            }
        return false;
    }

    private void ApplyUnchecked(Move move)
    {
        var piece = _squares[move.From];
        var captured = _squares[move.To];
        _squares[move.From] = Piece.None;
        _squares[move.To] = move.Promotion == PieceType.None ? piece : new(move.Promotion, piece.Color);
        if (move.Flags.HasFlag(MoveFlags.EnPassant))
            _squares[move.To + (piece.Color == PieceColor.White ? -8 : 8)] = Piece.None;
        if (move.Flags.HasFlag(MoveFlags.Castle))
        {
            var (rookFrom, rookTo) = move.To > move.From ? (move.From + 3, move.From + 1) : (move.From - 4, move.From - 1);
            _squares[rookTo] = _squares[rookFrom]; _squares[rookFrom] = Piece.None;
        }
        UpdateCastleRights(move, piece, captured);
        EnPassantSquare = move.Flags.HasFlag(MoveFlags.DoublePawn) ? (move.From + move.To) / 2 : -1;
        HalfmoveClock = piece.Type == PieceType.Pawn || !captured.IsNone || move.Flags.HasFlag(MoveFlags.EnPassant) ? 0 : HalfmoveClock + 1;
        if (SideToMove == PieceColor.Black) FullmoveNumber++;
        SideToMove = Opposite(SideToMove);
        RecordPosition();
    }

    private void UpdateCastleRights(Move move, Piece piece, Piece captured)
    {
        if (piece.Type == PieceType.King)
        {
            if (piece.Color == PieceColor.White) { WhiteCastleKing = false; WhiteCastleQueen = false; }
            else { BlackCastleKing = false; BlackCastleQueen = false; }
        }
        if (piece.Type == PieceType.Rook || captured.Type == PieceType.Rook)
        {
            if (move.From == 0 || move.To == 0) WhiteCastleQueen = false;
            if (move.From == 7 || move.To == 7) WhiteCastleKing = false;
            if (move.From == 56 || move.To == 56) BlackCastleQueen = false;
            if (move.From == 63 || move.To == 63) BlackCastleKing = false;
        }
    }

    private bool IsInsufficientMaterial()
    {
        var pieces = _squares.Select((piece, square) => (piece, square)).Where(x => !x.piece.IsNone && x.piece.Type != PieceType.King).ToArray();
        if (pieces.Length == 0) return true;
        if (pieces.Any(x => x.piece.Type is PieceType.Pawn or PieceType.Rook or PieceType.Queen)) return false;
        if (pieces.Length == 1) return true;
        if (pieces.All(x => x.piece.Type == PieceType.Bishop))
            return pieces.Select(x => ((x.square % 8) + (x.square / 8)) % 2).Distinct().Count() == 1;
        return pieces.Length <= 2 && pieces.All(x => x.piece.Type == PieceType.Knight);
    }

    private string PositionKey()
    {
        var fields = ToFen().Split(' ');
        return string.Join(' ', fields.Take(4));
    }

    private void RecordPosition()
    {
        var key = PositionKey();
        _positionHistory[key] = _positionHistory.GetValueOrDefault(key) + 1;
    }

    public static PieceColor Opposite(PieceColor color) => color == PieceColor.White ? PieceColor.Black : PieceColor.White;

    private static readonly (int, int)[] KnightOffsets = [(1, 2), (2, 1), (-1, 2), (-2, 1), (1, -2), (2, -1), (-1, -2), (-2, -1)];
    private static readonly (int, int)[] Diagonals = [(1, 1), (1, -1), (-1, 1), (-1, -1)];
    private static readonly (int, int)[] Orthogonals = [(1, 0), (-1, 0), (0, 1), (0, -1)];
    private static readonly (int, int)[] QueenDirections = [.. Diagonals, .. Orthogonals];
}
