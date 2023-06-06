using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using LibJmon;
using LibJmon.SuperTypes;
using LibJmon.Types;

namespace TestLibJmon;

public static class Lexing
{
    private static JsonSerializerOptions MakeJsonOptions() =>
        new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    
    [Theory]
    [InlineData(""" . a . b    """, """ {"a":{"b":1}}     """)]
    [InlineData(""" .' a '.b   """, """ {" a ":{"b":1}}   """)]
    [InlineData(""" ."\"a\"".b """, """ {"\"a\"":{"b":1}} """)]
    [InlineData(""" .a.+       """, """ {"a":[1]}         """)]
    public static void KeyPaths(string path, string expJson)
    {
        //path = path.Trim();
        expJson = expJson.Trim();
        
        var jmonCells = new string[,]
        {
            { ":{", "."   },
            { path, "::1" }
        };

        var json = ApiV0.ParseJmon(jmonCells, MakeJsonOptions());
        Assert.Equal(expJson, json);
    }

    [Theory]
    [InlineData(""" .+    """, """ [[1],[2]] """)]
    [InlineData(""" .$.+  """, """ [[1,[2]]] """)]
    [InlineData(""" .$.+* """, """ [[1,2]] """)]
    [InlineData(""" .+*   """, """ [[1],2] """)]
    public static void ArrayAndAppendPaths(string path, string expJson)
    {
        // path = path.Trim();
        expJson = expJson.Trim();
        
        var jmonCells = new string[,]
        {
            { ":[",   "."     },
            { ".+.+", "::1"   },
            { path,   "::[2]" }
        };
        
        var json = ApiV0.ParseJmon(jmonCells, MakeJsonOptions());
        Assert.Equal(expJson, json);
    }

    [Theory]
    [InlineData(""" :[            """, """ []         """)]
    [InlineData(""" :^[           """, """ []         """)]
    [InlineData(""" :{            """, """ {}         """)]
    [InlineData(""" :^{           """, """ {}         """)]
    [InlineData(""" i'm cool      """, """ "i'm cool" """)]
    [InlineData("""  "            """, """ "\""       """)]
    [InlineData(""" ::: " blah "  """, """ " blah "   """)]
    [InlineData(""" :::"\n\t\""   """, """ "\n\t\""   """)]
    [InlineData(""" :::null       """, """ null       """)]
    [InlineData(""" ::'"JMON"'    """, """ "\"JMON\"" """)]
    [InlineData(""" ::'i\'m cool' """, """ "i'm cool" """)]
    public static void LexValue(string cellText, string expJson)
    {
        // cellText = cellText.Trim();
        expJson = expJson.Trim();
        
        var cells = new string[,] { { cellText } };
        var json = ApiV0.ParseJmon(cells, MakeJsonOptions());
        Assert.Equal(expJson, json);
    }
    
    /*

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
    */
}