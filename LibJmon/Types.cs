using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using LibJmon.SuperTypes;
using OneOf;
using OneOf.Types;

namespace LibJmon.Types;

using ArrIdx = Int32;

public abstract record JsonVal : IUnion<JsonVal, JsonVal.Any, JsonVal.Str> // IToJsonDocument
{
    public sealed record Any(JsonNode? V) : JsonVal, IImplicitConversion<Any, JsonNode?>
    {
        public static implicit operator Any(JsonNode? v) => new(v);
        public static implicit operator JsonNode?(Any v) => v.V;

        public bool Equals(Any? other) => V?.ToJsonString() == other?.V?.ToJsonString();
        public override int GetHashCode() => V?.ToJsonString()?.GetHashCode() ?? 0;
    }
    
    public sealed record Str(string V) : JsonVal, IImplicitConversion<Str, string>
    {
        public static implicit operator Str(string v) => new(v);
        public static implicit operator string(Str v) => v.V;

        public string ToUtf16String() => V; // Encoding.UTF8.GetString(V.AsSpan());
    }
}

public static class JsonAnyExt
{
    public static OneOf<T, None> TryDeserialize<T>(this JsonVal.Any any)
    {
        try { return any.V.Deserialize<T>(JsonSerialization.Resources.JsonSerializerOptions).ToOneOf(); }
        catch (JsonException e) { return new None(); }
    }
}

public abstract record PathBase(ImmutableArray<PathItem> Items, bool IsAppend)
{
    public virtual bool Equals(PathBase? other) =>
        (other is not null) && (IsAppend == other.IsAppend) && Items.SequenceEqual(other.Items);
    public override int GetHashCode() => Items.Aggregate(0, HashCode.Combine);
}

public sealed record LexedPath(ImmutableArray<PathItem> Items, bool IsAppend) : PathBase(Items, IsAppend)
{
    public bool IdxsAreValid() => Items.OfType<PathItem.Idx>().All(i => i.V is 0 or 1);
}

public sealed record ConvertedPath(ImmutableArray<PathItem> Items, bool IsAppend) : PathBase(Items, IsAppend);

public abstract record PathItem : IUnion<PathItem, PathItem.Key, PathItem.Idx>
{
    public sealed record Key(JsonVal.Str V) : PathItem, IImplicitConversion<Key, JsonVal.Str>
    {
        public static implicit operator Key(JsonVal.Str s) => new(s);
        public static implicit operator JsonVal.Str(Key k) => k.V;
    }

    public sealed record Idx(int V) : PathItem, IImplicitConversion<Idx, int>
    {
        public static implicit operator Idx(int i) => new(i);
        public static implicit operator int(Idx i) => i.V;
    }
}

public enum MtxKind { Arr = 0, Obj = 1 };

public abstract record LexedCell
    : IUnion<LexedCell, LexedCell.Blank, LexedCell.Path, LexedCell.JVal, LexedCell.MtxHead, LexedCell.Error>
{
    public sealed record Blank : LexedCell { }

    public sealed record Path(LexedPath V) : LexedCell, IImplicitConversion<Path, LexedPath>
    {
        public static implicit operator LexedPath(Path p) => p.V;
        public static implicit operator Path(LexedPath p) => new(p);
    }

    public sealed record JVal(JsonVal.Any V) : LexedCell, IImplicitConversion<JVal, JsonVal.Any>
    {
        public static implicit operator JsonVal.Any(JVal v) => v.V;
        public static implicit operator JVal(JsonVal.Any v) => new(v);
    }

    public sealed record MtxHead(MtxKind Kind, bool IsTp) : LexedCell;

    public sealed record Error(string V) : LexedCell, IImplicitConversion<Error, string>
    {
        public static implicit operator string(Error e) => e.V;
        public static implicit operator Error(string e) => new(e);
    }

    public bool IsHeader() => this switch
    {
        MtxHead or JVal => true,
        _ => false
    };
}

public abstract record AstNode : IUnion<AstNode, AstNode.ValCell, AstNode.Matrix, AstNode.Error>
{
    public sealed record ValCell(JsonVal.Any V) : AstNode, IImplicitConversion<ValCell, JsonVal.Any>
    {
        public static implicit operator ValCell(JsonVal.Any v) => new(v);
        public static implicit operator JsonVal.Any(ValCell l) => l.V;
    }

    public sealed record Matrix(ImmutableArray<Matrix.Item> Items, MtxKind MtxKind) : AstNode
    {
        public readonly record struct Item(LexedPath Path, AstNode Node);
        
        public bool Equals(Matrix? other) =>
            (other is not null) && (MtxKind == other.MtxKind) && Items.SequenceEqual(other.Items);
        public override int GetHashCode() => Items.Aggregate(0, HashCode.Combine);
        
        public static Matrix Empty(MtxKind kind) => new(ImmutableArray<Matrix.Item>.Empty, kind);
    }

    public sealed record Error(string V) : AstNode, IImplicitConversion<Error, string>
    {
        public static implicit operator Error(string v) => new(v);
        public static implicit operator string(Error e) => e.V;
    }
}