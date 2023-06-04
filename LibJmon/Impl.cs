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

using TempError = OneOf.Types.Error<string>;

namespace LibJmon.Impl;

internal static class ApiV0Impl
{
    public static string ParseJmon(string[,] cells, JsonSerializerOptions jsonOptions)
    {
        LexedCell[,] lexedCells = Lexing.LexCells(cells, jsonOptions);
        AstNode ast = Ast.LexedCellsToAst(lexedCells);
        IReadOnlyList<JsonTreeOp> treeOps = JsonTreeOps.AstToJsonTreeOps(ast);
        JsonVal.Any jsonVal = Construction.ConstructFromTreeOps(treeOps);
        return jsonVal.ToUtf16String(jsonOptions);
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
                AstNode? valOrNull = SubSheetToAst(interior.SliceCols(begCol..endCol));
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

    private static AstNode? SubSheetToAst(SubSheet<LexedCell> subSheet)
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
        var parseResult = Ast.SubSheetToAst(SubSheet.Create(lexedCells));
        if (parseResult is { } astNode) { return astNode; }

        throw new Exception("TODO"); // TODO
    }
}

internal static class JsonTreeOps
{
    private static AssignPath
        ConvertPath(AssignPath prefixPath, LexedPath lexedPath, IDictionary<AssignPath, int> idxForPartialPath)
    {
        var cvtPathItems = prefixPath.Items.ToList();
        
        PathItem.Idx ConvertProtoIdxElmt(PathItem.Idx arrElmt)
        {
            AssignPath partialPath = new(cvtPathItems.ToImmutableArray());
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

        return new(cvtPathItems.ToImmutableArray());
    }

    readonly record struct MakeTreeOpsState(
        IDictionary<AssignPath, int> IdxForPartialPath,
        AssignPath CurPath,
        bool IsAppend // whether the path ends with an append operator
    );

    static IEnumerable<JsonTreeOp>
        MakeTreeOps(MakeTreeOpsState state, AstNode.Branch branch)
    {
        switch (branch.Kind)
        {
            case BranchKind.ArrMtx:
                yield return new JsonTreeOp.PushNode(state.CurPath, MtxKind.Arr, !state.IsAppend);
                state = state with { IsAppend = false };
                break;
            case BranchKind.ObjMtx:
                yield return new JsonTreeOp.PushNode(state.CurPath, MtxKind.Obj, !state.IsAppend);
                state = state with { IsAppend = false };
                break;
        }
        
        foreach (var (childPath, childAstNode) in branch.Items)
        {
            MakeTreeOpsState newState = state;
            
            if (state.IsAppend)
            {
                if (childPath != LexedPath.EmptyNonAppend)
                {
                    AssignPath badAssignPath = ConvertPath(state.CurPath, childPath, state.IdxForPartialPath);
                    yield return new JsonTreeOp.ReportErr(badAssignPath, "Append operator must be last in path");
                    continue;
                }
            }
            else
            {
                newState = state with
                {
                    IsAppend = childPath.IsAppend,
                    CurPath = ConvertPath(state.CurPath, childPath, state.IdxForPartialPath)
                };
            }

            var childOps = childAstNode.AsOneOf().Match(
                valCell => MakeTreeOps(newState, valCell),
                subBranch => MakeTreeOps(newState, subBranch),
                error => MakeTreeOps(newState, error)
            );
            
            foreach (var op in childOps) { yield return op; }
        }

        if (branch.Kind != BranchKind.Range) { yield return new JsonTreeOp.PopNode(); }
    }

    private static IEnumerable<JsonTreeOp> MakeTreeOps(MakeTreeOpsState state, AstNode.Error error)
    {
        yield return new JsonTreeOp.PushNode(state.CurPath, null, !state.IsAppend);
        yield return new JsonTreeOp.ReportErr(state.CurPath, error.V);
        yield return new JsonTreeOp.PopNode();
    }

    static IEnumerable<JsonTreeOp> MakeTreeOps(MakeTreeOpsState state, AstNode.ValCell valCell)
    {
        var (idxForPartialPath, parentPath, isAppend) = state;
        
        if (!isAppend)
        {
            yield return new JsonTreeOp.Create(parentPath, valCell.V);
            yield break;
        }
        
        switch (valCell.V.V)
        {
            case JsonObject obj:
                yield return new JsonTreeOp.PushNode(parentPath, MtxKind.Obj, false);
                foreach (var (key, val) in obj)
                {
                    AssignPath childPath = new(parentPath.Items.Append(new PathItem.Key(key)).ToImmutableArray());
                    yield return new JsonTreeOp.Create(childPath, new AstNode.ValCell(val));
                }
                yield return new JsonTreeOp.PopNode();
                yield break;
            case JsonArray arr:
                // yield return new(parentPath, valCell, AssignKind.Open);
                yield return new JsonTreeOp.PushNode(parentPath, MtxKind.Arr, false);
                if (!idxForPartialPath.TryGetValue(parentPath, out var startIdx)) { startIdx = 0; }
                foreach (var i in Enumerable.Range(startIdx, arr.Count))
                {
                    AssignPath childPath = new(parentPath.Items.Append(new PathItem.Idx(i)).ToImmutableArray());
                    yield return new JsonTreeOp.Create(childPath, new AstNode.ValCell(arr[i]));
                }
                yield return new JsonTreeOp.PopNode();
                yield break;
            default:
                yield return new JsonTreeOp.PushNode(parentPath, null, false);
                yield return new JsonTreeOp.ReportErr(parentPath, "Value cannot be appended");
                yield return new JsonTreeOp.PopNode();
                yield break;
        }
    }

    public static IReadOnlyList<JsonTreeOp> AstToJsonTreeOps(AstNode root)
    {
        MakeTreeOpsState initialState = new(new Dictionary<AssignPath, int>(), AssignPath.Empty, false);
        return root.AsOneOf().Match(
            valCell => MakeTreeOps(initialState, valCell),
            subBranch => MakeTreeOps(initialState, subBranch),
            error => MakeTreeOps(initialState, error)
        ).ToList();
    }

    static IEnumerable<(AssignPath, AstNode)> MakeTreeOps(AssignPath parentPath, AstNode.Error error)
    {
        yield return (parentPath, error);
    }
}

internal static class Construction
{
    private abstract record JsonLoc : IUnion<JsonLoc, JsonLoc.InObj, JsonLoc.InArr>
    {
        public sealed record InObj(JsonObject obj, string key) : JsonLoc;
        public sealed record InArr(JsonArray arr, int idx) : JsonLoc;
    }

