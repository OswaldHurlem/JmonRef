using System.Text.Json;
using System.Text.Json.Nodes;
using LibJmon;
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

        string[,] cells = CsvUtil.CsvToCells(aoaCsv, ",");
        string json = ApiV0.ParseJmon(cells, new());

        const string expJson = """[["00","01"]]""";
        Assert.Equal(expJson, json);
    }

    [Fact]
    public static void AppendObjFromLiteral()
    {
        var cells = new string[,]
        {
            { ":{" , "."         },
            { ".a" , "::1"       },
            { ".+*", "::{'b':2}" }
        };

        string expJson = """{"a":1,"b":2}""";
        string json = ApiV0.ParseJmon(cells, new());
        
        Assert.Equal(expJson, json);
    }
}