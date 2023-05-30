using System.Collections.Immutable;
using System.Diagnostics;
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

    public static (int idx, int len) Match(this Regex regex, ReadOnlySpan<char> text)
    {
        var matches = regex.EnumerateMatches(text);
        return matches.MoveNext()
            ? (matches.Current.Index, matches.Current.Length)
            : (-1, 0);
    }
    
    public static LexedPath LexPath(ReadOnlySpan<char> pathExpr)
    {
        var pathItems = new List<PathItem>();
        bool isAppend = false;

        var dqRegex = new Regex(@"^""(?:[^""\\]|\\.)*""");
        var sqRegex = new Regex(@"^'(?:[^'\\]|\\.)*'");
        var wordRegex = new Regex(@"^\w+");
        var kDot = ".".AsSpan();

        var remPathExpr = pathExpr;

        while (!remPathExpr.IsEmpty)
        {
            if (!remPathExpr.StartsWith(kDot)) { throw new Exception("Expected '.'"); }
            
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
                    var (idx, len) = dqRegex.Match(remPathExpr);
                    if (idx == -1) { throw new Exception("Unmatched quote"); }
                    var dqStr = remPathExpr[idx..(idx+len)];
                    var str = JsonSerializer.Deserialize<string>(dqStr)!;
                    pathItems.Add(new PathItem.Key(str));
                    remPathExpr = remPathExpr[(idx+len)..].TrimStart();
                    break;
                }
                case '\'':
                {
                    var (idx, len) = sqRegex.Match(remPathExpr);
                    if (idx == -1) { throw new Exception("Unmatched quote"); }
                    var sqStr = remPathExpr[idx..(idx+len)];
                    var dqStr = ConvertSqToDq(sqStr.ToString());
                    var str = JsonSerializer.Deserialize<string>(dqStr)!;
                    pathItems.Add(new PathItem.Key(str));
                    remPathExpr = remPathExpr[(idx+len)..].TrimStart();
                    break;
                }
                default:
                {
                    var (idx, len) = wordRegex.Match(remPathExpr);
                    if (idx == -1) { throw new Exception(@"Expected unquoted path matching regex ^\w+"); }
                    var word = remPathExpr[idx..(idx+len)];
                    pathItems.Add(new PathItem.Key(word.ToString()));
                    remPathExpr = remPathExpr[(idx+len)..].TrimStart();
                    break;
                }
            }
        }
        
        return new LexedPath(pathItems.ToImmutableArray(), isAppend);
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
            return new LexedCell.Path(LexPath(trimmedText.AsSpan()));
        }

        // String Cell
        JsonVal.Any jsonValue = JsonSerializer.SerializeToNode(trimmedText, jsonOpts);
        return new LexedCell.JVal(jsonValue);
    }
}