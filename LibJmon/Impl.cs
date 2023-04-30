using System.Buffers;
using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CommunityToolkit.HighPerformance;
using LibJmon.Linq;
using LibJmon.Sheets;
using LibJmon.SuperTypes;
using LibJmon.Types;
using Microsoft.VisualBasic.FileIO;
using OneOf;
using OneOf.Types;

namespace LibJmon.Impl;

// internal readonly record struct Assignment(ConvertedPath Path, JsonVal Value);

// internal readonly record struct LexedValSeq(IReadOnlyList<OneOf<JsonVal, None>> Vals);

public static class Logic
{
    readonly record struct ConvertPathsState(IDictionary<ConvertedPath, int> IdxForPartialPath)
    {
        public static ConvertPathsState Initial => new(new Dictionary<ConvertedPath, int>());
    }

    private static ConvertedPath ConvertPath(ConvertedPath prefixPath, LexedPath lexedPath, ConvertPathsState state)
    {
        var cvtPathElmts = prefixPath.V.ToList();

        PathItem ConvertProtoIdxElmt(PathItem.Idx arrElmt)
        {
            var partialPath = new ConvertedPath(cvtPathElmts.ToImmutableArray());
            if (!state.IdxForPartialPath.TryGetValue(partialPath, out var idx)) { idx = -1; }
            idx += arrElmt.V;
            state.IdxForPartialPath[partialPath] = idx;
            return new PathItem.Idx(idx);
        }

        var pathSegments = lexedPath.V.Segment(elmt => elmt is PathItem.Idx);
        
        foreach (IReadOnlyList<PathItem> pathSegment in pathSegments)
        {
            var elmt0 = pathSegment[0].AsOneOf().Match(
                keyElmt => new PathItem.Key(keyElmt),
                ConvertProtoIdxElmt
            );
            
            var otherElmts = pathSegment.Skip(1)
                .Cast<PathItem.Key>()
                .Select(elmt => new PathItem.Key(elmt));
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
            .Where(t => t.path is not null)
            .ToList();
        
        var endsSeq = seq.Select(t => t.idx).Skip(1).Concat(new[] { cellSeq.Count });
        
        return seq.Zip(endsSeq, (t, end) => (t.path!, t.idx, end));
    }

    // TODO return BadPathElmt
    private static IEnumerable<AstNode.Branch.Item>
        ParsePathCols(SubSheet<LexedCell> pathCols, SubSheet<LexedCell> pathRows, SubSheet<LexedCell> interior)
    {
        var seq = GetPathRanges(pathCols.SliceCols(0..1).CellSeq().ToList());
        var remCols = pathCols.SliceCols(1..);
        
        if (remCols.Rect.Dims().Col == 0)
        {
            foreach (var (path, begRow, endRow) in seq)
            {
                var desc = ParsePathRows(pathRows, interior.SliceRows(begRow..endRow));
                var prop = new AstNode.Branch(desc.ToImmutableArray());
                yield return new(path, prop);
            }
            yield break;
        }
        
        foreach (var (path, begRow, endRow) in seq)
        {
            var desc = ParsePathCols(remCols.SliceRows(begRow..endRow), pathRows, interior.SliceRows(begRow..endRow));
            var prop = new AstNode.Branch(desc.ToImmutableArray());
            yield return new(path, prop);
        }
    }

    private static IEnumerable<AstNode.Branch.Item>
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
            var desc = ParsePathRows(remRows.SliceCols(begCol..endCol), interior.SliceCols(begCol..endCol));
            var prop = new AstNode.Branch(desc.ToImmutableArray());
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
                _ => AstNode.Branch.Empty
            };
        }

        var pathColSearchRange = subSheet.SliceCols(0..pathRowBeg.Col).Tpose();
        var pathColBegOrNull = pathColSearchRange.CoordAndCellSeq().Find(cell => cell is LexedCell.Path);
        if (pathColBegOrNull is not Coord pathColBeg) { return StrayCellAtInnerCoord(subSheet, pathRowBeg); }
        pathColBeg = subSheet.ToInner(pathColSearchRange.ToOuter(pathColBeg));

        var ((margin, pathRows), (pathCols, interior)) = subSheet.Quarter((pathColBeg.Row, pathRowBeg.Col));
        
        if (FindStray(margin, 1, cell => cell is not LexedCell.Blank) is {} strayInMargin) { return strayInMargin; }
        if (FindStray(pathRows, 0, cell => cell.IsHeader()) is {} strayInPathRows) { return strayInPathRows; }
        if (FindStray(pathCols, 0, cell => cell.IsHeader()) is {} strayInPathCols) { return strayInPathCols; }
        
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

        return new AstNode.Branch(ParsePathCols(pathCols, pathRows, interior).ToImmutableArray());
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
                _ => new AstNode.Leaf(jVal.V)
            },
            LexedCell.MtxHead mtxHead => ParseMtx(valRange),
            _ => throw new UnreachableException()
        };
    }

    private static IReadOnlyDictionary<string, LexedCell> SimpleLexedCells { get; } = new Dictionary<string, LexedCell>
    {
        {"", new LexedCell.Blank()},
        {":[", new LexedCell.MtxHead(MtxKind.Arr, false)},
        {":{", new LexedCell.MtxHead(MtxKind.Obj, false)},
        {":^[", new LexedCell.MtxHead(MtxKind.Arr, true)},
        {":^{", new LexedCell.MtxHead(MtxKind.Obj, true)},
    };

    public static string ConvertSqToDq(string sqStr) =>
        sqStr.Replace("\"", "\\\"")
            .Replace("\\'", "\uE0E1").Replace('\'', '\uE001')
            .Replace('\uE001', '\"').Replace('\uE0E1', '\'');

    // TODO no need to return string here (I think)
    public static OneOf<JsonVal.Any, string, None> TryParseJsonExpr(string text)
    {
        const string kNotJson = "";
        bool isDq = text.StartsWith("::") && (2 < text.Length);
        bool isSq = !isDq && text.StartsWith(':') && (1 < text.Length);
        
        string jsonCode = isDq
            ? text[2..]
            : (isSq ? ConvertSqToDq(text[1..]) : kNotJson);
        
        if (jsonCode == kNotJson) { return new None(); }
        
        var jsonOpts = JsonSerialization.Resources.JsonSerializerOptions;
        try
        {
            if (JsonSerializer.Deserialize<JsonVal.Any>(jsonCode, jsonOpts) is { } jsonVal) { return jsonVal; }
            throw new UnreachableException();
        }
        catch (JsonException e) { return e.Message; }
    }

    public enum PathSegmentType
    {
        Unquoted,
        SingleQuoted,
        DoubleQuoted
    };

    public static LexedCell Lex(ReadOnlySpan<byte> cellTextUtf8)
    {
        var cellText = Encoding.UTF8.GetString(cellTextUtf8);
        var jsonOpts = JsonSerialization.Resources.JsonSerializerOptions;
        
        var trimmedText = cellText.Trim();
        
        if (SimpleLexedCells.TryGetValue(trimmedText, out var cell)) { return cell; }
        
        if (trimmedText.StartsWith("//")) { return new LexedCell.Blank(); }

        if (trimmedText.StartsWith(':'))
        {
            var jsonLitParse = TryParseJsonExpr(trimmedText[1..]);
            if (jsonLitParse.TryPickT0(out JsonVal.Any jsonVal, out _)) { return new LexedCell.JVal(jsonVal); }
            if (jsonLitParse.TryPickT1(out string errMsg, out _))
            {
                return new LexedCell.Error($"Error when parsing JSON Literal: {errMsg}");
            }

            return new LexedCell.Error("Cell starts with ':' but is not a Header.");
        }
        
        if (trimmedText.StartsWith('.'))
        {
            var textAfterDot = trimmedText[1..];
            if (textAfterDot == "") { return new LexedCell.Path(LexedPath.Empty); }
            var pathAsJsonParse = TryParseJsonExpr(textAfterDot);

            if (pathAsJsonParse.TryPickT0(out JsonVal.Any pathAsJson, out _))
            {
                return pathAsJson.TryDeserialize<LexedPath>()
                    .MapT1(_ => pathAsJson.TryDeserialize<PathItem>().MapT0(LexedPath.WithOneItem))
                    .Match<OneOf<LexedPath, None>>(p => p, pathOrNone => pathOrNone)
                    .MapT0<LexedCell.Path>(p => p)
                    .MapT1(_ => new LexedCell.Error("JSON expression is not a LexedPath or PathItem"))
                    .Match<LexedCell>(p => p, err => err);
            }
            if (pathAsJsonParse.TryPickT1(out string errMsg, out _)) { return new LexedCell.Error(errMsg); }  // TODO

            var segments = textAfterDot.Split('.');
            var pathElmts = new PathItem[segments.Length];

            foreach (var (segment, idx) in segments.Select((s, i) => (s, i)))
            {
                switch (segment)
                {
                    case "+":
                        pathElmts[idx] = new PathItem.Idx(1);
                        continue;
                    case "^":
                        pathElmts[idx] = new PathItem.Idx(0);
                        continue;
                    default:
                        if (!Regex.IsMatch(segment, @"^\w+$"))
                        {
                            return new LexedCell.Error(@"Path elements must match regex ^\w+$");
                        }
                        var segmentUtf8 = Encoding.UTF8.GetBytes(segment).ToImmutableArray();
                        pathElmts[idx] = new PathItem.Key(segmentUtf8);
                        continue;
                }
            }

            return new LexedCell.Path(pathElmts.ToImmutableArray());
        }

        JsonVal.Any jsonValue = JsonSerializer.SerializeToNode(trimmedText, jsonOpts);
        return new LexedCell.JVal(jsonValue);
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

public static class CsvUtil
{
    public static IReadOnlyList<IReadOnlyList<string>> SplitDelimitedText(Stream stream, char delimiter)
    {
        IEnumerable<IReadOnlyList<string>> Inner()
        {
            using var csvReader = new TextFieldParser(stream)
            {
                Delimiters = new[] { "," },
                HasFieldsEnclosedInQuotes = true
            };

            while (!csvReader.EndOfData)
            {
                var row = csvReader.ReadFields();
                if (row is null) { throw new Exception("null row"); }
                yield return row;
            }
        }

        return Inner().ToList();
    }
}

public static class TestingApi
{
    public static LexedCell[,] LexCells(ReadOnlyMemory<byte>[,] cells)
    {
        var rect = new Rect((0, 0), (cells.GetLength(0), cells.GetLength(1)));
        var lexedCells = new LexedCell[rect.Dims().Row, rect.Dims().Col];

        foreach (var c in rect.CoordSeq())
        {
            lexedCells[c.Row, c.Col] = Logic.Lex(cells[c.Row, c.Col].Span);
        }
        
        return lexedCells;
    }


    public static AstNode ParseLexedCells(LexedCell[,] lexedCells) => throw new NotImplementedException();
}