    private abstract record SetNodeAtLocRslt
        : IUnion<SetNodeAtLocRslt, SetNodeAtLocRslt.WasSet, SetNodeAtLocRslt.WasNodeSet, SetNodeAtLocRslt.Error>
    {
        public sealed record WasSet : SetNodeAtLocRslt;
        public sealed record WasNodeSet(JsonNode? V) : SetNodeAtLocRslt;
        public sealed record Error(string Msg) : SetNodeAtLocRslt;
    }
    
    private static SetNodeAtLocRslt SetNodeAtLoc(JsonLoc location, JsonNode? newNode) =>
        location.AsOneOf().Match<SetNodeAtLocRslt>(
            locInObj =>
            {
                var (obj, key) = locInObj;
                if (obj.TryGetPropertyValue(key, out var existing))
                {
                    return new SetNodeAtLocRslt.WasNodeSet(existing);
                }
                obj[key] = newNode;
                return new SetNodeAtLocRslt.WasSet();
            },
            locInArr =>
            {
		        var (arr, idx) = locInArr;
                if (idx == arr.Count)
                {
			        arr.Add(newNode);
			        return new SetNodeAtLocRslt.WasSet();
		        }
                else if (idx == (arr.Count - 1))
                {
			        return new SetNodeAtLocRslt.WasNodeSet(arr[idx]);
		        }
                return new SetNodeAtLocRslt.Error("bad idx");
            }
        );

    public static JsonVal.Any ConstructFromTreeOps(IReadOnlyList<JsonTreeOp> treeOps)
    {
        var superRoot = new JsonArray();
        JsonLoc rootLoc = new JsonLoc.InArr(superRoot, 0);
        HashSet<JsonLoc> sealedLocs = new();
        Stack<JsonLoc> pushedNodes = new();

        foreach (var treeOp in treeOps) // TEMP
        {
            if (treeOp.AsOneOf().TryPickT1(out JsonTreeOp.PopNode popNode, out var pushOrCreateOrReport))
            {
                pushedNodes.Pop();
                continue;
            }

            var path = pushOrCreateOrReport.Match(
                push => push.Path,
                create => create.Path,
                report => report.Path
            );
            
            var nodeLoc = rootLoc;

            foreach (var pathItem in path.Items)
            {
                if (sealedLocs.Contains(nodeLoc))
                {
                    throw new Exception("TODO");
                }
                
                pathItem.AsOneOf().Switch(
                    key =>
                    {
                        var newObj = new JsonObject();
                        var objForNextLoc =
                            SetNodeAtLoc(nodeLoc, newObj).AsOneOf().Match<JsonObject>(
                                success => newObj,
                                existing =>
                                {
                                    if (existing.V is JsonObject existingObj) { return existingObj; }
                                    throw new Exception("TODO");
                                },
                                err =>
                                {
                                    throw new Exception("TODO");
                                }
                            );
                        nodeLoc = new JsonLoc.InObj(objForNextLoc, key.V.ToUtf16String());
                    },
                    idx =>
                    {
                        var newArr = new JsonArray();
                        var arrForNextLoc =
                            SetNodeAtLoc(nodeLoc, newArr).AsOneOf().Match<JsonArray>(
                                success => newArr,
                                existing =>
                                {
                                    if (existing.V is JsonArray existingArr) { return existingArr; }
                                    throw new Exception("TODO");
                                },
                                err =>
                                {
                                    throw new Exception("TODO");
                                }
                            );
                        nodeLoc = new JsonLoc.InArr(arrForNextLoc, idx.V);
                    }
                );
            }

            if (pushOrCreateOrReport.TryPickT2(out JsonTreeOp.ReportErr reportErr, out var pushOrCreate))
            {
                throw new Exception("TODO");
            }

            var valueToSet = pushOrCreate.Match<JsonNode?>(
                push => push.NodeKind switch
                {
                    MtxKind.Obj => new JsonObject(),
                    MtxKind.Arr => new JsonArray(),
                    _ => null
                },
                create => create.Value
            );

            var (mustBeNew, seal) = pushOrCreate.Match(push => (push.MustBeNew, false), create => (true, true));
            
            SetNodeAtLoc(nodeLoc, valueToSet).AsOneOf().Switch(
                success => { },
                existing =>
                {
                    if (mustBeNew) { throw new Exception("TODO"); }
                    // can't overwrite open node
                },
                err =>
                {
                    throw new Exception("TODO"); // handle same way as above
                }
            );
            
            if (seal) { sealedLocs.Add(nodeLoc); }
            if (pushOrCreate.IsT0) { pushedNodes.Push(nodeLoc); }
        }
        
        var rVal = superRoot[0];
        superRoot.Clear();
        return rVal;
    }
}