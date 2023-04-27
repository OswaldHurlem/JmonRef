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

using ArrIdx = Int32;

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
        public readonly record struct Item(LexedPath Path, AstNode Result);
        
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

// internal readonly record struct Assignment(ConvertedPath Path, JsonVal Value);

// internal readonly record struct LexedValSeq(IReadOnlyList<OneOf<JsonVal, None>> Vals);

public sealed class ConvertedPathComparer : IEqualityComparer<ConvertedPath>
{
    public bool Equals(ConvertedPath pA, ConvertedPath pB) => pA.V.SequenceEqual(pB.V);
    public int GetHashCode(ConvertedPath p) => p.V.Aggregate(0, HashCode.Combine);
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

    // TODO no need to return string here (I think)
    public static OneOf<JsonVal.Any, string, None> TryParseJsonExpr(string text)
    {
        if (Regex.IsMatch("::.", text))
        {
            var jsonText = text[2..];
            JsonSerializerOptions jsonOpts = new JsonSerializerOptions(); // TODO
            if (JsonSerializer.Deserialize<JsonVal.Any>(jsonText, jsonOpts) is { } jsonVal) { return jsonVal; }
        }
        else if (Regex.IsMatch(":.", text))
        {
            // TODO
        }

        return new None(); // ???
    }

    public enum PathSegmentType
    {
        Unquoted,
        SingleQuoted,
        DoubleQuoted
    };

    public static LexedCell Lex(string cellText, Coord cellCoord)
    {
        var trimmedText = cellText.Trim();
        
        if (SimpleLexedCells.TryGetValue(trimmedText, out var cell)) { return cell; }
        
        if (trimmedText.StartsWith("//")) { return new LexedCell.Blank(); }

        // TODO make it so starting with colon but NOT having JSON literal is an error
        if (trimmedText.StartsWith(':'))
        {
            var jsonLitParse = TryParseJsonExpr(trimmedText[1..]);
            if (jsonLitParse.TryPickT0(out JsonVal.Any jsonVal, out _)) { return new LexedCell.JVal(jsonVal); }
            if (jsonLitParse.TryPickT1(out string errMsg, out _)) { return new LexedCell.Error(errMsg); } // TODO
        }
        
        if (trimmedText.StartsWith('.'))
        {
            var textAfterDot = trimmedText[1..];

            var pathAsJsonParse = TryParseJsonExpr(textAfterDot);
            if (pathAsJsonParse.TryPickT0(out JsonVal.Any pathAsJson, out _))
            {
                JsonSerializerOptions jsonOpts = new JsonSerializerOptions(); // TODO
                try { return new LexedCell.Path(pathAsJson.V.Deserialize<LexedPath>()); }
                catch (JsonException e) { return new LexedCell.Error(e.Message); } // TODO
            }
            if (pathAsJsonParse.TryPickT1(out string errMsg, out _)) { return new LexedCell.Error(errMsg); }  // TODO

            var segments = textAfterDot.Split('.');
            var pathElmts = new InputPathElmt[segments.Length];

            foreach (var (segment, idx) in segments.Select((s, i) => (s, i)))
            {
                switch (segment)
                {
                    case "+":
                        pathElmts[idx] = new InputPathElmt.ArrElmt(ArrElmtKind.Plus);
                        continue;
                    case "^":
                        pathElmts[idx] = new InputPathElmt.ArrElmt(ArrElmtKind.Stop);
                        continue;
                }

                if (Regex.IsMatch(segment, @"^\w+$"))
                {
                    var segmentUtf8 = Encoding.UTF8.GetBytes(segment).ToImmutableArray();
                    pathElmts[idx] = new InputPathElmt.Key(segmentUtf8);
                    continue;
                }
                
                InputPathElmt.ArrElmt? arrElmtOrNull = segment switch
                {
                    "+" => new InputPathElmt.ArrElmt(ArrElmtKind.Plus),
                    "^" => new InputPathElmt.ArrElmt(ArrElmtKind.Stop),
                    _ => null
                };

                return new LexedCell.Error("asdf"); // TODO
            }
            
            // TODO later: Allow quoted/json path segments to be used together with unquoted ones.
            if (textAfterDot.StartsWith(':'))
            {
                
            }
            
            // Scratch this. Instead permit \w's between dots OR .::<sq json array or string> OR .::<dq json arr/str>
            
            if (trimmedText.EndsWith('.')) { throw new Exception(); } // TODO
            
            // TODO RESUME HERE!!!!!!
            const string subEscapedQuote = "\uE000";
            var trimmedTextWithEscapedQuotes = trimmedText.Replace("\\\"", subEscapedQuote);
            
            List<char> trimmedTextWithEscapedQuotesAndQuotedDots = new List<char>();

            bool isInQuote = false;
            for (int i = 0; i < trimmedText.Length; i++)
            {
                if (trimmedTextWithEscapedQuotes[i] == '.')
                {
                    if (trimmedTextWithEscapedQuotes[i+1] == '\"')
                    {
                        
                    }
                }
            }

            foreach (var VARIABLE in COLLECTION)
            {
                
            }
            
            
            const string subQuotedDot = "\U000F000D";

            var subStrings = 
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

        return new LexedCell.JVal(jsonValue);
    }

    public static OneOf<SubSheet<LexedCell>, LexError> LexSheet(string[,] sheet)
    {
        var lexedCells = new LexedCell[sheet.GetLength(0), sheet.GetLength(1)];
        var rect = new Rect((0, 0), (sheet.GetLength(0), sheet.GetLength(1)));
        
        foreach (var (r,c) in rect.CoordSeq())
        {
            var lexResult = Lex(sheet[r, c], (r,c));
            if (lexResult.IsT0) { lexedCells[r, c] = lexResult.AsT0; }
            else { return lexResult.AsT1; }
        }
        
        return new SubSheet<LexedCell>(lexedCells, (0,0), false);
    }
}

public sealed record LexError(Coord Coord);

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