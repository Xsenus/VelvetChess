using Microsoft.Maui.Graphics;
using VelvetChess.Core.Model;

namespace VelvetChess.App.Controls;

public sealed class ChessBoardView : GraphicsView
{
    private readonly BoardDrawable _drawable;
    private int _selected = -1;
    private IReadOnlyList<Move> _moves = [];

    public ChessBoard Board { get; private set; } = new();
    public bool Flipped { get => _drawable.Flipped; set { _drawable.Flipped = value; Invalidate(); } }
    public bool InputEnabled { get; set; } = true;
    public event EventHandler<Move>? MoveRequested;

    public ChessBoardView()
    {
        HeightRequest = 420;
        _drawable = new BoardDrawable(() => Board, () => _selected, () => _moves);
        Drawable = _drawable;
        StartInteraction += OnTap;
    }

    public void SetBoard(ChessBoard board) { Board = board; ClearSelection(); }
    public void ClearSelection() { _selected = -1; _moves = []; Invalidate(); }

    private void OnTap(object? sender, TouchEventArgs args)
    {
        if (!InputEnabled || args.Touches.Length == 0) return;
        var point = args.Touches[0];
        var side = Math.Min(Width, Height); var cell = side / 8;
        var shownFile = (int)((point.X - (Width - side) / 2) / cell);
        var shownRank = (int)((point.Y - (Height - side) / 2) / cell);
        if (shownFile is < 0 or > 7 || shownRank is < 0 or > 7) return;
        var file = Flipped ? 7 - shownFile : shownFile; var rank = Flipped ? shownRank : 7 - shownRank;
        var square = rank * 8 + file;
        var target = _moves.FirstOrDefault(m => m.To == square);
        if (_selected >= 0 && target != default) { MoveRequested?.Invoke(this, target); ClearSelection(); return; }
        var piece = Board[square];
        if (!piece.IsNone && piece.Color == Board.SideToMove)
        {
            _selected = square; _moves = Board.GenerateLegalMoves().Where(m => m.From == square).ToArray(); Invalidate();
        }
        else ClearSelection();
    }

    private sealed class BoardDrawable(Func<ChessBoard> board, Func<int> selected, Func<IReadOnlyList<Move>> moves) : IDrawable
    {
        public bool Flipped { get; set; }
        private static readonly Color Light = Color.FromArgb("#E6D4B7");
        private static readonly Color Dark = Color.FromArgb("#6E4051");

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var side = Math.Min(dirtyRect.Width, dirtyRect.Height); var cell = side / 8;
            var ox = (dirtyRect.Width - side) / 2; var oy = (dirtyRect.Height - side) / 2;
            canvas.FillColor = Color.FromArgb("#151B2E"); canvas.FillRectangle(dirtyRect);
            for (var shownRank = 0; shownRank < 8; shownRank++)
            for (var shownFile = 0; shownFile < 8; shownFile++)
            {
                var file = Flipped ? 7 - shownFile : shownFile; var rank = Flipped ? shownRank : 7 - shownRank;
                var square = rank * 8 + file; var rect = new RectF(ox + shownFile * cell, oy + shownRank * cell, cell, cell);
                canvas.FillColor = (file + rank) % 2 == 0 ? Dark : Light; canvas.FillRectangle(rect);
                if (square == selected()) { canvas.FillColor = Color.FromArgb("#99E2B85A"); canvas.FillRectangle(rect); }
                if (moves().Any(m => m.To == square))
                {
                    canvas.FillColor = board()[square].IsNone ? Color.FromArgb("#88303A49") : Color.FromArgb("#88A51C30");
                    canvas.FillCircle(rect.Center, board()[square].IsNone ? cell * .12f : cell * .42f);
                }
                var piece = board()[square];
                if (!piece.IsNone)
                {
                    canvas.FontColor = piece.Color == PieceColor.White ? Color.FromArgb("#FFF9EB") : Color.FromArgb("#111629");
                    canvas.FontSize = cell * .76f; canvas.Font = new Microsoft.Maui.Graphics.Font("ChessPieces");
                    canvas.DrawString(Symbol(piece), rect, HorizontalAlignment.Center, VerticalAlignment.Center);
                }
            }
        }

        private static string Symbol(Piece piece) => (piece.Color, piece.Type) switch
        {
            (PieceColor.White, PieceType.King) => "♔", (PieceColor.White, PieceType.Queen) => "♕", (PieceColor.White, PieceType.Rook) => "♖",
            (PieceColor.White, PieceType.Bishop) => "♗", (PieceColor.White, PieceType.Knight) => "♘", (PieceColor.White, PieceType.Pawn) => "♙",
            (PieceColor.Black, PieceType.King) => "♚", (PieceColor.Black, PieceType.Queen) => "♛", (PieceColor.Black, PieceType.Rook) => "♜",
            (PieceColor.Black, PieceType.Bishop) => "♝", (PieceColor.Black, PieceType.Knight) => "♞", (PieceColor.Black, PieceType.Pawn) => "♟", _ => ""
        };
    }
}
