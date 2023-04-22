// TODO could use either real unions or replace abstract base classes with interfaces
// TODO consider specializing "value object" types which have ImmutableArray<T> & using ReadOnlyMemory<T> instead

using System.Buffers;
using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using OneOf;
using OneOf.Types;

namespace LibJmon.Impl;

using ArrIdx = Int32;

public interface IValueObject<TVal>
{
    TVal V { get; }
}

public interface IImplicitConversion<TSelf, TOther>
    where TSelf : IImplicitConversion<TSelf, TOther>
{
    static abstract implicit operator TOther(TSelf from);
    static abstract implicit operator TSelf(TOther to);
}

public interface IUnion<TBase, TDer1, TDer2>
    where TBase : IUnion<TBase, TDer1, TDer2>
    where TDer1 : TBase
    where TDer2 : TBase { }

public interface IUnion<TBase, TDer1, TDer2, TDer3>
    where TBase : IUnion<TBase, TDer1, TDer2, TDer3>
    where TDer1 : TBase
    where TDer2 : TBase
    where TDer3 : TBase { }

// public interface IAsOneOf<T1, T2, T3> { OneOf<T1, T2, T3> AsOneOf(); }

public static class UnionExt
{
    public static OneOf<TDerived1, TDerived2>
        AsOneOf<TObj, TDerived1, TDerived2>(this IUnion<TObj, TDerived1, TDerived2> obj)
        where TObj : IUnion<TObj, TDerived1, TDerived2> 
        where TDerived1 : TObj where TDerived2 : TObj =>
            obj switch
            {
                TDerived1 t1 => t1,
                TDerived2 t2 => t2,
                _ => throw new Exception(),
            };
    
    public static OneOf<TDerived1, TDerived2, TDerived3>
        AsOneOf<TObj, TDerived1, TDerived2, TDerived3>(this IUnion<TObj, TDerived1, TDerived2, TDerived3> obj)
        where TObj : IUnion<TObj, TDerived1, TDerived2, TDerived3>
        where TDerived1 : TObj
        where TDerived2 : TObj
        where TDerived3 : TObj =>
            obj switch
            {
                TDerived1 t1 => t1,
                TDerived2 t2 => t2,
                TDerived3 t3 => t3,
                _ => throw new Exception(),
            };
}

// internal readonly record struct Coord(int Row, int Col)
public readonly record struct Coord(int Row, int Col)
{
    public static Coord operator +(Coord a, Coord b) => new(a.Row + b.Row, a.Col + b.Col);
    public static Coord operator -(Coord a, Coord b) => new(a.Row - b.Row, a.Col - b.Col);

    public static implicit operator Coord((int row, int col) t) => new(t.row, t.col);
    public static implicit operator (int row, int col)(Coord c) => c.ToTuple();
    public static Coord Invalid => new Coord(int.MinValue, int.MinValue);

    public static Coord FromIndices((Index row, Index col) t, Coord outerEnd) =>
        new(t.row.GetOffset(outerEnd.Row), t.col.GetOffset(outerEnd.Col));

    public static Coord Of00 => (0, 0);
    public static Coord Of01 => (0, 1);
    public static Coord Of10 => (1, 0);
    public static Coord Of11 => (1, 1);
}

public static class CoordExt
{
    public static Coord Swiz00(this Coord c) => Coord.Of00;
    public static Coord Swiz0C(this Coord c) => (0, c.Col);
    public static Coord Swiz0R(this Coord c) => (0, c.Row);
    public static Coord SwizC0(this Coord c) => (c.Col, 0);
    public static Coord SwizCC(this Coord c) => (c.Col, c.Col);
    public static Coord SwizCR(this Coord c) => (c.Col, c.Row);
    public static Coord SwizR0(this Coord c) => (c.Row, 0);
    public static Coord SwizRC(this Coord c) => (c.Row, c.Col);
    public static Coord SwizRR(this Coord c) => (c.Row, c.Row);
    
    public static (int row, int col) ToTuple(this Coord c) => (c.Row, c.Col);
}

// internal readonly record struct Rect(Coord Beg, Coord End)
public readonly record struct Rect(Coord Beg, Coord End)
{
    public static implicit operator Rect((Coord beg, Coord end) t) => new(t.beg, t.end);
    public static implicit operator (Coord beg, Coord end)(Rect r) => (r.Beg, r.End);

    public static Rect FromRanges(Range rowRange, Range colRange, Coord outerEnd)
    {
        var beg = Coord.FromIndices((rowRange.Start, colRange.Start), outerEnd);
        var end = Coord.FromIndices((rowRange.End, colRange.End), outerEnd);
        return new Rect(beg, end);
    }
}

public static class RectExt
{
    public static Rect Tpose(this Rect r) => (r.Beg.SwizCR(), r.End.SwizCR());
    
    public readonly record struct Quartering((Rect l, Rect r) T, (Rect l, Rect r) B);
    
    public static Quartering Quarter(this Rect r, Coord brBeg) =>
        new(
            (((r.Beg.Row, r.Beg.Col), (brBeg.Row, brBeg.Col)), ((r.Beg.Row, brBeg.Col), (brBeg.Row, r.End.Col))),
            (((brBeg.Row, r.Beg.Col), (r.End.Row, brBeg.Col)), ((brBeg.Row, brBeg.Col), (r.End.Row, r.End.Col)))
        );


