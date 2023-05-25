using System.Text.Json;
using System.Text.Json.Nodes;
using LibJmon.Impl;
using LibJmon.Types;

namespace TestLibJmon;

public static class KnownBugs
{
    [Fact]
    public static void ArrayOfArrays()
    {
        const string aoaCsv =
            """
            :[,.+,.+
            .+,00,01
            .+,10,11
            """;

        ReadOnlyMemory<byte>[,] cells = CsvUtil.MakeCells(TestRsrc.JmonSampleNoAppend, "|");
        LexedCell[,] lexedCells = TestingApi.LexCells(cells);
        AstNode ast = TestingApi.ParseLexedCells(lexedCells);
        var parsedVal = LibJmon.TestingApi.AstToJson(ast);
        JsonArray? arr = parsedVal.V?.AsArray();
        Assert.Equal(arr?.Count, 2);
        JsonArray? innerArr0 = arr?[0]?.AsArray();
        Assert.Equal(innerArr0?.Count, 2);
        Assert.Equal(innerArr0?[0], "11");
        Assert.Equal(innerArr0?[1], "01");
        JsonArray? innerArr1 = arr?[1]?.AsArray();
        Assert.Equal(innerArr1?.Count, 2);
        Assert.Equal(innerArr1?[0], "10");
        Assert.Equal(innerArr1?[1], "11");
    }
}