﻿using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

class V0_5 : IChessBot
{
    class SearchResult
    {
        public List<Move> BestMoves = new();
        public int Value = MIN_SCORE;
    }

    private int[] PIECE_VALUES = { 0, 100, 320, 330, 500, 900, 20000 };
    private ulong[,] PSTS =
    {
        {0xFFCDE1EBFFEBCE00, 0xFFD7D7F5FFF5D800, 0xFFE1D7F5FFF5E200, 0xFFEBCDFAFFF5E200},
        {0xFFE1E1F604F5D832, 0xFFEBD80009FFEC32, 0xFFF5D8000A000032, 0xFFFFCE000A000032},
        {0xFFE1E1F5FAF5E20A, 0xFFF5D8000000000A, 0x0013D80500050A14, 0x001DCE05000A0F1E},
        {0xFFE1E1FAFAF5E205, 0xFFF5D80000050505, 0x001DD80500050F0A, 0x0027CE05000A1419},
        {0xFFE1EBFFFAF5E200, 0xFFF5E20000000000, 0x001DE205000A0F00, 0x0027D805000A1414},
        {0xFFE1F5F5FAF5E205, 0xFFF5EC05000A04FB, 0x0013EC05000A09F6, 0x001DEC05000A0F00},
        {0xFFE213F5FAF5D805, 0xFFE214000004EC0A, 0x000000050000000A, 0x00000000000004EC},
        {0xFFCE13EBFFEBCE00, 0xFFE21DF5FFF5D800, 0xFFE209F5FFF5E200, 0xFFE1FFFB04F5E200}
    };
    private const int MAX_SCORE = 1000000;
    private const int MIN_SCORE = -1000000;

    private Random _random = new(Environment.TickCount);
    private bool _isPlayingWhite;
    private Timer _timer;
    private int _maxTime;


    public Move Think(Board board, Timer timer)
    {
        _maxTime = 100;
        _timer = timer;

        _isPlayingWhite = board.IsWhiteToMove;

        SearchResult result = new();

        result.BestMoves.Add(GetOrderdMoves(board)[0]);

        int depth = 0;
        while (_timer.MillisecondsElapsedThisTurn < _maxTime && depth < 80)
        {
            try
            {
                result = NegaMax(board, depth);
                depth++;
            }
            catch { break; }
        }

        return result.BestMoves[_random.Next(result.BestMoves.Count())];
    }

    private SearchResult NegaMax(Board board, int depth, int alpha = MIN_SCORE, int beta = MAX_SCORE, int color = 1, int ply = 0)
    {
        if (_timer.MillisecondsElapsedThisTurn > _maxTime) throw new Exception();

        var moves = GetOrderdMoves(board);

        if (depth <= 0 || moves.Count() == 0)
        {
            SearchResult result = new();
            result.Value = color * Evaluate(board, depth);
            return result;
        }

        SearchResult bestResult = new();

        foreach (var move in moves)
        {
            board.MakeMove(move);
            var result = NegaMax(board, depth - 1, -beta, -alpha, -color, ply + 1);
            int value = -result.Value;
            board.UndoMove(move);

            if (value == bestResult.Value)
            {
                bestResult.BestMoves.Add(move);
            }
            if (value > bestResult.Value)
            {
                bestResult = new();
                bestResult.Value = value;
                bestResult.BestMoves.Add(move);
            }

            if (ply > 0)
            {
                alpha = Math.Max(alpha, value);
                if (alpha >= beta)
                    break;
            }
        }

        return bestResult;
    }

    private Move[] GetOrderdMoves(Board board)
    {
        return board.GetLegalMoves().OrderByDescending(move => move.IsCapture ? (int)move.CapturePieceType * 100 + (int)move.MovePieceType : 0).ToArray();
    }

    private int Evaluate(Board board, int depth)
    {
        if (board.IsInCheckmate())
        {
            if (board.IsWhiteToMove == _isPlayingWhite) return MIN_SCORE + 100 - depth;
            else return MAX_SCORE - 100 + depth;
        }
        else if (board.IsDraw()) return 0;

        int pieceValues = GetPieceValues(board);

        return pieceValues;
    }

    private int GetPieceValues(Board board)
    {
        int whitePositionBonus = 0;
        int blackPositionBonus = 0;

        for (int i = 0; i < 5; i++)
        {
            whitePositionBonus += GetPieceBonusForType(board, (PieceType)(i + 1), (ScoreType)i, true);
            blackPositionBonus += GetPieceBonusForType(board, (PieceType)(i + 1), (ScoreType)i, false);
        }

        int kingIndex = IsLateGame(board) ? 6 : 5;
        whitePositionBonus += GetPieceBonusForType(board, PieceType.King, (ScoreType)kingIndex, true);
        blackPositionBonus += GetPieceBonusForType(board, PieceType.King, (ScoreType)kingIndex, false);

        int result = whitePositionBonus + blackPositionBonus;
        if (!_isPlayingWhite) result *= -1;

        return result;
    }

    private int GetPieceBonusForType(Board board, PieceType pieceType, ScoreType scoreType, bool isWhite)
    {
        int value = PIECE_VALUES[(int)pieceType];
        if (!isWhite) value *= -1;

        var squares = board.GetPieceList(pieceType, isWhite).Select(piece => piece.Square);
        int result = 0;
        foreach (Square square in squares)
        {
            result += value + GetPieceBonusScore(scoreType, isWhite, square.Rank, square.File);
        }
        return result;
    }

    private enum ScoreType { Pawn, Knight, Bishop, Rook, Queen, King, KingEndgame, KingHunt };

    private int GetPieceBonusScore(ScoreType type, bool isWhite, int rank, int file)
    {
        if (file > 3) file = 7 - file;
        if (!isWhite) rank = 7 - rank;
        ulong bytemask = 0xFF;
        int unpackedData = (int)(sbyte)((PSTS[rank, file] & (bytemask << (int)type)) >> (int)type);
        if (!isWhite) unpackedData *= -1;
        return unpackedData;
    }

    private bool IsLateGame(Board board)
    {
        int numPieces = board.GetAllPieceLists()
            .Where(piece => piece.TypeOfPieceInList != PieceType.Pawn && piece.TypeOfPieceInList != PieceType.King)
            .Select(piece => piece.Count)
            .Sum();
        return numPieces < 7;
    }
}
