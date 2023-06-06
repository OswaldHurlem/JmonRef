using System.Text.Json;
using LibJmon;

if (!args.Any())
{
    throw new Exception("No CSV file specified");
}

using var csvFile = File.OpenRead(args[0]);

string[,] cells = CsvUtil.CsvToCells(csvFile, ",");
JsonSerializerOptions jsonOpts = new() { WriteIndented = true };

try
{
    string json = ApiV0.ParseJmon(cells, jsonOpts);
    Console.WriteLine(json);
}
catch (JmonException<LexingErr> e)
{
    Console.WriteLine("Encountered lexing error:");
    LogLexingErr(e.JmonErr);
    return -1;
}
catch (JmonException<ParseErr> e)
{
    Console.WriteLine("Encountered parsing error:");
    LogParseErr(e.JmonErr);
    return -1;
}
catch (JmonException<MultiErr> e)
{
    var allErrs = e.JmonErr.Errs;
    var lexingErrs = allErrs.OfType<LexingErr>().ToList();
    var parseErrs = allErrs.OfType<ParseErr>().ToList();
    var otherErrs = allErrs.Where(e => e is not (LexingErr or ParseErr)).ToList();

    if (lexingErrs.Any())
    {
        Console.WriteLine($"Encountered {lexingErrs.Count} lexing errors:");
        foreach (var (lexingErr, idx) in lexingErrs.Select((e, i) => (e, i)))
        {
            Console.WriteLine($"Lexing error {idx}:");
            LogLexingErr(lexingErr);
        }
    }
    
    if (parseErrs.Any())
    {
        Console.WriteLine($"Encountered {parseErrs.Count} parsing errors:");
        foreach (var (parseErr, idx) in parseErrs.Select((e, i) => (e, i)))
        {
            Console.WriteLine($"Parsing error {idx}:");
            LogParseErr(parseErr);
        }
    }

    if (otherErrs.Any())
    {
        throw new JmonException<MultiErr>(new(otherErrs));
    }

    return -1;
}

return 0;

void LogLexingErr(LexingErr e)
{
    Console.WriteLine($"\tMsg: {e.Msg}");
    Console.WriteLine($"\tCoord: {e.Coord}");
}

void LogParseErr(ParseErr e)
{
    Console.WriteLine($"\tPath: {e.Path}");
    Console.WriteLine($"\tMsg: {e.Msg}");
    Console.WriteLine($"\tFocusCell: {e.FocusCell}");
    Console.WriteLine($"\tExprCells: {e.ExprCells}");
}
