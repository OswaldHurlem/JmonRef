using System.Collections;
using System.Collections.Immutable;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using LibJmon;
using LibJmon.Impl;

namespace TestLibJmon;

public static class TestUtil
{
    public static JsonSerializerOptions GetJsonOptions()
    {
        var o = new JsonSerializerOptions
        {
            // WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            IncludeFields = true,
        };

//        foreach (var converter in LibJmon.JsonConverters.All)
//        {
//            o.Converters.Add(converter);
//        }
        
        o.Converters.Add(new JsonAny_Converter());
        o.Converters.Add(new LexedPath_Converter());
        o.Converters.Add(new LexedCell_Path_Converter());
        o.Converters.Add(new LexedCell_Header_Val_Converter());
        o.Converters.Add(new ConvertedPath_Converter());
        o.Converters.Add(new AstResult_Node_Leaf_Converter());
        o.Converters.Add(new AstResult_Node_Branch_Converter());
        o.Converters.Add(new JsonStrConverter());
        o.Converters.Add(new InputPathElmtConverter());
        o.Converters.Add(new ConvertedPathElmtConverter());
        o.Converters.Add(new JsonValConverter());
        o.Converters.Add(new LexedCellConverter());
        o.Converters.Add(new HeaderConverter());
        o.Converters.Add(new AstResultConverter());
        o.Converters.Add(new NodeConverter());
        o.Converters.Add(new ErrorConverter());

        return o;
    }
}

public static class Temp
{
    //[Fact]
    //static void TestAssignments()
    //{
    //    var grid = new[,]
    //    {
    //        { ""    , ".c"     , ".d.+"      , ".d.$"       },
    //        { ".a"  , ".A.C"   , ".A.D[0]"   , ".A.D[0]"    },
    //        { ".b.+", ".B[0].C", ".B[0].D[0]", ".B[0].D[0]" },
    //        { ".b.+", ".B[1].C", ".B[1].D[0]", ".B[1].D[0]" },
    //    };
    //
    //    var expPaths = new[]
    //    {
    //        ".a.c", ".a.d[0]", ".a.d[0]", ".b[0].c", ".b[0].d[0]",
    //        ".b[0].d[0]", ".b[1].c", ".b[1].d[0]", ".b[1].d[0]"
    //    };
    //    
    //    var expVals = new[]
    //    {
    //        ".A.C", ".A.D[0]", ".A.D[0]", ".B[0].C", ".B[0].D[0]",
    //        ".B[0].D[0]", ".B[1].C", ".B[1].D[0]", ".B[1].D[0]"
    //    };
    //    
    //    var assignments = LibJmon.Impl.TEMP.JQAssignmentsFromSimpleMatrix(grid).ToList();
    //    var actualPaths = assignments.Select(t => t.path).ToArray();
    //    var actualVals = assignments.Select(t => t.val).ToArray();
    //    Assert.Equal(expPaths, actualPaths);
    //    Assert.Equal(expVals, actualVals);
    //}

    // [Fact]
    // static void TestParseEmpty()
    // {
    //     var lexedCells = new LexedCell[,] { { } };
    //     var 
    // }
    /*[Fact]
    static void TestParseSimple()
    {
        var mtxHeader = new LexedCell.Header.Mtx(MtxKind.Obj, false);
        var pathA = new LexedPath(new[] { new InputPathElmt.Key(new JsonVal.Str("a")) });
        var pathB = new LexedPath(new[] { new InputPathElmt.Key(new JsonVal.Str("b")) });
        var pathACell = new LexedCell.Path(pathA);
        var pathBCell = new LexedCell.Path(pathB);
        var valCell = new LexedCell.Header.Val(new JsonVal.Str("h"));

        var lexedCells = new LexedCell[,]
        {
            { mtxHeader, pathACell },
            { pathBCell, valCell },
        };
        
        var subSheet = new LexedSubSheet(lexedCells, (0, 0), false);
        var actual = Logic.ParseJmon(subSheet).Match(result => result, _ => null) as AstResult.Node.Branch;
        Assert.NotNull(actual);
        Assert.IsType<AstResult.Node.Branch>(actual);
        
        var astVal = new AstResult.Node.Leaf(valCell.V);
        var branchA = new AstResult.Node.Branch(new[] { new AstResult.Node.Branch.Item(pathA, astVal) }.ToList());
        AstResult expected = new AstResult.Node.Branch(
            new[] { new AstResult.Node.Branch.Item(pathB, branchA)
            }.ToList());

        var comparer = new AstResultComparer();
        Assert.True(comparer.Equals(actual, expected));

        //var actualJson = LibJmon.JsonSerialization.Serialize(actual);
        //var expectedJson = LibJmon.JsonSerialization.Serialize(expected);
        //
        //Assert.Equivalent(actualJson, expectedJson);
    }*/

    static JsonVal.Str MakeJsonStr() => new("a"u8.ToImmutableArray());

    static (string jsonCode, JsonVal.Any jsonAny) MakeJsonAny() =>
    (
        "{\"a\":1,\"b\":null}",
        new JsonVal.Any(JsonNode.Parse("{\"a\":1,\"b\":null}"))
    );

    static InputPathElmt.ArrElmt MakeInputPathArrElmt() => new(ArrElmtKind.Plus);
    static InputPathElmt.Key MakeInputPathKeyElmt() => new(MakeJsonStr());

    static (string expSerialized, LexedPath lexedPath) MakeLexedPath() =>
    (
        "[1,\"a\"]",
        new(ImmutableArray.Create(MakeInputPathArrElmt() as InputPathElmt, MakeInputPathKeyElmt()))
    );
    
