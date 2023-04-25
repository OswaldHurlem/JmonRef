using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LibJmon.Impl;
using System.Collections.Immutable;

// TODO consider revising serialization for unions-of-unions like AstResult

namespace LibJmon;

public abstract class ImplicitConverter<TObj, TConverted> : JsonConverter<TObj>
    where TObj : IImplicitConversion<TObj, TConverted>
{
    public override TObj Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        JsonSerializer.Deserialize<TConverted?>(ref reader, options) ?? throw new Exception(); // TODO

    public override void Write(Utf8JsonWriter writer, TObj value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, (TConverted)value, options);
}

public sealed class JsonAny_Converter : ImplicitConverter<JsonVal.Any, JsonNode?> { }
public sealed class LexedPath_Converter : ImplicitConverter<LexedPath, ImmutableArray<InputPathElmt>> { }
public sealed class LexedCell_Path_Converter : ImplicitConverter<LexedCell.Path, LexedPath> { }
public sealed class LexedCell_Header_Val_Converter : ImplicitConverter<LexedCell.Header.Val, JsonVal.Any> { }
public sealed class ConvertedPath_Converter : ImplicitConverter<ConvertedPath, ImmutableArray<ConvertedPathElmt>> { }

public sealed class AstNode_Leaf_Converter : ImplicitConverter<AstNode.Leaf, JsonVal.Any> { }
public sealed class AstNode_Branch_Converter
    : ImplicitConverter<AstNode.Branch, ImmutableArray<AstNode.Branch.Item>> { }
public sealed class AstNode_Error_Converter : ImplicitConverter<AstNode.Error, string> { }

public sealed class JsonStrConverter : JsonConverter<JsonVal.Str>
{
    public override JsonVal.Str? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var buffer = new byte[reader.HasValueSequence ? (int)reader.ValueSequence.Length : reader.ValueSpan.Length];
		int charsRead = reader.CopyString(buffer);
        return new JsonVal.Str(buffer[0..charsRead].ToImmutableArray());
    }

    public override void Write(Utf8JsonWriter writer, JsonVal.Str value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.V.AsSpan());
}

public abstract class PathElmtConverter<TPathElmt> : JsonConverter<TPathElmt> where TPathElmt : IPathElmt<TPathElmt>
{
    public override TPathElmt? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => TPathElmt.FromStr(JsonSerializer.Deserialize<JsonVal.Str>(ref reader, options)!),
            JsonTokenType.Number => TPathElmt.FromInt(reader.GetInt32()),
            _ => throw new Exception()
        };

    public override void Write(Utf8JsonWriter writer, TPathElmt value, JsonSerializerOptions options) =>
        value.ToStrOrInt().Switch(
            s => JsonSerializer.Serialize(writer, s, options),
            writer.WriteNumberValue
        );
}

public sealed class InputPathElmtConverter : PathElmtConverter<InputPathElmt> { }

public sealed class ConvertedPathElmtConverter : PathElmtConverter<ConvertedPathElmt> { }

internal sealed record NameAndNode(string Type, JsonNode Val);

public class UnionConverter<TBase, TDerived0, TDerived1> : JsonConverter<TBase>
    where TBase : IUnion<TBase, TDerived0, TDerived1>
    where TDerived0 : TBase
    where TDerived1 : TBase
{
    private Dictionary<string, int> IdxFromName { get; } = new()
    {
        [typeof(TDerived0).Name] = 0,
        [typeof(TDerived1).Name] = 1,
    };

    private string[] NameFromIdx { get; } =
        new[] { typeof(TDerived0).Name, typeof(TDerived1).Name };

    public override TBase? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (JsonSerializer.Deserialize<NameAndNode>(ref reader, options) is (string name, JsonNode node))
        {
            return IdxFromName[name] switch
            {
                0 => node.Deserialize<TDerived0>(options),
                1 => node.Deserialize<TDerived1>(options),
                _ => throw new Exception(), // TODO
            };
        }

        throw new Exception(); // TODO
    }

    public override void Write(Utf8JsonWriter writer, TBase value, JsonSerializerOptions options)
    {
        var (name, nodeOrNull) = value.AsOneOf().Match(
            td0 => (NameFromIdx[0], JsonSerializer.SerializeToNode(td0, options)),
            td1 => (NameFromIdx[1], JsonSerializer.SerializeToNode(td1, options))
        );

        if (nodeOrNull is null) { throw new Exception(); } // TODO
        
        JsonSerializer.Serialize(writer, new NameAndNode(name, nodeOrNull!), options);
    }
}

