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
            """;

        ReadOnlyMemory<byte>[,] cells = CsvUtil.MakeCells(aoaCsv, ",");
        LexedCell[,] lexedCells = TestingApi.LexCells(cells);
        AstNode ast = TestingApi.ParseLexedCells(lexedCells);
        var parsedVal = LibJmon.TestingApi.AstToJson(ast);
        JsonArray? arr = parsedVal.V?.AsArray();
        Assert.Equal(1, arr?.Count);
        JsonArray? innerArr0 = arr?[0]?.AsArray();
        Assert.Equal(2, innerArr0?.Count);
        Assert.Equal("00", (string?)innerArr0?[0]);
        Assert.Equal("01", (string?)innerArr0?[1]);
    }

    private const string desiredAst = """
    {
        "Type":"Matrix",
        "Val":{
            "Items":[
                {
                    "Path":{
                        "Items":[1],
                        "IsAppend":false
                    },
                    "Node":{
                        "Type":"Matrix",
                        "Val":{
                            "Items":[
                                {
                                    "Path":{
                                        "Items":[1],
                                        "IsAppend":false
                                    },
                                    "Node":{
                                        "Type":"ValCell",
                                        "Val":"00"
                                    }
                                },
                                {
                                    "Path":{
                                        "Items":[1],
                                        "IsAppend":false
                                    },
                                    "Node":{
                                        "Type":"ValCell",
                                        "Val":"01"
                                    }
                                }
                            ],
                            "MtxKind":0
                        }
                    }
                }
            ],
            "MtxKind":0
        }
    }
    """;

    [Fact]
    public static void ArrayOfArrays_AstToJson()
    {
        var ast = JsonSerializer.Deserialize<AstNode>(
            desiredAst,
            LibJmon.JsonSerialization.Resources.JsonSerializerOptions
        )!;
        
        var parsedVal = LibJmon.TestingApi.AstToJson(ast);
        JsonArray? arr = parsedVal.V?.AsArray();
        Assert.Equal(1, arr?.Count);
        JsonArray? innerArr0 = arr?[0]?.AsArray();
        Assert.Equal(2, innerArr0?.Count);
        Assert.Equal("00", (string?)innerArr0?[0]);
        Assert.Equal("01", (string?)innerArr0?[1]);
    }
}