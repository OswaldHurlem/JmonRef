using System.Diagnostics;
using CommunityToolkit.HighPerformance;
using OneOf;
using OneOf.Types;

namespace LibJmon.Impl;

using ArrIdx = Int32;

internal readonly record struct Coord(int Row, int Col)
{
    public static Coord operator +(Coord a, Coord b) => new(a.Row + b.Row, a.Col + b.Col);
    public static Coord operator -(Coord a, Coord b) => new(a.Row - b.Row, a.Col - b.Col);

    public Coord Swiz00 => Of00;
    public Coord Swiz0C => (0, Col);
    public Coord Swiz0R => (0, Row);
    public Coord SwizC0 => (Col, 0);
    public Coord SwizCC => (Col, Col);
    public Coord SwizCR => (Col, Row);
    public Coord SwizR0 => (Row, 0);
    public Coord SwizRC => (Row, Col);
    public Coord SwizRR => (Row, Row);

    public (int row, int col) ToTuple() => (Row, Col);
    
    public static implicit operator Coord((int row, int col) t) => new(t.row, t.col);
    public static implicit operator (int row, int col)(Coord c) => c.ToTuple();
    public static Coord Invalid => new Coord(int.MinValue, int.MinValue);

    public static Coord FromIndices((Index row, Index col) t, Coord outerEnd) =>
        new(t.row.GetOffset(outerEnd.Row), t.col.GetOffset(outerEnd.Col));
    
    (Index row, Index col) ToIndices(Coord end) => (Row, Col);

    public static Coord Of00 => (0, 0);
    public static Coord Of01 => (0, 1);
    public static Coord Of10 => (1, 0);
    public static Coord Of11 => (1, 1);
}

internal readonly record struct Rect(Coord Beg, Coord End)
{
    public static implicit operator Rect((Coord beg, Coord end) t) => new(t.beg, t.end);
    public static implicit operator (Coord beg, Coord end)(Rect r) => (r.Beg, r.End);
    
    public (Range rows, Range cols) ToRanges() => (Beg.Row..End.Row, Beg.Col..End.Col);
    
    public static Rect FromRanges(Range rowRange, Range colRange, Coord outerEnd)
    {
        var beg = Coord.FromIndices((rowRange.Start, colRange.Start), outerEnd);
        var end = Coord.FromIndices((rowRange.End, colRange.End), outerEnd);
        return new Rect(beg, end);
    }

    public IEnumerable<Coord> CoordSeq => GetCoordSeq(Beg);

    public IEnumerable<Coord> GetCoordSeq(Coord first) => GetCoordSeq(first, End - Coord.Of11);

    public IEnumerable<Coord> GetCoordSeq(Coord first, Coord last)
    {
        if (first.Row == last.Row)
        {
            for (var col = first.Col; col < last.Col+1; col++) { yield return first with { Col = col }; }
        }
        else
        {
            for (var col = first.Col; col < End.Col; col++) { yield return first with { Col = col }; }
            for (var row = first.Row + 1; row < last.Row; row++) {
                for (var col = Beg.Col; col < End.Col; col++) {
                    yield return new Coord(row, col);
                }
            }
            for (var col = Beg.Col; col < last.Col+1; col++) { yield return last with { Col = col }; }
        }
    }

    public readonly record struct Quartering((Rect l, Rect r) T, (Rect l, Rect r) B);
    
    public Quartering Quarter(Coord brBeg) =>
        new(
            ((Beg, brBeg),              (Beg + brBeg.Swiz0C, End)),
            ((Beg + brBeg.SwizR0, End), (brBeg, End)             )
        );

    public Rect Tpose => (Beg.SwizCR, End.SwizCR);
    
    public Coord Dims => End - Beg;
    
    public int Area => Dims.Row * Dims.Col;
    
    public bool IsEmpty => (Beg.Row == End.Row) || (Beg.Col == End.Col);
}

internal readonly record struct CoordFind(OneOf<Coord, NotFound> V)
{
    // public static implicit operator CoordFind(OneOf<Coord, NotFound> v) => new(v);
    // public static implicit operator OneOf<Coord, NotFound>(CoordFind v) => v.V;
    
    public bool IsFound => V.IsT0;
    public Coord Coord => V.AsT0;
    
    public static CoordFind NotFound => new(new NotFound());
    
    public OneOf<T, NotFound> MapFound<T>(Func<Coord, T> f) => IsFound ? f(Coord) : new NotFound();

    public bool TryPickFound(out Coord coord) => V.TryPickT0(out coord, out _);
}

