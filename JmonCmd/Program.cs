using System.Text.Json;
using Microsoft.VisualBasic.FileIO;

if (!args.Any())
{
    throw new Exception("No CSV file specified");
}

bool unitTestOutput = args is [_, "-asdf"];

using var csvFile = File.OpenRead(args[0]);

var csvCells = LibJmon.Impl.CsvUtil.MakeCells(csvFile, ",");
var lexedCells = LibJmon.TestingApi.LexCells(csvCells);
var ast = LibJmon.TestingApi.ParseLexedCells(lexedCells);
var json = LibJmon.TestingApi.AstToJson(ast).V;

// Prettify
var jsonOpts = new JsonSerializerOptions { WriteIndented = true };
var prettyJson = JsonSerializer.Serialize(json, jsonOpts);

if (unitTestOutput)
{
    Console.WriteLine(args[0]);
    Console.WriteLine("--- CSV ---");
    Console.WriteLine(File.ReadAllText(args[0]));
    Console.WriteLine("--- JSON ---");
    Console.WriteLine(prettyJson);
}
else
{
    Console.WriteLine(prettyJson);
}