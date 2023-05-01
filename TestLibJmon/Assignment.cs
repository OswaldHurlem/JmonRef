using System.Text.Json;
using LibJmon.Impl;
using LibJmon.Types;

namespace TestLibJmon;

public static class Assignment
{
    [Fact]
    public static void Pee()
    {
        ReadOnlyMemory<byte>[,] cells = CsvUtil.MakeCells(TestRsrc.JmonSampleNoAppend, "|");
        LexedCell[,] lexedCells = TestingApi.LexCells(cells);
        AstNode ast = TestingApi.ParseLexedCells(lexedCells);
        var assignments = TestingApi.ComputeAssignments(ast);
        var json = JsonSerializer.Serialize(assignments, LibJmon.JsonSerialization.Resources.JsonSerializerOptions);
        Console.WriteLine(json);
    }
}