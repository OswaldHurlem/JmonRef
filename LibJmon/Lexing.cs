using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LibJmon.Types;
using OneOf;
using OneOf.Types;

namespace LibJmon.Impl;

public static class Lexing
{
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

    public static LexedCell Lex(string cellText)
    {
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
            if (trimmedText == ".")
            {
                LexedPath emptyPath = new(ImmutableArray<PathItem>.Empty, false);
                return new LexedCell.Path(emptyPath);
            }
            
            var textAfterDot = trimmedText[1..];
            var pathAsJsonParse = TryParseJsonExpr(textAfterDot);

            if (pathAsJsonParse.TryPickT1(out string errMsg, out _)) { return new LexedCell.Error(errMsg); }  // TODO
            if (pathAsJsonParse.TryPickT0(out JsonVal.Any pathAsJson, out _))
            {
                LexedCell CheckPathAndMakeCell(LexedPath p) =>
                    p.IdxsAreValid()
                        ? new LexedCell.Path(p)
                        : new LexedCell.Error("Path expressed as JSON must only contain index entries equal to 0 or 1");
                
                return pathAsJson.TryDeserialize<LexedPath>()
                    .MapT0(CheckPathAndMakeCell)
                    .MapT1(_ => pathAsJson.TryDeserialize<PathItem>()
                        .MapT0(item => new LexedPath(ImmutableArray.Create(item), false))
                        .MapT0(CheckPathAndMakeCell)
                        .Match(c => c, _ => new LexedCell.Error("JSON expression is not a LexedPath or PathItem"))
                    )
                    .Match(c => c, c => c);
            }

            var segments = textAfterDot.Split('.');
            bool isAppend = segments[^1] == "+*";
            if (isAppend) { segments = segments[..^1]; }
            
            var pathElmts = new PathItem[segments.Length];

            foreach (var (segment, idx) in segments.Select((s, i) => (s, i)))
            {
                switch (segment)
                {
                    case "+":
                        pathElmts[idx] = new PathItem.Idx(1);
                        continue;
                    case "$":
                        pathElmts[idx] = new PathItem.Idx(0);
                        continue;
                    case "+*":
                        return new LexedCell.Error("Append operator '+*' must be the last element of the path");
                    default:
                        if (!Regex.IsMatch(segment, @"^\w+$"))
                        {
                            return new LexedCell.Error(@"Path elements must match regex ^\w+$");
                        }
                        pathElmts[idx] = new PathItem.Key(segment);
                        continue;
                }
            }
            
            LexedPath path = new(pathElmts.ToImmutableArray(), isAppend);
            return new LexedCell.Path(path);
        }

        // String Cell
        JsonVal.Any jsonValue = JsonSerializer.SerializeToNode(trimmedText, jsonOpts);
        return new LexedCell.JVal(jsonValue);
    }
}