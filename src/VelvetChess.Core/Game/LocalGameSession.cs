using VelvetChess.Core.Model;

namespace VelvetChess.Core.Game;

public sealed record PlayedMove(string Uci, string San, PieceColor Color, int MoveNumber);

public sealed class LocalGameSession
{
    private readonly List<PlayedMove> _history = [];
    public ChessBoard Board { get; private set; } = new();
    public IReadOnlyList<PlayedMove> History => _history;
    public bool CanUndo => _history.Count > 0;

    public bool TryMove(Move move, out PlayedMove played)
    {
        var color = Board.SideToMove; var number = Board.FullmoveNumber;
        var legal = Board.GenerateLegalMoves().FirstOrDefault(candidate => candidate.From == move.From && candidate.To == move.To &&
            move.Promotion == candidate.Promotion);
        if (legal == default) { played = default!; return false; }
        var san = ChessNotation.ToSan(Board, legal);
        Board.ApplyLegalMove(legal);
        played = new(legal.Uci, san, color, number);
        _history.Add(played);
        return true;
    }

    public void NewGame()
    {
        Board = new ChessBoard();
        _history.Clear();
    }

    public bool UndoPlayerTurn()
    {
        if (_history.Count == 0) return false;
        var remove = Board.SideToMove == PieceColor.White && _history.Count >= 2 ? 2 : 1;
        _history.RemoveRange(_history.Count - remove, remove);
        Rebuild();
        return true;
    }

    public string SerializeMoves() => string.Join(' ', _history.Select(move => move.Uci));

    public static LocalGameSession Restore(string? moves)
    {
        var session = new LocalGameSession();
        if (string.IsNullOrWhiteSpace(moves)) return session;
        foreach (var uci in moves.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            if (!session.TryMove(Move.ParseUci(uci), out _)) throw new FormatException($"Saved game contains illegal move: {uci}");
        return session;
    }

    private void Rebuild()
    {
        var moves = _history.Select(move => move.Uci).ToArray();
        Board = new ChessBoard(); _history.Clear();
        foreach (var uci in moves) TryMove(Move.ParseUci(uci), out _);
    }
}
