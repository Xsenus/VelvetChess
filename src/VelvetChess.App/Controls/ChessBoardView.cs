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
    public bool ShowCoordinates { get => _drawable.ShowCoordinates; set { _drawable.ShowCoordinates = value; Invalidate(); } }
    public PieceTheme PieceTheme { get => _drawable.PieceTheme; set { _drawable.PieceTheme = value; Invalidate(); } }
    public BoardTheme BoardTheme { get => _drawable.BoardTheme; set { _drawable.BoardTheme = value; Invalidate(); } }
    public bool InputEnabled { get; set; } = true;
    public event EventHandler<Move>? MoveRequested;

    public ChessBoardView()
    {
        HeightRequest = 420;
        _drawable = new BoardDrawable(() => Board, () => _selected, () => _moves);
        Drawable = _drawable;
        SemanticProperties.SetDescription(this, "Интерактивная шахматная доска. Коснитесь фигуры, затем поля назначения.");
        StartInteraction += OnTap;
    }

    public void SetBoard(ChessBoard board) { Board = board; ClearSelection(); }
    public void SetAppearance(PieceTheme pieces, BoardTheme board)
    {
        _drawable.PieceTheme = pieces;
        _drawable.BoardTheme = board;
        Invalidate();
    }

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
        private sealed record Palette(string Light, string Dark, string Frame, string Coordinate, string Selection, string Move, string Capture);

        private static readonly IReadOnlyDictionary<BoardTheme, Palette> Palettes = new Dictionary<BoardTheme, Palette>
        {
            [BoardTheme.Velvet] = new("#EADCC4", "#74465A", "#151B2E", "#F4E9D8", "#E7B95B", "#28364A", "#A51C30"),
            [BoardTheme.Walnut] = new("#F0D9B5", "#B58863", "#3A251B", "#FFF1D6", "#F6C453", "#34495E", "#A93226"),
            [BoardTheme.Forest] = new("#E8EDCF", "#769656", "#24342A", "#F4F7E8", "#F2C94C", "#29483A", "#A33C35"),
            [BoardTheme.Ocean] = new("#DCEAF2", "#5C86A3", "#182A3A", "#F1F8FC", "#FFD166", "#244A65", "#B33A4A"),
            [BoardTheme.Graphite] = new("#D8DAE0", "#666C79", "#202329", "#F5F6F8", "#E0B857", "#343A46", "#A63B4B")
        };

        public bool Flipped { get; set; }
        public bool ShowCoordinates { get; set; } = true;
        public PieceTheme PieceTheme { get; set; } = PieceTheme.Tournament;
        public BoardTheme BoardTheme { get; set; } = BoardTheme.Velvet;

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var palette = Palettes.GetValueOrDefault(BoardTheme, Palettes[BoardTheme.Velvet]);
            var side = Math.Min(dirtyRect.Width, dirtyRect.Height); var cell = side / 8;
            var ox = (dirtyRect.Width - side) / 2; var oy = (dirtyRect.Height - side) / 2;
            canvas.FillColor = Color.FromArgb(palette.Frame); canvas.FillRectangle(dirtyRect);
            for (var shownRank = 0; shownRank < 8; shownRank++)
            for (var shownFile = 0; shownFile < 8; shownFile++)
            {
                var file = Flipped ? 7 - shownFile : shownFile; var rank = Flipped ? shownRank : 7 - shownRank;
                var square = rank * 8 + file; var rect = new RectF(ox + shownFile * cell, oy + shownRank * cell, cell, cell);
                var isDark = (file + rank) % 2 == 0;
                canvas.FillColor = Color.FromArgb(isDark ? palette.Dark : palette.Light); canvas.FillRectangle(rect);
                if (square == selected()) { canvas.FillColor = Color.FromArgb($"99{palette.Selection[1..]}"); canvas.FillRectangle(rect); }
                if (moves().Any(m => m.To == square))
                {
                    var occupied = !board()[square].IsNone;
                    canvas.FillColor = Color.FromArgb($"88{(occupied ? palette.Capture : palette.Move)[1..]}");
                    canvas.FillCircle(rect.Center, occupied ? cell * .42f : cell * .12f);
                }
                var piece = board()[square];
                if (!piece.IsNone) DrawPiece(canvas, piece, rect, cell);
                if (ShowCoordinates) DrawCoordinates(canvas, rect, cell, file, rank, shownFile, shownRank, isDark, palette);
            }
        }

        private void DrawPiece(ICanvas canvas, Piece piece, RectF rect, float cell)
        {
            var white = piece.Color == PieceColor.White;
            switch (PieceTheme)
            {
                case PieceTheme.Classic:
                    DrawGlyph(canvas, ClassicSymbol(piece), rect, cell * .76f, white ? "#FFF9EB" : "#111629", "#4A3340", cell * .018f);
                    break;
                case PieceTheme.Silhouette:
                    DrawGlyph(canvas, SolidSymbol(piece.Type), rect, cell * .75f, white ? "#F8F1E4" : "#202637", white ? "#6B5C50" : "#0A0E18", cell * .012f);
                    break;
                case PieceTheme.Royal:
                    canvas.FillColor = Color.FromArgb(white ? "#FFF4D8" : "#1A2440");
                    canvas.FillCircle(rect.Center, cell * .38f);
                    canvas.StrokeColor = Color.FromArgb("#C89B45"); canvas.StrokeSize = Math.Max(1, cell * .025f);
                    canvas.DrawCircle(rect.Center, cell * .38f);
                    DrawGlyph(canvas, SolidSymbol(piece.Type), rect, cell * .62f, white ? "#7A4F22" : "#E7C576", white ? "#FFF8E7" : "#11182B", cell * .012f);
                    break;
                case PieceTheme.Minimal:
                    canvas.FillColor = Color.FromArgb(white ? "#FAF7F0" : "#202737");
                    canvas.FillCircle(rect.Center, cell * .35f);
                    canvas.StrokeColor = Color.FromArgb(white ? "#394152" : "#F1D9A3"); canvas.StrokeSize = Math.Max(1, cell * .022f);
                    canvas.DrawCircle(rect.Center, cell * .35f);
                    canvas.Font = new Microsoft.Maui.Graphics.Font("OpenSansSemibold");
                    canvas.FontSize = cell * .38f; canvas.FontColor = Color.FromArgb(white ? "#283144" : "#F8E7BF");
                    canvas.DrawString(PieceLetter(piece.Type), rect, HorizontalAlignment.Center, VerticalAlignment.Center);
                    break;
                default:
                    // Both colours use the same solid glyph geometry, giving the set a consistent weight.
                    DrawGlyph(canvas, SolidSymbol(piece.Type), rect, cell * .77f, white ? "#FFF8E9" : "#182033", white ? "#544755" : "#E5D4B8", cell * .022f);
                    break;
            }
        }

        private static void DrawGlyph(ICanvas canvas, string glyph, RectF rect, float size, string fill, string outline, float offset)
        {
            canvas.Font = new Microsoft.Maui.Graphics.Font("ChessPieces"); canvas.FontSize = size;
            if (offset > 0)
            {
                canvas.FontColor = Color.FromArgb(outline);
                foreach (var (dx, dy) in new[] { (-offset, 0f), (offset, 0f), (0f, -offset), (0f, offset) })
                    canvas.DrawString(glyph, new RectF(rect.X + dx, rect.Y + dy, rect.Width, rect.Height), HorizontalAlignment.Center, VerticalAlignment.Center);
            }
            canvas.FontColor = Color.FromArgb(fill);
            canvas.DrawString(glyph, rect, HorizontalAlignment.Center, VerticalAlignment.Center);
        }

        private static void DrawCoordinates(ICanvas canvas, RectF rect, float cell, int file, int rank, int shownFile, int shownRank, bool isDark, Palette palette)
        {
            canvas.Font = new Microsoft.Maui.Graphics.Font("OpenSansSemibold"); canvas.FontSize = cell * .14f;
            canvas.FontColor = Color.FromArgb(isDark ? palette.Coordinate : palette.Dark);
            if (shownFile == 0)
                canvas.DrawString((rank + 1).ToString(), rect.X + 3, rect.Y + 1, cell - 5, cell, HorizontalAlignment.Left, VerticalAlignment.Top);
            if (shownRank == 7)
                canvas.DrawString(((char)('a' + file)).ToString(), rect.X + 2, rect.Y, cell - 5, cell - 2, HorizontalAlignment.Right, VerticalAlignment.Bottom);
        }

        private static string SolidSymbol(PieceType type) => type switch
        {
            PieceType.King => "♚", PieceType.Queen => "♛", PieceType.Rook => "♜", PieceType.Bishop => "♝",
            PieceType.Knight => "♞", PieceType.Pawn => "♟", _ => ""
        };

        private static string PieceLetter(PieceType type) => type switch
        {
            PieceType.King => "K", PieceType.Queen => "Q", PieceType.Rook => "R", PieceType.Bishop => "B",
            PieceType.Knight => "N", PieceType.Pawn => "P", _ => ""
        };

        private static string ClassicSymbol(Piece piece) => (piece.Color, piece.Type) switch
        {
            (PieceColor.White, PieceType.King) => "♔", (PieceColor.White, PieceType.Queen) => "♕", (PieceColor.White, PieceType.Rook) => "♖",
            (PieceColor.White, PieceType.Bishop) => "♗", (PieceColor.White, PieceType.Knight) => "♘", (PieceColor.White, PieceType.Pawn) => "♙",
            (PieceColor.Black, PieceType.King) => "♚", (PieceColor.Black, PieceType.Queen) => "♛", (PieceColor.Black, PieceType.Rook) => "♜",
            (PieceColor.Black, PieceType.Bishop) => "♝", (PieceColor.Black, PieceType.Knight) => "♞", (PieceColor.Black, PieceType.Pawn) => "♟", _ => ""
        };
    }
}
