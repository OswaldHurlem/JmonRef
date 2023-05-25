using LibJmon.Linq;
using LibJmon.Sheets;
using LibJmon.Types;
using Microsoft.VisualBasic.FileIO;

namespace LibJmon.Impl;

public static class TestingApi
{
    public static LexedCell[,] LexCells(string[,] cells)
    {
        var rect = new Rect((0, 0), (cells.GetLength(0), cells.GetLength(1)));
        var lexedCells = new LexedCell[rect.Dims().Row, rect.Dims().Col];

        foreach (var c in rect.CoordSeq())
        {
            lexedCells[c.Row, c.Col] = Lexing.Lex(cells[c.Row, c.Col]);
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
    public static JsonVal.Any AstToJson(AstNode ast) => Assignments.AstToJson(ast);
}


public static class CsvUtil
{
    public static string[,] MakeCells(TextReader textReader, string delimiter)
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
    
    public static string[,] MakeCells(string text, string delimiter)
    {
        using var stringReader = new StringReader(text);
        return MakeCells(stringReader, delimiter);
    }

    public static string[,] MakeCells(Stream stream, string delimiter)
    {
        using var streamReader = new StreamReader(stream);
        return MakeCells(streamReader, delimiter);
    }
}