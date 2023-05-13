using System.Collections;
using System.Collections.Immutable;
using System.Text;
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
    
    public sealed record Str(ImmutableArray<byte> V) : JsonVal, IImplicitConversion<Str, ImmutableArray<byte>>
    {
        public static implicit operator Str(ImmutableArray<byte> v) => new(v);
        public static implicit operator ImmutableArray<byte>(Str v) => v.V;

        public bool Equals(Str? other) => (other?.V)?.SequenceEqual(V) ?? false;
        public override int GetHashCode() => V.Aggregate(0, HashCode.Combine);

        public string ToUtf16String() => Encoding.UTF8.GetString(V.AsSpan());
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

public abstract record PathBase<TSelf>(ImmutableArray<PathItem> V)
    where TSelf : PathBase<TSelf>, IImplicitConversion<TSelf, ImmutableArray<PathItem>>
{
    public virtual bool Equals(PathBase<TSelf>? other) => (other?.V)?.SequenceEqual(V) ?? false;
    public override int GetHashCode() => V.Aggregate(0, HashCode.Combine);

    public static TSelf Empty => ImmutableArray<PathItem>.Empty;
    public static TSelf WithOneItem(PathItem item) => ImmutableArray.Create(item);
}

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

public sealed record LexedPath(ImmutableArray<PathItem> V) :
    PathBase<LexedPath>(V), IImplicitConversion<LexedPath, ImmutableArray<PathItem>>
{
    public static implicit operator ImmutableArray<PathItem>(LexedPath from) => from.V;
    public static implicit operator LexedPath(ImmutableArray<PathItem> to) => new(to);
}

public sealed record ConvertedPath(ImmutableArray<PathItem> V)
    : PathBase<ConvertedPath>(V), IImplicitConversion<ConvertedPath, ImmutableArray<PathItem>>
{
    public static implicit operator ImmutableArray<PathItem>(ConvertedPath from) => from.V;
    public static implicit operator ConvertedPath(ImmutableArray<PathItem> to) => new(to);
}

public static class PathUtil
{
    private static TPath CreateEmpty<TPath>()
        where TPath : IImplicitConversion<TPath, ImmutableArray<PathItem>>
    {
        return ImmutableArray<PathItem>.Empty;
    }
    
    
}

public enum MtxKind { Arr, Obj };

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

public abstract record AstNode : IUnion<AstNode, AstNode.Leaf, AstNode.Branch, AstNode.Error>
{
    public sealed record Leaf(JsonVal.Any V) : AstNode, IImplicitConversion<Leaf, JsonVal.Any>
    {
        public static implicit operator Leaf(JsonVal.Any v) => new(v);
        public static implicit operator JsonVal.Any(Leaf l) => l.V;
    }

    public sealed record Branch(ImmutableArray<Branch.Item> V)
        : AstNode, IImplicitConversion<Branch, ImmutableArray<Branch.Item>>
    {
        public readonly record struct Item(LexedPath Path, AstNode Node);
        
        public static Branch Empty => new(ImmutableArray<Item>.Empty);
        public static implicit operator ImmutableArray<Item>(Branch from) => from.V;

        public static implicit operator Branch(ImmutableArray<Item> to) => new(to);
        
        private IStructuralEquatable AsStructEq => V;
        public bool Equals(Branch? other) => StructuralComparisons.StructuralEqualityComparer.Equals(V, other?.V);
        public override int GetHashCode() => V.Aggregate(0, HashCode.Combine);
    }

    public sealed record Error(string V) : AstNode, IImplicitConversion<Error, string>
    {
        public static implicit operator Error(string v) => new(v);
        public static implicit operator string(Error e) => e.V;
    }
}