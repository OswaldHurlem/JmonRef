using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using LibJmon.Types;
using OneOf;
using OneOf.Types;
using LibJmon.Linq;
using LibJmon.Sheets;
using LibJmon.SuperTypes;

namespace LibJmon.Impl;

internal static class ApiV0Impl
{
    public static string ParseJmon(string[,] cells, JsonSerializerOptions jsonOptions)
    {
        LexedCell[,] lexedCells = Lexing.LexCells(cells, jsonOptions);
        AstNode ast = Ast.LexedCellsToAst(lexedCells);
        JsonVal.Any jsonVal = Construction.AstToJson(ast);
        return jsonVal.ToUtf16String();
    }
}

internal static partial class Lexing
{
    private static partial class Regexes
    {
        public static readonly Regex Dq = GenDq();
        public static readonly Regex Sq = GenSq();
        public static readonly Regex Word = GenWord();

        [GeneratedRegex("^\"(?:[^\"\\\\]|\\\\.)*\"")]
        private static partial Regex GenDq();
        [GeneratedRegex("^'(?:[^'\\\\]|\\\\.)*'")]
        private static partial Regex GenSq();
        [GeneratedRegex("^\\w+")]
        private static partial Regex GenWord();
    }

    private static IReadOnlyDictionary<string, LexedCell> SimpleLexedCells { get; } = new Dictionary<string, LexedCell>
    {
        {"", new LexedCell.Blank()},
        {":[", new LexedCell.MtxHead(MtxKind.Arr, false)},
        {":{", new LexedCell.MtxHead(MtxKind.Obj, false)},
        {":^[", new LexedCell.MtxHead(MtxKind.Arr, true)},
        {":^{", new LexedCell.MtxHead(MtxKind.Obj, true)},
    };

    // TODO 5/31 error handling
    private static LexedCell.Path LexPathCell(ReadOnlySpan<char> pathExpr, Coord coord)
    {
        const string kDot = ".";
        
        var pathItems = new List<PathItem>();
        bool isAppend = false;
        var remPathExpr = pathExpr;

        int itemIdx = 0;
        
        JmonException<LexingError> MakeExcForCurrentItem(string msg) =>
            new(new LexingError(coord.Row, coord.Col, $"Error when parsing path item {itemIdx}: {msg}"));
        
        while (!remPathExpr.IsEmpty)
        {
            if (!remPathExpr.StartsWith(kDot)) { throw MakeExcForCurrentItem("Expected '.' preceding path item."); }
            
            remPathExpr = remPathExpr[1..].TrimStart();
            if (remPathExpr.IsEmpty) { break; }

            if (remPathExpr.Equals("+*", StringComparison.Ordinal))
            {
                isAppend = true;
                break;
            }
            
            switch (remPathExpr[0])
            {
                case '+':
                {
                    pathItems.Add(PathItem.ArrayPlus);
                    remPathExpr = remPathExpr[1..].TrimStart();
                    break;
                }
                case '$':
                {
                    pathItems.Add(PathItem.ArrayStop);
                    remPathExpr = remPathExpr[1..].TrimStart();
                    break;
                }
                case '\"':
                {
                    var (idx, len) = Regexes.Dq.Match(remPathExpr);
                    if (idx == -1) { throw MakeExcForCurrentItem("Unmatched quote"); }
                    var dqStr = remPathExpr[idx..(idx+len)];
                    try
                    {
                        var str = JsonSerializer.Deserialize<string>(dqStr)!;
                        pathItems.Add(new PathItem.Key(str));
                    }
                    catch (JsonException jsonExc)
                    {
                        throw MakeLexingExcFromJsonExc(coord, jsonExc, $"parsing JSON string for path item {itemIdx}");
                    }
                    
                    remPathExpr = remPathExpr[(idx+len)..].TrimStart();
                    break;
                }
                case '\'':
                {
                    var (idx, len) = Regexes.Sq.Match(remPathExpr);
                    if (idx == -1) { throw MakeExcForCurrentItem("Unmatched quote"); }
                    var sqStr = remPathExpr[idx..(idx+len)];
                    var dqStr = StrUtil.ConvertSqJsonStrToDq(sqStr.ToString());
                    try
                    {
                        var str = JsonSerializer.Deserialize<string>(dqStr)!;
                        pathItems.Add(new PathItem.Key(str));
                    }
                    catch (JsonException jsonExc)
                    {
                        throw MakeLexingExcFromJsonExc(
                            coord,
                            jsonExc,
                            $"parsing single-quoted string for path item {itemIdx}"
                        );
                    }
                    remPathExpr = remPathExpr[(idx+len)..].TrimStart();
                    break;
                }
                default:
                {
                    var (idx, len) = Regexes.Word.Match(remPathExpr);
                    if (idx == -1) { throw MakeExcForCurrentItem(@"Unquoted path does not match regex ^\w+$"); }
                    var word = remPathExpr[idx..(idx+len)];
                    pathItems.Add(new PathItem.Key(word.ToString()));
                    remPathExpr = remPathExpr[(idx+len)..].TrimStart();
                    break;
                }
            }
        }
        
        LexedPath path = new(pathItems.ToImmutableArray(), isAppend);
        return path;
    }

