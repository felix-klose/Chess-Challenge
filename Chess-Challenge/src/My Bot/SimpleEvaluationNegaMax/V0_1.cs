using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

class V0_1 : IChessBot
{
    private int[] PIECE_VALUES = { 0, 100, 320, 330, 500, 900, 20000 };
    private const int MAX_SCORE = 1000000;
    private const int MIN_SCORE = -1000000;

    private Random _random = new(Environment.TickCount);
    private List<Move> _bestMoves = new();


    public Move Think(Board board, Timer timer)
    {
        _bestMoves = new();

        NegaMax(board, 3, board.IsWhiteToMove ? 1 : -1, 0);

        return _bestMoves[_random.Next(_bestMoves.Count)];
    }

    private int NegaMax(Board board, int depth, int color, int ply)
    {
        var moves = board.GetLegalMoves();
        if (depth == 0 || moves.Length == 0) return color * Evaluate(board);
        int value = MIN_SCORE;

        foreach (var move in moves)
        {
            board.MakeMove(move);
            int currentValue = -NegaMax(board, depth - 1, -color, ply + 1);

            if (currentValue == value && ply == 0) _bestMoves.Add(move);
            if (currentValue > value)
            {
                value = currentValue;
                if (ply == 0) _bestMoves = new() { move };
            }
            board.UndoMove(move);
        }

        return value;
    }

    private int Evaluate(Board board)
    {
        if (board.IsInCheckmate())
        {
            if (board.IsWhiteToMove) return MIN_SCORE;
            else return MAX_SCORE;
        }
        else if (board.IsDraw()) return 0;

        var pieceLists = board.GetAllPieceLists();
        var whitePieces = pieceLists.Where(pl => pl.IsWhitePieceList);
        var blackPieces = pieceLists.Where(pl => !pl.IsWhitePieceList);

        var whiteMaterial = whitePieces.Select(piece => piece.Count * PIECE_VALUES[(int)piece.TypeOfPieceInList])
                                       .Sum();
        var blackMaterial = -blackPieces.Select((piece) => piece.Count * PIECE_VALUES[(int)piece.TypeOfPieceInList])
                                       .Sum();

        int material = whiteMaterial + blackMaterial;

        return material;
    }
}