    public static Coord Dims(this Rect r) => r.End - r.Beg;

    public static int Area(this Rect r) => r.Dims().Row * r.Dims().Col;

    public static bool IsEmpty(this Rect r) => (r.Beg.Row == r.End.Row) || (r.Beg.Col == r.End.Col);
    
    public static IEnumerable<Coord> CoordSeq(this Rect r)
    {
        var (beg, end) = r;
        for (var row = beg.Row; row < end.Row; row++) {
            for (var col = beg.Col; col < end.Col; col++) {
                yield return new Coord(row, col);
            }
        }
    }

    public static IEnumerable<Coord> CoordSeq(this Rect r, Coord first) =>
        r.CoordSeq().SkipWhile(c => c != first);

    public static IEnumerable<Coord> CoordSeq(this Rect r, Coord first, Coord last) =>
        r.CoordSeq().SkipWhile(c => c != first).TakeWhile(c => c != last);
    
    public static (Range rows, Range cols) ToRanges(this Rect r) => (r.Beg.Row..r.End.Row, r.Beg.Col..r.End.Col);
}

static class Util
{
    public static OneOf<T, None> AsOneOfNone<T>(this T? t)  => t is { } val ? val : new None();
}

// internal readonly record struct LexedSubSheet(ReadOnlyMemory2D<LexedCell> NonTposedMem, Coord OuterBeg, bool IsTposed)
public readonly record struct LexedSubSheet(ReadOnlyMemory2D<LexedCell> NonTposedMem, Coord OuterBeg, bool IsTposed)
{
    // TODO move to Ext?
    private ReadOnlySpan2D<LexedCell> NonTposedSpan => NonTposedMem.Span;

    public LexedCell this[Coord coord] => this[coord.Row, coord.Col];
        
    
    public LexedCell this[int row, int col] =>
        IsTposed ? NonTposedSpan[col, row] : NonTposedSpan[row, col];

    private Coord NonTposedEnd => new Coord(NonTposedMem.Height, NonTposedMem.Width);
    
    // public Coord End => new Coord(Memory.Height, Memory.Width);
    public Coord End => IsTposed ? NonTposedEnd.SwizCR() : NonTposedEnd;

    public Rect Rect => (Coord.Of00, End);
    
    private Rect NonTposedRect => (Coord.Of00, NonTposedEnd);

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(
           $"{nameof(NonTposedMem)} = {NonTposedMem}, " +
           $"{nameof(OuterBeg)} = {OuterBeg}, " +
           $"{nameof(IsTposed)} = {IsTposed}"
        );
        return true;
    }
}

public static class LexedSubSheetExt
{
    public static IEnumerable<(Coord coord, LexedCell cell)> CoordAndCellSeq(this LexedSubSheet sheet) =>
        sheet.Rect.CoordSeq().Select(c => (c, sheet[c]));
    public static IEnumerable<(Coord coord, LexedCell cell)> CoordAndCellSeq(this LexedSubSheet sheet, Coord first) =>
        sheet.Rect.CoordSeq(first).Select(c => (c, sheet[c]));
    public static IEnumerable<(Coord coord, LexedCell cell)>
        CoordAndCellSeq(this LexedSubSheet sheet, Coord first, Coord last) =>
            sheet.Rect.CoordSeq(first, last).Select(c => (c, sheet[c]));

    public static IEnumerable<LexedCell> CellSeq(this LexedSubSheet sheet) =>
        sheet.Rect.CoordSeq().Select(c => sheet[c]);
    public static IEnumerable<LexedCell> CellSeq(this LexedSubSheet sheet, Coord first) =>
        sheet.Rect.CoordSeq(first).Select(c => sheet[c]);
    public static IEnumerable<LexedCell> CellSeq(this LexedSubSheet sheet, Coord first, Coord last) =>
        sheet.Rect.CoordSeq(first, last).Select(c => sheet[c]);
    
    public static LexedSubSheet Slice(this LexedSubSheet sheet, Rect innerRect)
    {
        Rect nonTpInnerRect = sheet.IsTposed ? innerRect.Tpose() : innerRect;
        var (rows, cols) = nonTpInnerRect.ToRanges();

        // TODO post issue to Github
        var buggyRows = sheet.NonTposedMem.Height..sheet.NonTposedMem.Height;
        var buggyCols = sheet.NonTposedMem.Width..sheet.NonTposedMem.Width;
        if (rows.Equals(buggyRows)) { rows = 0..0; }
        if (cols.Equals(buggyCols)) { cols = 0..0; }

        return new LexedSubSheet(sheet.NonTposedMem[rows, cols], sheet.OuterBeg + nonTpInnerRect.Beg, sheet.IsTposed);
    }
    
    public static LexedSubSheet Slice(this LexedSubSheet sheet, Range rowRange, Range colRange) =>
        sheet.Slice(Rect.FromRanges(rowRange, colRange, sheet.End));
    
