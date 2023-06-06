using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using LibJmon.Types;
using LibJmon.Linq;
using LibJmon.Sheets;
using LibJmon.SuperTypes;
using OneOf;
using OneOf.Types;
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
        
        List<PathItem> pathItems = new();
        bool isAppend = false;
        var remPathExpr = pathExpr;

        JmonException MakeExcForCurrentItem(string msg, string remExpr) =>
            new(new JmonLexErr($"Error when parsing path item {pathItems.Count}: {msg}.", coord.ToPublic(), remExpr));
        
        while (!remPathExpr.IsEmpty)
        {
            if (!remPathExpr.StartsWith(kDot))
            {
                throw MakeExcForCurrentItem("Expected '.' preceding path item", remPathExpr.ToString());
            }
            
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
                    (int idx, int len) = Regexes.Dq.Match(remPathExpr);
                    if (idx == -1) { throw MakeExcForCurrentItem("Unmatched quote", remPathExpr.ToString()); }
                    var dqStr = remPathExpr[idx..(idx+len)];
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
                            $"parsing JSON string for path item {pathItems.Count}",
                            dqStr.ToString()
                        );
                    }
                    
                    remPathExpr = remPathExpr[(idx+len)..].TrimStart();
                    break;
                }
                case '\'':
                {
                    (int idx, int len) = Regexes.Sq.Match(remPathExpr);
                    if (idx == -1) { throw MakeExcForCurrentItem("Unmatched quote", remPathExpr.ToString()); }
                    var sqStr = remPathExpr[idx..(idx+len)];
                    string dqStr = StrUtil.ConvertSqJsonStrToDq(sqStr.ToString());
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
                            $"parsing single-quoted string for path item {pathItems.Count}",
                            dqStr
                        );
                    }
                    remPathExpr = remPathExpr[(idx+len)..].TrimStart();
                    break;
                }
                default:
                {
                    (int idx, int len) = Regexes.Word.Match(remPathExpr);
                    if (idx == -1)
                    {
                        throw MakeExcForCurrentItem(
                            $@"Unquoted path does not match regex {Regexes.Word}",
                            remPathExpr.ToString()
                        );
                    }
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

    private static JmonException MakeLexingExc(Coord coord, string msg, string? expr) =>
        (new JmonLexErr(msg, coord.ToPublic(), expr)).ToExc();

    private static JmonException
        MakeLexingExcFromJsonExc(Coord coord, JsonException jsonException, string context, string? expr)
    {
        // TODO use jsonException to get specific location of lexed expression
        // (this is a bit tricky to do with .NET APIs)
        JmonLexErr err = new(
            $"JsonException encountered while {context}: {jsonException.Message}", coord.ToPublic(),
            expr
        );
        return err.ToExc(jsonException);
    }

    public static LexedCell[,] LexCells(string[,] cells, JsonSerializerOptions jsonOptions)
    {
        Rect rect = new((0, 0), (cells.GetLength(0), cells.GetLength(1)));
        var lexedCells = new LexedCell[rect.Dims().Row, rect.Dims().Col];
        List<JmonLexErr> errs = new();

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
                            ? StrUtil.ConvertSqJsonStrToDq(trimmedText[2..])
                            : throw MakeLexingExc(coord, "Cell begins with ':' but is not a valid header", trimmedText);
    
                    try
                    {
                        JsonNode? nodeOrNull = JsonSerializer.Deserialize<JsonNode>(jsonCode, jsonOptions);
                        cellRef = new LexedCell.JVal(nodeOrNull);
                        continue;
                    }
                    catch (JsonException jsonException)
                    {
                        throw MakeLexingExcFromJsonExc(coord, jsonException, "parsing JSON literal", jsonCode);
                    }
                }
                
                {
                    JsonNode? nodeOrNull = JsonSerializer.SerializeToNode(trimmedText, jsonOptions);
                    cellRef = new LexedCell.JVal(nodeOrNull);
                }
            }
            catch (JmonException e) when (e.JmonErr is JmonLexErr lexingErr)
            {
                errs.Add(lexingErr);
            }
        }
        
        if (errs.Count() == 1) { throw new JmonException(errs.First()); }
        if (errs.Any()) { throw new JmonException(new JmonMultiErr(errs)); }
        
        return lexedCells;
    }
}

