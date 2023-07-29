using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

class V0_9 : IChessBot
{
    class SearchResult
    {
        public List<Move> BestMoves = new();
        public int Value = MIN_SCORE;
    }

    enum TTFlag { EXACT, UPPERBOUND, LOWERBOUND }

    class Transposition
    {
        public SearchResult BestResult;
        public TTFlag Flag;
        public int Depth;
    }

    private int[] PIECE_VALUES = { 0, 100, 320, 330, 500, 900, 20000 };
    private ulong[,] PSTS =
    {
        { 0x00050500050A3200, 0x000AFB00050A3200, 0x000AF6000A143200, 0x00EC0014191E3200 },
        { 0xCED8E2E2E2E2D8CE, 0xD8EC05000500ECD8, 0xE2000A0F0F0A00E2, 0xE2050F14140F00E2 },
        { 0xECF6F6F6F6F6F6EC, 0xF6050A00050000F6, 0xF6000A0A050500F6, 0xF6000A0A0A0A00F6 },
        { 0x00FBFBFBFBFB0500, 0x0000000000000A00, 0x0000000000000A00, 0x0500000000000A00 },
        { 0xECF6F600FBF6F6EC, 0xF6000500000000F6, 0xF6050505050500F6, 0xFB000505050500FB },
        { 0x1414F6ECE2E2E2E2, 0x1E14ECE2D8D8D8D8, 0x0A00ECE2D8D8D8D8, 0x0000ECD8CECECECE },
        { 0xCEE2E2E2E2E2E2CE, 0xE2E2F6F6F6F6ECD8, 0xE200141E1E14F6E2, 0xE2001E28281E00EC }
    };
    private const int MAX_SCORE = 1000000;
    private const int MIN_SCORE = -1000000;

    private Random _random = new(Environment.TickCount);
    private bool _isPlayingWhite;
    private Timer _timer;
    private int _maxTime;

    private Dictionary<ulong, Transposition> _transpositionTable = new();


    public Move Think(Board board, Timer timer)
    {
        _timer = timer;
        _maxTime = Math.Max(100, _timer.MillisecondsRemaining / 30);
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

        ulong zobristKey = board.ZobristKey;
        int alphaOrig = alpha;
        Transposition entry;
        if (_transpositionTable.TryGetValue(zobristKey, out entry) && entry.Depth >= depth)
        {
            switch (entry.Flag)
            {
                case TTFlag.EXACT:
                    return entry.BestResult;
                case TTFlag.LOWERBOUND:
                    alpha = Math.Max(alpha, entry.BestResult.Value);
                    break;
                case TTFlag.UPPERBOUND:
                    beta = Math.Min(alpha, entry.BestResult.Value);
                    break;
            }
            if (alpha >= beta) return entry.BestResult;
        }

        var moves = GetOrderdMoves(board);

        if (depth == 0 || moves.Count() == 0)
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


        entry = new();
        entry.BestResult = bestResult;
        entry.Depth = depth;
        if (bestResult.Value <= alphaOrig)
            entry.Flag = TTFlag.UPPERBOUND;
        else if (bestResult.Value >= beta)
            entry.Flag = TTFlag.LOWERBOUND;
        else
            entry.Flag = TTFlag.EXACT;

        _transpositionTable[zobristKey] = entry;

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

    private enum ScoreType { Pawn, Knight, Bishop, Rook, Queen, King, KingEndgame };

    private int GetPieceBonusScore(ScoreType type, bool isWhite, int rank, int file)
    {
        if (file > 3) file = 7 - file;
        if (!isWhite) rank = 7 - rank;
        int unpackedData = (sbyte)((PSTS[(int)type, file] >> (rank * 8)) & 0xFF);
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
