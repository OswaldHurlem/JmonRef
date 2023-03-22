using System.Diagnostics;
using CommunityToolkit.HighPerformance;

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

    private IEnumerable<Coord> GetCoords() =>
        from r in Enumerable.Range(this.Beg.Row, this.End.Row - this.Beg.Row)
        from c in Enumerable.Range(this.Beg.Col, this.End.Col - this.Beg.Col)
        select new Coord(r, c);

    public IEnumerable<Coord> Coords => GetCoords();
    
    public readonly record struct Quartering((Rect l, Rect r) T, (Rect l, Rect r) B);
    
    public Quartering Quarter(Coord brBeg) =>
        new(
            ((Beg, brBeg),              (Beg + brBeg.Swiz0C, End)),
            ((Beg + brBeg.SwizR0, End), (brBeg, End)             )
        );
}

/*public static class Glyphs
{
    public const string Comment = "//";
    public const string Path = ".";
    public const string ArrHeader = ":[";
    public const string ObjHeader = ":{";
    public const string DQJsonLit = ":::";
}*/

public enum MtxKind { Arr, Obj };

internal abstract record class LexedCell
{
    public record class Blank : LexedCell;
    public record class PathCell(LexedPath Path) : LexedCell;

    public abstract record class ValHead : LexedCell {
        public record class MtxHead(MtxKind MtxKind, bool IsTp) : ValHead;
        public record class ValCell(JsonVal Val) : ValHead;
    }
}

internal readonly record struct LexedSubSheet(ReadOnlyMemory2D<LexedCell> Memory, Coord OuterBeg)
{
    public ReadOnlySpan2D<LexedCell> Span => Memory.Span;
    
    public LexedCell this[Coord coord] => Span[coord.Row, coord.Col];

    public Coord End => new Coord(Memory.Height, Memory.Width);

    public Rect Rect => (Coord.Of00, End);
    
    public Coord? Find(Func<LexedCell, Coord, bool> pred) =>
        Rect.Coords.Cast<Coord?>()
            .FirstOrDefault(coord => pred(this[coord!.Value], coord!.Value));

    public LexedSubSheet Slice(Rect innerRect)
    {
        var ranges = innerRect.ToRanges();
        var subMemory = Memory[ranges.rows, ranges.cols];
        return new LexedSubSheet(subMemory, OuterBeg + innerRect.Beg);
    }
    
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
}

/*internal readonly record struct LexedSheet(LexedCell[,] Cells)
{
    public LexedCell this[Coord coord] => Cells[coord.Row, coord.Col];
    public LexedCell this[int row, int col] => Cells[row, col];
}*/

internal abstract record class JsonVal(/* TODO */)
{
    public sealed record class Str(string V) : JsonVal;
    public sealed record class AssignmentList(IEnumerable<Assignment> Assignments) : JsonVal;
}

internal abstract record class InputPathElmt
{
    public sealed record class Key(JsonVal.Str V) : InputPathElmt;
    public sealed record class Arr(bool IsPlus) : InputPathElmt;
}

internal readonly record struct LexedPath(IReadOnlyList<InputPathElmt> Elmts)
{
    public static LexedPath Empty { get; } = new(Array.Empty<InputPathElmt>());
}

internal abstract record class MtxNode
{
    public sealed record class Leaf(JsonVal Val) : MtxNode;
    public sealed record class Branch(LexedPath LexedPath, IEnumerable<MtxNode> Children) : MtxNode;
}

internal abstract record ConvertedPathElmt
{
    public sealed record class Key(JsonVal.Str V) : ConvertedPathElmt;
    public sealed record class Idx(ArrIdx V) : ConvertedPathElmt;
}

internal readonly record struct ConvertedPath(IReadOnlyList<ConvertedPathElmt> Elmts)
{
    public bool Equals(ConvertedPath other) => Elmts.SequenceEqual(other.Elmts);
    public override int GetHashCode() => Elmts.Aggregate(0, HashCode.Combine);
    public static ConvertedPath Empty { get; } = new(Array.Empty<ConvertedPathElmt>());

    public string ToJsonPath() =>
        string.Join("", Elmts.Select(elmt => elmt switch
        {
            ConvertedPathElmt.Key key => $".{key.V.V}",
            ConvertedPathElmt.Idx idx => $"[{idx.V}]",
            _ => throw new UnreachableException()
        }));
}

internal readonly record struct Assignment(ConvertedPath Path, JsonVal Value);

internal static class Logic
{
    readonly record struct ConvertPathsState(IDictionary<ConvertedPath, int> IdxForPartialPath)
    {
        public static ConvertPathsState Initial => new(new Dictionary<ConvertedPath, int>());
    }

    private static ConvertedPath ConvertPath(
        ConvertedPath prefixPath,
        LexedPath lexedPath,
        ConvertPathsState state
    )
    {
        var inputElmts = lexedPath.Elmts;

        var arrElmtsAt = lexedPath.Elmts.Select((elmt, idx) => (elmt, idx))
            .Where(t => t.elmt is InputPathElmt.Arr)
            .Select(t => t.idx).ToArray();

        var keyRangeStarts = new[] { 0 }.Concat(arrElmtsAt.Select(i => i + 1));
        var keyRangeEnds = arrElmtsAt.Concat(new[] { inputElmts.Count });

        var cvtPathElmts = prefixPath.Elmts.ToList();

        foreach ((int start, int end) keyRange in keyRangeStarts.Zip(keyRangeEnds))
        {
            var nKeys = keyRange.end - keyRange.start;
            var inputKeys = inputElmts
                .Skip(keyRange.start).Take(nKeys)
                .Cast<InputPathElmt.Key>()
                .Select(keyElmt => new ConvertedPathElmt.Key(keyElmt.V));
            cvtPathElmts.AddRange(inputKeys);

            if (keyRange.end == inputElmts.Count) { break; }

            var partialPath = new ConvertedPath(cvtPathElmts.ToArray());
            if (!state.IdxForPartialPath.TryGetValue(partialPath, out var idx)) { idx = -1; }
            idx += ((InputPathElmt.Arr)inputElmts[keyRange.end]).IsPlus ? 1 : 0;
            state.IdxForPartialPath[partialPath] = idx;
            cvtPathElmts.Add(new ConvertedPathElmt.Idx(idx));
        }
        
        return new ConvertedPath(cvtPathElmts);
    }

