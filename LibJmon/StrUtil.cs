using System.Text.RegularExpressions;

namespace LibJmon;

internal static class StrUtil
{
    public static (int idx, int len) Match(this Regex regex, ReadOnlySpan<char> text)
    {
        var matches = regex.EnumerateMatches(text);
        return matches.MoveNext()
            ? (matches.Current.Index, matches.Current.Length)
            : (-1, 0);
    }
    
    public static string ConvertSqJsonStrToDq(string sqStr) =>
        sqStr.Replace("\"", "\\\"")
            .Replace("\\'", "\uE0E1").Replace('\'', '\uE001')
            .Replace('\uE001', '\"').Replace('\uE0E1', '\'');
}