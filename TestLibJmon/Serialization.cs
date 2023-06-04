/*
using System.Collections.Immutable;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using LibJmon;
using LibJmon.JsonSerialization;
using LibJmon.Types;

namespace TestLibJmon;

// TODO serialize enums to strings

public static class Serialization
{
    private static (string jsonCode, JsonVal.Str jsonStr) MakeJsonStr() => ("\"a\"", "a");

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
        LexedPath path = new(ImmutableArray.Create(t0.arrElmt as PathItem, t1.keyElmt), false);
        return ($"{{\"Items\":[{t0.jsonCode},{t1.jsonCode}],\"IsAppend\":false}}", path);
    }

    private static (string jsonCode, AstNode.ValCell nodeLeaf) MakeAstValCell()
    {
        var (expSerialized, jsonAny) = MakeJsonAny();
        return ($@"{{""Type"":""ValCell"",""Val"":{expSerialized}}}", jsonAny);
    }

    [Fact]
    private static void JsonAnySerializes()
    {
        var (jsonCode, jsonAny) = MakeJsonAny();
        var serialized = JsonSerializer.Serialize(jsonAny, Resources.JsonSerializerOptions);
        Assert.Equal(jsonCode, serialized);
        var deserialized = JsonSerializer.Deserialize<JsonVal.Any>(serialized, Resources.JsonSerializerOptions);
        Assert.Equal(jsonAny, deserialized);
    }

    [Fact]
    private static void JsonStrSerializes()
    {
        var strObj = new JsonVal.Str("&<>");
        var jsonCode = "\"" + JavaScriptEncoder.Default.Encode("&<>") + "\"";
        var serialized = JsonSerializer.Serialize(strObj, Resources.JsonSerializerOptions);
        Assert.Equal(jsonCode, serialized);
        var deserialized = JsonSerializer.Deserialize<JsonVal.Str>(serialized, Resources.JsonSerializerOptions);
        Assert.Equal(strObj, deserialized);
    }

    [Fact]
    private static void JsonAnyAsJsonValSerializes()
    {
        (string jsonCode, JsonVal jsonAny) = MakeJsonAny();
        jsonCode = $@"{{""Type"":""Any"",""Val"":{jsonCode}}}";
        var serialized = JsonSerializer.Serialize(jsonAny, Resources.JsonSerializerOptions);
        Assert.Equal(jsonCode, serialized);
        var deserialized = JsonSerializer.Deserialize<JsonVal>(serialized, Resources.JsonSerializerOptions);
        Assert.Equal(jsonAny, deserialized);
    }

    [Fact]
    private static void JsonStrAsJsonValSerializes()
    {
        (string jsonCode, JsonVal jsonStr) = MakeJsonStr();
        jsonCode = $@"{{""Type"":""Str"",""Val"":{jsonCode}}}";
        var serialized = JsonSerializer.Serialize(jsonStr, Resources.JsonSerializerOptions);
        Assert.Equal(jsonCode, serialized);
        var deserialized = JsonSerializer.Deserialize<JsonVal>(serialized, Resources.JsonSerializerOptions);
        Assert.Equal(jsonStr, deserialized);
    }

    [Fact]
    private static void LexedPathSerializes()
    {
        var (expSerialized, path) = MakeLexedPath();
        var serialized = JsonSerializer.Serialize(path, Resources.JsonSerializerOptions);
        Assert.Equal(expSerialized, serialized);
        var deserialized = JsonSerializer.Deserialize<LexedPath>(serialized, Resources.JsonSerializerOptions);
        Assert.Equal(path, deserialized);
    }

    [Fact]
    private static void LexedCellBlankSerializes()
    {
        LexedCell cell = new LexedCell.Blank();
        var serialized = JsonSerializer.Serialize(cell, Resources.JsonSerializerOptions);
        Assert.Equal(@"{""Type"":""Blank"",""Val"":{}}", serialized);
        var deserialized = JsonSerializer.Deserialize<LexedCell.Blank>(serialized, Resources.JsonSerializerOptions);
        Assert.Equal(cell, deserialized);
    }

    [Fact]
    private static void LexedCellPathSerializes()
    {
        var (expJson, path) = MakeLexedPath();
        expJson = $@"{{""Type"":""Path"",""Val"":{expJson}}}";
        LexedCell cell = new LexedCell.Path(path);
        var serialized = JsonSerializer.Serialize(cell, Resources.JsonSerializerOptions);
        Assert.Equal(expJson, serialized);
        var deserialized = JsonSerializer.Deserialize<LexedCell>(serialized, Resources.JsonSerializerOptions);
        Assert.Equal(cell, deserialized);
    }

    [Fact]
    private static void LexedCellJValSerializes()
    {
        var (jsonAnyCode, jsonAny) = MakeJsonAny();
        LexedCell header = new LexedCell.JVal(jsonAny);
        var serialized = JsonSerializer.Serialize(header, Resources.JsonSerializerOptions);
        var expJsonCode = @$"{{""Type"":""JVal"",""Val"":{jsonAnyCode}}}";
        Assert.Equal(expJsonCode, serialized);
        var deserialized = JsonSerializer.Deserialize<LexedCell>(serialized, Resources.JsonSerializerOptions);
        Assert.Equal(header, deserialized);
    }

    [Fact]
    private static void LexedCellMtxHeadSerializes()
    {
        LexedCell header = new LexedCell.MtxHead(MtxKind.Arr, false);
        var innerJson = @"{""Kind"":0,""IsTp"":false}";
        var expJsonCode = @$"{{""Type"":""MtxHead"",""Val"":{innerJson}}}";
        var serialized = JsonSerializer.Serialize(header, Resources.JsonSerializerOptions);
        Assert.Equal(expJsonCode, serialized);
        var deserialized = JsonSerializer.Deserialize<LexedCell>(serialized, Resources.JsonSerializerOptions);
        Assert.Equal(header, deserialized);
    }

    [Fact]
    private static void ConvertedPathSerializes()
    {
        PathItem a = new PathItem.Key("a");
        PathItem i = new PathItem.Idx(0);
        ConvertedPath path = new(ImmutableArray.Create(a, i), false);
        var serialized = JsonSerializer.Serialize(path, Resources.JsonSerializerOptions);
        Assert.Equal(@"{""Items"":[""a"",0],""IsAppend"":false}", serialized);
        var deserialized = JsonSerializer.Deserialize<ConvertedPath>(serialized, Resources.JsonSerializerOptions);
        Assert.Equal(path, deserialized);
    }

    [Fact]
    private static void AstNodeLeafSerializes()
    {
        (string expJson, AstNode leaf) = MakeAstValCell();
        var serialized = JsonSerializer.Serialize(leaf, Resources.JsonSerializerOptions);
        Assert.Equal(expJson, serialized);
        var deserialized = JsonSerializer.Deserialize<AstNode>(serialized, Resources.JsonSerializerOptions);
        Assert.Equal(leaf, deserialized);
    }

    [Fact]
    private static void AstMatrixSerializes()
    {
        var (expLeafSzd, leaf) = MakeAstValCell();
        var (expPathSzd, path) = MakeLexedPath();
        BranchItem branchItem = new(path, leaf);
        AstNode matrix = new AstNode.Branch(ImmutableArray.Create(branchItem), BranchKind.ObjMtx);
        var serialized = JsonSerializer.Serialize(matrix, Resources.JsonSerializerOptions);
        var expInner = $@"[{{""Path"":{expPathSzd},""Node"":{expLeafSzd}}}]";
        var expJson = @$"{{""Type"":""Branch"",""Val"":{{""Items"":{expInner},""Kind"":1}}}}";
        Assert.Equal(expJson, serialized);
        var deserialized = JsonSerializer.Deserialize<AstNode>(serialized, Resources.JsonSerializerOptions);
        Assert.Equal(matrix, deserialized);
    }

    [Fact]
    private static void AstResultErrorStrayCellSerializes()
    {
        var errMsg = "Error message";
        var jsonCode = @$"{{""Type"":""Error"",""Val"":""{errMsg}""}}";
        AstNode err = new AstNode.Error(errMsg);
        var serialized = JsonSerializer.Serialize(err, Resources.JsonSerializerOptions);
        Assert.Equal(jsonCode, serialized);
        var deserialized = JsonSerializer.Deserialize<AstNode>(serialized, Resources.JsonSerializerOptions);
        Assert.Equal(err, deserialized);
    }
}
*/