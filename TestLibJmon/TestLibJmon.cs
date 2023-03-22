/*using LibJmon.Values;
using static LibJmon.ApiV0;
namespace TestLibJmon;

public class TestLibJmon
{
    static JsonFromJmon_Options DefOpts =>
        JsonFromJmon_Options.Default;
    
    [Theory]
    [InlineData("::0"    , "0"    ,   false)]
    [InlineData(":["     , "[]"   ,   false)]
    [InlineData(":{"     , "{}"   ,   false)]
    [InlineData("a"      , "\"a\"",   true )]
    [InlineData("::\"a\"", "\"a\"",   true )]
    void TestSingleCell(string singleCell, string expJson, bool expTypeIsStr)
    {
        var grid = new[,]
        {
            { "", "", "" },
            { "", "", "" },
            { "", "", "" },
        };
        
        grid[1,1] = singleCell;
        var jsonVal = JsonFromJmon(new JmonSheet(grid), DefOpts);
        Assert.Equal(expJson, jsonVal.Text);
        Assert.Equal(expTypeIsStr, jsonVal is JsonStr);
    }

    [Fact]
    void TestArray()
    {
        var grid = new[,]
        {
            { ":[", "" , "" },
            { ""  , "a", "" },
            { ""  , "b", "" },
        };
        
        var jsonVal = JsonFromJmon(new(grid), DefOpts);
        Assert.Equal("[\"a\",\"b\"]", jsonVal.Text);
    }
    
    [Fact]
    void TestObject()
    {
        var grid = new[,]
        {
            { ":{", "" ,  ""  },
            { ""  , "a",  "b" },
            { ""  , "c",  "d" },
        };
        
        var jsonVal = JsonFromJmon(new(grid), DefOpts);
        var jsonText = """"{"a":"b","c":"d"}"""";
        Assert.Equal(jsonText, jsonVal.Text);
    }

    [Fact]
    void TestArrayOfArrays()
    {
        var grid = new[,]
        {
            { ":[[", ""   , ""   , ""    },
            { ""   , "e00", "e01", "e02" },
            { ""   , "e10", ""   , ""    },
            { ""   , "e20", "e21", ""    },
        };
        
        var jsonVal = JsonFromJmon(new(grid), DefOpts);
        var jsonText = """[["e00","e01","e02"],["e10"],["e20","e21"]]""";
        Assert.Equal(jsonText, jsonVal.Text);
    }
    
    [Fact]
    void TestObjectOfArrays()
    {
        var grid = new[,]
        {
            { ":{[", ""  , ""   , ""    },
            { ""   , "k0", "v00", "v01" },
            { ""   , "k1", ""   , ""    },
            { ""   , "k2", "v20", ""    },
        };
        
        var jsonVal = JsonFromJmon(new(grid), DefOpts);
        var jsonText = """{"k0":["v00","v01"],"k1":[],"k2":["v20"]}""";
        Assert.Equal(jsonText, jsonVal.Text);
    }
}*/