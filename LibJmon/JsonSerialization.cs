using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LibJmon.Impl;
using System.Collections.Immutable;
using System.Text.Encodings.Web;
using LibJmon.SuperTypes;
using LibJmon.Types;

namespace LibJmon.JsonSerialization;

public static class Resources
{
    private static Lazy<JsonSerializerOptions> _options = new Lazy<JsonSerializerOptions>(
        () => {
            var o = new JsonSerializerOptions
            {
                // WriteIndented = true,
                Encoder = JavaScriptEncoder.Default,
                IncludeFields = true,
            };
            
            o.Converters.Add(new JsonAny_Converter());
            o.Converters.Add(new LexedCell_Path_Converter());
            o.Converters.Add(new LexedCell_JVal_Converter());
            o.Converters.Add(new LexedCell_Error_Converter());
            o.Converters.Add(new AstNode_ValCell_Converter());
            o.Converters.Add(new AstNode_Error_Converter());
            o.Converters.Add(new JsonStrConverter());
            o.Converters.Add(new PathBaseItemConverter());
            o.Converters.Add(new LexedCellConverter());
            o.Converters.Add(new JsonValConverter());
            o.Converters.Add(new AstNodeConverter());

            return o;
        });

    public static JsonSerializerOptions JsonSerializerOptions => _options.Value;
}

public abstract class ImplicitConverter<TObj, TConverted> : JsonConverter<TObj>
    where TObj : IImplicitConversion<TObj, TConverted>
{
    public override TObj Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        JsonSerializer.Deserialize<TConverted?>(ref reader, options) ?? throw new Exception(); // TODO

    public override void Write(Utf8JsonWriter writer, TObj value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, (TConverted)value, options);
}

public sealed class JsonAny_Converter : JsonConverter<JsonVal.Any>
{
    public override JsonVal.Any Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        JsonSerializer.Deserialize<JsonNode>(ref reader, options) ?? null;

    public override void Write(Utf8JsonWriter writer, JsonVal.Any value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value.V, options);

    public override bool HandleNull => true;
}

public sealed class LexedCell_Path_Converter : ImplicitConverter<LexedCell.Path, LexedPath> { }
public sealed class LexedCell_JVal_Converter : ImplicitConverter<LexedCell.JVal, JsonVal.Any> { }
public sealed class LexedCell_Error_Converter : ImplicitConverter<LexedCell.Error, string> { }

public sealed class AstNode_ValCell_Converter : ImplicitConverter<AstNode.ValCell, JsonVal.Any> { }
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

public class PathBaseItemConverter : JsonConverter<PathItem>
{
    public override PathItem? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => new PathItem.Key(
                JsonSerializer.Deserialize<JsonVal.Str>(ref reader, options)!
            ),
            JsonTokenType.Number => new PathItem.Idx(reader.GetInt32()),
            _ => throw new Exception()
        };

    public override void Write(Utf8JsonWriter writer, PathItem value, JsonSerializerOptions options) =>
        value.AsOneOf().Switch(
            key => JsonSerializer.Serialize(writer, key.V, options),
            idx => writer.WriteNumberValue(idx.V)
        );
}

internal sealed record NameAndNode(string Type, JsonNode? Val);

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

        JsonSerializer.Serialize(writer, new NameAndNode(name, nodeOrNull), options);
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

        JsonSerializer.Serialize(writer, new NameAndNode(name, nodeOrNull), options);
    }
}

public abstract class UnionConverter<TBase, TDer0, TDer1, TDer2, TDer3, TDer4> : JsonConverter<TBase>
    where TBase : IUnion<TBase, TDer0, TDer1, TDer2, TDer3, TDer4>
    where TDer0 : TBase
    where TDer1 : TBase
    where TDer2 : TBase
    where TDer3 : TBase
    where TDer4 : TBase
{
    private Dictionary<string, int> IdxFromName { get; } = new()
    {
        [typeof(TDer0).Name] = 0,
        [typeof(TDer1).Name] = 1,
        [typeof(TDer2).Name] = 2,
        [typeof(TDer3).Name] = 3,
        [typeof(TDer4).Name] = 4,
    };

    private string[] NameFromIdx { get; } =
        new[] { typeof(TDer0).Name, typeof(TDer1).Name, typeof(TDer2).Name, typeof(TDer3).Name, typeof(TDer4).Name };
    
    public override TBase? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (JsonSerializer.Deserialize<NameAndNode>(ref reader, options) is (string name, JsonNode node))
        {
            return IdxFromName[name] switch
            {
                0 => node.Deserialize<TDer0>(options),
                1 => node.Deserialize<TDer1>(options),
                2 => node.Deserialize<TDer2>(options),
                3 => node.Deserialize<TDer3>(options),
                4 => node.Deserialize<TDer4>(options),
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
            td2 => (NameFromIdx[2], JsonSerializer.SerializeToNode(td2, options)),
            td3 => (NameFromIdx[3], JsonSerializer.SerializeToNode(td3, options)),
            td4 => (NameFromIdx[4], JsonSerializer.SerializeToNode(td4, options))
        );

        JsonSerializer.Serialize(writer, new NameAndNode(name, nodeOrNull), options);
    }
}

public sealed class LexedCellConverter
    : UnionConverter<LexedCell, LexedCell.Blank, LexedCell.Path, LexedCell.JVal, LexedCell.MtxHead, LexedCell.Error> { }
public sealed class JsonValConverter : UnionConverter<JsonVal, JsonVal.Any, JsonVal.Str> { }
public sealed class AstNodeConverter : UnionConverter<AstNode, AstNode.ValCell, AstNode.Matrix, AstNode.Error> { }