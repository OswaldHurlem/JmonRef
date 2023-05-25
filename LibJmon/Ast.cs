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
    private static IEnumerable<(IEnumerable<LexedPath> pathSeq, int beg, int end)>
        GetPathRanges(IReadOnlyList<LexedCell> cells, IEnumerable<LexedPath> pathPrefix)
    {
        IEnumerable<LexedPath>? MakePathBuilder(LexedCell cell) =>
            cell is LexedCell.Path pathCell
                ? pathPrefix.Append(pathCell.V)
                : null;
        
        var cellsWithPaths = cells
            .Select((cell, idx) => (pathSeq: MakePathBuilder(cell), idx))
            .Where(t => t.pathSeq is not null)
            .ToList();

        if (!cellsWithPaths.Any())
        {
            var singleRange = (pathPrefix, 0, cells.Count);
            return new[] { singleRange };
        }
        
        var endsSeq = cellsWithPaths.Select(t => t.idx).Skip(1).Concat(new[] { cells.Count });

        return cellsWithPaths.Zip(endsSeq, (t, end) => (t.pathSeq!, t.idx, end));
    }

    // Converts to AstNode.Mtx.Item
    // LexedPaths combine by concatenation BUT only the last LexedPath can have IsAppend=true
    private readonly record struct MtxItemProto(ImmutableArray<LexedPath> PathBuilder, AstNode Node);
    
    // TODO return BadPathElmt
    private static IEnumerable<MtxItemProto> ParsePathCols(
            IEnumerable<LexedPath> pathPrefix,
            SubSheet<LexedCell> pathCols,
            SubSheet<LexedCell> pathRows,
            SubSheet<LexedCell> interior
        )
    {
        var seq = GetPathRanges(pathCols.SliceCols(..1).CellSeq().ToList(), pathPrefix);
        var remCols = pathCols.SliceCols(1..);
        bool goToRows = remCols.Rect.Dims().Col == 0;

        return seq.SelectMany(
            t => goToRows
                ? ParsePathRows(t.pathSeq, pathRows, interior.SliceRows(t.beg..t.end))
                : ParsePathCols(t.pathSeq, remCols.SliceRows(t.beg..t.end), pathRows, interior.SliceRows(t.beg..t.end))
            // );
        ).ToList(); // TODO remove
    }

    private static IEnumerable<MtxItemProto>
        ParsePathRows(
            IEnumerable<LexedPath> pathPrefix,
            SubSheet<LexedCell> pathRows,
            SubSheet<LexedCell> interior
        )
    {
        var seq = GetPathRanges(pathRows.SliceRows(0..1).CellSeq().ToList(), pathPrefix);
        var remRows = pathRows.SliceRows(1..);

        if (remRows.Rect.Dims().Row == 0)
        {
            foreach (var (path, begCol, endCol) in seq)
            {
                var valOrNone = ParseJmon(interior.SliceCols(begCol..endCol));
                if (valOrNone.TryPickT0(out var val, out _)) { yield return new(path.ToImmutableArray(), val); }
            }
            yield break;
        }

        foreach (var (path, begCol, endCol) in seq)
        {
            var subSeq = ParsePathRows(path, remRows.SliceCols(begCol..endCol), interior.SliceCols(begCol..endCol));
            foreach (var item in subSeq) { yield return item; }
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
                _ => AstNode.Matrix.Empty(mtxKind)
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

        const AstNode.Error? kNotErr = null;

        AstNode.Error ErrorForPath(LexedPath path)
        {
            var pathJson = JsonSerializer.Serialize(path, JsonSerialization.Resources.JsonSerializerOptions);
            return new AstNode.Error($"Path {pathJson} not valid for matrix of kind {mtxKind}");
        }

        var mtxProtoItems = ParsePathCols(Enumerable.Empty<LexedPath>(), pathCols, pathRows, interior);
        List<AstNode.Matrix.Item> mtxItems = new();

        foreach (var (pathBuilder, node) in mtxProtoItems)
        {
            var nonBlankPaths = pathBuilder.Where(p => p.Items.Any()).ToList();
            
            if (nonBlankPaths.SkipLast(1).Any(path => path.IsAppend))
            {
                return new AstNode.Error("TODO"); // TODO
            }

            LexedPath combinedPath = new(
                nonBlankPaths.SelectMany(path => path.Items).ToImmutableArray(),
                nonBlankPaths.Last().IsAppend
            );

            if (!combinedPath.Items.Any())
            {
                return new AstNode.Error("Empty path"); // TODO
            }
            
            var expMtxKind = combinedPath.Items.First().AsOneOf().Match(key => MtxKind.Obj, idx => MtxKind.Arr);

            if (expMtxKind != mtxKind)
            {
                // TODO improve
                var pathJson = JsonSerializer.Serialize(combinedPath, JsonSerialization.Resources.JsonSerializerOptions);
                return new AstNode.Error($"Path {pathJson} not valid for matrix of kind {mtxKind}");
            }

            mtxItems.Add(new(combinedPath, node));
        }
        
        return new AstNode.Matrix(mtxItems.ToImmutableArray(), mtxKind);
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