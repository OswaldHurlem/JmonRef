using System.Collections.Immutable;
using System.Text.Json.Nodes;
using LibJmon.Linq;
using LibJmon.SuperTypes;
using LibJmon.Types;

namespace LibJmon.Impl;

public readonly record struct Assignment(ConvertedPath Path, JsonVal.Any Value);

public static class Assignments
{
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

    public static IEnumerable<Assignment> ComputeAssignmentsForMtx(AstNode.Branch mtx)
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

    public static JsonVal.Any MtxToJson(AstNode.Branch mtx)
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