internal static class Ast
{
    private static IEnumerable<(LexedPath path, int beg, int end)> GetPathRanges(IReadOnlyList<LexedCell> cellSeq)
    {
        List<(LexedPath? path, int idx)> seq = cellSeq
            .Select((cell, idx) => (path: (cell as LexedCell.Path)?.V, idx))
            .Where(t => t.path is not null)
            .ToList();

        // Special case for when row is blank
        // (Will have same effect for blank row)
        if (seq.Count == 0) { return new[] { (LexedPath.EmptyNonAppend, 0, cellSeq.Count) }; }
        
        IEnumerable<int> endsSeq = seq.Select(t => t.idx).Skip(1).Concat(new[] { cellSeq.Count });
        return seq.Zip(endsSeq, (t, end) => (t.path!, t.idx, end));
    }

    private static IEnumerable<BranchItem>
        ParsePathCols(
            SubSheet<LexedCell> pathCols,
            SubSheet<LexedCell> pathRows,
            SubSheet<LexedCell> interior,
            Rect mtxRect
        )
    {
        var seq = GetPathRanges(pathCols.SliceCols(..1).CellSeq().ToList());
        var remCols = pathCols.SliceCols(1..);
        
        if (remCols.Rect.Dims().Col == 0)
        {
            foreach ((LexedPath path, int begRow, int endRow) in seq)
            {
                ImmutableArray<BranchItem> branchItems =
                    ParsePathRows(pathRows, interior.SliceRows(begRow..endRow), mtxRect).ToImmutableArray();
                AstNode.Branch prop = new(branchItems, BranchKind.Range, mtxRect);
                yield return new BranchItem(path, prop);
            }
            yield break;
        }
        
        foreach ((LexedPath path, int begRow, int endRow) in seq)
        {
            ImmutableArray<BranchItem> branchItems = ParsePathCols(
                    remCols.SliceRows(begRow..endRow),
                    pathRows,
                    interior.SliceRows(begRow..endRow),
                    mtxRect
                ).ToImmutableArray();
            AstNode.Branch prop = new(branchItems, BranchKind.Range, mtxRect);
            yield return new(path, prop);
        }
    }

    private static IEnumerable<BranchItem>
        ParsePathRows(SubSheet<LexedCell> pathRows, SubSheet<LexedCell> interior, Rect mtxRect)
    {
        var seq = GetPathRanges(pathRows.SliceRows(0..1).CellSeq().ToList());
        var remRows = pathRows.SliceRows(1..);

        if (remRows.Rect.Dims().Row == 0)
        {
            foreach ((LexedPath path, int begCol, int endCol) in seq)
            {
                AstNode? valOrNull = SubSheetToAst(interior.SliceCols(begCol..endCol));
                if (valOrNull is {} val) { yield return new BranchItem(path, val); }
            }
            yield break;
        }

        foreach ((LexedPath path, int begCol, int endCol) in seq)
        {
            ImmutableArray<BranchItem> children =
                ParsePathRows(remRows.SliceCols(begCol..endCol), interior.SliceCols(begCol..endCol), mtxRect)
                .ToImmutableArray();
            AstNode.Branch prop = new(children, BranchKind.Range, mtxRect);
            yield return new BranchItem(path, prop);
        }
    }

    private record StrayCell(Coord Coord, string Type);

    // TODO include context
    private static AstNode.Error StrayCellAtInnerCoord(SubSheet<LexedCell> sheet, Coord innerCoord, string parseDesc)
    {
        string cellDesc = sheet[innerCoord].AsOneOf().Match(
            blank => "Blank Cell",
            path => "Path Cell",
            jval => "JSON Literal",
            mtxHead => "Matrix Header"
        );
        Coord coord = sheet.ToOuter(innerCoord);
        string msg = $"Found unexpected {cellDesc} at {coord.ToPublic()} (while parsing {parseDesc}).";
        return new AstNode.Error(coord, msg, sheet.OuterRect);
    }

