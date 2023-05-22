using System.Text;
using System.Text.Json;
using LibJmon;
using LibJmon.JsonSerialization;
using LibJmon.Types;

namespace TestLibJmon;

public static class Lexing
{
    private const string openBracketErr =
        "Expected depth to be zero at the end of the JSON payload. " +
        "There is an open JSON object or array that should be closed. " +
        "Path: $ | LineNumber: 0 | BytePositionInLine: 1.";
    // {"Items":[],IsAppend:false}}
    [Theory]
    [InlineData(""" :[                          """, """ {"Type":"MtxHead","Val":{"Kind":0,"IsTp":false}}          """)]
    [InlineData(""" :^[                         """, """ {"Type":"MtxHead","Val":{"Kind":0,"IsTp":true}}           """)]
    [InlineData(""" :{                          """, """ {"Type":"MtxHead","Val":{"Kind":1,"IsTp":false}}          """)]
    [InlineData(""" :^{                         """, """ {"Type":"MtxHead","Val":{"Kind":1,"IsTp":true}}           """)]
    [InlineData(""" .                           """, """ {"Type":"Path","Val":{"Items":[],"IsAppend":false}}       """)]
    [InlineData(""" .a                          """, """ {"Type":"Path","Val":{"Items":["a"],"IsAppend":false}}    """)]
    [InlineData(""" .+                          """, """ {"Type":"Path","Val":{"Items":[1],"IsAppend":false}}      """)]
    [InlineData(""" .$                          """, """ {"Type":"Path","Val":{"Items":[0],"IsAppend":false}}      """)]
    [InlineData(""" .$.+.a                      """, """ {"Type":"Path","Val":{"Items":[0,1,"a"],"IsAppend":false}}""")]
    [InlineData(""" .:'a'                       """, """ {"Type":"Path","Val":{"Items":["a"],"IsAppend":false}}    """)]
    [InlineData(""" .::"a"                      """, """ {"Type":"Path","Val":{"Items":["a"],"IsAppend":false}}    """)]
    [InlineData(""" i'm cool                    """, """ {"Type":"JVal","Val":"i\u0027m cool"}            """)]
    [InlineData("""  "                          """, """ {"Type":"JVal","Val":"\u0022"}                   """)]
    [InlineData(""" :::["a","b"]                """, """ {"Type":"JVal","Val":["a","b"]}                  """)]
    [InlineData(""" ::: " blah "                """, """ {"Type":"JVal","Val":" blah "}                   """)]
    [InlineData(""" :::"\n\t\""                 """, """ {"Type":"JVal","Val":"\n\t\u0022"}               """)]
    [InlineData(""" :::null                     """, """ {"Type":"JVal","Val":null}                       """)]
    [InlineData(""" ::'"JMON"'                  """, """ {"Type":"JVal","Val":"\u0022JMON\u0022"}         """)]
    [InlineData(""" ::'i\'m cool'               """, """ {"Type":"JVal","Val":"i\u0027m cool"}            """)]
    [InlineData("""                             """, """ {"Type":"Blank","Val":{}}                        """)]
    [InlineData(""" // comment                  """, """ {"Type":"Blank","Val":{}}                        """)]
    
    // TODO fix
    /*[InlineData(
        """.:{"Items":[0,1,'a'],"IsAppend":false}""",
        """{"Type":"Path","Val":[0,1,"a"]}"""
    )]*/
    public static void CellLexesTo(string cellText, string expJson)
    {
        ReadOnlyMemory<byte>[,] grid = { { Encoding.UTF8.GetBytes(cellText).AsMemory() } };
        LexedCell[,] lexedCells = TestingApi.LexCells(grid);
        var json = JsonSerializer.Serialize(lexedCells[0, 0], Resources.JsonSerializerOptions);
        Assert.Equal(expJson.Trim(), json);
    }

    [Theory]
    [InlineData(""" :::[   """)]
    [InlineData(""" :::'a' """)]
    [InlineData(""" ::"a"   """)]
    [InlineData(""" .. """)]
    [InlineData(""" .a. """)]
    [InlineData(""" . .a """)]
    [InlineData(""" .- """)]
    [InlineData(""" : """)]
    public static void ErrorWhenLexing(string cellText)
    {
        ReadOnlyMemory<byte>[,] grid = { { Encoding.UTF8.GetBytes(cellText).AsMemory() } };
        LexedCell[,] lexedCells = TestingApi.LexCells(grid);
        Assert.IsType<LexedCell.Error>(lexedCells[0,0]);
    }
}