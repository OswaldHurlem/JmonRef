using System.Collections.Immutable;
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
        
        const string placeHolderPropName = "\uE0E1";
        JsonNode MakeValPlaceholder() => new JsonObject { { placeHolderPropName, null } };
        bool IsValPlaceholder(JsonNode? node) => node is JsonObject obj && obj.ContainsKey(placeHolderPropName);
        
        JsonNode root = matrix.MtxKind == MtxKind.Arr ? new JsonArray() : new JsonObject();
        
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
        
        List<(JsonNode, PathItem, AstNode)> assignmentsToDo = new();

        foreach (var (path, astNode) in matrix.Items)
        {
            var cvtPath = ConvertPath(path, idxForPartialPath);
            var curNode = root;

            foreach (var (elmtN, elmtNPlus1) in cvtPath.Items.Zip(cvtPath.Items[1..]))
            {
                JsonNode newChild = elmtNPlus1.AsOneOf().Match<JsonNode>(k => new JsonObject(), i => new JsonArray());
                curNode = AddOrReturnExisting(curNode, elmtN, newChild)!;
                if (IsValPlaceholder(curNode)) { throw new Exception("Implicitly setting fields of value"); } // TODO
            }
            
            var newPlaceholder = MakeValPlaceholder();
            bool phAdded = object.ReferenceEquals(
                newPlaceholder,
                AddOrReturnExisting(curNode, cvtPath.Items[^1], newPlaceholder)
            );
            
            if (!phAdded) { throw new Exception("Cannot assign"); } // TODO
            
            assignmentsToDo.Add((curNode, cvtPath.Items[^1], astNode));
        }
        
        foreach (var (node, path, astNode) in assignmentsToDo)
        {
            JsonVal.Any jVal = AstToJson(astNode);

            path.AsOneOf().Switch(
                key =>
                {
                    var keyStr = key.V.ToUtf16String();
                    // TODO: I think this is impossible
                    if (!IsValPlaceholder(node[keyStr])) { throw new Exception("TODO"); }
                    node[keyStr] = jVal; // TODO: or append!!
                },
                idx =>
                {
                    // TODO: I think this is impossible
                    if (!IsValPlaceholder(node[idx])) { throw new Exception("TODO"); }
                    node[idx] = jVal; // TODO: or append!!
                }
            );
        }

        return root;
    }
    
    public static JsonVal.Any AstToJson(AstNode astNode) => astNode.AsOneOf().Match(
        valCell => valCell.V,
        MtxToJson,
        err => throw new Exception("TODO") // Ensure this is unreachable at this point
    );
}