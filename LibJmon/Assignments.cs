using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json.Nodes;
using LibJmon.Linq;
using LibJmon.SuperTypes;
using LibJmon.Types;

namespace LibJmon.Impl;

public readonly record struct Assignment(ConvertedPath Path, JsonVal.Any Value);

public static class Assignments
{
    private static ConvertedPath ConvertPath(LexedPath lexedPath, IDictionary<ConvertedPath, int> idxForPartialPath)
    {
        var cvtPathItems = new List<PathItem>();
        
        PathItem.Idx ConvertProtoIdxElmt(PathItem.Idx arrElmt)
        {
            ConvertedPath partialPath = new(cvtPathItems.ToImmutableArray(), false);
            if (!idxForPartialPath.TryGetValue(partialPath, out var idx)) { idx = -1; }

            idx += arrElmt.V;
            idxForPartialPath[partialPath] = idx;
            return idx;
        }

        var pathSegments = lexedPath.Items.Segment(elmt => elmt is PathItem.Idx).ToArray();

        foreach (IReadOnlyList<PathItem> pathSegment in pathSegments)
        {
            var elmt0 = pathSegment[0].AsOneOf().Match<PathItem>(keyElmt => keyElmt, ConvertProtoIdxElmt);
            cvtPathItems.Add(elmt0);
            cvtPathItems.AddRange(pathSegment.Skip(1));
        }

        return new ConvertedPath(cvtPathItems.ToImmutableArray(), lexedPath.IsAppend);
    }

    public static JsonVal.Any MtxToJson(AstNode.Matrix matrix)
    {
        // TODO replace placeholders / assignmentsToDo with something a little more elegant
        
        // Throw if empty??
        
        Dictionary<ConvertedPath, int> idxForPartialPath = new();
        
        HashSet<JsonNode> sealedNodes = new();
        List<(JsonArray, int)> nullsInArrays = new();
        List<(JsonObject, string)> nullsInObjects = new();
        JsonNode root = matrix.MtxKind == MtxKind.Arr ? new JsonArray() : new JsonObject();
        
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

        foreach (var (path, astNode) in matrix.Items)
        {
            var cvtPath = ConvertPath(path, idxForPartialPath);
            var curNode = root;

            foreach (var (elmtN, elmtNPlus1) in cvtPath.Items.Zip(cvtPath.Items[1..]))
            {
                JsonNode newChild = elmtNPlus1.AsOneOf().Match<JsonNode>(k => new JsonObject(), i => new JsonArray());
                curNode = AddOrReturnExisting(curNode, elmtN, newChild)!;
                if (sealedNodes.Contains(curNode)) { throw new Exception("Implicit modification of sealed node"); } // TODO
            }

            if (path.IsAppend)
            {
                JsonVal.Any srcVal = AstToJson(astNode);
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
                JsonVal.Any jVal = AstToJson(astNode);

                if (jVal.V is not JsonNode jNode)
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