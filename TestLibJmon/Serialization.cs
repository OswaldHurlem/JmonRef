using System.Collections.Immutable;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using LibJmon;
using LibJmon.JsonSerialization;
using LibJmon.Types;

namespace TestLibJmon;

public static class Serialization
{
    public static JsonSerializerOptions GetJsonOptions() => Resources.JsonSerializerOptions;
    
    private static (string jsonCode, JsonVal.Str jsonStr) MakeJsonStr() => ("\"a\"", new("a"u8.ToImmutableArray()));

    private static (string jsonCode, JsonVal.Any jsonAny) MakeJsonAny() =>
    (
        "{\"a\":1,\"b\":null}",
        new JsonVal.Any(JsonNode.Parse("{\"a\":1,\"b\":null}"))
    );

    private static (string jsonCode, PathItem.Idx arrElmt) MakePathIdx() =>
        ("1", new PathItem.Idx(1));

    private static (string jsonCode, PathItem.Key keyElmt) MakePathKey()
    {
        var (code, str) = MakeJsonStr();
        return (code, new PathItem.Key(str));
    }

    private static (string jsonCode, LexedPath lexedPath) MakeLexedPath()
    {
        var t0 = MakePathIdx();
        var t1 = MakePathKey();
        return ($"[{t0.jsonCode},{t1.jsonCode}]", new(ImmutableArray.Create(t0.arrElmt as PathItem, t1.keyElmt)));
    }

    private static (string jsonCode, AstNode.Leaf nodeLeaf) MakeAstNodeLeaf()
    {
        var (expSerialized, jsonAny) = MakeJsonAny();
        return ($@"{{""Type"":""Leaf"",""Val"":{expSerialized}}}", jsonAny);
    }

    [Fact]
    private static void JsonAnySerializes()
    {
        var (jsonCode, jsonAny) = MakeJsonAny();
        var serialized = JsonSerializer.Serialize(jsonAny, GetJsonOptions());
        Assert.Equal(jsonCode, serialized);
        var deserialized = JsonSerializer.Deserialize<JsonVal.Any>(serialized, GetJsonOptions());
        Assert.Equal(jsonAny, deserialized);
    }

    [Fact]
    private static void JsonStrSerializes()
    {
        var strObj = new JsonVal.Str("&<>"u8.ToImmutableArray());
        var jsonCode = "\"" + JavaScriptEncoder.Default.Encode("&<>") + "\"";
        var serialized = JsonSerializer.Serialize(strObj, GetJsonOptions());
        Assert.Equal(jsonCode, serialized);
        var deserialized = JsonSerializer.Deserialize<JsonVal.Str>(serialized, GetJsonOptions());
        Assert.Equal(strObj, deserialized);
    }

    [Fact]
    private static void JsonAnyAsJsonValSerializes()
    {
        (string jsonCode, JsonVal jsonAny) = MakeJsonAny();
        jsonCode = $@"{{""Type"":""Any"",""Val"":{jsonCode}}}";
        var serialized = JsonSerializer.Serialize(jsonAny, GetJsonOptions());
        Assert.Equal(jsonCode, serialized);
        var deserialized = JsonSerializer.Deserialize<JsonVal>(serialized, GetJsonOptions());
        Assert.Equal(jsonAny, deserialized);
    }

    [Fact]
    private static void JsonStrAsJsonValSerializes()
    {
        (string jsonCode, JsonVal jsonStr) = MakeJsonStr();
        jsonCode = $@"{{""Type"":""Str"",""Val"":{jsonCode}}}";
        var serialized = JsonSerializer.Serialize(jsonStr, GetJsonOptions());
        Assert.Equal(jsonCode, serialized);
        var deserialized = JsonSerializer.Deserialize<JsonVal>(serialized, GetJsonOptions());
        Assert.Equal(jsonStr, deserialized);
    }

    [Fact]
    private static void LexedPathSerializes()
    {
        var (expSerialized, path) = MakeLexedPath();
        var serialized = JsonSerializer.Serialize(path, GetJsonOptions());
        Assert.Equal(expSerialized, serialized);
        var deserialized = JsonSerializer.Deserialize<LexedPath>(serialized, GetJsonOptions());
        Assert.Equal(path, deserialized);
    }