    public readonly record struct Quartering(
        (LexedSubSheet l, LexedSubSheet r) T,
        (LexedSubSheet l, LexedSubSheet r) B
    );

    public static Quartering Quarter(this LexedSubSheet sheet, Coord brBeg)
    {
        var ((tl, tr), (bl, br)) = sheet.Rect.Quarter(brBeg);
        return new Quartering(
            (sheet.Slice(tl), sheet.Slice(tr)),
            (sheet.Slice(bl), sheet.Slice(br))
        );
    }

    public static LexedSubSheet SliceCols(this LexedSubSheet sheet, Range colRange) => sheet.Slice(Range.All, colRange);
    public static LexedSubSheet SliceRows(this LexedSubSheet sheet, Range rowRange) => sheet.Slice(rowRange, Range.All);
    
    public static LexedSubSheet Tpose(this LexedSubSheet sheet) =>
        new(sheet.NonTposedMem, sheet.OuterBeg, !sheet.IsTposed);

    public static Coord ToOuter(this LexedSubSheet sheet, Coord localCoord) =>
        sheet.OuterBeg + (sheet.IsTposed ? localCoord.SwizCR() : localCoord);

    public static Coord ToInner(this LexedSubSheet sheet, Coord outerCoord) =>
        (sheet.IsTposed ? outerCoord.SwizRC() : outerCoord) - sheet.OuterBeg;
}

public interface IToJsonDocument
{
    JsonDocument ToJsonDocument(JsonSerializerOptions options);
}

public abstract record class JsonVal : IToJsonDocument, IUnion<JsonVal, JsonVal.Any, JsonVal.Str>
{
    public sealed record class Any(JsonNode? V)
        : JsonVal, IValueObject<JsonNode?>, IImplicitConversion<Any, JsonNode?>
    {
        public override JsonDocument ToJsonDocument(JsonSerializerOptions options) =>
            JsonSerializer.SerializeToDocument(V, options);

        public static implicit operator Any(JsonNode? v) => new(v);
        public static implicit operator JsonNode?(Any v) => v.V;

        public bool Equals(Any? other) => V?.ToJsonString() == other.V?.ToJsonString();
        public override int GetHashCode() => V?.ToJsonString()?.GetHashCode() ?? 0;
    }
    
    public sealed record class Str(ImmutableArray<byte> V)
        : JsonVal, IValueObject<ImmutableArray<byte>>, IImplicitConversion<Str, ImmutableArray<byte>>
    {
        public override JsonDocument ToJsonDocument(JsonSerializerOptions options)
        {
            var writerOptions = new JsonWriterOptions
            {
                Encoder = options.Encoder,
                Indented = options.WriteIndented,
                MaxDepth = options.MaxDepth,
                SkipValidation = true
            };
            
            var bufferWriter = new ArrayBufferWriter<byte>(16);
            using (Utf8JsonWriter jsonWriter = new(bufferWriter, writerOptions))
            {
                jsonWriter.WriteStringValue(V.AsSpan());
            }
            return JsonDocument.Parse(bufferWriter.WrittenMemory);
        }
        
        public static implicit operator Str(ImmutableArray<byte> v) => new(v);
        public static implicit operator ImmutableArray<byte>(Str v) => v.V;

        public bool Equals(Str? other) => StructuralComparisons.StructuralEqualityComparer.Equals(V, other?.V);
        public override int GetHashCode() => V.Aggregate(0, HashCode.Combine);
    }
    
    public abstract JsonDocument ToJsonDocument(JsonSerializerOptions options);
}

public interface IPathElmt<TSelf> where TSelf : IPathElmt<TSelf>
{
    public static abstract TSelf FromStr(JsonVal.Str s);
    public static abstract TSelf FromInt(int i);

    public OneOf<JsonVal.Str, int> ToStrOrInt();
}

public enum ArrElmtKind { Stop, Plus };

