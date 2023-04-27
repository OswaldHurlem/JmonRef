using System.Collections;
using System.Collections.Immutable;
using System.Text.Json.Nodes;
using LibJmon.SuperTypes;
using OneOf;

namespace LibJmon.Types;

using ArrIdx = Int32;

public abstract record JsonVal : IUnion<JsonVal, JsonVal.Any, JsonVal.Str> // IToJsonDocument
{
    public sealed record Any(JsonNode? V) : JsonVal, IImplicitConversion<Any, JsonNode?>
    {
        // public override JsonDocument ToJsonDocument(JsonSerializerOptions options) =>
        //     JsonSerializer.SerializeToDocument(V, options);

        public static implicit operator Any(JsonNode? v) => new(v);
        public static implicit operator JsonNode?(Any v) => v.V;

        public bool Equals(Any? other) => V?.ToJsonString() == other?.V?.ToJsonString();
        public override int GetHashCode() => V?.ToJsonString()?.GetHashCode() ?? 0;
    }
    
    public sealed record Str(ImmutableArray<byte> V) : JsonVal, IImplicitConversion<Str, ImmutableArray<byte>>
    {
        // public override JsonDocument ToJsonDocument(JsonSerializerOptions options)
        // {
        //     var writerOptions = new JsonWriterOptions
        //     {
        //         Encoder = options.Encoder,
        //         Indented = options.WriteIndented,
        //         MaxDepth = options.MaxDepth,
        //         SkipValidation = true
        //     };
        //     
        //     var bufferWriter = new ArrayBufferWriter<byte>(16);
        //     using (Utf8JsonWriter jsonWriter = new(bufferWriter, writerOptions))
        //     {
        //         jsonWriter.WriteStringValue(V.AsSpan());
        //     }
        //     return JsonDocument.Parse(bufferWriter.WrittenMemory);
        // }
        
        public static implicit operator Str(ImmutableArray<byte> v) => new(v);
        public static implicit operator ImmutableArray<byte>(Str v) => v.V;

        public bool Equals(Str? other) => StructuralComparisons.StructuralEqualityComparer.Equals(V, other?.V);
        public override int GetHashCode() => V.Aggregate(0, HashCode.Combine);
    }
}

public interface IPathElmt<TSelf> where TSelf : IPathElmt<TSelf>
{
    public static abstract TSelf FromStr(JsonVal.Str s);
    public static abstract TSelf FromInt(int i);

    public OneOf<JsonVal.Str, int> ToStrOrInt();
}

public enum ArrElmtKind { Stop, Plus };

// TODO condense code for InputPath/ConvertedPath
public abstract record InputPathElmt
    : IUnion<InputPathElmt, InputPathElmt.Key, InputPathElmt.ArrElmt>, IPathElmt<InputPathElmt>
{
    public sealed record Key(JsonVal.Str V) : InputPathElmt, IImplicitConversion<Key, JsonVal.Str>
    {
        public static implicit operator Key(JsonVal.Str s) => new(s);
        public static implicit operator JsonVal.Str(Key k) => k.V;
    }

    public sealed record ArrElmt(ArrElmtKind V) : InputPathElmt, IImplicitConversion<ArrElmt, ArrElmtKind>
    {
        public static implicit operator ArrElmt(ArrElmtKind k) => new(k);
        public static implicit operator ArrElmtKind(ArrElmt k) => k.V;
    }

    static InputPathElmt IPathElmt<InputPathElmt>.FromStr(JsonVal.Str s) => new Key(s);

    static InputPathElmt IPathElmt<InputPathElmt>.FromInt(int i) => new ArrElmt((ArrElmtKind)i);

    OneOf<JsonVal.Str, int> IPathElmt<InputPathElmt>.ToStrOrInt() =>
        this.AsOneOf().MapT0(k => k.V).MapT1(a => (int)a.V);
}

public readonly record struct LexedPath(ImmutableArray<InputPathElmt> V)
    : IImplicitConversion<LexedPath, ImmutableArray<InputPathElmt>>
{
    public static LexedPath Empty { get; } = new(ImmutableArray<InputPathElmt>.Empty);
    
    public static implicit operator ImmutableArray<InputPathElmt>(LexedPath from) => from.V;
    public static implicit operator LexedPath(ImmutableArray<InputPathElmt> to) => new(to);

    public bool Equals(LexedPath other) => StructuralComparisons.StructuralEqualityComparer.Equals(V, other.V);
    public override int GetHashCode() => V.Aggregate(0, HashCode.Combine);
}

public enum MtxKind { Arr, Obj };

public abstract record ConvertedPathElmt
    : IUnion<ConvertedPathElmt, ConvertedPathElmt.Key, ConvertedPathElmt.Idx>, IPathElmt<ConvertedPathElmt>
{
    public sealed record Key(JsonVal.Str V) : ConvertedPathElmt, IImplicitConversion<Key, JsonVal.Str>
    {
        public static implicit operator Key(JsonVal.Str s) => new(s);
        public static implicit operator JsonVal.Str(Key k) => k.V;
    }

    public sealed record Idx(ArrIdx V) : ConvertedPathElmt, IImplicitConversion<Idx, ArrIdx>
    {
        public static implicit operator Idx(ArrIdx i) => new(i);
        public static implicit operator ArrIdx(Idx i) => i.V;
    }
    
    static ConvertedPathElmt IPathElmt<ConvertedPathElmt>.FromStr(JsonVal.Str s) => new Key(s);
    static ConvertedPathElmt IPathElmt<ConvertedPathElmt>.FromInt(int i) => new Idx((ArrIdx)i);
    
    OneOf<JsonVal.Str, int> IPathElmt<ConvertedPathElmt>.ToStrOrInt() =>
        this.AsOneOf().MapT0(k => k.V).MapT1(a => (int)a.V);
}

public readonly record struct ConvertedPath(ImmutableArray<ConvertedPathElmt> V)
    : IImplicitConversion<ConvertedPath, ImmutableArray<ConvertedPathElmt>>
{
    public static ConvertedPath Empty { get; } = new(ImmutableArray<ConvertedPathElmt>.Empty);

    /*public string ToJsonPath() =>
        string.Join("", V.Select(elmt => elmt.AsOneOf().Match(key => $".{key.V}", idx => $"[{idx}]")));*/
    
    public static implicit operator ImmutableArray<ConvertedPathElmt>(ConvertedPath from) => from.V;
    public static implicit operator ConvertedPath(ImmutableArray<ConvertedPathElmt> to) => new(to);
    
    private IStructuralEquatable AsStructEq => V;
    public bool Equals(ConvertedPath other) => StructuralComparisons.StructuralEqualityComparer.Equals(V, other.V);
    public override int GetHashCode() => V.Aggregate(0, HashCode.Combine);
}