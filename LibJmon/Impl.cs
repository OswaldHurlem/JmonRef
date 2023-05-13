using System.Buffers;
using System.Collections;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json.Nodes;
using CommunityToolkit.HighPerformance;
using LibJmon.Linq;
using LibJmon.Sheets;
using LibJmon.SuperTypes;
using LibJmon.Types;
using Microsoft.VisualBasic.FileIO;

namespace LibJmon.Impl;

public static class Construction
{
    
}

public static class TestingApi
{
    public static LexedCell[,] LexCells(ReadOnlyMemory<byte>[,] cells)
    {
        var rect = new Rect((0, 0), (cells.GetLength(0), cells.GetLength(1)));
        var lexedCells = new LexedCell[rect.Dims().Row, rect.Dims().Col];

        foreach (var c in rect.CoordSeq())
        {
            lexedCells[c.Row, c.Col] = Lexing.Lex(cells[c.Row, c.Col].Span);
        }
        
        return lexedCells;
    }

    public static AstNode ParseLexedCells(LexedCell[,] lexedCells)
    {
        var sheet = SubSheet.Create(lexedCells);
        if (sheet.CoordAndCellSeq().Find(cell => cell is LexedCell.Error) is { } coord)
        {
            return new AstNode.Error($"Cell at {coord} contains lexing error");
        }

        return Ast.ParseJmon(SubSheet.Create(lexedCells)).Match(
            astNode => astNode,
            none => throw new Exception("JMON contains no elements")
        );
    }

    // TODO error handling
    public static IReadOnlyList<Assignment> ComputeAssignments(AstNode ast)
        => Assignments.ComputeAssignments(ast);

    public static JsonNode MakeJsonFromAssignments(IReadOnlyList<Assignment> assignments)
    {
        if (!assignments.Any()) { throw new Exception(); } // TODO
        
        const string placeHolderPropName = "\uE0E1";
        JsonNode MakeValPlaceholder() => new JsonObject { { placeHolderPropName, null } };
        bool IsValPlaceholder(JsonNode? node) => node is JsonObject obj && obj.ContainsKey(placeHolderPropName);
        
        var root = assignments.First().Path.V.First().AsOneOf()
            .Match<JsonNode>(key => new JsonObject(), idx => new JsonArray())!; // needed??
        
        List<(JsonNode, PathItem, JsonVal.Any)> assignmentsToDo = new();

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

        foreach (var (path, val) in assignments)
        {
            var curNode = root;

            foreach (var (elmtN, elmtNPlus1) in path.V.Zip(path.V[1..]))
            {
                JsonNode newChild = elmtNPlus1!.AsOneOf().Match<JsonNode>(k => new JsonObject(), i => new JsonArray());
                curNode = AddOrReturnExisting(curNode, elmtN, newChild)!;
                if (IsValPlaceholder(curNode)) { throw new Exception("Implicitly setting fields of value"); } // TODO
            }

            var newPlaceholder = MakeValPlaceholder();
            bool phAdded = object.ReferenceEquals(
                newPlaceholder,
                AddOrReturnExisting(curNode, path.V[^1], newPlaceholder)
            );
            if (!phAdded) { throw new Exception("Cannot assign"); } // TODO

            assignmentsToDo.Add((curNode, path.V[^1], val));
        }

        foreach (var (node, path, val) in assignmentsToDo)
        {
            path.AsOneOf().Switch(
                key =>
                {
                    var keyStr = key.V.ToUtf16String();
                    // TODO: I think this is impossible
                    if (!IsValPlaceholder(node[keyStr])) { throw new Exception("TODO"); }
                    node[keyStr] = val;
                },
                idx =>
                {
                    // TODO: I think this is impossible
                    if (!IsValPlaceholder(node[idx])) { throw new Exception("TODO"); }
                    node[idx] = val;
                });
        }

        return root;
    }
    
    

    // TODO: Error handling saved for later
    /*public static JsonVal PerformAssignments(IReadOnlyList<Assignment> assignments)
    {
        // ReadOnlySpan<Assignment> span = assignments.ToArray().AsSpan();

        if (!assignments.Any()) { throw new Exception("TODO"); } // TODO

        var isObj = assignments.First().Path.V.First().AsOneOf().Match(key => true, idx => false);
        
        void UpdateAtKey(JsonObject obj, string key, ReadOnlySpan<PathItem> remPath, JsonVal val) { }

        if (isObj)
        {
            var obj = new JsonObject();
            
            
            
            foreach (var (path, val) in assignments)
            {
                var keyStr = path.V.First().AsOneOf().Match(
                    key => key.V.ToUtf16String(),
                    idx => throw new Exception("TODO")
                );

                UpdateAtKey(obj, keyStr, path.V.AsSpan()[1..], val);
            }
        }
        else
        {
            
        }

        var root = assignments.First().Path.V.First().AsOneOf()
            .Match<JsonNode>(key => new JsonObject(), idx => new JsonArray());

        const string placeHolderPropName = "\uE0E1";
        JsonNode MakePlaceholder() => new JsonObject { { placeHolderPropName, null } };
        bool IsPlaceholder(JsonNode node) => node is JsonObject obj && obj.ContainsKey(placeHolderPropName);

        foreach (var (path, val) in assignments)
        {
            JsonNode node = root;
            
            foreach (var pathItem in path.V)
            {
                pathItem.AsOneOf().Switch(
                    key =>
                    {
                        var keyStr = key.V.ToUtf16String();
                        if (node is not JsonObject obj) { throw new Exception("TODO"); } // TODO

                        if (!obj.ContainsKey(keyStr)) { obj[keyStr] = MakePlaceholder(); }
                        
                    },
                    idx =>
                    {
                        
                    });
            }
        }

        var isObject = assignments.First().Path.V.First() is PathItem.Key;

        if (isObject)
        {
            var node = new JsonObject();

            foreach (var (path, val) in assignments)
            {
                node[path]
            }
        }
    }*/
}


public static class CsvUtil
{
    public static ReadOnlyMemory<byte>[,] MakeCells(TextReader textReader, string delimiter)
    {
        IEnumerable<IReadOnlyList<ReadOnlyMemory<byte>>> Inner()
        {
            using var csvReader = new TextFieldParser(textReader)
            {
                Delimiters = new[] { delimiter },
                HasFieldsEnclosedInQuotes = true
            };

            while (!csvReader.EndOfData)
            {
                var row = csvReader.ReadFields();
                if (row is null) { throw new Exception("null row"); }
                yield return row.Select(field => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(field)).ToList();
            }
        }

        var rows = Inner().ToList();
        var rect = new Rect((0, 0), (rows.Count, rows.Max(row => row.Count)));
        var cells = new ReadOnlyMemory<byte>[rect.Dims().Row, rect.Dims().Col];
        foreach (var coord in rect.CoordSeq())
        {
            cells[coord.Row, coord.Col] = coord.Col < rows[coord.Row].Count
                ? rows[coord.Row][coord.Col]
                : ReadOnlyMemory<byte>.Empty;
        }
        
        return cells;
    }
    
    public static ReadOnlyMemory<byte>[,] MakeCells(string text, string delimiter)
    {
        using var stringReader = new StringReader(text);
        return MakeCells(stringReader, delimiter);
    }

    public static ReadOnlyMemory<byte>[,] MakeCells(Stream stream, string delimiter)
    {
        using var streamReader = new StreamReader(stream);
        return MakeCells(streamReader, delimiter);
    }
}