    private static AstNode ParseMtx(SubSheet<LexedCell> subSheet)
    {
        (MtxKind mtxKind, bool isTp) = (subSheet[0, 0] as LexedCell.MtxHead)!; 
        subSheet = isTp ? subSheet.Tpose() : subSheet;

        AstNode.Error? FindStray(SubSheet<LexedCell> sheet, int skip, Func<LexedCell, bool> pred, string context) =>
            sheet.CoordAndCellSeq().Skip(skip).Find(pred) switch
            {
                Coord strayCoord => StrayCellAtInnerCoord(sheet, strayCoord, context),
                _ => null
            };

        Coord? pathRowBegOrNull = subSheet.CoordAndCellSeq().Find(cell => cell is LexedCell.Path);
        if (pathRowBegOrNull is not Coord pathRowBeg)
        {
            return FindStray(subSheet, 1, cell => cell is not LexedCell.Blank, "Matrix with no Paths") switch
            {
                { } strayInEmptyMtx => strayInEmptyMtx,
                _ => AstNode.Branch.Empty(mtxKind.ToBranchKind(), subSheet.OuterRect)
            };
        }

        var pathColSearchRange = subSheet.SliceCols(0..pathRowBeg.Col).Tpose();
        Coord? pathColBegOrNull = pathColSearchRange.CoordAndCellSeq().Find(cell => cell is LexedCell.Path);
        // TODO improve this error. Situation is that no pathColBeg can be found given a pathRowBeg
        if (pathColBegOrNull is not Coord pathColBeg)
        {
            return new AstNode.Error(
                subSheet.ToOuter(pathRowBeg),
                isTp ? "Transposed Matrix has path column but no path row." : "Matrix has path row but no path column.",
                subSheet.OuterRect
            );
        }
        pathColBeg = subSheet.ToInner(pathColSearchRange.ToOuter(pathColBeg));

        var ((margin, pathRows), (pathCols, interior)) = subSheet.Quarter((pathColBeg.Row, pathRowBeg.Col));

        if (FindStray(margin, 1, cell => cell is not LexedCell.Blank, "") is { } strayInMargin)
        {
            Coord c = strayInMargin.FocusCell!.Value;
            return strayInMargin with
            {
                Msg = isTp 
                    ? $"Cell at {c} is above the first path column, so it must be blank."
                    : $"Cell at {c} is left of the first path row, so it must be blank."
            };
        }

        if (FindStray(pathRows, 0, cell => cell.IsHeader(), isTp ? "Path Columns" : "Path Rows") is { } strayInRows)
        {
            return strayInRows;
        }

        if (FindStray(pathCols, 0, cell => cell.IsHeader(), isTp ? "Path Rows" : "Path Columns") is { } strayInCols)
        {
            return strayInCols;
        }
        
        pathRows = pathRows.SliceRows(pathRowBeg.Row..);
        pathCols = pathCols.SliceCols(pathColBeg.Col..);

        // TODO where to check validity of paths with IsAppend=true?
        // TODO where to check that first path element has correct type?

        var mtxRect = subSheet.OuterRect;
        ImmutableArray<BranchItem> mtxItems = ParsePathCols(pathCols, pathRows, interior, mtxRect).ToImmutableArray();
        return new AstNode.Branch(mtxItems, mtxKind.ToBranchKind(), mtxRect);
    }