internal readonly record struct LexedSubSheet(ReadOnlyMemory2D<LexedCell> NonTposedMem, Coord OuterBeg, bool IsTposed)
{
    private ReadOnlySpan2D<LexedCell> NonTposedSpan => NonTposedMem.Span;
    
    public LexedCell this[Coord coord] =>
        IsTposed ? NonTposedSpan[coord.Col, coord.Row] : NonTposedSpan[coord.Row, coord.Col];

    private Coord NonTposedEnd => new Coord(NonTposedMem.Height, NonTposedMem.Width);
    
    // public Coord End => new Coord(Memory.Height, Memory.Width);
    public Coord End => IsTposed ? NonTposedEnd.SwizCR : NonTposedEnd;

    public Rect Rect => (Coord.Of00, End);
    
    private Rect NonTposedRect => (Coord.Of00, NonTposedEnd);

    private IEnumerable<LexedCell> GetCellSeq()
    {
        var this2 = this;
        return Rect.CoordSeq.Select(coord => this2[coord]);
    }
    
    public IEnumerable<LexedCell> CellSeq => GetCellSeq();

    public CoordFind Find(Coord first, Coord last, Func<LexedCell, bool> pred)
    {
        var this2 = this;
        return Rect.GetCoordSeq(first, last)
            .Where(coord => pred(this2[coord]))
            .Select(coord => new CoordFind(coord))
            .FirstOrDefault(CoordFind.NotFound);
    }
    
    public CoordFind Find(Coord first, Func<LexedCell, bool> pred) =>
        Find(first, End - Coord.Of11, pred);
    
    public CoordFind Find(Func<LexedCell, bool> pred) =>
        Find(Coord.Of00, End - Coord.Of11, pred);

    public LexedSubSheet Slice(Rect innerRect)
    {
        var nonTpInnerRect = IsTposed ? innerRect.Tpose : innerRect;
        var ranges = nonTpInnerRect.ToRanges();
        var subMemory = NonTposedMem[ranges.rows, ranges.cols];
        return new LexedSubSheet(subMemory, OuterBeg + nonTpInnerRect.Beg, IsTposed);
    }
    
    public LexedSubSheet Slice(Range rowRange, Range colRange) =>
        Slice(Rect.FromRanges(rowRange, colRange, End));
    
    public readonly record struct Quartering(
        (LexedSubSheet l, LexedSubSheet r) T,
        (LexedSubSheet l, LexedSubSheet r) B
    );

    public Quartering Quarter(Coord brBeg)
    {
        var rectQ = Rect.Quarter(brBeg);
        return new Quartering(
            (Slice(rectQ.T.l), Slice(rectQ.T.r)),
            (Slice(rectQ.B.l), Slice(rectQ.B.r))
        );
    }

    public LexedSubSheet SliceCols(Range colRange) => Slice(Range.All, colRange);
    public LexedSubSheet SliceRows(Range rowRange) => Slice(rowRange, Range.All);
    
    public LexedSubSheet Tpose => new(NonTposedMem, OuterBeg, !IsTposed);

    public Coord ToOuter(Coord localCoord) => OuterBeg + (IsTposed ? localCoord.SwizCR : localCoord);
}

internal readonly record struct JsonVal(OneOf<JsonVal.Str> V)
{
    public record struct Str(string V);
}

public enum ArrElmtKind { Plus, Stop };

internal readonly record struct InputPathElmt(OneOf<JsonVal.Str, ArrElmtKind> V)
{
    // public static implicit operator InputPathElmt(OneOf<JsonVal.Str, ArrElmtKind> v) => new(v);
    // public static implicit operator OneOf<JsonVal.Str, ArrElmtKind>(InputPathElmt v) => v.V;
    
    public bool IsKey => V.IsT0;
    public bool IsArrElmt => V.IsT1;
    
    public JsonVal.Str AsKey => V.AsT0;
    public ArrElmtKind AsArrElmt => V.AsT1;

    public OneOf<T, ArrElmtKind> MapKey<T>(Func<JsonVal.Str, T> f) => V.MapT0(f);
    // public OneOf<JsonVal.Str, T> MapArrElmt<T>(Func<ArrElmtKind, T> f) => V.MapT1(f);
    
    public bool TryPickKey(out JsonVal.Str key, out ArrElmtKind arrElmt) => V.TryPickT0(out key, out arrElmt);
}

