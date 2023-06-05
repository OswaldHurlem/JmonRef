using System.Text.Json;
using LibJmon;
using Microsoft.VisualBasic.FileIO;

if (!args.Any())
{
    throw new Exception("No CSV file specified");
}

using var csvFile = File.OpenRead(args[0]);

string[,] cells = CsvUtil.CsvToCells(csvFile, ",");
JsonSerializerOptions jsonOpts = new() { WriteIndented = true };
string json = ApiV0.ParseJmon(cells, jsonOpts);
Console.WriteLine(json);