    private static AstNode? SubSheetToAst(SubSheet<LexedCell> subSheet)
    {
        Coord? firstNonBlankOrNull = subSheet.CoordAndCellSeq().Find(cell => cell is not LexedCell.Blank);
        if (firstNonBlankOrNull is not Coord firstNonBlank) { return null; }
        
        if (!subSheet[firstNonBlank].IsHeader())
        {
            return StrayCellAtInnerCoord(subSheet, firstNonBlank, "Value");
        }

        var lower = subSheet.SliceRows(firstNonBlank.Row..);
        
        var left = lower.SliceCols(0..firstNonBlank.Col);
        Coord? strayInLeftOrNull = left.CoordAndCellSeq().Find(cell => cell is not LexedCell.Blank);
        if (strayInLeftOrNull is { } stray) { return StrayCellAtInnerCoord(left, stray, "Value"); }

        var valRange = lower.SliceCols(firstNonBlank.Col..);

        return subSheet[firstNonBlank] switch
        {
            LexedCell.JVal jVal => valRange.CoordAndCellSeq().Skip(1).Find(cell => cell is not LexedCell.Blank) switch
            {
                { } strayCoord => StrayCellAtInnerCoord(valRange, strayCoord, "Value Cell"),
                _ => new AstNode.ValCell(jVal.V, valRange.OuterRect)
            },
            LexedCell.MtxHead mtxHead => ParseMtx(valRange),
            _ => throw new JmonException(new JmonInternalErr("Reached end of switch statement"))
        };
    }

    public static AstNode LexedCellsToAst(LexedCell[,] lexedCells)
    {
        var sheet = SubSheet.Create(lexedCells);
        AstNode? parseResult = SubSheetToAst(sheet);
        if (parseResult is { } ast) { return ast; }
        return new AstNode.Error(null, "Sheet is blank.", sheet.OuterRect);
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
            if (!idxForPartialPath.TryGetValue(partialPath, out int idx)) { idx = -1; }

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
                yield return new JsonTreeOp.PushNode(state.CurPath, MtxKind.Arr, !state.IsAppend, branch.ContribCells);
                state = state with { IsAppend = false };
                break;
            case BranchKind.ObjMtx:
                yield return new JsonTreeOp.PushNode(state.CurPath, MtxKind.Obj, !state.IsAppend, branch.ContribCells);
                state = state with { IsAppend = false };
                break;
        }
        
