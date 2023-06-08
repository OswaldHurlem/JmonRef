using System.Text.Encodings.Web;
using System.Text.Json;
using LibJmon;

const int kErrCode = -1;

void LogLexingErr(JmonLexErr e)
{
    Console.WriteLine($"\tMsg: {e.Msg}");
    Console.WriteLine($"\tCoord: {e.Coord}");
    if (e.LexedExpression is not null)
    {
        var expr = e.LexedExpression;
        if (expr.Length > 50) { expr = $"{expr[..50]}.."; }
        Console.WriteLine($"\tLexedExpression: {expr}");
    }
}

void LogParseErr(JmonParseErr e)
{
    Console.WriteLine($"\tPath: {e.Path}");
    Console.WriteLine($"\tMsg: {e.Msg}");
    if (e.FocusCell is not null) { Console.WriteLine($"\tFocusCell: {e.FocusCell}"); }
    Console.WriteLine($"\tExprCells: {e.ExprCells}");
}

// Top-Level statement

if (!args.Any())
{
    throw new Exception("No CSV file specified");
}

using var csvFile = File.OpenRead(args[0]);

string[,] cells = CsvUtil.CsvToCells(csvFile, ",");
JsonSerializerOptions jsonOpts = new()
{
    WriteIndented = true,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
};

try
{
    string json = ApiV0.ParseJmon(cells, jsonOpts);
    Console.WriteLine(json);
    return 0;
}
catch (JmonException e) when (e.JmonErr is JmonLexErr lexErr)
{
    Console.WriteLine("Encountered lexing error:");
    LogLexingErr(lexErr);
    return kErrCode;
}
catch (JmonException e) when (e.JmonErr is JmonParseErr parseErr)
{
    Console.WriteLine("Encountered parsing error:");
    LogParseErr(parseErr);
    return kErrCode;
}
catch (JmonException e) when (e.JmonErr is JmonMultiErr multiErr)
{
    var lexingErrs = multiErr.Errs.OfType<JmonLexErr>().ToList();
    var parseErrs = multiErr.Errs.OfType<JmonParseErr>().ToList();
    var otherErrs = multiErr.Errs.Where(err => err is not (JmonLexErr or JmonParseErr)).ToList();

    if (lexingErrs.Any())
    {
        Console.WriteLine($"Encountered {lexingErrs.Count} lexing errors:");
        foreach (var (lexingErr, idx) in lexingErrs.Select((o, i) => (o, i)))
        {
            Console.WriteLine($"Lexing error {idx}:");
            LogLexingErr(lexingErr);
        }
    }
    
    if (parseErrs.Any())
    {
        Console.WriteLine($"Encountered {parseErrs.Count} parsing errors:");
        foreach (var (parseErr, idx) in parseErrs.Select((o, i) => (o, i)))
        {
            Console.WriteLine($"Parsing error {idx}:");
            LogParseErr(parseErr);
        }
    }

    if (otherErrs.Any())
    {
        throw new JmonException(new JmonMultiErr(otherErrs));
    }

    return kErrCode;
}