using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using LibJmon.SuperTypes;
using OneOf;
using OneOf.Types;

namespace LibJmon.Types;

using ArrIdx = Int32;

public abstract record JsonVal : IUnion<JsonVal, JsonVal.Any, JsonVal.Str>
{
    public sealed record Any(JsonNode? V) : JsonVal, IImplicitConversion<Any, JsonNode?>
    {
        public static implicit operator Any(JsonNode? v) => new(v);
        public static implicit operator JsonNode?(Any v) => v.V;

        public bool Equals(Any? other) => V?.ToJsonString() == other?.V?.ToJsonString();
        public override int GetHashCode() => V?.ToJsonString()?.GetHashCode() ?? 0;
        
        public string ToUtf16String(JsonSerializerOptions options) => V?.ToJsonString(options) ?? "null";
    }
    
    public sealed record Str(string V) : JsonVal, IImplicitConversion<Str, string>
    {
        public static implicit operator Str(string v) => new(v);
        public static implicit operator string(Str v) => v.V;

        public string ToUtf16String() => V;
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
    public static LexedPath EmptyNonAppend => new(ImmutableArray<PathItem>.Empty, false);
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

    public static Idx ArrayPlus => 1;
    public static Idx ArrayStop => 0;
}

public enum MtxKind { Arr = 0, Obj = 1 };

public abstract record LexedCell
    : IUnion<LexedCell, LexedCell.Blank, LexedCell.Path, LexedCell.JVal, LexedCell.MtxHead>
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

    public bool IsHeader() => this switch
    {
        MtxHead or JVal => true,
        _ => false
    };
}

public abstract record AstNode : IUnion<AstNode, AstNode.ValCell, AstNode.Branch, AstNode.Error>
{
    public sealed record ValCell(JsonVal.Any V) : AstNode, IImplicitConversion<ValCell, JsonVal.Any>
    {
        public static implicit operator ValCell(JsonVal.Any v) => new(v);
        public static implicit operator JsonVal.Any(ValCell l) => l.V;
    }

    public sealed record Branch(ImmutableArray<BranchItem> Items, BranchKind Kind) : AstNode
    {
        public bool Equals(Branch? other) =>
            (other is not null) && (Kind == other.Kind) && Items.SequenceEqual(other.Items);
        public override int GetHashCode() => Items.Aggregate(0, HashCode.Combine);
        
        public static Branch Empty(BranchKind kind) => new(ImmutableArray<BranchItem>.Empty, kind);
    }

    public sealed record Error(string V) : AstNode, IImplicitConversion<Error, string>
    {
        public static implicit operator Error(string v) => new(v);
        public static implicit operator string(Error e) => e.V;
    }
}

// TODO remove and replace with MtxKind?
public enum BranchKind { ArrMtx = 0, ObjMtx = 1, Range = 2 };

public readonly record struct BranchItem(LexedPath Path, AstNode Node);

public static class MtxKindConversion
{
    public static BranchKind ToBranchKind(this MtxKind kind) => kind switch
    {
        MtxKind.Arr => BranchKind.ArrMtx,
        MtxKind.Obj => BranchKind.ObjMtx,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}

public readonly record struct AssignPath(ImmutableArray<PathItem> Items)
{
    public bool Equals(AssignPath other) => Items.SequenceEqual(other.Items);
    public override int GetHashCode() => Items.Aggregate(0, HashCode.Combine);
    
    public static AssignPath Empty => new(ImmutableArray<PathItem>.Empty);
    
    public static implicit operator ImmutableArray<PathItem>(AssignPath p) => p.Items;
    public static implicit operator AssignPath(ImmutableArray<PathItem> items) => new(items);
}

public abstract record JsonTreeOp()
    : IUnion<JsonTreeOp, JsonTreeOp.PushNode, JsonTreeOp.PopNode, JsonTreeOp.Create, JsonTreeOp.ReportErr>
{
    public sealed record PushNode(AssignPath Path, MtxKind? NodeKind, bool MustBeNew) : JsonTreeOp();
    public sealed record PopNode() : JsonTreeOp();
    public sealed record Create(AssignPath Path, JsonVal.Any Value) : JsonTreeOp();
    public sealed record ReportErr(AssignPath Path, string Message) : JsonTreeOp();

    // public sealed record Open(AssignPath Path, MtxKind MtxKind, bool MustBeNew) : JsonTreeOp(Path);

}