internal readonly record struct LexedPath(IReadOnlyList<InputPathElmt> V)
{
    public static LexedPath Empty { get; } = new(Array.Empty<InputPathElmt>());
}

public enum MtxKind { Arr, Obj };

enum CellKind { Empty = 0, Path = 1, Header = 2 };

internal readonly struct EmptyCell { }

internal readonly record struct LexedCell(OneOf<EmptyCell, LexedPath, LexedCell.Header> V)
{
    public readonly record struct Header(OneOf<JsonVal, (MtxKind kind, bool isTp)> V)
    {
        // public static implicit operator Header(OneOf<JsonVal, (MtxKind kind, bool isTp)> v) => new(v);
        // public static implicit operator OneOf<JsonVal, (MtxKind kind, bool isTp)>(Header h) => h.V;
        
        public bool IsVal => V.IsT0;
        public bool IsMtx => V.IsT1;
        
        public JsonVal AsVal => V.AsT0;
        public (MtxKind kind, bool isTp) AsMtx => V.AsT1;
        
        public OneOf<T, (MtxKind kind, bool isTp)> MapVal<T>(Func<JsonVal, T> f) => V.MapT0(f);
        public OneOf<JsonVal, T> MapMtx<T>(Func<(MtxKind kind, bool isTp), T> f) => V.MapT1(f);
        
        public bool TryPickVal(out JsonVal val, out (MtxKind kind, bool isTp) mtx) => V.TryPickT0(out val, out mtx);
        public bool TryPickMtx(out (MtxKind kind, bool isTp) mtx, out JsonVal val) => V.TryPickT1(out mtx, out val);
    }
    
    // public static implicit operator LexedCell(OneOf<None, LexedPath, LexedCell.Header> v) => new(v);
    // public static implicit operator OneOf<None, LexedPath, LexedCell.Header>(LexedCell c) => c.V;
    
    public bool IsBlank => V.IsT0;
    public bool IsPath => V.IsT1;
    public bool IsHeader => V.IsT2;
    
    public LexedPath AsPath => V.AsT1;
    public Header AsHeader => V.AsT2;
    
    public OneOf<T, LexedPath, Header> MapEmpty<T>(T val) =>
        V.Match(_ => (OneOf<T, LexedPath, Header>)val, path => path, header => header);
    public OneOf<EmptyCell, T, Header> MapPath<T>(Func<LexedPath, T> f) => V.MapT1(f);
    public OneOf<EmptyCell, LexedPath, T> MapHeader<T>(Func<Header, T> f) => V.MapT2(f);
    
    public bool TryPickPath(out LexedPath path, out OneOf<EmptyCell, LexedCell.Header> rem) =>
        V.TryPickT1(out path, out rem);
    public bool TryPickHeader(out Header header, out OneOf<EmptyCell, LexedPath> rem) =>
        V.TryPickT2(out header, out rem);

    public bool Is(CellKind kind) => kind == (CellKind)V.Index;
}

internal readonly record struct ConvertedPathElmt(OneOf<JsonVal.Str, ArrIdx> V)
{
    // public static implicit operator ConvertedPathElmt(OneOf<JsonVal.Str, ArrIdx> v) => new(v);
    // public static implicit operator OneOf<JsonVal.Str, ArrIdx>(ConvertedPathElmt v) => v.V;
    
    public bool IsKey => V.IsT0;
    public bool IsIdx => V.IsT1;
    
    public JsonVal.Str AsKey => V.AsT0;
    public ArrIdx AsIdx => V.AsT1;
    
    public OneOf<T, ArrIdx> MapKey<T>(Func<JsonVal.Str, T> f) => V.MapT0(f);
    public OneOf<JsonVal.Str, T> MapIdx<T>(Func<ArrIdx, T> f) => V.MapT1(f);
    
    public bool TryPickKey(out JsonVal.Str key, out ArrIdx idx) => V.TryPickT0(out key, out idx);
}