    private static JmonException<LexingError> MakeLexingExc(Coord coord, string msg) =>
        new(new(coord.Row, coord.Col, msg));
    
    private static JmonException<LexingError>
        MakeLexingExcFromJsonExc(Coord coord, JsonException jsonException, string context) =>
        new(
            new(coord.Row, coord.Col, $"JsonException encountered while {context}: {jsonException.Message}"),
            jsonException
        );

    public static LexedCell[,] LexCells(string[,] cells, JsonSerializerOptions jsonOptions)
    {
        var rect = new Rect((0, 0), (cells.GetLength(0), cells.GetLength(1)));
        var lexedCells = new LexedCell[rect.Dims().Row, rect.Dims().Col];

        foreach (var coord in rect.CoordSeq())
        {
            try
            {
                string trimmedText = cells[coord.Row, coord.Col].Trim();
                ref var cellRef = ref lexedCells[coord.Row, coord.Col];
                
                if (trimmedText.StartsWith("//")) { cellRef = new LexedCell.Blank(); continue; }
                if (SimpleLexedCells.TryGetValue(trimmedText, out cellRef!)) { continue; }
    
                if (trimmedText.StartsWith('.'))
                {
                    cellRef = LexPathCell(trimmedText.AsSpan(), coord);
                    continue;
                }
                
                if (trimmedText.StartsWith(':'))
                {
                    string jsonCode = trimmedText.StartsWith(":::")
                        ? trimmedText[3..]
                        : trimmedText.StartsWith("::")
                            ? trimmedText[2..]
                            : throw MakeLexingExc(coord, "Cell begins with ':' but is not a valid header");
    
                    try
                    {
                        JsonNode? nodeOrNull = JsonSerializer.Deserialize<JsonNode>(jsonCode, jsonOptions);
                        cellRef = new LexedCell.JVal(nodeOrNull);
                        continue;
                    }
                    catch (JsonException jsonException)
                    {
                        throw MakeLexingExcFromJsonExc(coord, jsonException, "parsing JSON literal");
                    }
                }
                
                {
                    JsonNode? nodeOrNull = JsonSerializer.SerializeToNode(trimmedText, jsonOptions);
                    cellRef = new LexedCell.JVal(nodeOrNull);
                }
            }
            catch (JmonException<LexingError> e)
            {
                // TODO: Maybe have a mode where these errors are aggregated into a list?
                // (For now, a stack trace is more useful)
                throw;
            }
        }
        
        return lexedCells;
    }
}

internal static class Ast
{
    private static IEnumerable<(LexedPath path, int beg, int end)> GetPathRanges(IReadOnlyList<LexedCell> cellSeq)
    {
        var seq = cellSeq
            .Select((cell, idx) => (path: (cell as LexedCell.Path)?.V, idx))
            .Where(t => t.path is not null)
            .ToList();

        // Special case for when row is blank
        // TODO consider changing this so that between 0 and first non-blank is a range with implicit path of `.`
        // (Will have same effect for blank row)
        if (seq.Count == 0) { return new[] { (LexedPath.EmptyNonAppend, 0, cellSeq.Count) }; }
        
        var endsSeq = seq.Select(t => t.idx).Skip(1).Concat(new[] { cellSeq.Count });
        return seq.Zip(endsSeq, (t, end) => (t.path!, t.idx, end));
    }

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
                AstNode? valOrNull = ParseJmon(interior.SliceCols(begCol..endCol));
                if (valOrNull is {} val) { yield return new(path, val); }
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

