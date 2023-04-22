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
public sealed class AstResult_Node_Leaf_Converter : ImplicitConverter<AstResult.Node.Leaf, JsonVal.Any> { }
public sealed class AstResult_Node_Branch_Converter
    : ImplicitConverter<AstResult.Node.Branch, ImmutableArray<AstResult.Node.Branch.Item>> { }

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

    private record NameAndNode(string Type, JsonNode Val);

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

    private record NameAndNode(string Type, JsonNode Val);

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

        if (nodeOrNull is { } node) { JsonSerializer.Serialize(writer, new NameAndNode(name, node), options); }

        throw new Exception(); // TODO
    }
}

public sealed class JsonValConverter
    : UnionConverter<JsonVal, JsonVal.Any, JsonVal.Str> { }
public sealed class LexedCellConverter
    : UnionConverter<LexedCell, LexedCell.Blank, LexedCell.Path, LexedCell.Header> { }
public sealed class HeaderConverter
    : UnionConverter<LexedCell.Header, LexedCell.Header.Val, LexedCell.Header.Mtx> { }
public sealed class AstResultConverter
    : UnionConverter<AstResult, AstResult.Node, AstResult.Error> { }
public sealed class NodeConverter
    : UnionConverter<AstResult.Node, AstResult.Node.Leaf, AstResult.Node.Branch> { }
public sealed class ErrorConverter
    : UnionConverter<AstResult.Error, AstResult.Error.StrayCell, AstResult.Error.BadPathElmt> { }


public static class JsonSerialization
{
    /*public static string Serialize(AstResult astResult, JsonSerializerOptions? options = null)
    {
        options ??= new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        
        foreach (var converter in JsonConverters.All())
        {
            options.Converters.Add(converter);
        }
        
        return JsonSerializer.Serialize(astResult, options);
    }*/
}

internal static class JsonConverters
{
    /*public static IList<JsonConverter> All() => new List<JsonConverter>
    {
        new _InputPathElmt(),
        new _JsonVal(),
        new _ConvertedPathElmt(),
        new _AstResult(),
        new _LexedPath(),
    };*/

    /*public sealed class _InputPathElmt : JsonConverter<InputPathElmt>
    {
        public override InputPathElmt?
            Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    JsonEncodedText.
                    return new InputPathElmt.Key(reader.GetString()!);
               case JsonTokenType.Number:
                   return new InputPathElmt.ArrElmt(reader.GetInt32() == 0 ? ArrElmtKind.Plus : ArrElmtKind.Stop);
                default:
                    throw new Exception(); // TODO
            };
        }

        public override void Write(Utf8JsonWriter writer, InputPathElmt value, JsonSerializerOptions options)
        {
            switch (value)
            {
                case InputPathElmt.ArrElmt arrElmt:
                    writer.WriteNumberValue(arrElmt.V == ArrElmtKind.Plus ? 0 : 1);
                    break;
                case InputPathElmt.Key key:
                    writer.WriteStringValue(key.V);
                    break;
            }
        }
    }*/

    /*public sealed class _JsonVal : JsonConverter<JsonVal>
    {
        public override JsonVal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            
            
            //return reader.TokenType switch
            //{
            //    JsonTokenType.String => new JsonVal.Str(reader.GetString()!),
            //    _ => throw new Exception()
            //};
        }

        public override void Write(Utf8JsonWriter writer, JsonVal value, JsonSerializerOptions options)
        {
            switch (value)
            {
                case JsonVal.Str s:
                    writer.WriteStringValue(s.V);
                    break;
                default:
                    throw new Exception();
            }
        }
    }*/

    /*public sealed class _ConvertedPathElmt : JsonConverter<ConvertedPathElmt>
    {
        public override ConvertedPathElmt? Read(ref Utf8JsonReader reader, Type typeToConvert,
            JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.String => new ConvertedPathElmt.Key(reader.GetString()!),
                JsonTokenType.Number => new ConvertedPathElmt.Idx(reader.GetInt32()),
                _ => throw new Exception()
            };
        }

        public override void Write(Utf8JsonWriter writer, ConvertedPathElmt value, JsonSerializerOptions options)
        {
            switch (value)
            {
                case ConvertedPathElmt.Key key:
                    writer.WriteStringValue(key.V);
                    break;
                case ConvertedPathElmt.Idx idx:
                    writer.WriteNumberValue(idx.V);
                    break;
            }
        }
    }*/

    /*public sealed class _AstResult : JsonConverter<AstResult>
    {
        private readonly record struct TypeAndJVal(string type, JsonValue obj);

        public override AstResult? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var typeAndJObj = JsonSerializer.Deserialize<TypeAndJVal>(ref reader, options);
            return typeAndJObj switch
            {
                (nameof(AstResult.Node.Branch), { } jVal) =>
                    new AstResult.Node.Branch(jVal.Deserialize<List<AstResult.Node.Branch.Item>>()!),
                (nameof(AstResult.Node.Leaf), { } jVal) =>
                    new AstResult.Node.Leaf(jVal.Deserialize<JsonVal>()!),
                ("error", _) => throw new NotImplementedException(),
                _ => throw new Exception()
            };
        }

        public override void Write(Utf8JsonWriter writer, AstResult value, JsonSerializerOptions options)
        {
            var typeAndJObj = value switch
            {
                AstResult.Node.Branch branch =>
                    new TypeAndJVal(nameof(AstResult.Node.Branch), JsonValue.Create(branch.V)!),
                AstResult.Node.Leaf leaf =>
                    new TypeAndJVal(nameof(AstResult.Node.Leaf), JsonValue.Create(leaf.V)!),
                AstResult.Error _ => throw new NotImplementedException(),
                _ => throw new UnreachableException()
            };
            
            JsonSerializer.Serialize(writer, typeAndJObj, options);
        }
    }*/
}