    private static IEnumerable<Assignment>
        YieldAssignments(ConvertedPath parentPath, ConvertPathsState cvtPathsState, MtxNode mtxNode)
    {
        switch (mtxNode)
        {
            case MtxNode.Branch branch:
                var cvtPath = ConvertPath(parentPath, branch.LexedPath, cvtPathsState);
                return from child in branch.Children
                    from assignment in YieldAssignments(cvtPath, cvtPathsState, child)
                    select assignment;
            case MtxNode.Leaf leaf:
                return new[] { new Assignment(parentPath, leaf.Val) };
            default:
                throw new ArgumentOutOfRangeException(nameof(mtxNode));
        }
    }

    //public static IEnumerable<Assignment> MakeAssignments(IEnumerable<MtxNode> mtxNodes) =>
    //    YieldAssignments(
    //        ConvertedPath.Empty,
    //        ConvertPathsState.Initial,
    //        new MtxNode.Branch(LexedPath.Empty, mtxNodes)
    //    );

    // public static LexedSheet LexSheet(Values.JmonSheet jmonSheet) => throw new NotImplementedException();

    static void CheckForUnexpected<T>(LexedSubSheet subSheet, bool include00, string msg) where T: LexedCell
    {
        bool ValHeadOtherThan00(LexedCell cell, Coord coord) =>
            cell is T && (!include00 || coord != Coord.Of00);
        if (subSheet.Find(ValHeadOtherThan00) is not Coord coord) { return; }
        var trueCoord = coord + subSheet.OuterBeg;
        throw JmonException.AtCoord(trueCoord, msg);
    }
    
    public static MtxNode Asdf(LexedSubSheet subSheet)
    {
        if (subSheet.Span[0,0] is not LexedCell.ValHead head) { throw new UnreachableException(); }

        
        
        switch (head)
        {
            case LexedCell.ValHead.MtxHead mtxHead:
                var intBeg = subSheet
                    .Slice((Coord.Of11, subSheet.End))
                    .Find((cell, coord) => cell is LexedCell.ValHead)
                    ?? subSheet.End;
                var ((margin, pathRows), (pathCols, interior)) = subSheet.Quarter(intBeg);
                CheckForUnexpected<LexedCell.ValHead>(margin, false, "Unexpected Header Cell");
                CheckForUnexpected<LexedCell.ValHead>(pathRows, true, "Unexpected Header Cell");
                CheckForUnexpected<LexedCell.ValHead>(pathCols, true, "Unexpected Header Cell");
                CheckForUnexpected<LexedCell.PathCell>(interior, false, "Unexpected Path Cell");
                
                break;
            case LexedCell.ValHead.ValCell valCell:
                CheckForUnexpected<LexedCell.ValHead>(subSheet, false, "Unexpected Header Cell");
                return new MtxNode.Leaf(valCell.Val);
                break;
        }
        
        throw new UnreachableException();
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
    private static LexedPath LexInputPath(string pathString)
    {
        var elmts = pathString.Split('.').Skip(1).Select(elmt => elmt switch
        {
            "+" => new InputPathElmt.Arr(true) as InputPathElmt,
            "$" => new InputPathElmt.Arr(false) as InputPathElmt,
            _ => new InputPathElmt.Key(new JsonVal.Str(elmt)) as InputPathElmt
        }).ToList();
        return new LexedPath(elmts);
    }
    
    private static IEnumerable<MtxNode> ParseMtxNodes(string[,] simpleMatrix)
    {
        var colPathsWithIdxs = Enumerable.Range(0, simpleMatrix.GetLength(0)).Skip(1)
            .Select(rowIdx => (rowIdx, inputPath: LexInputPath(simpleMatrix[rowIdx, 0])))
            .ToList();
        var rowPathsWithIdxs = Enumerable.Range(0, simpleMatrix.GetLength(1)).Skip(1)
            .Select(colIdx => (colIdx, inputPath: LexInputPath(simpleMatrix[0, colIdx])))
            .ToList();

        foreach (var (rowIdx, pathFromCol) in colPathsWithIdxs)
        {
            MtxNode.Branch MakeBranch((int colIdx, LexedPath inputPath) t) =>
                new (
                    t.inputPath,
                    new[] { new MtxNode.Leaf(new JsonVal.Str(simpleMatrix[rowIdx, t.colIdx])) }
                );
            yield return new MtxNode.Branch(pathFromCol, rowPathsWithIdxs.Select(MakeBranch));
        }
    }
    
    public static IEnumerable<(string path, string val)> JQAssignmentsFromSimpleMatrix(string[,] simpleMatrix)
    {
        var assignments = Logic.MakeAssignments(ParseMtxNodes(simpleMatrix));
        return assignments.Select(assignment => (
            assignment.Path.ToJsonPath(),
            (assignment.Value as JsonVal.Str)?.V ?? throw new Exception()
        ));
    }
}