internal readonly record struct ConvertedPath(IReadOnlyList<ConvertedPathElmt> Elmts)
{
    public bool Equals(ConvertedPath other) => Elmts.SequenceEqual(other.Elmts);
    public override int GetHashCode() => Elmts.Aggregate(0, HashCode.Combine);
    public static ConvertedPath Empty { get; } = new(Array.Empty<ConvertedPathElmt>());

    public string ToJsonPath() =>
        string.Join("",
            Elmts.Select(elmt =>
                elmt.V.Match(key => $".{key.V}", idx => $"[{idx}]")
            )
        );
}

internal readonly record struct AstNode(LexedPath Path, OneOf<JsonVal, IReadOnlyList<AstNode>> ValOrDesc)
{
    public bool HasVal => ValOrDesc.IsT0;
    public bool HasDesc => ValOrDesc.IsT1;
    
    public JsonVal AsVal => ValOrDesc.AsT0;
    public IReadOnlyList<AstNode> AsDesc => ValOrDesc.AsT1;
    
    public bool TryPickVal(out JsonVal val, out IReadOnlyList<AstNode> desc) =>
        ValOrDesc.TryPickT0(out val, out desc);
    public bool TryPickDesc(out IReadOnlyList<AstNode> desc, out JsonVal val) =>
        ValOrDesc.TryPickT1(out desc, out val);
}

internal readonly record struct StrayCell(CellKind Kind, Coord Coord);

internal readonly record struct Assignment(ConvertedPath Path, JsonVal Value);

// internal readonly record struct LexedValSeq(IReadOnlyList<OneOf<JsonVal, None>> Vals);

internal static class Logic
{
    readonly record struct ConvertPathsState(IDictionary<ConvertedPath, int> IdxForPartialPath)
    {
        public static ConvertPathsState Initial => new(new Dictionary<ConvertedPath, int>());
    }

    private static ConvertedPath ConvertPath(ConvertedPath prefixPath, LexedPath lexedPath, ConvertPathsState state)
    {
        var pathSegments = lexedPath.V.Segment(elmt => elmt.IsArrElmt);
        var cvtPathElmts = prefixPath.Elmts.ToList();

        ConvertedPathElmt ConvertProtoIdxElmt(ArrElmtKind arrElmt)
        {
            var partialPath = new ConvertedPath(cvtPathElmts.ToArray());
            if (!state.IdxForPartialPath.TryGetValue(partialPath, out var idx)) { idx = -1; }
            idx += arrElmt == ArrElmtKind.Plus ? 1 : 0;
            state.IdxForPartialPath[partialPath] = idx;
            return new ConvertedPathElmt(idx);
        }

        foreach (var pathSegment in pathSegments)
        {
            var elmt0 = pathSegment[0].V.Match(
                keyElmt => new ConvertedPathElmt(keyElmt),
                ConvertProtoIdxElmt
            );
            var otherElmts = pathSegment.Skip(1).Select(elmt => new ConvertedPathElmt(elmt.AsKey));
            cvtPathElmts.Add(elmt0);
            cvtPathElmts.AddRange(otherElmts);
        }
        
        return new ConvertedPath(cvtPathElmts);
    }

    private static IEnumerable<Assignment>
        YieldAssignments(ConvertedPath parentPath, ConvertPathsState cvtPathsState, AstNode astNode)
    {
        var cvtPath = ConvertPath(parentPath, astNode.Path, cvtPathsState);
        
        return astNode.ValOrDesc.Match(
            val => new[] { new Assignment(cvtPath, val) },
            desc => desc.SelectMany(d => YieldAssignments(cvtPath, cvtPathsState, d))
        );
    }

    private static bool FindStrayCell(LexedSubSheet sheet, CellKind kind, Coord firstCoord, out StrayCell stray) =>
        sheet.Find(firstCoord, cell => cell.Is(kind))
            .MapFound(coord => new StrayCell(kind, sheet.ToOuter(coord)))
            .TryPickT0(out stray, out _);
    