        foreach ((LexedPath childPath, AstNode childAstNode) in branch.Items)
        {
            MakeTreeOpsState newState = state;
            
            if (state.IsAppend)
            {
                if (childPath != LexedPath.EmptyNonAppend)
                {
                    AssignPath badAssignPath = ConvertPath(state.CurPath, childPath, state.IdxForPartialPath);
                    JmonParseErr parsingErr = new(
                        badAssignPath.ToJsonPath(),
                        "Append operator must be last in path.",
                        null,
                        childAstNode.ContribCells.ToPublic()
                    );
                    yield return new JsonTreeOp.ReportErr(badAssignPath, parsingErr);
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
            
            foreach (JsonTreeOp op in childOps) { yield return op; }
        }

        if (branch.Kind != BranchKind.Range) { yield return new JsonTreeOp.PopNode(); }
    }

    private static IEnumerable<JsonTreeOp> MakeTreeOps(MakeTreeOpsState state, AstNode.Error error)
    {
        yield return new JsonTreeOp.PushNode(state.CurPath, null, !state.IsAppend, error.ContribCells);
        JmonParseErr parseErr = new(
            state.CurPath.ToJsonPath(),
            error.Msg,
            error.FocusCell?.ToPublic(),
            error.ContribCells.ToPublic()
        );
        yield return new JsonTreeOp.ReportErr(state.CurPath, parseErr);
        yield return new JsonTreeOp.PopNode();
    }

    static IEnumerable<JsonTreeOp> MakeTreeOps(MakeTreeOpsState state, AstNode.ValCell valCell)
    {
        (IDictionary<AssignPath, int> idxForPartialPath, AssignPath parentPath, bool isAppend) = state;
        
        if (!isAppend)
        {
            yield return new JsonTreeOp.Create(parentPath, valCell.Value, valCell.ContribCells);
            yield break;
        }
        
        switch (valCell.Value.V)
        {
            case JsonObject obj:
                yield return new JsonTreeOp.PushNode(parentPath, MtxKind.Obj, false, valCell.ContribCells);
                var objElmts = obj.ToList();
                obj.Clear();
                foreach ((string key, JsonNode? val) in objElmts)
                {
                    AssignPath childPath = new(parentPath.Items.Append(new PathItem.Key(key)).ToImmutableArray());
                    yield return new JsonTreeOp.Create(childPath, val, valCell.ContribCells);
                }
                yield return new JsonTreeOp.PopNode();
                yield break;
            case JsonArray arr:
                // yield return new(parentPath, valCell, AssignKind.Open);
                yield return new JsonTreeOp.PushNode(parentPath, MtxKind.Arr, false, valCell.ContribCells);
                var arrElmts = arr.ToList();
                arr.Clear();
                foreach (JsonNode? arrElmt in arrElmts)
                {
                    LexedPath lexedPath = new(ImmutableArray.Create<PathItem>(PathItem.ArrayPlus), false);
                    AssignPath assignPath = ConvertPath(parentPath, lexedPath, idxForPartialPath);
                    yield return new JsonTreeOp.Create(assignPath, arrElmt, valCell.ContribCells);
                }
                yield return new JsonTreeOp.PopNode();
                yield break;
            default:
                yield return new JsonTreeOp.PushNode(parentPath, null, false, valCell.ContribCells);
                JmonParseErr err = new(
                    parentPath.ToJsonPath(),
                    "Value cannot be appended.",
                    null,
                    valCell.ContribCells.ToPublic()
                );
                yield return new JsonTreeOp.ReportErr(parentPath, err);
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
}

internal static class Construction
{
    private abstract record JsonLoc : IUnion<JsonLoc, JsonLoc.InObj, JsonLoc.InArr>
    {
        public sealed record InObj(JsonObject obj, string key) : JsonLoc;
        public sealed record InArr(JsonArray arr, int idx) : JsonLoc;
    }

    private static OneOf<Success, JsonNode?> SetNodeAtLoc(JsonLoc location, JsonNode? newNode) =>
        location.AsOneOf().Match<OneOf<Success, JsonNode?>>(
            locInObj =>
            {
                (JsonObject obj, string key) = locInObj;
                if (obj.TryGetPropertyValue(key, out JsonNode? existing)) { return existing; }
                obj[key] = newNode;
                return new Success();
            },
            locInArr =>
            {
		        (JsonArray arr, int idx) = locInArr;
                if (idx == arr.Count)
                {
			        arr.Add(newNode);
			        return new Success();
		        }
                if (idx == (arr.Count - 1)) { return arr[idx]; }
                throw new JmonException(new JmonInternalErr($"Unexpected index for {arr.GetPath()}: {idx}."));
            }
        );

    public static JsonVal.Any ConstructFromTreeOps(IReadOnlyList<JsonTreeOp> treeOps)
    {
        JsonArray superRoot = new();
        JsonLoc rootLoc = new JsonLoc.InArr(superRoot, 0);
        HashSet<JsonLoc> sealedLocs = new();
        Stack<JsonLoc?> pushedNodes = new();
        List<JmonParseErr> parseErrs = new();

        foreach (JsonTreeOp treeOp in treeOps)
        {
            if (treeOp.AsOneOf().TryPickT1(out JsonTreeOp.PopNode popNode, out var pushOrCreateOrReport))
            {
                var poppedNodeOrNull = pushedNodes.Pop();
                if (poppedNodeOrNull is {} node) { sealedLocs.Add(node); }
                continue;
            }

            AssignPath path = pushOrCreateOrReport.Match(
                push => push.Path,
                create => create.Path,
                report => report.Path
            );
            
            CellRect exprCells = pushOrCreateOrReport.Match(
                push => push.ContribCells.ToPublic(),
                create => create.ContribCells.ToPublic(),
                report => report.Err.ExprCells
            );

            bool gotNodeLoc = GetNodeLoc(rootLoc, path, exprCells, sealedLocs)
                .TryPickT1(out JsonLoc nodeLoc, out JmonParseErr getNodeLocErr);
            if (pushOrCreateOrReport.IsT0) { pushedNodes.Push(gotNodeLoc ? null : nodeLoc); }
            if (!gotNodeLoc) { parseErrs.Add(getNodeLocErr); continue; }

            if (pushOrCreateOrReport.TryPickT2(out JsonTreeOp.ReportErr reportErr, out var pushOrCreate))
            {
                parseErrs.Add(reportErr.Err);
                continue;
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

            (bool mustBeNew, bool seal) = pushOrCreate.Match(push => (push.MustBeNew, false), create => (true, true));
            
            JmonParseErr? errOrNull = SetNodeAtLoc(nodeLoc, valueToSet)
                .Match<JmonParseErr?>(
                    success => null,
                    existing =>
                    {
                        if (!mustBeNew) { return null; }

                        string msg = existing switch
                        {
                            JsonObject obj => "Path already has a value (an implicitly-created Object).",
                            JsonArray arr => "Path already has value (an implicitly-created Array).",
                            _ => "Path already has a value.", // This is unexpected but not dire.
                        };

                        return new JmonParseErr(path.ToJsonPath(), msg, null, exprCells);
                    });

            if (errOrNull is { } err) { parseErrs.Add(err); continue; }
            if (seal) { sealedLocs.Add(nodeLoc); }
        }
        
        if (parseErrs.Count == 1) { throw new JmonException(parseErrs[0]); }
        if (parseErrs.Any()) { throw new JmonException(new JmonMultiErr(parseErrs)); }
        
        JsonNode? rVal = superRoot[0];
        superRoot.Clear();
        return rVal;
    }

    private static OneOf<JmonParseErr, JsonLoc>
        GetNodeLoc(JsonLoc rootLoc, AssignPath path, CellRect exprCells, IReadOnlySet<JsonLoc> sealedLocs)
    {
        JsonLoc? nodeLoc = rootLoc;

        foreach (int i in Enumerable.Range(0, path.Items.Length))
        {
            JmonParseErr MakeParseErr(string msg) =>
                new(new AssignPath(path.Items[..(i + 1)]).ToJsonPath(), msg, null, exprCells);

            PathItem pathItem = path.Items[i];

            if (sealedLocs.Contains(nodeLoc))
            {
                // TODO explain in more detail (perhaps have subPath use range ..i)
                string msg = "Path is inaccessible; you have already explicitly" +
                             " assigned a value to its parent.";
                return MakeParseErr(msg);
            }

            var errOrNextNodeLoc = pathItem.AsOneOf()
                .Match<OneOf<JmonParseErr, JsonLoc>>(
                    key =>
                    {
                        var newObj = new JsonObject();
                        var keyStr = key.V.ToUtf16String();
                        return SetNodeAtLoc(nodeLoc, newObj)
                            .Match<OneOf<JmonParseErr, JsonLoc>>(
                                success => new JsonLoc.InObj(newObj, keyStr),
                                existing => existing is JsonObject existingObj
                                    ? new JsonLoc.InObj(existingObj, keyStr)
                                    : MakeParseErr("Path is invalid; its parent is not an Object.")
                            );
                    },
                    idx =>
                    {
                        var newArr = new JsonArray();
                        return SetNodeAtLoc(nodeLoc, newArr)
                            .Match<OneOf<JmonParseErr, JsonLoc>>(
                                success => new JsonLoc.InArr(newArr, idx.V),
                                existing => existing is JsonArray existingArr
                                    ? new JsonLoc.InArr(existingArr, idx.V)
                                    : MakeParseErr("Path is invalid; its parent is not an Array.")
                            );
                    }
                );

            bool gotNextNodeLoc = errOrNextNodeLoc.TryPickT1(out nodeLoc, out _);
            if (!gotNextNodeLoc) { return errOrNextNodeLoc; }
        }

        return nodeLoc;
    }
}