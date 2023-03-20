using System.Text.Json;
using Microsoft.VisualBasic.FileIO;

if (!args.Any())
{
    throw new Exception("No CSV file specified");
}

var csvCells = ReadCsvFile(args[0]);
var ffjgDoc = new LibJmon.Values.JmonSheet(csvCells);
var options = LibJmon.ApiV0.JsonFromJmon_Options.Default;
var jsonVal = LibJmon.ApiV0.JsonFromJmon(ffjgDoc, options);

// Prettify
using var jsonDoc = JsonDocument.Parse(jsonVal.Text);
var jsonOpts = new JsonSerializerOptions { WriteIndented = true };
var prettyJson = JsonSerializer.Serialize(jsonDoc.RootElement, jsonOpts);
Console.WriteLine(prettyJson);

string[,] ReadCsvFile(string fileName)
{
    IEnumerable<string[]> ReadCsvInnner()
    {
        using var csvReader = new TextFieldParser(fileName)
        {
            Delimiters = new[] { "," },
            HasFieldsEnclosedInQuotes = true,
        };

        while (!csvReader.EndOfData)
        {
            var row = csvReader.ReadFields();
            if (row is null) { throw new Exception("null row"); }
            yield return row;
        }
    }
    
    var rows = ReadCsvInnner().ToArray();
    var nRows = rows.Length;
    var nCols = rows.FirstOrDefault()?.Length ?? 0;
    
    if (nCols == 0) { throw new Exception("CSV is empty"); }

    var cells = new string[nRows, nCols];
    foreach (var (row, i) in rows.Select((r, i) => (r, i)))
    {
        foreach (var (cell, j) in row.Select((c, j) => (c, j)))
        {
            cells[i, j] = cell;
        }
    }
    
    return cells;
}