    private static IEnumerable<(LexedPath path, int beg, int end)> GetPathRanges(IReadOnlyList<LexedCell> cellSeq)
    {
        var seq = cellSeq
            .Select((cell, idx) => (cell, idx))
            .Where(t => t.cell.IsPath)
            .Select(t => (t.cell.AsPath, t.idx))
            .ToList();
        
        var endsSeq = seq.Select(t => t.idx).Skip(1).Concat(new[] { cellSeq.Count });
        
        return seq.Zip(endsSeq, (t, end) => (t.AsPath, t.idx, end));
    }

    readonly record struct AstStub(LexedPath path, OneOf<LexedSubSheet, IReadOnlyList<AstStub>> StubOrDesc);
    
    private static IEnumerable<AstStub> ParsePathCols(
        LexedSubSheet pathCols,
        LexedSubSheet pathRows,
        LexedSubSheet interior
        )
    {
        var seq = GetPathRanges(pathCols.SliceCols(0..1).CellSeq.ToList());
        var remCols = pathCols.SliceCols(1..);
        
        if (remCols.Rect.Dims.Col == 0)
        {
            foreach (var (path, begRow, endRow) in seq)
            {
                var desc = ParsePathRows(pathRows, interior.SliceRows(begRow..endRow));
                yield return new AstStub(path, desc.ToList());
            }
            yield break;
        }
        
        foreach (var (path, begRow, endRow) in seq)
        {
            var desc = ParsePathCols(remCols.SliceRows(begRow..endRow), pathRows, interior.SliceRows(begRow..endRow));
            yield return new AstStub(path, desc.ToList());
        }
    }

    private static IEnumerable<AstStub> ParsePathRows(LexedSubSheet pathRows, LexedSubSheet interior)
    {
        var seq = GetPathRanges(pathRows.SliceRows(0..1).CellSeq.ToList());
        var remRows = pathRows.SliceRows(1..);

        if (remRows.Rect.Dims.Row == 0)
        {
            foreach (var (path, begCol, endCol) in seq)
            {
                yield return new AstStub(path, interior.SliceCols(begCol..endCol));
            }
            yield break;
        }

        foreach (var (path, begCol, endCol) in seq)
        {
            var desc = ParsePathRows(remRows.SliceCols(begCol..endCol), interior.SliceCols(begCol..endCol));
            yield return new AstStub(path, desc.ToList());
        }
    }

