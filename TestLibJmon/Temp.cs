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
            Encoder = JavaScriptEncoder.Default,
            IncludeFields = true,
        };

//        foreach (var converter in LibJmon.JsonConverters.All)
//        {
//            o.Converters.Add(converter);
//        }
        
        o.Converters.Add(new JsonAny_Converter());
        o.Converters.Add(new LexedPath_Converter());
        o.Converters.Add(new LexedCell_Path_Converter());
        o.Converters.Add(new LexedCell_JVal_Converter());
        o.Converters.Add(new LexedCell_Error_Converter());
        o.Converters.Add(new ConvertedPath_Converter());
        o.Converters.Add(new AstNode_Leaf_Converter());
        o.Converters.Add(new AstNode_Branch_Converter());
        o.Converters.Add(new AstNode_Error_Converter());
        o.Converters.Add(new JsonStrConverter());
        o.Converters.Add(new InputPathElmtConverter());
        o.Converters.Add(new ConvertedPathElmtConverter());
        
        o.Converters.Add(new LexedCellConverter());
        o.Converters.Add(new JsonValConverter());
        o.Converters.Add(new AstNodeConverter());

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

    static (string jsonCode, JsonVal.Str jsonStr) MakeJsonStr() => ("\"a\"", new("a"u8.ToImmutableArray()));

    static (string jsonCode, JsonVal.Any jsonAny) MakeJsonAny() =>
    (
        "{\"a\":1,\"b\":null}",
        new JsonVal.Any(JsonNode.Parse("{\"a\":1,\"b\":null}"))
    );

    static (string jsonCode, InputPathElmt.ArrElmt arrElmt) MakeInputPathArrElmt() =>
        ("1", new InputPathElmt.ArrElmt(ArrElmtKind.Plus));

    static (string jsonCode, InputPathElmt.Key keyElmt) MakeInputPathKeyElmt()
    {
        var (code, str) = MakeJsonStr();
        return (code, new InputPathElmt.Key(str));
    }

    static (string jsonCode, LexedPath lexedPath) MakeLexedPath()
    {
        var t0 = MakeInputPathArrElmt();
        var t1 = MakeInputPathKeyElmt();
        return ($"[{t0.jsonCode},{t1.jsonCode}]", new(ImmutableArray.Create(t0.arrElmt as InputPathElmt, t1.keyElmt)));
    }

    static (string jsonCode, AstNode.Leaf nodeLeaf) MakeAstNodeLeaf()
    {
        var (expSerialized, jsonAny) = MakeJsonAny();
        return ($@"{{""Type"":""Leaf"",""Val"":{expSerialized}}}", jsonAny);
    }

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

    [Fact]
    static void JsonStrSerializes()
    {
        var strObj = new JsonVal.Str("&<>"u8.ToImmutableArray());
        var jsonCode = "\"" + JavaScriptEncoder.Default.Encode("&<>") + "\"";
        var serialized = JsonSerializer.Serialize(strObj, TestUtil.GetJsonOptions());
        Assert.Equal(jsonCode, serialized);
        var deserialized = JsonSerializer.Deserialize<JsonVal.Str>(serialized, TestUtil.GetJsonOptions());
        Assert.Equal(strObj, deserialized);
    }
    
    [Fact]
    static void JsonAnyAsJsonValSerializes()
    {
        (string jsonCode, JsonVal jsonAny) = MakeJsonAny();
        jsonCode = $@"{{""Type"":""Any"",""Val"":{jsonCode}}}";
        var serialized = JsonSerializer.Serialize(jsonAny, TestUtil.GetJsonOptions());
        Assert.Equal(jsonCode, serialized);
        var deserialized = JsonSerializer.Deserialize<JsonVal>(serialized, TestUtil.GetJsonOptions());
        Assert.Equal(jsonAny, deserialized);
    }

    [Fact]
    static void JsonStrAsJsonValSerializes()
    {
        (string jsonCode, JsonVal jsonStr) = MakeJsonStr();
        jsonCode = $@"{{""Type"":""Str"",""Val"":{jsonCode}}}";
        var serialized = JsonSerializer.Serialize(jsonStr, TestUtil.GetJsonOptions());
        Assert.Equal(jsonCode, serialized);
        var deserialized = JsonSerializer.Deserialize<JsonVal>(serialized, TestUtil.GetJsonOptions());
        Assert.Equal(jsonStr, deserialized);
    }
    
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
    static void LexedCellBlankSerializes()
    {
        LexedCell cell = new LexedCell.Blank();
        var serialized = JsonSerializer.Serialize(cell, TestUtil.GetJsonOptions());
        Assert.Equal(@"{""Type"":""Blank"",""Val"":{}}", serialized);
        var deserialized = JsonSerializer.Deserialize<LexedCell.Blank>(serialized, TestUtil.GetJsonOptions());
        Assert.Equal(cell, deserialized);
    }
    
    [Fact]
    static void LexedCellPathSerializes()
    {
        var (expJson, path) = MakeLexedPath();
        expJson = $@"{{""Type"":""Path"",""Val"":{expJson}}}";
        LexedCell cell = new LexedCell.Path(path);
        var serialized = JsonSerializer.Serialize(cell, TestUtil.GetJsonOptions());
        Assert.Equal(expJson, serialized);
        var deserialized = JsonSerializer.Deserialize<LexedCell>(serialized, TestUtil.GetJsonOptions());
        Assert.Equal(cell, deserialized);
    }
    
    [Fact]
    static void LexedCellJValSerializes()
    {
        var (jsonAnyCode, jsonAny) = MakeJsonAny();
        LexedCell header = new LexedCell.JVal(jsonAny);
        var serialized = JsonSerializer.Serialize(header, TestUtil.GetJsonOptions());
        var expJsonCode = @$"{{""Type"":""JVal"",""Val"":{jsonAnyCode}}}";
        Assert.Equal(expJsonCode, serialized);
        var deserialized = JsonSerializer.Deserialize<LexedCell>(serialized, TestUtil.GetJsonOptions());
        Assert.Equal(header, deserialized);
    }
    
    [Fact]
    static void LexedCellMtxHeadSerializes()
    {
        LexedCell header = new LexedCell.MtxHead(MtxKind.Arr, false);
        var innerJson = @"{""Kind"":0,""IsTp"":false}";
        var expJsonCode = @$"{{""Type"":""MtxHead"",""Val"":{innerJson}}}";
        var serialized = JsonSerializer.Serialize(header, TestUtil.GetJsonOptions());
        Assert.Equal(expJsonCode, serialized);
        var deserialized = JsonSerializer.Deserialize<LexedCell>(serialized, TestUtil.GetJsonOptions());
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
    
    [Fact]
    static void AstNodeLeafSerializes()
    {
        (string expJson, AstNode leaf) = MakeAstNodeLeaf();
        var serialized = JsonSerializer.Serialize(leaf, TestUtil.GetJsonOptions());
        Assert.Equal(expJson, serialized);
        var deserialized = JsonSerializer.Deserialize<AstNode>(serialized, TestUtil.GetJsonOptions());
        Assert.Equal(leaf, deserialized);
    }

    [Fact]
    static void AstResultNodeBranchSerializes()
    {
        var (expLeafSzd, leaf) = MakeAstNodeLeaf();
        var (expPathSzd, path) = MakeLexedPath();
        AstNode.Branch.Item item = new(path, leaf);
        AstNode branch = new AstNode.Branch(ImmutableArray.Create(item));
        var serialized = JsonSerializer.Serialize(branch, TestUtil.GetJsonOptions());
        var expInner = $@"[{{""Path"":{expPathSzd},""Result"":{expLeafSzd}}}]";
        var expJson = @$"{{""Type"":""Branch"",""Val"":{expInner}}}";
        Assert.Equal(expJson, serialized);
        var deserialized = JsonSerializer.Deserialize<AstNode>(serialized, TestUtil.GetJsonOptions());
        Assert.Equal(branch, deserialized);
    }

    [Fact]
    static void AstResultErrorStrayCellSerializes()
    {
        var errMsg = "Error message";
        var jsonCode = @$"{{""Type"":""Error"",""Val"":""{errMsg}""}}";
        AstNode err = new AstNode.Error(errMsg);
        var serialized = JsonSerializer.Serialize(err, TestUtil.GetJsonOptions());
        Assert.Equal(jsonCode, serialized);
        var deserialized = JsonSerializer.Deserialize<AstNode>(serialized, TestUtil.GetJsonOptions());
        Assert.Equal(err, deserialized);
    }
}