public abstract class UnionConverter<TBase, TDerived0, TDerived1, TDerived2> : JsonConverter<TBase>
    where TBase : IUnion<TBase, TDerived0, TDerived1, TDerived2>
    where TDerived0 : TBase
    where TDerived1 : TBase
    where TDerived2 : TBase
{
    private Dictionary<string, int> IdxFromName { get; } = new()
    {
        [typeof(TDerived0).Name] = 0,
        [typeof(TDerived1).Name] = 1,
        [typeof(TDerived2).Name] = 2,
    };

    private string[] NameFromIdx { get; } =
        new[] { typeof(TDerived0).Name, typeof(TDerived1).Name, typeof(TDerived2).Name };
    
    public override TBase? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (JsonSerializer.Deserialize<NameAndNode>(ref reader, options) is (string name, JsonNode node))
        {
            return IdxFromName[name] switch
            {
                0 => node.Deserialize<TDerived0>(options),
                1 => node.Deserialize<TDerived1>(options),
                2 => node.Deserialize<TDerived2>(options),
                _ => throw new Exception(), // TODO
            };
        }

        throw new Exception(); // TODO
    }

    public override void Write(Utf8JsonWriter writer, TBase value, JsonSerializerOptions options)
    {
        var (name, nodeOrNull) = value.AsOneOf().Match(
            td0 => (NameFromIdx[0], JsonSerializer.SerializeToNode(td0, options)),
            td1 => (NameFromIdx[1], JsonSerializer.SerializeToNode(td1, options)),
            td2 => (NameFromIdx[2], JsonSerializer.SerializeToNode(td2, options))
        );

        if (nodeOrNull is null) { throw new Exception(); } // TODO
        
        JsonSerializer.Serialize(writer, new NameAndNode(name, nodeOrNull!), options);
    }
}

public sealed class LexedCellConverter : JsonConverter<LexedCell>
{
    const string nameBlank = $"{nameof(LexedCell.Blank)}";
    const string namePath = $"{nameof(LexedCell.Path)}";
    const string nameHeaderVal = $"{nameof(LexedCell.Header)}.{nameof(LexedCell.Header.Val)}";
    const string nameHeaderMtx = $"{nameof(LexedCell.Header)}.{nameof(LexedCell.Header.Mtx)}";
    
    public override LexedCell? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (JsonSerializer.Deserialize<NameAndNode>(ref reader, options) is (string name, JsonNode node))
        {
            return name switch
            {
                nameBlank       => node.Deserialize<LexedCell.Blank>(options),
                namePath        => node.Deserialize<LexedCell.Path>(options),
                nameHeaderVal   => node.Deserialize<LexedCell.Header.Val>(options),
                nameHeaderMtx   => node.Deserialize<LexedCell.Header.Mtx>(options),
                _ => throw new Exception(), // TODO
            };
        }
        
        throw new Exception(); // TODO
    }
    
    public override void Write(Utf8JsonWriter writer, LexedCell value, JsonSerializerOptions options)
    {
        var (name, nodeOrNull) = value.AsOneOf().Match(
            blank => (nameBlank, JsonSerializer.SerializeToNode(blank, options)),
            path => (namePath, JsonSerializer.SerializeToNode(path, options)),
            header => header.AsOneOf().Match(
                val => (nameHeaderVal, JsonSerializer.SerializeToNode(val, options)),
                mtx => (nameHeaderMtx, JsonSerializer.SerializeToNode(mtx, options))
            )
        );
        
        if (nodeOrNull is null) { throw new Exception(); } // TODO
        
        JsonSerializer.Serialize(writer, new NameAndNode(name, nodeOrNull!), options);
    }
}

public sealed class JsonValConverter : UnionConverter<JsonVal, JsonVal.Any, JsonVal.Str> { }
public sealed class AstNodeConverter : UnionConverter<AstNode, AstNode.Leaf, AstNode.Branch, AstNode.Error> { }