    private static OneOf<IReadOnlyList<AstNode>, StrayCell> ParseMtx(LexedSubSheet subSheet)
    {
        var (mtxKind, isTp) = subSheet[(0,0)].AsHeader.AsMtx;
        subSheet = isTp ? subSheet.Tpose : subSheet;

        StrayCell strayCell = new();
        
        if (subSheet.Rect.Dims.Row == 1) {
            if (FindStrayCell(subSheet.SliceCols(1..), CellKind.Header, (0,0), out strayCell)) { return strayCell; }
            return new List<AstNode>();
        }
        
        if (subSheet.Rect.Dims.Col == 1) {
            if (FindStrayCell(subSheet.SliceRows(1..), CellKind.Header, (0,0), out strayCell)) { return strayCell; }
            return new List<AstNode>();
        }

        var pathRowBeg = subSheet.SliceCols(1..).Find(cell => cell.IsPath)
            .V.Match(coord => coord + Coord.Of01, _ => subSheet.End);
        var pathColBeg = subSheet.SliceCols(0..pathRowBeg.Col).Tpose.Find(cell => cell.IsPath)
            .V.Match(coord => coord.SwizCR, _ => subSheet.End with { Col = pathRowBeg.Col });
        
        var ((margin, pathRows), (pathCols, interior)) = subSheet.Quarter((pathColBeg.Row, pathRowBeg.Col));

        if (margin.Rect.CoordSeq.Cast<Coord?>().ElementAtOrDefault(1) is Coord marginCoord1)
        {
            if (FindStrayCell(margin, CellKind.Header, marginCoord1, out strayCell)) { return strayCell; }
            if (FindStrayCell(margin, CellKind.Path, marginCoord1, out strayCell)) { return strayCell; }
        }
        
        if (FindStrayCell(pathRows, CellKind.Header, (0,0), out strayCell)) { return strayCell; }
        pathRows = pathRows.SliceRows(pathRowBeg.Row..);
        if (FindStrayCell(pathCols, CellKind.Header, (0,0), out strayCell)) { return strayCell; }
        pathCols = pathCols.SliceCols(pathColBeg.Col..);

        var ((intPadTL, intPadTR), (intPadBL, newInterior)) = interior.Quarter((pathColBeg.Row, pathRowBeg.Col));
        if (FindStrayCell(intPadBL, CellKind.Header, (0,0), out strayCell)) { return strayCell; }
        if (FindStrayCell(intPadTR, CellKind.Header, (0,0), out strayCell)) { return strayCell; }
        if (FindStrayCell(intPadTL, CellKind.Header, (0,0), out strayCell)) { return strayCell; }
        if (FindStrayCell(intPadBL, CellKind.Path, (0,0), out strayCell)) { return strayCell; }
        if (FindStrayCell(intPadTR, CellKind.Path, (0,0), out strayCell)) { return strayCell; }
        if (FindStrayCell(intPadTL, CellKind.Path, (0,0), out strayCell)) { return strayCell; }
        interior = newInterior;

        var stubs = ParsePathCols(pathCols, pathRows, interior);
        
        void IEnumerable<AstStub> ParsePathRows(LexedSubSheet pathRows, LexedSubSheet interior)
        {
            
        }
          
        // TODO WORK FROM HERE!!
        
        if (!ParsePathRow(pathCol.Tpose).TryPickT0(out var pathsAndRowRanges, out strayCell)) { return strayCell; }
        if (!ParsePathRow(pathRow).TryPickT0(out var pathsAndColRanges, out strayCell)) { return strayCell; }

        var firstRow = pathsAndRowRanges[0].beg;
        if (firstRow != 0)
        {
            var hSlice = interior.SliceRows(..firstRow);
            if (HasStrayHeader(hSlice, true, out strayCell)) { return strayCell; }
            if (HasStrayPath(hSlice, out strayCell)) { return strayCell; }
        }
        
        var firstCol = pathsAndColRanges[0].beg;
        if (firstCol != 0)
        {
            var vSlice = interior.SliceCols(..firstCol);
            if (HasStrayHeader(vSlice, true, out strayCell)) { return strayCell; }
            if (HasStrayPath(vSlice, out strayCell)) { return strayCell; }
        }

        IEnumerable<MtxNode.Branch> BranchesForHSlice(LexedPath rowPath, LexedSubSheet hSlice)
        {
            foreach (var (colPath, colBeg, colEnd) in pathsAndColRanges.V)
            {
                var leaf = new MtxNode.Leaf(hSlice.SliceCols(colBeg..colEnd));
                yield return new MtxNode.Branch(colPath, new[] { leaf });
            }
        }

        IEnumerable<MtxNode.Branch> Branches()
        {
            foreach (var (rowPath, rowBeg, rowEnd) in pathsAndRowRanges.V)
            {
                var hSlice = interior.SliceRows(rowBeg..rowEnd);
                var branches = BranchesForHSlice(rowPath, hSlice);
                yield return new MtxNode.Branch(rowPath, branches);
            }
        }

        return new MtxNode.Branch(LexedPath.Empty, Branches());*/
    }

    private static MtxParseResult ParseValCell(LexedSubSheet subSheet) =>
        HasStrayHeader(subSheet, false, out var sh)
            ? sh
            : HasStrayPath(subSheet, out var sp)
                ? sp
                : new MtxNode.Leaf(subSheet[(0, 0)].AsHeader.AsVal.Val);
    
    public static OneOf<IReadOnlyList<AstNode>, JsonVal, StrayCell> ParseHeader(LexedSubSheet subSheet)
    {
        var firstNonBlank = subSheet.Find((cell, _) => !cell.IsBlank);
        if (!firstNonBlank.TryPickFound(out var coord)) { return new List<AstNode>(); }
        
        
        
        // var findHeader = subSheet.Find((cell, coord) => cell.IsHeader)
        //     .V.Match(coord => coord, notFound => subSheet.End);
        // var innerSheet = subSheet.Slice(findHeader..subSheet.End);
        // if (!findHeader.TryPickFound(out var coord)) { return new NotFound(); }
        
        

        /*return subSheet[(0, 0)].
            
            .Match(
            mtxHeader => ParseMtx(subSheet),
            va => ParseValCell(subSheet)
        );*/
    }
}

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