    // TODO can remove
    [Fact]
    static void JsonAnySerializes()
    {
        var (jsonCode, jsonAny) = MakeJsonAny();
        var serialized = JsonSerializer.Serialize(jsonAny, TestUtil.GetJsonOptions());
        Assert.Equal(jsonCode, serialized);
        var deserialized = JsonSerializer.Deserialize<JsonVal.Any>(serialized, TestUtil.GetJsonOptions());
        Assert.Equal(jsonAny, deserialized);
    }
    
    // TODO can remove
    [Fact]
    static void LexedPathSerializes()
    {
        var (expSerialized, path) = MakeLexedPath();
        var serialized = JsonSerializer.Serialize(path, TestUtil.GetJsonOptions());
        Assert.Equal(expSerialized, serialized);
        var deserialized = JsonSerializer.Deserialize<LexedPath>(serialized, TestUtil.GetJsonOptions());
        Assert.Equal(path, deserialized);
    }

    [Fact]
    static void LexedCellPathSerializes()
    {
        var (expSerialized, path) = MakeLexedPath();
        LexedCell.Path cell = new LexedCell.Path(path);
        var serialized = JsonSerializer.Serialize(cell, TestUtil.GetJsonOptions());
        Assert.Equal(expSerialized, serialized);
        var deserialized = JsonSerializer.Deserialize<LexedCell.Path>(serialized, TestUtil.GetJsonOptions());
        Assert.Equal(cell, deserialized);
    }

    // TODO test for Mtx Header
    [Fact]
    static void LexedCellHeaderValSerializes()
    {
        var (jsonAnyCode, jsonAny) = MakeJsonAny();
        LexedCell.Header header = new LexedCell.Header.Val(jsonAny);
        var serialized = JsonSerializer.Serialize(header, TestUtil.GetJsonOptions());
        var expJsonCode = @$"{{""Type"":""Val"",""Val"":{jsonAnyCode}}}";
        Assert.Equal(expJsonCode, serialized);
        var deserialized = JsonSerializer.Deserialize<LexedCell.Header>(serialized, TestUtil.GetJsonOptions());
        Assert.Equal(header, deserialized);
    }

    [Fact]
    static void ConvertedPathSerializes()
    {
        ConvertedPathElmt a = new ConvertedPathElmt.Key("a"u8.ToImmutableArray());
        ConvertedPathElmt i = new ConvertedPathElmt.Idx(0);
        ConvertedPath path = new(ImmutableArray.Create(a, i));
        var serialized = JsonSerializer.Serialize(path, TestUtil.GetJsonOptions());
        Assert.Equal("[\"a\",0]", serialized);
        var deserialized = JsonSerializer.Deserialize<ConvertedPath>(serialized, TestUtil.GetJsonOptions());
        Assert.Equal(path, deserialized);
    }
    
    // AstResult.Node.Leaf

    static (string expSerialized, AstResult.Node.Leaf leaf) MakeAstResultNodeLeaf()
    {
        var (expSerialized, jsonAny) = MakeJsonAny();
        AstResult.Node.Leaf leaf = new(jsonAny);
        return (expSerialized, jsonAny);
    }

    [Fact]
    static void AstResultNodeBranchSerializes()
    {
        var (expLeafSzd, leaf) = MakeAstResultNodeLeaf();
        var (expPathSzd, path) = MakeLexedPath();
        AstResult.Node.Branch.Item item = new(path, leaf);
        AstResult.Node.Branch branch = new(ImmutableArray.Create(item));
        var serialized = JsonSerializer.Serialize(branch, TestUtil.GetJsonOptions());
        var leafJson = $@"{{""Type"":""Node"",""Val"":{{""Type"":""Leaf"",""Val"":{expLeafSzd}}}}}";
        var expJson = $@"[{{""Path"":{expPathSzd},""Result"":{leafJson}}}]";
        Assert.Equal(expJson, serialized);
        var deserialized = JsonSerializer.Deserialize<AstResult.Node.Branch>(serialized, TestUtil.GetJsonOptions());
        Assert.Equal(branch, deserialized);
    }
    
    // JsonVal.Str
    
    // InputPathElmt
    
    // ConvertedPathElmt
    
    // TODO: JsonVal
    
    // TODO: LexedCell
    
    // TODO: LexedCell.Header
    
    // TODO: AstResult
    
    // TODO: AstResult.Node
    
    // TODO: AstResult.Error

    [Fact]
    static void JsonStrSerializes()
    {
        var jsonStr = MakeJsonStr();
        var serialized = JsonSerializer.Serialize(jsonStr, TestUtil.GetJsonOptions());
        Assert.Equal("\"a\"", serialized);
        var deserialized = JsonSerializer.Deserialize<JsonVal.Str>(serialized, TestUtil.GetJsonOptions());
        Assert.Equal(jsonStr, deserialized);
    }

    // TODO can remove
    [Fact]
    static void InputPathKeyElmtSerializes()
    {
        InputPathElmt key = new InputPathElmt.Key("a"u8.ToImmutableArray());
        var serialized = JsonSerializer.Serialize(key, TestUtil.GetJsonOptions());
        Assert.Equal("\"a\"", serialized);
        var deserialized = JsonSerializer.Deserialize<InputPathElmt>(serialized, TestUtil.GetJsonOptions());
        Assert.Equal(key, deserialized);
    }
    
    // TODO can remove
    [Fact]
    static void InputPathArrElmtSerializes()
    {
        InputPathElmt arrElmt = new InputPathElmt.ArrElmt(ArrElmtKind.Plus);
        var serialized = JsonSerializer.Serialize(arrElmt, TestUtil.GetJsonOptions());
        Assert.Equal("1", serialized);
        var deserialized = JsonSerializer.Deserialize<InputPathElmt>(serialized, TestUtil.GetJsonOptions());
        Assert.Equal(arrElmt, deserialized);
    }
}