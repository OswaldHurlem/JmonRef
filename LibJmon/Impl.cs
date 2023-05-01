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

public readonly record struct Assignment(ConvertedPath Path, JsonVal.Any Value);

// internal readonly record struct LexedValSeq(IReadOnlyList<OneOf<JsonVal, None>> Vals);

public static class Assignments
{
    readonly record struct ConvertPathsState(IDictionary<ConvertedPath, int> IdxForPartialPath)
    {
        public static ConvertPathsState Initial => new(new Dictionary<ConvertedPath, int>());
    }

    private static ConvertedPath
        ConvertPath(ConvertedPath prefixPath, LexedPath lexedPath, IDictionary<ConvertedPath, int> idxForPartialPath)
    {
        var cvtPathElmts = prefixPath.V.ToList();

        PathItem.Idx ConvertProtoIdxElmt(PathItem.Idx arrElmt)
        {
            ConvertedPath partialPath = cvtPathElmts.ToImmutableArray();
            if (!idxForPartialPath.TryGetValue(partialPath, out var idx)) { idx = -1; }

            idx += arrElmt.V;
            idxForPartialPath[partialPath] = idx;
            return idx;
        }

        var pathSegments = lexedPath.V.Segment(elmt => elmt is PathItem.Idx);

        foreach (IReadOnlyList<PathItem> pathSegment in pathSegments)
        {
            var elmt0 = pathSegment[0].AsOneOf().Match<PathItem>(keyElmt => keyElmt, ConvertProtoIdxElmt);
            cvtPathElmts.Add(elmt0);
            cvtPathElmts.AddRange(pathSegment.Skip(1));
        }

        return new ConvertedPath(cvtPathElmts.ToImmutableArray());
    }

    public static IReadOnlyList<Assignment> ComputeAssignments(AstNode head)
    {
        Dictionary<ConvertedPath, int> idxForPartialPath = new();

        IEnumerable<Assignment> Inner(ConvertedPath parentPath, AstNode node) =>
            node.AsOneOf().Match(
                leaf => new[] { new Assignment(parentPath, leaf) },
                branch => branch.V.SelectMany(
                    item => Inner(ConvertPath(parentPath, item.Path, idxForPartialPath), item.Node)
                ),
                error => throw new Exception("Asdf") // TODO
            );

        return Inner(ConvertedPath.Empty, head).ToList();
    }


   // private static bool FindStrayCell(LexedSubSheet sheet, CellKind kind, Coord firstCoord, out AstRslt.Error stray) =>
   //     sheet.Find(firstCoord, cell => cell.Is(kind))
   //         .MapFound(coord => AstRslt.MakeStrayCell(kind, sheet.ToOuter(coord)))
   //         .TryPickT0(out stray, out _);
}

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

    public static IReadOnlyList<Assignment> ComputeAssignments(AstNode ast)
        => Assignments.ComputeAssignments(ast);
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