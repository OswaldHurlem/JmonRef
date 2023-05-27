using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using LibJmon.Linq;
using LibJmon.Sheets;
using LibJmon.SuperTypes;
using LibJmon.Types;
using OneOf;
using OneOf.Types;
  
namespace LibJmon.Impl;

public static class Ast
{
    private static IEnumerable<(LexedPath path, int beg, int end)> GetPathRanges(IReadOnlyList<LexedCell> cellSeq)
    {
        var seq = cellSeq
            .Select((cell, idx) => (path: (cell as LexedCell.Path)?.V, idx))
            .Where(t => t.path is not null)
            .ToList();
        
        var endsSeq = seq.Select(t => t.idx).Skip(1).Concat(new[] { cellSeq.Count });
        return seq.Zip(endsSeq, (t, end) => (t.path!, t.idx, end));
    }

    // Converts to AstNode.Mtx.Item
    // LexedPaths combine by concatenation BUT only the last LexedPath can have IsAppend=true
    // private readonly record struct MtxItemProto(ImmutableArray<LexedPath> PathBuilder, AstNode Node);
    
    // TODO return BadPathElmt
    private static IEnumerable<BranchItem>
        ParsePathCols(SubSheet<LexedCell> pathCols, SubSheet<LexedCell> pathRows, SubSheet<LexedCell> interior)
    {
        var seq = GetPathRanges(pathCols.SliceCols(..1).CellSeq().ToList());
        var remCols = pathCols.SliceCols(1..);
        
        if (remCols.Rect.Dims().Col == 0)
        {
            foreach (var (path, begRow, endRow) in seq)
            {
                var branchItems = ParsePathRows(pathRows, interior.SliceRows(begRow..endRow));
                AstNode.Branch prop = new(branchItems.ToImmutableArray(), BranchKind.Range);
                yield return new(path, prop);
            }
            yield break;
        }
        
        foreach (var (path, begRow, endRow) in seq)
        {
            var branchItems =
                ParsePathCols(remCols.SliceRows(begRow..endRow), pathRows, interior.SliceRows(begRow..endRow));
            AstNode.Branch prop = new(branchItems.ToImmutableArray(), BranchKind.Range);
            yield return new(path, prop);
        }
    }

    private static IEnumerable<BranchItem>
        ParsePathRows(SubSheet<LexedCell> pathRows, SubSheet<LexedCell> interior)
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
            var subSeq = ParsePathRows(remRows.SliceCols(begCol..endCol), interior.SliceCols(begCol..endCol));
            AstNode.Branch prop = new(subSeq.ToImmutableArray(), BranchKind.Range);
            yield return new(path, prop);
        }
    }

    private record StrayCell(Coord Coord, string Type);

    private static AstNode.Error StrayCellAtInnerCoord(SubSheet<LexedCell> sheet, Coord innerCoord)
    {
        var obj = new StrayCell(sheet.ToOuter(innerCoord), sheet[innerCoord].GetType().Name);
        return new AstNode.Error(obj.ToString());
    }

    private static AstNode ParseMtx(SubSheet<LexedCell> subSheet)
    {
        var (mtxKind, isTp) = (subSheet[0, 0] as LexedCell.MtxHead)!; 
        subSheet = isTp ? subSheet.Tpose() : subSheet;

        AstNode? FindStray(SubSheet<LexedCell> sheet, int skip, Func<LexedCell, bool> pred) =>
            sheet.CoordAndCellSeq().Skip(skip).Find(pred) switch
            {
                Coord strayCoord => StrayCellAtInnerCoord(sheet, strayCoord),
                _ => null
            };

        var pathRowBegOrNull = subSheet.CoordAndCellSeq().Find(cell => cell is LexedCell.Path);
        if (pathRowBegOrNull is not Coord pathRowBeg)
        {
            return FindStray(subSheet, 1, cell => cell is not LexedCell.Blank) switch
            {
                { } strayInEmptyMtx => strayInEmptyMtx,
                _ => AstNode.Branch.Empty(mtxKind.ToBranchKind())
            };
        }

        var pathColSearchRange = subSheet.SliceCols(0..pathRowBeg.Col).Tpose();
        var pathColBegOrNull = pathColSearchRange.CoordAndCellSeq().Find(cell => cell is LexedCell.Path);
        // TODO improve this error. Situation is that no pathColBeg can be found given a pathRowBeg
        if (pathColBegOrNull is not Coord pathColBeg) { return StrayCellAtInnerCoord(subSheet, pathRowBeg); }
        pathColBeg = subSheet.ToInner(pathColSearchRange.ToOuter(pathColBeg));

        var ((margin, pathRows), (pathCols, interior)) = subSheet.Quarter((pathColBeg.Row, pathRowBeg.Col));
        
        if (FindStray(margin, 1, cell => cell is not LexedCell.Blank) is {} strayInMargin) { return strayInMargin; }
        if (FindStray(pathRows, 0, cell => cell.IsHeader()) is {} strayInPathRows) { return strayInPathRows; }
        if (FindStray(pathCols, 0, cell => cell.IsHeader()) is {} strayInPathCols) { return strayInPathCols; }
        
        pathRows = pathRows.SliceRows(pathRowBeg.Row..);
        pathCols = pathCols.SliceCols(pathColBeg.Col..);

        // TODO where to check validity of paths with IsAppend=true?
        // TODO where to check that first path element has correct type?
        
        var mtxItems = ParsePathCols(pathCols, pathRows, interior);
        return new AstNode.Branch(mtxItems.ToImmutableArray(), mtxKind.ToBranchKind());
    }

    public static OneOf<AstNode, None> ParseJmon(SubSheet<LexedCell> subSheet)
    {
        var firstNonBlankOrNull = subSheet.CoordAndCellSeq().Find(cell => cell is not LexedCell.Blank);
        if (firstNonBlankOrNull is not Coord firstNonBlank) { return new None(); }
        
        if (!subSheet[firstNonBlank].IsHeader())
        {
            return StrayCellAtInnerCoord(subSheet, firstNonBlank);
        }

        var lower = subSheet.SliceRows(firstNonBlank.Row..);
        
        var left = lower.SliceCols(0..firstNonBlank.Col);
        var strayInLeftOrNull = left.CoordAndCellSeq().Find(cell => cell is not LexedCell.Blank);
        if (strayInLeftOrNull is { } stray) { return StrayCellAtInnerCoord(left, stray); }

        var valRange = lower.SliceCols(firstNonBlank.Col..);

        return subSheet[firstNonBlank] switch
        {
            LexedCell.JVal jVal => valRange.CoordAndCellSeq().Skip(1).Find(cell => cell is not LexedCell.Blank) switch
            {
                { } strayCoord => StrayCellAtInnerCoord(valRange, strayCoord),
                _ => new AstNode.ValCell(jVal.V)
            },
            LexedCell.MtxHead mtxHead => ParseMtx(valRange),
            _ => throw new UnreachableException()
        };
    }
}