    // private record StrayCell(Coord Coord, string Type);

    // private static AstNode.Error StrayCellAtInnerCoord(SubSheet<LexedCell> sheet, Coord innerCoord)
    // {
    //     var obj = new StrayCell(sheet.ToOuter(innerCoord), sheet[innerCoord].GetType().Name);
    //     return new AstNode.Error(obj.ToString());
    // }

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

    private static AstNode? ParseJmon(SubSheet<LexedCell> subSheet)
    {
        var firstNonBlankOrNull = subSheet.CoordAndCellSeq().Find(cell => cell is not LexedCell.Blank);
        if (firstNonBlankOrNull is not Coord firstNonBlank) { return null; }
        
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

    public static AstNode LexedCellsToAst(LexedCell[,] lexedCells)
    {
        var parseResult = Ast.ParseJmon(SubSheet.Create(lexedCells));
        if (parseResult is { } astNode) { return astNode; }

        throw new Exception("TODO"); // TODO
    }
}

internal static class Construction
{
    private readonly record struct Assignment(ConvertedPath Path, JsonVal.Any Value);
    
    private static ConvertedPath
        ConvertPath(ConvertedPath prefixPath, LexedPath lexedPath, IDictionary<ConvertedPath, int> idxForPartialPath)
    {
        var cvtPathItems = prefixPath.Items.ToList();
        
        PathItem.Idx ConvertProtoIdxElmt(PathItem.Idx arrElmt)
        {
            ConvertedPath partialPath = new(cvtPathItems.ToImmutableArray(), false);
            if (!idxForPartialPath.TryGetValue(partialPath, out var idx)) { idx = -1; }

            idx += arrElmt.V;
            idxForPartialPath[partialPath] = idx;
            return idx;
        }

        var pathSegments = lexedPath.Items.Segment(elmt => elmt is PathItem.Idx);

        foreach (IReadOnlyList<PathItem> pathSegment in pathSegments)
        {
            var elmt0 = pathSegment[0].AsOneOf().Match<PathItem>(keyElmt => keyElmt, ConvertProtoIdxElmt);
            cvtPathItems.Add(elmt0);
            cvtPathItems.AddRange(pathSegment.Skip(1));
        }

        return new ConvertedPath(cvtPathItems.ToImmutableArray(), lexedPath.IsAppend);
    }

    private static IEnumerable<Assignment> ComputeAssignmentsForMtx(AstNode.Branch mtx)
    {
        Dictionary<ConvertedPath, int> idxForPartialPath = new();
        
        IEnumerable<Assignment> Inner(ConvertedPath parentPath, AstNode node) =>
            node.AsOneOf().Match(
                leaf => new[] { new Assignment(parentPath, leaf) },
                branch => branch.Items.SelectMany(
                    item => item.Node switch
                    {
                        AstNode.Branch { Kind: BranchKind.ArrMtx } mtx1 =>
                            new[] { new Assignment(ConvertPath(parentPath, item.Path, idxForPartialPath), MtxToJson(mtx1)) },
                        AstNode.Branch { Kind: BranchKind.ObjMtx } mtx1 =>
                            new[] { new Assignment(ConvertPath(parentPath, item.Path, idxForPartialPath), MtxToJson(mtx1)) },
                        _ => Inner(ConvertPath(parentPath, item.Path, idxForPartialPath), item.Node)
                    }
                ),
                error => throw new Exception("Asdf") // TODO
            );

        return Inner(ConvertedPath.EmptyNonAppend, mtx);
    }

