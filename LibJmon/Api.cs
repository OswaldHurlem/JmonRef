using System.Text.Json.Nodes;
using LibJmon.Impl;
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

/*public static class ApiV0
{
    public static Values.JsonVal JsonFromJmon(Values.JmonSheet jmonSheet) => //Impl.ApiV0.JsonFromJmon(jmonSheet);
        throw new NotImplementedException();
}*/

// public sealed class JmonException : Exception
// {
//     public (int row, int col)? Coord { get; } = null;
//     
//     private JmonException(string message, Exception? innerException)
//         : base(message, innerException) { }
// 
//     private JmonException(string message, Exception? innerException, (int row, int col)? coord = null)
//         : this(message, innerException) => Coord = coord;
// 
//     public static JmonException AtCoord((int row, int col) coord, string message) =>
//         new ($"Error @{coord}: {message}", null, coord);
//     
//     public static JmonException General(string message) =>
//         new (message, null);
// }