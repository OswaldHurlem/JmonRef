using System.Buffers;
using System.Collections;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json.Nodes;
using CommunityToolkit.HighPerformance;
using LibJmon.Linq;
using LibJmon.Sheets;
using LibJmon.SuperTypes;
using LibJmon.Types;
using Microsoft.VisualBasic.FileIO;

namespace LibJmon.Impl;

public static class TestingApi
{
    public static LexedCell[,] LexCells(ReadOnlyMemory<byte>[,] cells)
    {
        var rect = new Rect((0, 0), (cells.GetLength(0), cells.GetLength(1)));
        var lexedCells = new LexedCell[rect.Dims().Row, rect.Dims().Col];

        foreach (var c in rect.CoordSeq())
        {
            lexedCells[c.Row, c.Col] = Lexing.Lex(cells[c.Row, c.Col].Span);
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
    public static ReadOnlyMemory<byte>[,] MakeCells(TextReader textReader, string delimiter)
    {
        IEnumerable<IReadOnlyList<ReadOnlyMemory<byte>>> Inner()
        {
            using var csvReader = new TextFieldParser(textReader)
            {
                Delimiters = new[] { delimiter },
                HasFieldsEnclosedInQuotes = true
            };

            while (!csvReader.EndOfData)
            {
                var row = csvReader.ReadFields();
                if (row is null) { throw new Exception("null row"); }
                yield return row.Select(field => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(field)).ToList();
            }
        }

        var rows = Inner().ToList();
        var rect = new Rect((0, 0), (rows.Count, rows.Max(row => row.Count)));
        var cells = new ReadOnlyMemory<byte>[rect.Dims().Row, rect.Dims().Col];
        foreach (var coord in rect.CoordSeq())
        {
            cells[coord.Row, coord.Col] = coord.Col < rows[coord.Row].Count
                ? rows[coord.Row][coord.Col]
                : ReadOnlyMemory<byte>.Empty;
        }
        
        return cells;
    }
    
    public static ReadOnlyMemory<byte>[,] MakeCells(string text, string delimiter)
    {
        using var stringReader = new StringReader(text);
        return MakeCells(stringReader, delimiter);
    }

    public static ReadOnlyMemory<byte>[,] MakeCells(Stream stream, string delimiter)
    {
        using var streamReader = new StreamReader(stream);
        return MakeCells(streamReader, delimiter);
    }
}