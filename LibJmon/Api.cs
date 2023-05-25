using LibJmon.Types;

namespace LibJmon;

public static class TestingApi
{
    public static LexedCell[,] LexCells(string[,] cells) =>
        Impl.TestingApi.LexCells(cells);
    
    public static AstNode ParseLexedCells(LexedCell[,] lexedCells) =>
        Impl.TestingApi.ParseLexedCells(lexedCells);
    
    public static JsonVal.Any AstToJson(AstNode astNode) =>
        Impl.TestingApi.AstToJson(astNode);
}