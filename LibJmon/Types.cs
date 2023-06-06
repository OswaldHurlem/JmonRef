using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using LibJmon.Sheets;
using LibJmon.SuperTypes;

namespace LibJmon.Types;

using ArrIdx = Int32;

internal abstract record JsonVal : IUnion<JsonVal, JsonVal.Any, JsonVal.Str>
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

internal abstract record PathBase(ImmutableArray<PathItem> Items, bool IsAppend)
{
    public virtual bool Equals(PathBase? other) =>
        (other is not null) && (IsAppend == other.IsAppend) && Items.SequenceEqual(other.Items);
    public override int GetHashCode() => Items.Aggregate(0, HashCode.Combine);
}

internal sealed record LexedPath(ImmutableArray<PathItem> Items, bool IsAppend) : PathBase(Items, IsAppend)
{
    public bool IdxsAreValid() => Items.OfType<PathItem.Idx>().All(i => i.V is 0 or 1);
    public static LexedPath EmptyNonAppend => new(ImmutableArray<PathItem>.Empty, false);
}

internal abstract record PathItem : IUnion<PathItem, PathItem.Key, PathItem.Idx>
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

internal enum MtxKind { Arr = 0, Obj = 1 };

internal abstract record LexedCell
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

internal abstract record AstNode(Rect ContribCells)
    : IUnion<AstNode, AstNode.ValCell, AstNode.Branch, AstNode.Error>
{
    public sealed record ValCell(JsonVal.Any Value, Rect ContribCells) : AstNode(ContribCells);

    public sealed record Branch(ImmutableArray<BranchItem> Items, BranchKind Kind, Rect ContribCells)
        : AstNode(ContribCells)
    {
        public static Branch Empty(BranchKind kind, Rect contribCells) =>
            new(ImmutableArray<BranchItem>.Empty, kind, contribCells);
    }

    public sealed record Error(Coord? FocusCell, string Msg, Rect ContribCells)
        : AstNode(ContribCells);
}

internal enum BranchKind { ArrMtx = 0, ObjMtx = 1, Range = 2 };

internal readonly record struct BranchItem(LexedPath Path, AstNode Node);

internal static class MtxKindConversion
{
    public static BranchKind ToBranchKind(this MtxKind kind) => kind switch
    {
        MtxKind.Arr => BranchKind.ArrMtx,
        MtxKind.Obj => BranchKind.ObjMtx,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}

internal readonly record struct AssignPath(ImmutableArray<PathItem> Items)
{
    public bool Equals(AssignPath other) => Items.SequenceEqual(other.Items);
    public override int GetHashCode() => Items.Aggregate(0, HashCode.Combine);
    
    public static AssignPath Empty => new(ImmutableArray<PathItem>.Empty);
    
    public static implicit operator ImmutableArray<PathItem>(AssignPath p) => p.Items;
    public static implicit operator AssignPath(ImmutableArray<PathItem> items) => new(items);

    public string ToJsonPath()
    {
        // TODO: format as a.b[i].c if keys all match \w+
        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        var list = Items.Select(item =>
            item.AsOneOf().Match<object>(
                key => key.V.ToUtf16String(),
                idx => idx.V
            )
        ).ToList();

        return JsonSerializer.Serialize(list, options);
    }
}

// TODO some values here that might not be necessary, eg:
// null value for PushNode.NodeKind
// both Path and Err in ReportErr
internal abstract record JsonTreeOp
    : IUnion<JsonTreeOp, JsonTreeOp.PushNode, JsonTreeOp.PopNode, JsonTreeOp.Create, JsonTreeOp.ReportErr>
{
    public sealed record PushNode(AssignPath Path, MtxKind? NodeKind, bool MustBeNew, Rect ContribCells) : JsonTreeOp;
    public sealed record PopNode : JsonTreeOp;
    public sealed record Create(AssignPath Path, JsonVal.Any Value, Rect ContribCells) : JsonTreeOp;
    public sealed record ReportErr(AssignPath Path, JmonParseErr Err) : JsonTreeOp;
}