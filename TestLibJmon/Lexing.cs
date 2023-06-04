/*using System.Text;
using System.Text.Json;
using LibJmon;
using LibJmon.JsonSerialization;
using LibJmon.SuperTypes;
using LibJmon.Types;

namespace TestLibJmon;

public static class Lexing
{
    private const string openBracketErr =
        "Expected depth to be zero at the end of the JSON payload. " +
        "There is an open JSON object or array that should be closed. " +
        "Path: $ | LineNumber: 0 | BytePositionInLine: 1.";
    [Theory]
    [InlineData(""" :[            """, """ {"Type":"MtxHead","Val":{"Kind":0,"IsTp":false}}           """)]
    [InlineData(""" :^[           """, """ {"Type":"MtxHead","Val":{"Kind":0,"IsTp":true}}            """)]
    [InlineData(""" :{            """, """ {"Type":"MtxHead","Val":{"Kind":1,"IsTp":false}}           """)]
    [InlineData(""" :^{           """, """ {"Type":"MtxHead","Val":{"Kind":1,"IsTp":true}}            """)]
    [InlineData(""" .             """, """ {"Type":"Path","Val":{"Items":[],"IsAppend":false}}        """)]
    [InlineData(""" .a            """, """ {"Type":"Path","Val":{"Items":["a"],"IsAppend":false}}     """)]
    [InlineData(""" .+            """, """ {"Type":"Path","Val":{"Items":[1],"IsAppend":false}}       """)]
    [InlineData(""" .$            """, """ {"Type":"Path","Val":{"Items":[0],"IsAppend":false}}       """)]
    [InlineData(""" .$.+.a        """, """ {"Type":"Path","Val":{"Items":[0,1,"a"],"IsAppend":false}} """)]
    [InlineData(""" .a.+*         """, """ {"Type":"Path","Val":{"Items":["a"],"IsAppend":true}}      """)]
    [InlineData(""" .'a'          """, """ {"Type":"Path","Val":{"Items":["a"],"IsAppend":false}}     """)]
    [InlineData(""" .$.+."a"      """, """ {"Type":"Path","Val":{"Items":[0,1,"a"],"IsAppend":false}} """)]
    [InlineData(""" i'm cool      """, """ {"Type":"JVal","Val":"i\u0027m cool"}    """)]
    [InlineData("""  "            """, """ {"Type":"JVal","Val":"\u0022"}           """)]
    [InlineData(""" :::["a","b"]  """, """ {"Type":"JVal","Val":["a","b"]}          """)]
    [InlineData(""" ::: " blah "  """, """ {"Type":"JVal","Val":" blah "}           """)]
    [InlineData(""" :::"\n\t\""   """, """ {"Type":"JVal","Val":"\n\t\u0022"}       """)]
    [InlineData(""" :::null       """, """ {"Type":"JVal","Val":null}               """)]
    [InlineData(""" ::'"JMON"'    """, """ {"Type":"JVal","Val":"\u0022JMON\u0022"} """)]
    [InlineData(""" ::'i\'m cool' """, """ {"Type":"JVal","Val":"i\u0027m cool"}    """)]
    [InlineData("""               """, """ {"Type":"Blank","Val":{}}                """)]
    [InlineData(""" // comment    """, """ {"Type":"Blank","Val":{}}                """)]
    public static void CellLexesTo(string cellText, string expJson)
    {
        string[,] grid = { { cellText } };
        LexedCell[,] lexedCells = TestingApi.LexCells(grid);
        var json = JsonSerializer.Serialize(lexedCells[0, 0], Resources.JsonSerializerOptions);
        Assert.Equal(expJson.Trim(), json);
    }

    // [Theory]
    // [InlineData(""" :::[   """)]
    // [InlineData(""" :::'a' """)]
    // [InlineData(""" ::"a"   """)]
    // [InlineData(""" .. """)]
    // [InlineData(""" .a. """)]
    // [InlineData(""" . .a """)]
    // [InlineData(""" .- """)]
    // [InlineData(""" : """)]
    // public static void ErrorWhenLexing(string cellText)
    // {
    //     string[,] grid = { { cellText } };
    //     LexedCell[,] lexedCells = TestingApi.LexCells(grid);
    //     Assert.IsType<LexedCell.Error>(lexedCells[0,0]);
    // }

    [Theory]
    [InlineData(""" .foo. 'bar' ."b a z".+.$ """)]
    public static void PathLexes(string pathExpr)
    {
        var path = LibJmon.Impl.Lexing.LexPath(pathExpr.Trim());
        string key0 = path.Items[0].AsOneOf().Match(key => key.V.V, i => "");
        string key1 = path.Items[1].AsOneOf().Match(key => key.V.V, i => "");
        string key2 = path.Items[2].AsOneOf().Match(key => key.V.V, i => "");
        int idx0 = path.Items[3].AsOneOf().Match(key => -1, idx => idx.V);
        int idx1 = path.Items[4].AsOneOf().Match(key => -1, idx => idx.V);
        
        Assert.Equal("foo", key0);
        Assert.Equal("bar", key1);
        Assert.Equal("b a z", key2);
        Assert.Equal(1, idx0);
        Assert.Equal(0, idx1);
    }
}*/