    [Fact]
    private static void LexedCellBlankSerializes()
    {
        LexedCell cell = new LexedCell.Blank();
        var serialized = JsonSerializer.Serialize(cell, GetJsonOptions());
        Assert.Equal(@"{""Type"":""Blank"",""Val"":{}}", serialized);
        var deserialized = JsonSerializer.Deserialize<LexedCell.Blank>(serialized, GetJsonOptions());
        Assert.Equal(cell, deserialized);
    }

    [Fact]
    private static void LexedCellPathSerializes()
    {
        var (expJson, path) = MakeLexedPath();
        expJson = $@"{{""Type"":""Path"",""Val"":{expJson}}}";
        LexedCell cell = new LexedCell.Path(path);
        var serialized = JsonSerializer.Serialize(cell, GetJsonOptions());
        Assert.Equal(expJson, serialized);
        var deserialized = JsonSerializer.Deserialize<LexedCell>(serialized, GetJsonOptions());
        Assert.Equal(cell, deserialized);
    }

    [Fact]
    private static void LexedCellJValSerializes()
    {
        var (jsonAnyCode, jsonAny) = MakeJsonAny();
        LexedCell header = new LexedCell.JVal(jsonAny);
        var serialized = JsonSerializer.Serialize(header, GetJsonOptions());
        var expJsonCode = @$"{{""Type"":""JVal"",""Val"":{jsonAnyCode}}}";
        Assert.Equal(expJsonCode, serialized);
        var deserialized = JsonSerializer.Deserialize<LexedCell>(serialized, GetJsonOptions());
        Assert.Equal(header, deserialized);
    }

    [Fact]
    private static void LexedCellMtxHeadSerializes()
    {
        LexedCell header = new LexedCell.MtxHead(MtxKind.Arr, false);
        var innerJson = @"{""Kind"":0,""IsTp"":false}";
        var expJsonCode = @$"{{""Type"":""MtxHead"",""Val"":{innerJson}}}";
        var serialized = JsonSerializer.Serialize(header, GetJsonOptions());
        Assert.Equal(expJsonCode, serialized);
        var deserialized = JsonSerializer.Deserialize<LexedCell>(serialized, GetJsonOptions());
        Assert.Equal(header, deserialized);
    }

    [Fact]
    private static void ConvertedPathSerializes()
    {
        PathItem a = new PathItem.Key("a"u8.ToImmutableArray());
        PathItem i = new PathItem.Idx(0);
        ConvertedPath path = new(ImmutableArray.Create(a, i));
        var serialized = JsonSerializer.Serialize(path, GetJsonOptions());
        Assert.Equal("[\"a\",0]", serialized);
        var deserialized = JsonSerializer.Deserialize<ConvertedPath>(serialized, GetJsonOptions());
        Assert.Equal(path, deserialized);
    }

    [Fact]
    private static void AstNodeLeafSerializes()
    {
        (string expJson, AstNode leaf) = MakeAstNodeLeaf();
        var serialized = JsonSerializer.Serialize(leaf, GetJsonOptions());
        Assert.Equal(expJson, serialized);
        var deserialized = JsonSerializer.Deserialize<AstNode>(serialized, GetJsonOptions());
        Assert.Equal(leaf, deserialized);
    }

    [Fact]
    private static void AstResultNodeBranchSerializes()
    {
        var (expLeafSzd, leaf) = MakeAstNodeLeaf();
        var (expPathSzd, path) = MakeLexedPath();
        AstNode.Branch.Item item = new(path, leaf);
        AstNode branch = new AstNode.Branch(ImmutableArray.Create(item));
        var serialized = JsonSerializer.Serialize(branch, GetJsonOptions());
        var expInner = $@"[{{""Path"":{expPathSzd},""Result"":{expLeafSzd}}}]";
        var expJson = @$"{{""Type"":""Branch"",""Val"":{expInner}}}";
        Assert.Equal(expJson, serialized);
        var deserialized = JsonSerializer.Deserialize<AstNode>(serialized, GetJsonOptions());
        Assert.Equal(branch, deserialized);
    }

    [Fact]
    private static void AstResultErrorStrayCellSerializes()
    {
        var errMsg = "Error message";
        var jsonCode = @$"{{""Type"":""Error"",""Val"":""{errMsg}""}}";
        AstNode err = new AstNode.Error(errMsg);
        var serialized = JsonSerializer.Serialize(err, GetJsonOptions());
        Assert.Equal(jsonCode, serialized);
        var deserialized = JsonSerializer.Deserialize<AstNode>(serialized, GetJsonOptions());
        Assert.Equal(err, deserialized);
    }
}