    private static JsonVal.Any MtxToJson(AstNode.Branch mtx)
    {
        // Throw if empty??
        
        HashSet<JsonNode> sealedNodes = new();
        List<(JsonArray, int)> nullsInArrays = new();
        List<(JsonObject, string)> nullsInObjects = new();
        JsonNode root = mtx.Kind switch
        {
            BranchKind.ObjMtx => new JsonObject(),
            BranchKind.ArrMtx => new JsonArray(),
            _ => throw new Exception("Unexpected branch kind")
        };

        JsonNode MakeNullPlaceholder() => new JsonObject { { "\uE0E1", null } };
        
        JsonNode? AddOrReturnExisting(JsonNode parent, PathItem pathItem, JsonNode? child)
        {
            return pathItem.AsOneOf().Match<JsonNode?>(
                key =>
                {
                    if (parent is not JsonObject obj) { throw new Exception("Unexpected key"); } // TODO
                    var keyStr = key.V.ToUtf16String();
                    return obj.TryAdd(key.V.ToUtf16String(), child) ? child : obj[keyStr];
                },
                idx =>
                {
                    if (parent is not JsonArray arr) { throw new Exception("Unexpected idx"); } // TODO
                    if (arr.Count < idx) { throw new Exception("Bad idx"); } // TODO
                    if (arr.Count != idx) { return arr[idx]; }
                    arr.Add(child);
                    return child;
                }
            );
        }

        var assignments = ComputeAssignmentsForMtx(mtx);

        foreach (var (cvtPath, srcVal) in assignments)
        {
            var curNode = root;

            foreach (var (elmtN, elmtNPlus1) in cvtPath.Items.Zip(cvtPath.Items[1..]))
            {
                JsonNode newChild = elmtNPlus1.AsOneOf().Match<JsonNode>(k => new JsonObject(), i => new JsonArray());
                curNode = AddOrReturnExisting(curNode, elmtN, newChild)!;
                if (sealedNodes.Contains(curNode)) { throw new Exception("Implicit modification of sealed node"); } // TODO
            }

            if (cvtPath.IsAppend)
            {
                if (srcVal.V is not (JsonObject or JsonArray)) { throw new Exception(); }  // TODO
                var dstNode = AddOrReturnExisting(curNode, cvtPath.Items[^1], srcVal.V);

                if (!object.ReferenceEquals(dstNode, srcVal.V))
                {
                    switch (dstNode)
                    {
                        case JsonObject dstObj:
                        {
                            if (srcVal.V is not JsonObject srcObj) { throw new Exception(); } // TODO
                            foreach (var (key, val) in srcObj.ToList())
                            {
                                if (dstObj.ContainsKey(key)) { throw new Exception("TODO"); } // TODO
                                srcObj[key] = null;
                                dstObj[key] = val;
                            }
                            break;
                        }
                        case JsonArray dstArr:
                        {
                            if (srcVal.V is not JsonArray srcArr) { throw new Exception(); } // TODO
                            foreach (var idx in Enumerable.Range(0, srcArr.Count))
                            {
                                var val = srcArr[idx];
                                srcArr[idx] = null;
                                dstArr.Add(val);
                            }
                            break;
                        }
                        default: throw new Exception("TODO");
                    }
                }
                
                sealedNodes.Add(dstNode);
            }
            else
            {
                if (srcVal.V is not JsonNode jNode)
                {
                    jNode = MakeNullPlaceholder();
                    cvtPath.Items[^1].AsOneOf().Switch(
                        key => { nullsInObjects.Add((curNode.AsObject(), key.V.ToUtf16String())); },
                        idx => { nullsInArrays.Add((curNode.AsArray(), idx)); }
                    );
                }
                
                bool nodeAdded = object.ReferenceEquals(jNode, AddOrReturnExisting(curNode, cvtPath.Items[^1], jNode));
                if (!nodeAdded) { throw new Exception("Cannot assign"); } // TODO
                
                sealedNodes.Add(jNode);
            }
        }

        foreach (var (objNode, key) in nullsInObjects) { objNode[key] = null; }
        foreach (var (arrNode, idx) in nullsInArrays) { arrNode[idx] = null; }

        return root;
    }
    
    public static JsonVal.Any AstToJson(AstNode astNode) => astNode.AsOneOf().Match(
        valCell => valCell.V,
        MtxToJson,
        err => throw new Exception("TODO") // Ensure this is unreachable at this point
    );
}