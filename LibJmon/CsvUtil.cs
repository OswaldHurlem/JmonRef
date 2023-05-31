using LibJmon.Sheets;
using Microsoft.VisualBasic.FileIO;

namespace LibJmon;

public static class CsvUtil
{
    public static string[,] CsvToCells(TextReader textReader, string delimiter)
    {
        IEnumerable<IReadOnlyList<string>> Inner()
        {
            using var csvReader = new TextFieldParser(textReader)
            {
                Delimiters = new[] { delimiter },
                HasFieldsEnclosedInQuotes = true
            };

            while (!csvReader.EndOfData)
            {
                if (csvReader.ReadFields() is { } row) { yield return row; }
                else { throw new Exception("null row"); }
            }
        }

        var rows = Inner().ToList();
        var rect = new Rect((0, 0), (rows.Count, rows.Max(row => row.Count)));
        var cells = new string[rect.Dims().Row, rect.Dims().Col];
        foreach (var coord in rect.CoordSeq())
        {
            cells[coord.Row, coord.Col] = coord.Col < rows[coord.Row].Count ? rows[coord.Row][coord.Col] : string.Empty;
        }
        
        return cells;
    }
    
    public static string[,] CsvToCells(string text, string delimiter)
    {
        using var stringReader = new StringReader(text);
        return CsvToCells(stringReader, delimiter);
    }

    public static string[,] CsvToCells(Stream stream, string delimiter)
    {
        using var streamReader = new StreamReader(stream);
        return CsvToCells(streamReader, delimiter);
    }
}