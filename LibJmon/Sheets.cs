using System.Text;
using CommunityToolkit.HighPerformance;

namespace LibJmon.Sheets;

internal readonly record struct Coord(int Row, int Col)
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

internal static class CoordExt
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

internal readonly record struct Rect(Coord Beg, Coord End)
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

internal static class RectExt
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

internal static class SubSheet
{
    public static SubSheet<T> Create<T>(ReadOnlyMemory2D<T> mem) => new(mem, Coord.Of00, false);
    public static SubSheet<T> Create<T>(T[,] array) => new(array, Coord.Of00, false);
}

internal readonly record struct SubSheet<T>(ReadOnlyMemory2D<T> NonTposedMem, Coord OuterBeg, bool IsTposed)
{
    // TODO move to Ext?
    private ReadOnlySpan2D<T> NonTposedSpan => NonTposedMem.Span;

    public T this[Coord coord] => this[coord.Row, coord.Col];
    
    public T this[int row, int col] =>
        IsTposed ? NonTposedSpan[col, row] : NonTposedSpan[row, col];

    private Coord NonTposedEnd => new Coord(NonTposedMem.Height, NonTposedMem.Width);
    
    // public Coord End => new Coord(Memory.Height, Memory.Width);
    public Coord End => IsTposed ? NonTposedEnd.SwizCR() : NonTposedEnd;

    public Rect Rect => (Coord.Of00, End);
    
    public Rect OuterRect => (OuterBeg, OuterBeg + NonTposedEnd);

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

internal static class SubSheetExt
{
    public static IEnumerable<(Coord coord, T cell)> CoordAndCellSeq<T>(this SubSheet<T> sheet) =>
        sheet.Rect.CoordSeq().Select(c => (c, sheet[c]));
    public static IEnumerable<(Coord coord, T cell)> CoordAndCellSeq<T>(this SubSheet<T> sheet, Coord first) =>
        sheet.Rect.CoordSeq(first).Select(c => (c, sheet[c]));
    public static IEnumerable<(Coord coord, T cell)>
        CoordAndCellSeq<T>(this SubSheet<T> sheet, Coord first, Coord last) =>
            sheet.Rect.CoordSeq(first, last).Select(c => (c, sheet[c]));

    public static IEnumerable<T> CellSeq<T>(this SubSheet<T> sheet) =>
        sheet.Rect.CoordSeq().Select(c => sheet[c]);
    public static IEnumerable<T> CellSeq<T>(this SubSheet<T> sheet, Coord first) =>
        sheet.Rect.CoordSeq(first).Select(c => sheet[c]);
    public static IEnumerable<T> CellSeq<T>(this SubSheet<T> sheet, Coord first, Coord last) =>
        sheet.Rect.CoordSeq(first, last).Select(c => sheet[c]);
    
    public static SubSheet<T> Slice<T>(this SubSheet<T> sheet, Rect innerRect)
    {
        Rect nonTpInnerRect = sheet.IsTposed ? innerRect.Tpose() : innerRect;
        var (rows, cols) = nonTpInnerRect.ToRanges();

        // https://github.com/CommunityToolkit/dotnet/issues/673
        // TODO: Revise if updating CommunityToolkit.HighPerformance
        var buggyRows = sheet.NonTposedMem.Height..sheet.NonTposedMem.Height;
        var buggyCols = sheet.NonTposedMem.Width..sheet.NonTposedMem.Width;
        if (rows.Equals(buggyRows)) { rows = 0..0; }
        if (cols.Equals(buggyCols)) { cols = 0..0; }

        return new SubSheet<T>(
            sheet.NonTposedMem[rows, cols],
            sheet.OuterBeg + nonTpInnerRect.Beg,
            sheet.IsTposed
        );
    }
    
    public static SubSheet<T> Slice<T>(this SubSheet<T> sheet, Range rowRange, Range colRange) =>
        sheet.Slice(Rect.FromRanges(rowRange, colRange, sheet.End));
    
    public readonly record struct Quartering<Tc>(
        (SubSheet<Tc> l, SubSheet<Tc> r) T,
        (SubSheet<Tc> l, SubSheet<Tc> r) B
    );

    public static Quartering<T> Quarter<T>(this SubSheet<T> sheet, Coord brBeg)
    {
        var ((tl, tr), (bl, br)) = sheet.Rect.Quarter(brBeg);
        return new Quartering<T>(
            (sheet.Slice(tl), sheet.Slice(tr)),
            (sheet.Slice(bl), sheet.Slice(br))
        );
    }

    public static SubSheet<T> SliceCols<T>(this SubSheet<T> sheet, Range colRange) =>
        sheet.Slice(Range.All, colRange);
    public static SubSheet<T> SliceRows<T>(this SubSheet<T> sheet, Range rowRange) =>
        sheet.Slice(rowRange, Range.All);
    
    public static SubSheet<T> Tpose<T>(this SubSheet<T> sheet) =>
        sheet with { IsTposed = !sheet.IsTposed };

    public static Coord ToOuter<T>(this SubSheet<T> sheet, Coord localCoord) =>
        sheet.OuterBeg + (sheet.IsTposed ? localCoord.SwizCR() : localCoord);

    public static Coord ToInner<T>(this SubSheet<T> sheet, Coord outerCoord)
    {
        var c = outerCoord - sheet.OuterBeg;
        return sheet.IsTposed ? c.SwizCR() : c;
    }
}