public abstract record class InputPathElmt
    : IUnion<InputPathElmt, InputPathElmt.Key, InputPathElmt.ArrElmt>, IPathElmt<InputPathElmt>
{
    public sealed record class Key(JsonVal.Str V)
        : InputPathElmt, IValueObject<JsonVal.Str>, IImplicitConversion<Key, JsonVal.Str>
    {
        public static implicit operator Key(JsonVal.Str s) => new(s);
        public static implicit operator JsonVal.Str(Key k) => k.V;
    }

    public sealed record class ArrElmt(ArrElmtKind V)
        : InputPathElmt, IValueObject<ArrElmtKind>, IImplicitConversion<ArrElmt, ArrElmtKind>
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
    : IValueObject<ImmutableArray<InputPathElmt>>, IImplicitConversion<LexedPath, ImmutableArray<InputPathElmt>>
{
    public static LexedPath Empty { get; } = new(ImmutableArray<InputPathElmt>.Empty);
    
    public static implicit operator ImmutableArray<InputPathElmt>(LexedPath from) => from.V;
    public static implicit operator LexedPath(ImmutableArray<InputPathElmt> to) => new(to);

    public bool Equals(LexedPath other) => StructuralComparisons.StructuralEqualityComparer.Equals(V, other.V);
    public override int GetHashCode() => V.Aggregate(0, HashCode.Combine);
}

public enum MtxKind { Arr, Obj };

// enum CellKind { Empty = 0, Path = 1, Header = 2 };
public enum CellKind { Empty = 0, Path = 1, Header = 2 };

// TODO(later) enable JSON serialization
public abstract record class LexedCell : IUnion<LexedCell, LexedCell.Blank, LexedCell.Path, LexedCell.Header>
{
    public sealed record class Blank : LexedCell
    {
    }

    public sealed record class Path(LexedPath V)
        : LexedCell, IValueObject<LexedPath>, IImplicitConversion<Path, LexedPath>
    {
        public static implicit operator LexedPath(Path p) => p.V;
        public static implicit operator Path(LexedPath p) => new(p);
    }

    public abstract record class Header
        : LexedCell, IUnion<Header, Header.Val, Header.Mtx> //, IUnion<LexedCell, Blank, Path, Header>
    {
        public sealed record class Val(JsonVal.Any V)
            : Header, IValueObject<JsonVal.Any>, IImplicitConversion<Val, JsonVal.Any>
        {
            public static implicit operator Val(JsonVal.Any v) => new(v);
            public static implicit operator JsonVal.Any(Val v) => v.V;
        }

        public sealed record class Mtx(MtxKind kind, bool isTp) : Header
        {
        }

        public OneOf<Val, Mtx> AsOneOf() => (this as IUnion<Header, Val, Mtx>).AsOneOf();
    }
}

public abstract record class ConvertedPathElmt
    : IUnion<ConvertedPathElmt, ConvertedPathElmt.Key, ConvertedPathElmt.Idx>, IPathElmt<ConvertedPathElmt>
{
    public sealed record class Key(JsonVal.Str V)
        : ConvertedPathElmt, IValueObject<JsonVal.Str>, IImplicitConversion<Key, JsonVal.Str>
    {
        public static implicit operator Key(JsonVal.Str s) => new(s);
        public static implicit operator JsonVal.Str(Key k) => k.V;
    }

    public sealed record class Idx(ArrIdx V)
        : ConvertedPathElmt, IValueObject<ArrIdx>, IImplicitConversion<Idx, ArrIdx>
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
    : IValueObject<ImmutableArray<ConvertedPathElmt>>,
        IImplicitConversion<ConvertedPath, ImmutableArray<ConvertedPathElmt>>
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

public abstract record class AstResult : IUnion<AstResult, AstResult.Node, AstResult.Error>
{
    public abstract record class Node : AstResult, IUnion<Node, Node.Leaf, Node.Branch>
    {
        public sealed record class Leaf(JsonVal.Any V)
            : Node, IValueObject<JsonVal.Any>, IImplicitConversion<Leaf, JsonVal.Any>
        {
            public static implicit operator Leaf(JsonVal.Any v) => new(v);
            public static implicit operator JsonVal.Any(Leaf l) => l.V;
        }

        public sealed record class Branch(ImmutableArray<Branch.Item> V)
            : Node, IValueObject<ImmutableArray<Branch.Item>>, IImplicitConversion<Branch, ImmutableArray<Branch.Item>>
        {
            public readonly record struct Item(LexedPath Path, AstResult Result);
            
            public static Branch Empty => new(ImmutableArray<Item>.Empty);
            public static implicit operator ImmutableArray<Item>(Branch from) => from.V;

            public static implicit operator Branch(ImmutableArray<Item> to) => new(to);
            
            private IStructuralEquatable AsStructEq => V;
            public bool Equals(Branch? other) => StructuralComparisons.StructuralEqualityComparer.Equals(V, other?.V);
            public override int GetHashCode() => V.Aggregate(0, HashCode.Combine);
        }
        
        public OneOf<Leaf, Branch> AsOneOf() => (this as IUnion<Node, Leaf, Branch>).AsOneOf();
    }

    public abstract record class Error(Coord Coord) : AstResult, IUnion<Error, Error.StrayCell, Error.BadPathElmt>
    {
        public sealed record class StrayCell(Coord Coord, Type StrayCellType) : Error(Coord)
        {
            public static StrayCell AtInnerCoord(LexedSubSheet sheet, Coord innerCoord) =>
                new(sheet.ToOuter(innerCoord), sheet[innerCoord].GetType());

            protected override OneOf<StrayCell, BadPathElmt> AsOneOfImpl2() => this;
        }
        
        // TODO consider removing?
        public sealed record class BadPathElmt(Coord Coord, Type PathElmtType) : Error(Coord)
        {
            protected override OneOf<StrayCell, BadPathElmt> AsOneOfImpl2() => this;
        }

        public OneOf<StrayCell, BadPathElmt> AsOneOf() => (this as IUnion<Error, StrayCell, BadPathElmt>).AsOneOf();
        protected abstract OneOf<StrayCell, BadPathElmt> AsOneOfImpl2();
    }
}

// internal readonly record struct Assignment(ConvertedPath Path, JsonVal Value);

// internal readonly record struct LexedValSeq(IReadOnlyList<OneOf<JsonVal, None>> Vals);

public static class SeqExt
{
    public static TCoord? Find<TCoord,TObj>(this IEnumerable<(TCoord coord, TObj val)> seq, Func<TObj, bool> pred)
        where TCoord : struct =>
            seq.Select(t => (coord: (TCoord?)t.coord, t.val)).FirstOrDefault(t => pred(t.val)).coord;
}

public sealed class ConvertedPathComparer : IEqualityComparer<ConvertedPath>
{
    public bool Equals(ConvertedPath pA, ConvertedPath pB) => pA.V.SequenceEqual(pB.V);
    public int GetHashCode(ConvertedPath p) => p.V.Aggregate(0, HashCode.Combine);
}

public sealed class AstResultComparer : IEqualityComparer<AstResult>
{
    public bool Equals(AstResult? rA, AstResult? rB)
    {
        ItemComparer ??= new BranchItemComparer(this);
        return (rA, rB) switch {
            (AstResult.Node.Branch bA, AstResult.Node.Branch bB) => bA.V.SequenceEqual(bB.V, ItemComparer),
            _ => rA?.Equals(rB) ?? false
        };
    }
    
    public int GetHashCode(AstResult r) => r switch {
        AstResult.Node.Branch b => b.V.Aggregate(0, HashCode.Combine),
        _ => r.GetHashCode()
    };

    private IEqualityComparer<AstResult.Node.Branch.Item>? ItemComparer { get; set; } = null;
    
    private sealed record class BranchItemComparer(AstResultComparer RsltComparer)
        : IEqualityComparer<AstResult.Node.Branch.Item>
    {
        public bool Equals(AstResult.Node.Branch.Item iA, AstResult.Node.Branch.Item iB) =>
            iA.Path.V.SequenceEqual(iB.Path.V)
            && RsltComparer.Equals(iA.Result, iB.Result);
        
        public int GetHashCode(AstResult.Node.Branch.Item i) =>
            HashCode.Combine(
                i.Path.V.Aggregate(0, HashCode.Combine),
                RsltComparer.GetHashCode(i.Result)
            );
    }
}

public static class Logic
{
    readonly record struct ConvertPathsState(IDictionary<ConvertedPath, int> IdxForPartialPath)
    {
        public static ConvertPathsState Initial => new(new Dictionary<ConvertedPath, int>(new ConvertedPathComparer()));
    }

    private static ConvertedPath ConvertPath(ConvertedPath prefixPath, LexedPath lexedPath, ConvertPathsState state)
    {
        var cvtPathElmts = prefixPath.V.ToList();

        ConvertedPathElmt ConvertProtoIdxElmt(InputPathElmt.ArrElmt arrElmt)
        {
            var partialPath = new ConvertedPath(cvtPathElmts.ToImmutableArray());
            if (!state.IdxForPartialPath.TryGetValue(partialPath, out var idx)) { idx = -1; }
            idx += arrElmt.V == ArrElmtKind.Plus ? 1 : 0;
            state.IdxForPartialPath[partialPath] = idx;
            return new ConvertedPathElmt.Idx(idx);
        }

        var pathSegments = lexedPath.V.Segment(elmt => elmt is InputPathElmt.ArrElmt);
        
        foreach (IReadOnlyList<InputPathElmt> pathSegment in pathSegments)
        {
            var elmt0 = pathSegment[0].AsOneOf().Match(
                keyElmt => new ConvertedPathElmt.Key(keyElmt.V),
                ConvertProtoIdxElmt
            );
            
            var otherElmts = pathSegment.Skip(1)
                .Cast<InputPathElmt.Key>()
                .Select(elmt => new ConvertedPathElmt.Key(elmt.V));
            cvtPathElmts.Add(elmt0);
            cvtPathElmts.AddRange(otherElmts);
        }
        
        return new ConvertedPath(cvtPathElmts.ToImmutableArray());
    }

   //private static IEnumerable<Assignment>
   //    YieldAssignments(ConvertedPath parentPath, ConvertPathsState cvtPathsState, AstNode astNode)
   //{
   //    var cvtPath = ConvertPath(parentPath, astNode.Path, cvtPathsState);
   //    
   //    return astNode.ValOrDesc.Match(
   //        val => new[] { new Assignment(cvtPath, val) },
   //        desc => desc.SelectMany(d => YieldAssignments(cvtPath, cvtPathsState, d))
   //    );
   //}

    // private static bool FindStrayCell(LexedSubSheet sheet, CellKind kind, Coord firstCoord, out AstRslt.Error stray) =>
    //     sheet.Find(firstCoord, cell => cell.Is(kind))
    //         .MapFound(coord => AstRslt.MakeStrayCell(kind, sheet.ToOuter(coord)))
    //         .TryPickT0(out stray, out _);

    private static IEnumerable<(LexedPath path, int beg, int end)> GetPathRanges(IReadOnlyList<LexedCell> cellSeq)
    {
        var seq = cellSeq
            .Select((cell, idx) => (path: (cell as LexedCell.Path)?.V, idx))
            .Where(t => t.path.HasValue)
            .ToList();
        
        var endsSeq = seq.Select(t => t.idx).Skip(1).Concat(new[] { cellSeq.Count });
        
        return seq.Zip(endsSeq, (t, end) => (t.path!.Value, t.idx, end));
    }

    // TODO return BadPathElmt
    private static IEnumerable<AstResult.Node.Branch.Item> ParsePathCols(
        LexedSubSheet pathCols,
        LexedSubSheet pathRows,
        LexedSubSheet interior
        )
    {
        var seq = GetPathRanges(pathCols.SliceCols(0..1).CellSeq().ToList());
        var remCols = pathCols.SliceCols(1..);
        
        if (remCols.Rect.Dims().Col == 0)
        {
            foreach (var (path, begRow, endRow) in seq)
            {
                var desc = ParsePathRows(pathRows, interior.SliceRows(begRow..endRow));
                var prop = new AstResult.Node.Branch(desc.ToImmutableArray());
                yield return new(path, prop);
            }
            yield break;
        }
        
        foreach (var (path, begRow, endRow) in seq)
        {
            var desc = ParsePathCols(remCols.SliceRows(begRow..endRow), pathRows, interior.SliceRows(begRow..endRow));
            var prop = new AstResult.Node.Branch(desc.ToImmutableArray());
            yield return new(path, prop);
        }
    }

    private static IEnumerable<AstResult.Node.Branch.Item> ParsePathRows(LexedSubSheet pathRows, LexedSubSheet interior)
    {
        var seq = GetPathRanges(pathRows.SliceRows(0..1).CellSeq().ToList());
        var remRows = pathRows.SliceRows(1..);

        if (remRows.Rect.Dims().Row == 0)
        {
            foreach (var (path, begCol, endCol) in seq)
            {
                var valOrNone = ParseJmon(interior.SliceCols(begCol..endCol));
                if (valOrNone.TryPickT0(out var val, out _)) { yield return new(path, val); }
            }
            yield break;
        }

        foreach (var (path, begCol, endCol) in seq)
        {
            var desc = ParsePathRows(remRows.SliceCols(begCol..endCol), interior.SliceCols(begCol..endCol));
            var prop = new AstResult.Node.Branch(desc.ToImmutableArray());
            yield return new(path, prop);
        }
    }

    private static AstResult ParseMtx(LexedSubSheet subSheet) =>
        ParseMtxToBranchOrError(subSheet).CastToBase(o => (AstResult)o);
    
    private static OneOf<AstResult.Node.Branch, AstResult.Error> ParseMtxToBranchOrError(LexedSubSheet subSheet)
    {
        var (mtxKind, isTp) = (subSheet[0, 0] as LexedCell.Header.Mtx)!; 
        subSheet = isTp ? subSheet.Tpose() : subSheet;

        AstResult.Error.StrayCell? FindStray(LexedSubSheet sheet, int skip, Func<LexedCell, bool> pred) =>
            sheet.CoordAndCellSeq().Skip(skip).Find(pred) switch
            {
                Coord strayCoord => AstResult.Error.StrayCell.AtInnerCoord(sheet, strayCoord),
                _ => null
            };

        var pathRowBegOrNull = subSheet.CoordAndCellSeq().Find(cell => cell is LexedCell.Path);
        if (pathRowBegOrNull is not Coord pathRowBeg)
        {
            return FindStray(subSheet, 1, cell => cell is not LexedCell.Blank) switch
            {
                { } strayInEmptyMtx => strayInEmptyMtx,
                _ => AstResult.Node.Branch.Empty
            };
        }

        var pathColSearchRange = subSheet.SliceCols(0..pathRowBeg.Col).Tpose();
        var pathColBegOrNull = pathColSearchRange.CoordAndCellSeq().Find(cell => cell is LexedCell.Path);
        if (pathColBegOrNull is not Coord pathColBeg) { return AstResult.Error.StrayCell.AtInnerCoord(subSheet, pathRowBeg); }
        pathColBeg = subSheet.ToInner(pathColSearchRange.ToOuter(pathColBeg));

        var ((margin, pathRows), (pathCols, interior)) = subSheet.Quarter((pathColBeg.Row, pathRowBeg.Col));

        if (FindStray(margin, 1, cell => cell is not LexedCell.Blank) is {} strayInMargin) { return strayInMargin; }
        if (FindStray(pathRows, 0, cell => cell is LexedCell.Header) is {} strayInPathRows) { return strayInPathRows; }
        if (FindStray(pathCols, 0, cell => cell is LexedCell.Header) is {} strayInPathCols) { return strayInPathCols; }
        
        pathRows = pathRows.SliceRows(pathRowBeg.Row..);
        pathCols = pathCols.SliceCols(pathColBeg.Col..);

        // TODO WTF was I thinking here??
        /*var ((intPadTL, intPadTR), (intPadBL, newInterior)) = interior.Quarter((pathColBeg.Row, pathRowBeg.Col));
        if (FindStrayCell(intPadBL, CellKind.Header, (0,0), out strayCell)) { return strayCell; }
        if (FindStrayCell(intPadTR, CellKind.Header, (0,0), out strayCell)) { return strayCell; }
        if (FindStrayCell(intPadTL, CellKind.Header, (0,0), out strayCell)) { return strayCell; }
        if (FindStrayCell(intPadBL, CellKind.Path, (0,0), out strayCell)) { return strayCell; }
        if (FindStrayCell(intPadTR, CellKind.Path, (0  ,0), out strayCell)) { return strayCell; }
        if (FindStrayCell(intPadTL, CellKind.Path, (0,0), out strayCell)) { return strayCell; }
        interior = newInterior;*/

        return new AstResult.Node.Branch(ParsePathCols(pathCols, pathRows, interior).ToImmutableArray());
    }

    public static OneOf<AstResult, None> ParseJmon(LexedSubSheet subSheet)
    {
        var firstNonBlankOrNull = subSheet.CoordAndCellSeq().Find(cell => cell is not LexedCell.Blank);
        if (firstNonBlankOrNull is not Coord firstNonBlank) { return new None(); }
        
        if (subSheet[firstNonBlank] is not LexedCell.Header header)
        {
            return AstResult.Error.StrayCell.AtInnerCoord(subSheet, firstNonBlank);
        }

        var lower = subSheet.SliceRows(firstNonBlank.Row..);
        
        var left = lower.SliceCols(0..firstNonBlank.Col);
        var strayInLeftOrNull = left.CoordAndCellSeq().Find(cell => cell is not LexedCell.Blank);
        if (strayInLeftOrNull is { } stray) { return AstResult.Error.StrayCell.AtInnerCoord(left, stray); }

        var valRange = lower.SliceCols(firstNonBlank.Col..);

        return header.AsOneOf().Match(
            valCell => valRange.CoordAndCellSeq().Skip(1).Find(cell => cell is not LexedCell.Blank) switch
            {
                { } strayCoord => AstResult.Error.StrayCell.AtInnerCoord(valRange, strayCoord),
                _ => new AstResult.Node.Leaf(valCell.V)
            },
            mtxHeader => ParseMtx(valRange)
        );
    }

    private static IReadOnlyDictionary<string, LexedCell> SimpleLexedCells { get; } = new Dictionary<string, LexedCell>
    {
        {"", new LexedCell.Blank()},
        {":[", new LexedCell.Header.Mtx(MtxKind.Arr, false)},
        {":{", new LexedCell.Header.Mtx(MtxKind.Obj, false)},
        {":^[", new LexedCell.Header.Mtx(MtxKind.Arr, true)},
        {":^{", new LexedCell.Header.Mtx(MtxKind.Obj, true)},
    };

    public static OneOf<LexedCell, LexError> Lex(string cellText, Coord cellCoord)
    {
        var trimmedText = cellText.Trim();
        
        if (SimpleLexedCells.TryGetValue(trimmedText, out var cell)) { return cell; }
        
        if (trimmedText.StartsWith("//")) { return new LexedCell.Blank(); }
        
        if (trimmedText.StartsWith("."))
        {
            // parse as if it is CSV
            // then re-quote strings and parse as JSON
            throw new NotImplementedException();
        }

        if (trimmedText.StartsWith(":"))
        {
            if (trimmedText.StartsWith(":::"))
            {
                
            }

            if (trimmedText.StartsWith("::"))
            {
                throw new NotImplementedException();
            }

            return new LexError(cellCoord);
        }

        JsonValue? jsonValue = null;
        try
        {
            jsonValue = JsonNode.Parse(trimmedText)?.AsValue();
        }
        catch (Exception)
        {
            throw; // TODO
        }

        return new LexedCell.Header.Val(jsonValue);
    }

    public static OneOf<LexedSubSheet, LexError> LexSheet(string[,] sheet)
    {
        var lexedCells = new LexedCell[sheet.GetLength(0), sheet.GetLength(1)];
        var rect = new Rect((0, 0), (sheet.GetLength(0), sheet.GetLength(1)));
        
        foreach (var (r,c) in rect.CoordSeq())
        {
            var lexResult = Lex(sheet[r, c], (r,c));
            if (lexResult.IsT0) { lexedCells[r, c] = lexResult.AsT0; }
            else { return lexResult.AsT1; }
        }
        
        return new LexedSubSheet(lexedCells, (0,0), false);
    }
}

public sealed record class LexError(Coord Coord);

public static class ApiV0
{
    /*public static Values.JsonVal JsonFromJmon(Values.JmonSheet jmonSheet)
    {
        var sheet = new Sheet(jmonSheet.Cells);
        sheet.Cells.AsSpan2D().
    }*/
}

public static class TEMP
{
    //private static LexedPath LexInputPath(string pathString)
    //{
    //    var elmts = pathString.Split('.').Skip(1).Select(elmt => elmt switch
    //    {
    //        "+" => new InputPathElmt.Arr(true) as InputPathElmt,
    //        "$" => new InputPathElmt.Arr(false) as InputPathElmt,
    //        _ => new InputPathElmt.Key(new JsonVal.Str(elmt)) as InputPathElmt
    //    }).ToList();
    //    return new LexedPath(elmts);
    //}
    //
    //private static IEnumerable<MtxNode> ParseMtxNodes(string[,] simpleMatrix)
    //{
    //    var colPathsWithIdxs = Enumerable.Range(0, simpleMatrix.GetLength(0)).Skip(1)
    //        .Select(rowIdx => (rowIdx, inputPath: LexInputPath(simpleMatrix[rowIdx, 0])))
    //        .ToList();
    //    var rowPathsWithIdxs = Enumerable.Range(0, simpleMatrix.GetLength(1)).Skip(1)
    //        .Select(colIdx => (colIdx, inputPath: LexInputPath(simpleMatrix[0, colIdx])))
    //        .ToList();
//
    //    foreach (var (rowIdx, pathFromCol) in colPathsWithIdxs)
    //    {
    //        MtxNode.Branch MakeBranch((int colIdx, LexedPath inputPath) t) =>
    //            new (
    //                t.inputPath,
    //                new[] { new MtxNode.Leaf(new JsonVal.Str(simpleMatrix[rowIdx, t.colIdx])) }
    //            );
    //        yield return new MtxNode.Branch(pathFromCol, rowPathsWithIdxs.Select(MakeBranch));
    //    }
    //}
    //
    public static IEnumerable<(string path, string val)> JQAssignmentsFromSimpleMatrix(string[,] simpleMatrix)
    {
        throw new NotImplementedException();
        //var assignments = Logic.MakeAssignments(ParseMtxNodes(simpleMatrix));
        //return assignments.Select(assignment => (
        //    assignment.Path.ToJsonPath(),
        //    (assignment.Value as JsonVal.Str)?.V ?? throw new Exception()
        //));
    }
}

// TODO remove
public static class Ext
{
    public static TOut CastToBase<TIn, TOut>(this OneOf<TIn> oneOf, Func<object, TOut> exemplar)
        where TIn : TOut =>
            oneOf.Match(v => v);
    public static TOut CastToBase<TIn1, TIn2, TOut>(this OneOf<TIn1, TIn2> oneOf, Func<object, TOut> exemplar)
        where TIn1 : TOut where TIn2 : TOut =>
            oneOf.Match(v => (TOut)v, v => v);
    public static TOut CastToBase<TIn1, TIn2, TIn3, TOut>(this OneOf<TIn1, TIn2, TIn3> oneOf, Func<object, TOut> exemplar)
        where TIn1 : TOut where TIn2 : TOut where TIn3 : TOut =>
            oneOf.Match(v => (TOut)v, v => v, v => v);
}

/*
 * Adapted from:
 *     MoreLINQ - Extensions to LINQ to Objects
 *     Copyright (c) 2010 Leopold Bushkin. All rights reserved.
 *     
 *     Licensed under the Apache License, Version 2.0 (the "License");
 *     you may not use this file except in compliance with the License.
 *     You may obtain a copy of the License at
 *     
 *         http://www.apache.org/licenses/LICENSE-2.0
 *     
 *     Unless required by applicable law or agreed to in writing, software
 *     distributed under the License is distributed on an "AS IS" BASIS,
 *     WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *     See the License for the specific language governing permissions and
 *     limitations under the License.
 * 
 * With the following changes:
 *   - Edited for terseness
 *   - Changed return type to IEnumerable<IReadonlyList<T>>
 *   - Moved to namespace JmonLib.Impl
 */
public static class MoreLinq
{
    /// <summary>
    /// Divides a sequence into multiple sequences by using a segment detector based on the original sequence
    /// </summary>
    /// <param name="predicate">
    /// A function, which returns <c>true</c> if the given element
    /// begins a new segment, and <c>false</c> otherwise
    /// </param>
    public static IEnumerable<IReadOnlyList<T>> Segment<T>(this IEnumerable<T> source, Func<T, bool> predicate) =>
        Segment(source, (curr, _) => predicate(curr));

    /// <summary>
    /// Divides a sequence into multiple sequences by using a segment detector based on the original sequence
    /// </summary>
    /// <param name="predicate">
    /// A function, which returns <c>true</c> if the given element or
    /// index indicate a new segment, and <c>false</c> otherwise
    /// </param>
    public static IEnumerable<IReadOnlyList<T>> Segment<T>(this IEnumerable<T> source, Func<T, int, bool> predicate) =>
        Segment(source, (curr, _, index) => predicate(curr, index));

    /// <summary>
    /// Divides a sequence into multiple sequences by using a segment detector based on the original sequence
    /// </summary>
    /// <param name="predicate">
    /// A function, which returns <c>true</c> if the given current element,
    /// previous element or index indicate a new segment, and <c>false</c> otherwise
    /// </param>
    public static IEnumerable<IReadOnlyList<T>> Segment<T>(this IEnumerable<T> source, Func<T, T, int, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        return _(); IEnumerable<IReadOnlyList<T>> _()
        {
            using var e = source.GetEnumerator();
            if (!e.MoveNext())
                yield break;
            
            var previous = e.Current;
            var segment = new List<T> { previous };

            for (var index = 1; e.MoveNext(); index++)
            {
                if (predicate(e.Current, previous, index))
                {
                    yield return segment;
                    segment = new List<T> { e.Current };
                }
                else { segment.Add(e.Current); }
                previous = e.Current;
            }
            yield return segment;
        }
    }
}

