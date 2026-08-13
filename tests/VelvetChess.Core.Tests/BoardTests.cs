using VelvetChess.Core.AI;
using VelvetChess.Core.Game;
using VelvetChess.Core.Model;
using Xunit;

namespace VelvetChess.Core.Tests;

public sealed class BoardTests
{
    [Fact]
    public void InitialPositionHasTwentyLegalMoves()
    {
        var board = new ChessBoard();
        Assert.Equal(20, board.GenerateLegalMoves().Count);
    }

    [Fact]
    public void FenRoundTrips()
    {
        const string fen = "r3k2r/ppp2ppp/2n1bn2/3qp3/8/2NPBN2/PPP2PPP/R2Q1RK1 w kq - 4 10";
        Assert.Equal(fen, new ChessBoard(fen).ToFen());
    }

    [Fact]
    public void FoolsMateIsCheckmate()
    {
        var board = new ChessBoard();
        foreach (var uci in new[] { "f2f3", "e7e5", "g2g4", "d8h4" }) board.ApplyLegalMove(Move.ParseUci(uci));
        var status = board.GetStatus();
        Assert.Equal(GameOutcome.Checkmate, status.Outcome);
        Assert.Equal(PieceColor.Black, status.Winner);
    }

    [Fact]
    public void EnPassantRemovesCapturedPawn()
    {
        var board = new ChessBoard("4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 2");
        board.ApplyLegalMove(Move.ParseUci("e5d6"));
        Assert.True(board[ChessSquare.FromName("d5")].IsNone);
        Assert.Equal(PieceType.Pawn, board[ChessSquare.FromName("d6")].Type);
    }

    [Fact]
    public void CastlingMovesRook()
    {
        var board = new ChessBoard("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
        board.ApplyLegalMove(Move.ParseUci("e1g1"));
        Assert.Equal(PieceType.King, board[ChessSquare.FromName("g1")].Type);
        Assert.Equal(PieceType.Rook, board[ChessSquare.FromName("f1")].Type);
    }

    [Fact]
    public void ExpertAiReturnsALegalMove()
    {
        var board = new ChessBoard();
        var move = new ChessAi(42).FindMove(board, DifficultyProfile.For(Difficulty.Advanced));
        Assert.NotNull(move);
        Assert.Contains(move!.Value, board.GenerateLegalMoves());
    }

    [Fact]
    public void ExpertAiFindsMateInOne()
    {
        var board = new ChessBoard("7k/5Q2/6K1/8/8/8/8/8 w - - 0 1");
        var move = new ChessAi(42).FindMove(board, DifficultyProfile.For(Difficulty.Expert));
        Assert.NotNull(move);
        var next = board.Clone(); next.ApplyLegalMove(move!.Value);
        Assert.Equal(GameOutcome.Checkmate, next.GetStatus().Outcome);
    }

    [Fact]
    public void ThreefoldRepetitionIsDetected()
    {
        var board = new ChessBoard();
        foreach (var uci in new[] { "g1f3", "g8f6", "f3g1", "f6g8", "g1f3", "g8f6", "f3g1", "f6g8" })
            board.ApplyLegalMove(Move.ParseUci(uci));
        Assert.Equal(GameOutcome.DrawThreefoldRepetition, board.GetStatus().Outcome);
    }

    [Fact]
    public void InitialPositionPerftDepthThreeMatchesReference()
    {
        Assert.Equal(8902, Perft(new ChessBoard(), 3));
    }

    [Fact]
    public void KiwipetePerftDepthTwoMatchesReference()
    {
        var board = new ChessBoard("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1");
        Assert.Equal(2039, Perft(board, 2));
    }

    [Fact]
    public void OpposingKnightsAreNotAutomaticallyInsufficientMaterial()
    {
        var board = new ChessBoard("7k/8/6n1/8/8/1N6/8/K7 w - - 0 1");
        Assert.Equal(GameOutcome.Ongoing, board.GetStatus().Outcome);
    }

    [Fact]
    public void TwoKnightsAgainstKingAreNotAnAutomaticDraw()
    {
        var board = new ChessBoard("7k/8/8/8/8/1NN5/8/K7 w - - 0 1");
        Assert.Equal(GameOutcome.Ongoing, board.GetStatus().Outcome);
    }

    private static long Perft(ChessBoard board, int depth)
    {
        if (depth == 0) return 1;
        long nodes = 0;
        foreach (var move in board.GenerateLegalMoves())
        {
            var next = board.Clone(); next.ApplyLegalMove(move);
            nodes += Perft(next, depth - 1);
        }
        return nodes;
    }

    [Theory]
    [InlineData("e2e4", "e4")]
    [InlineData("g1f3", "Nf3")]
    public void SanNotationFormatsOpeningMoves(string uci, string expected)
    {
        var board = new ChessBoard();
        Assert.Equal(expected, ChessNotation.ToSan(board, Move.ParseUci(uci)));
    }

    [Fact]
    public void LocalSessionRestoresAndUndoesFullTurn()
    {
        var session = LocalGameSession.Restore("e2e4 e7e5 g1f3 b8c6");
        Assert.Equal(4, session.History.Count);
        Assert.True(session.UndoPlayerTurn());
        Assert.Equal("e2e4 e7e5", session.SerializeMoves());
        Assert.Equal(PieceColor.White, session.Board.SideToMove);
    }

    [Fact]
    public void SanNotationMarksCheckmate()
    {
        var board = new ChessBoard();
        foreach (var uci in new[] { "f2f3", "e7e5", "g2g4" }) board.ApplyLegalMove(Move.ParseUci(uci));
        Assert.Equal("Qh4#", ChessNotation.ToSan(board, Move.ParseUci("d8h4")));
    }

    [Fact]
    public void SanNotationDisambiguatesSamePieceType()
    {
        var board = new ChessBoard("7k/8/8/8/8/5N2/8/1N5K w - - 0 1");
        Assert.Equal("Nbd2", ChessNotation.ToSan(board, Move.ParseUci("b1d2")));
        Assert.Equal("Nfd2", ChessNotation.ToSan(board, Move.ParseUci("f3d2")));
    }

    [Fact]
    public void SanNotationFormatsCastlingAndPromotion()
    {
        var castle = new ChessBoard("4k3/8/8/8/8/8/8/4K2R w K - 0 1");
        Assert.Equal("O-O", ChessNotation.ToSan(castle, Move.ParseUci("e1g1")));
        var promotion = new ChessBoard("7k/P7/8/8/8/8/8/7K w - - 0 1");
        Assert.Equal("a8=Q+", ChessNotation.ToSan(promotion, Move.ParseUci("a7a8q")));
    }
}
