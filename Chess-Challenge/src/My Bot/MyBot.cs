using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

class MyBot : IChessBot
{
    // Arbitrary, high values. We don't use int.MinValue and int.MaxValue to ensure alpha and beta don't over-/underflow when negated
    private const int MAX_SCORE = 1000000;
    private const int MIN_SCORE = -1000000;

    // Search result and Transposition table
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

    private Dictionary<ulong, Transposition> _transpositionTable = new();

    // Piece Values and PSTs taken from https://www.chessprogramming.org/Simplified_Evaluation_Function
    private enum ScoreType { Pawn, Knight, Bishop, Rook, Queen, King, KingEndgame };
    private int[] PIECE_VALUES = { 0, 100, 320, 330, 500, 900, 20000 };

    /* Structure: 
     * 
     *  - each array contains the PST for one piece type
     *  - each value contains the bonus value for one file on the board
     *      - least significant byte is the value for rank 0, next byte for rank 1, etc
     *      - note that values can be negative, so packing and unpacking values requires caution while casting to/from ulong
     */
    private ulong[,] PSTS =
    {
        { 0x00050500050A3200, 0x000AFB00050A3200, 0x000AF6000A143200, 0x00EC0014191E3200 }, // Pawn
        { 0xCED8E2E2E2E2D8CE, 0xD8EC05000500ECD8, 0xE2000A0F0F0A00E2, 0xE2050F14140F00E2 }, // Knight
        { 0xECF6F6F6F6F6F6EC, 0xF6050A00050000F6, 0xF6000A0A050500F6, 0xF6000A0A0A0A00F6 }, // Bishop
        { 0x00FBFBFBFBFB0500, 0x0000000000000A00, 0x0000000000000A00, 0x0500000000000A00 }, // Rook
        { 0xECF6F600FBF6F6EC, 0xF6000500000000F6, 0xF6050505050500F6, 0xFB000505050500FB }, // Queen
        { 0x1414F6ECE2E2E2E2, 0x1E14ECE2D8D8D8D8, 0x0A00ECE2D8D8D8D8, 0x0000ECD8CECECECE }, // King early/midgame
        { 0xCEE2E2E2E2E2E2CE, 0xE2E2F6F6F6F6ECD8, 0xE200141E1E14F6E2, 0xE2001E28281E00EC }  // King endgame
    };

    private Random _random = new(Environment.TickCount);

    // Used to control evaluation sign
    private bool _isPlayingWhite;

    // Search depth control
    private Timer _timer;
    private int _maxTime;
    private bool _searchCancelled;

    public Move Think(Board board, Timer timer)
    {
        _timer = timer;
        _maxTime = Math.Max(100, _timer.MillisecondsRemaining / 30);
        _isPlayingWhite = board.IsWhiteToMove;
        _searchCancelled = false;

        SearchResult result = new();

        // Fallback if search fails completely. Just use move with highest MVV-LVA
        result.BestMoves.Add(GetOrderdMoves(board, new())[0]);

        // https://www.chessprogramming.org/Iterative_Deepening
        int depth = 0;
        while (_timer.MillisecondsElapsedThisTurn < _maxTime && depth < 80)
        {
            result = NegaMax(board, depth, result);
            depth++;
        }

        // Ensure some non-determinism
        return result.BestMoves[_random.Next(result.BestMoves.Count())];
    }

    // Best-first alpha-beta NegaMax search with transposition tables
    // https://en.wikipedia.org/wiki/Negamax
    //
    // - previousBest is used to prime move ordering for the first iteration after deepening
    // - ply is inverse to depth and is used to disable pruning in the first iteration
    private SearchResult NegaMax(Board board, int depth, SearchResult previousBest, int alpha = MIN_SCORE, int beta = MAX_SCORE, int player = 1, int ply = 0)
    {
        // Timer control
        if (_timer.MillisecondsElapsedThisTurn > _maxTime)
        {
            _searchCancelled = true;
        }

        // Transposition table evaluation
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

        // Order moves and evaluate board if necessary
        var moves = GetOrderdMoves(board, previousBest);
        if (_searchCancelled || depth == 0 || moves.Count() == 0)
        {
            SearchResult result = new();
            result.Value = player * Evaluate(board, depth);
            return result;
        }

        // Main search loop
        SearchResult bestResult = new();
        foreach (var move in moves)
        {
            board.MakeMove(move);
            var result = NegaMax(board, depth - 1, new(), -beta, -alpha, -player, ply + 1);
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
            if (_searchCancelled)
                break;
        }

        // Transposition table update
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

    // Order moves for a best-first approach using https://www.chessprogramming.org/MVV-LVA primed by previous search results
    private Move[] GetOrderdMoves(Board board, SearchResult previousBest)
    {
        List<Move> result = new();
        result.AddRange(previousBest.BestMoves);
        result.AddRange(
            board.GetLegalMoves()
                .Where(move => !previousBest.BestMoves.Contains(move))
                .OrderByDescending(move => move.IsCapture ? (int)move.CapturePieceType * 100 + (int)move.MovePieceType : 0).ToArray()
        );
        return result.ToArray();
    }

    // Simple evaluation function, taken from https://www.chessprogramming.org/Simplified_Evaluation_Function
    private int Evaluate(Board board, int depth)
    {
        // Evaluate draws and mates. Order mates by move count before mate
        if (board.IsInCheckmate())
        {
            if (board.IsWhiteToMove == _isPlayingWhite) return MIN_SCORE + 100 - depth;
            else return MAX_SCORE - 100 + depth;
        }
        else if (board.IsDraw() || board.IsInsufficientMaterial() || board.IsRepeatedPosition())
            return 0;

        // Calculate and return piece values otherwise
        return GetPieceValues(board);
    }

    // Calculate piece values for the whole board
    private int GetPieceValues(Board board)
    {
        int whitePositionBonus = 0;
        int blackPositionBonus = 0;

        // Evaluate everything other than kings
        for (int i = 0; i < 5; i++)
        {
            whitePositionBonus += GetPieceBonusForType(board, (PieceType)(i + 1), (ScoreType)i, true);
            blackPositionBonus += GetPieceBonusForType(board, (PieceType)(i + 1), (ScoreType)i, false);
        }

        // Evaluate everything other than kings depending on current game phase
        int kingIndex = IsLateGame(board) ? 6 : 5;
        whitePositionBonus += GetPieceBonusForType(board, PieceType.King, (ScoreType)kingIndex, true);
        blackPositionBonus += GetPieceBonusForType(board, PieceType.King, (ScoreType)kingIndex, false);

        int result = whitePositionBonus + blackPositionBonus;

        // Accomodate for player color
        if (!_isPlayingWhite) result *= -1;

        return result;
    }

    // Calculate piece values for one piece type for one color
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

    // Get piece bonus for a single piece
    private int GetPieceBonusScore(ScoreType type, bool isWhite, int rank, int file)
    {
        if (file > 3) file = 7 - file;
        if (!isWhite) rank = 7 - rank;
        int unpackedData = (sbyte)((PSTS[(int)type, file] >> (rank * 8)) & 0xFF);
        if (!isWhite) unpackedData *= -1;
        return unpackedData;
    }

    // Decide whether the game is in the endgame phase or not.
    // If there are 6 or less non-pawn, non-king pieces on the board, we are in the endgame
    private bool IsLateGame(Board board)
    {
        int numPieces = board.GetAllPieceLists()
            .Where(piece => piece.TypeOfPieceInList != PieceType.Pawn && piece.TypeOfPieceInList != PieceType.King)
            .Select(piece => piece.Count)
            .Sum();
        return numPieces < 7;
    }
}
