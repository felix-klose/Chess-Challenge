using System;
using System.Collections.Generic;

class PSTUtil
{
    /*

    ================================================================================
    ================================================================================
    =                               EVALUATION                                     =
    ================================================================================
    ================================================================================
    private enum ScoreType { Pawn, Knight, Bishop, Rook, Queen, King, KingEndgame };

    private int GetPieceBonusScore(ScoreType type, bool isWhite, int rank, int file)
    {
        if (file > 3) file = 7 - file;
        if (!isWhite) rank = 7 - rank;
        int unpackedData = (sbyte)((PSTS[(int)type, file] >> (rank * 8)) & 0xFF);
        if (!isWhite) unpackedData *= -1;
        return unpackedData;
    }

    */

    public static sbyte[,] pawnScores =
    {
        {  0,  0,  0,  0 },
        { 50, 50, 50, 50 },
        { 10, 10, 20, 30 },
        {  5,  5, 10, 25 },
        {  0,  0,  0, 20 },
        {  5, -5,-10,  0 },
        {  5, 10, 10,-20 },
        {  0,  0,  0,  0 }
    };

    public static sbyte[,] knightScores =
    {
        { -50,-40,-30,-30 },
        { -40,-20,  0,  0 },
        { -30,  0, 10, 15 },
        { -30,  5, 15, 20 },
        { -30,  0, 15, 20 },
        { -30,  5, 10, 15 },
        { -40,-20,  0,  5 },
        { -50,-40,-30,-30 }
    };

    public static sbyte[,] bishopScores =
    {
        { -20,-10,-10,-10 },
        { -10,  0,  0,  0 },
        { -10,  0,  5, 10 },
        { -10,  5,  5, 10 },
        { -10,  0, 10, 10 },
        { -10, 10, 10, 10 },
        { -10,  5,  0,  0 },
        { -20,-10,-10,-10 }
    };

    public static sbyte[,] rookScores =
    {
        {  0,  0,  0,  0 },
        {  5, 10, 10, 10 },
        { -5,  0,  0,  0 },
        { -5,  0,  0,  0 },
        { -5,  0,  0,  0 },
        { -5,  0,  0,  0 },
        { -5,  0,  0,  0 },
        {  0,  0,  0,  5 }
    };

    public static sbyte[,] queenScores =
    {
        { -20,-10,-10, -5 },
        { -10,  0,  0,  0 },
        { -10,  0,  5,  5 },
        {  -5,  0,  5,  5 },
        {   0,  0,  5,  5 },
        { -10,  5,  5,  5 },
        { -10,  0,  5,  0 },
        { -20,-10,-10, -5 }
    };

    public static sbyte[,] kingScores =
    {
        { -30,-40,-40,-50 },
        { -30,-40,-40,-50 },
        { -30,-40,-40,-50 },
        { -30,-40,-40,-50 },
        { -20,-30,-30,-40 },
        { -10,-20,-20,-20 },
        {  20, 20,  0,  0 },
        {  20, 30, 10,  0 }
    };

    public static sbyte[,] kingEndgameScores =
    {
        { -50,-40,-30,-20 },
        { -30,-20,-10,  0 },
        { -30,-10, 20, 30 },
        { -30,-10, 30, 40 },
        { -30,-10, 30, 40 },
        { -30,-10, 20, 30 },
        { -30,-30,  0,  0 },
        { -50,-30,-30,-30 }
    };

    public static void PackData()
    {
        List<sbyte[,]> allScores = new();
        allScores.Add(pawnScores);
        allScores.Add(knightScores);
        allScores.Add(bishopScores);
        allScores.Add(rookScores);
        allScores.Add(queenScores);
        allScores.Add(kingScores);
        allScores.Add(kingEndgameScores);

        Console.WriteLine("{");
        for (int i = 0; i < 7; i++)
        {
            Console.Write("\t{ ");
            PackData(allScores[i]);
            if (i < 6)
                Console.WriteLine(" },");
            else
                Console.WriteLine(" }");
        }
        Console.WriteLine("}");
    }

    public static ulong[] PackData(sbyte[,] data)
    {
        ulong[] result = { 0, 0, 0, 0 };

        for (int file = 0; file < 4; file++)
        {
            for (int rank = 0; rank < 8; rank++)
            {
                ulong currentValue = (ulong)unchecked((byte)data[rank, file]);
                result[file] |= currentValue << (rank * 8);
            }
        }

        Console.Write("0x" + result[0].ToString("X16") + ", ");
        Console.Write("0x" + result[1].ToString("X16") + ", ");
        Console.Write("0x" + result[2].ToString("X16") + ", ");
        Console.Write("0x" + result[3].ToString("X16"));

        return result;
    }

    public static void Test()
    {
        List<sbyte[,]> allScores = new();
        allScores.Add(pawnScores);
        allScores.Add(knightScores);
        allScores.Add(bishopScores);
        allScores.Add(rookScores);
        allScores.Add(queenScores);
        allScores.Add(kingScores);
        allScores.Add(kingEndgameScores);

        foreach (var score in allScores)
        {
            UnpackData(PackData(score), score);
        }
    }

    public static sbyte[,] UnpackData(ulong[] data, sbyte[,] test)
    {
        sbyte[,] result = new sbyte[8, 4];

        for (int file = 0; file < 4; file++)
        {
            for (int rank = 0; rank < 8; rank++)
            {
                result[rank, file] = GetValue(data, file, rank);
            }
        }

        int errors = 0;
        int maxError = 0;
        int errorSum = 0;
        for (int file = 0; file < 4; file++)
        {
            for (int rank = 0; rank < 8; rank++)
            {
                int error = Math.Abs(result[rank, file] - test[rank, file]);
                maxError = Math.Max(maxError, error);
                errorSum += error;

                if (error > 0) errors++;
            }
        }

        Console.WriteLine("Unpacking resulted in " + errors + " errors.");

        return result;
    }

    public static sbyte GetValue(ulong[] data, int file, int rank)
    {
        if (file > 3) file = 7 - file;
        return (sbyte)((data[file] >> (rank * 8)) & 0xFF);
    }
}
