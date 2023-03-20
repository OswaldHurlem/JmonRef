// TODO check for stray values
// TODO how to have empty array in ArrayOfArrays?

using System.Diagnostics;
using System.Text.Json;
using LibJmon.Values;

namespace LibJmon.Impl;

enum CellType
{
    Invalid,
    String,
    JsonLit,
    Array,
    Object,
    ArrayOfArrays,
    ObjectOfArrays,
    Table,
    TableWithKeys,
};

readonly record struct Coord(int Row, int Col)
{
    public static Coord operator +(Coord a, Coord b) => new(a.Row + b.Row, a.Col + b.Col);
    public static Coord operator -(Coord a, Coord b) => new(a.Row - b.Row, a.Col - b.Col);

    public static implicit operator Coord((int row, int col) t) => new(t.row, t.col);
    public static implicit operator (int row, int col)(Coord c) => (c.Row, c.Col);
}

readonly record struct Rect(Coord Beg, Coord End)
{
    public static implicit operator Rect((Coord beg, Coord end) t) => new(t.beg, t.end);
    public static implicit operator (Coord beg, Coord end)(Rect r) => (r.Beg, r.End);
    
    public Rect OffsetBeg(Coord offset) => this with { Beg = Beg + offset };
}

readonly record struct Grid(string[,] Cells)
{
    public string this[Coord coord] => Cells[coord.Row, coord.Col];
    public string this[int row, int col] => Cells[row, col];
    
    public int NRows => Cells.GetLength(0);
    public int NCols => Cells.GetLength(1);
}

static class Defaults
{
    public static JsonStr JsonStrFromTxt(string val) => new(JsonSerializer.Serialize(val));

    public static JsonVal JsonAnyFromCode(string val)
    {
        var json = JsonSerializer.Deserialize<JsonElement>(val);
        return json.ValueKind switch
        {
            JsonValueKind.String => new JsonStr(val),
            _ => new JsonNonStr(val),
        };
    }
}

static class Glyphs
{
    public const string Array = ":[";
    public const string Object = ":{";
    public const string ArrayOfArrays = ":[[";
    public const string ObjectOfArrays = ":{[";
    public const string Table = ":[{";
    public const string TableWithKeys = ":{{";
    public const string TpArray = ":'[";
    public const string TpObject = ":'{";
    public const string TpArrayOfArrays = ":'[[";
    public const string TpObjectOfArrays = ":'{[";
    public const string TpTable = ":'[{";
    public const string TpTableWithKeys = ":'{{";

    public const string JsonPrefix = "::";
    public const string NonStrCellPrefix = ":";
}

static class Dicts
{
    public static IReadOnlyDictionary<string, (CellType cellType, bool isTransposed)>
        CellTypeFromGlyph =>
        new Dictionary<string, (CellType, bool)>
        {
            { Glyphs.Array            , (CellType.Array         , false) },
            { Glyphs.TpArray          , (CellType.Array         , true ) },
            { Glyphs.Object           , (CellType.Object        , false) },
            { Glyphs.TpObject         , (CellType.Object        , true ) },
            { Glyphs.ArrayOfArrays    , (CellType.ArrayOfArrays , false) },
            { Glyphs.TpArrayOfArrays  , (CellType.ArrayOfArrays , true ) },
            { Glyphs.ObjectOfArrays   , (CellType.ObjectOfArrays, false) },
            { Glyphs.TpObjectOfArrays , (CellType.ObjectOfArrays, true ) },
            { Glyphs.Table            , (CellType.Table         , false) },
            { Glyphs.TpTable          , (CellType.Table         , true ) },
            { Glyphs.TableWithKeys    , (CellType.TableWithKeys , false) },
            { Glyphs.TpTableWithKeys  , (CellType.TableWithKeys , true ) },
        };
}

static class Logic
{
    private static CellType GetOtherCellType(string cellTxt) =>
        cellTxt.StartsWith(Glyphs.JsonPrefix)
            ? CellType.JsonLit
            : cellTxt.StartsWith(Glyphs.NonStrCellPrefix)
                ? CellType.Invalid
                : CellType.String;
    
    public static (CellType, bool isTransposed) GetCellType(string cellTxt) =>
        Dicts.CellTypeFromGlyph.TryGetValue(cellTxt, out var result)
            ? result
            : (GetOtherCellType(cellTxt), false);
    
    public static IEnumerable<Coord> Iterate(Rect r) =>
        from row in Enumerable.Range(r.Beg.Row, r.End.Row - r.Beg.Row)
        from col in Enumerable.Range(r.Beg.Col, r.End.Col - r.Beg.Col)
        select new Coord(row, col);
    
    public static Rect? FindValueRect(this Grid grid, Rect searchRect)
    {
        var nullOrElmt0 = grid.FirstValueCell(searchRect);
        if (nullOrElmt0 is null) { return null; }
        return searchRect with { Beg = nullOrElmt0.Value };
    }

    public static IEnumerable<Rect>
        GetElmtRects(this Grid grid, Rect searchRect, bool isTransposed)
    {
        var nullOrElmt0 = grid.FirstValueCell(searchRect);
        if (nullOrElmt0 is null) { return Array.Empty<Rect>(); }
        var elmt0 = nullOrElmt0.Value;

        if (!isTransposed)
        {
            var rows = Enumerable.Range(elmt0.Row, searchRect.End.Row - elmt0.Row)
                .Where(row => grid[row, elmt0.Col] != "")
                .Concat(new[] { searchRect.End.Row })
                .ToList();

            return Enumerable.Zip(
                    rows,
                    rows.Skip(1),
                    (bRow, eRow) => new Rect((bRow, elmt0.Col), (eRow, searchRect.End.Col))
                ).ToList();
        }
        else
        {
            var cols = Enumerable.Range(elmt0.Col, searchRect.End.Col - elmt0.Col)
                    .Where(col => grid[elmt0.Row, col] != "")
                    .Concat(new[] { searchRect.End.Col })
                    .ToList();

            return Enumerable.Zip(
                    cols,
                    cols.Skip(1),
                    (bCol, eCol) => new Rect((elmt0.Row, bCol), (searchRect.End.Row, eCol))
                ).ToList();
        }
    }
    
    public static Coord? FirstCell(this Grid grid, Rect rect, Func<string, bool> pred) =>
        Iterate(rect)
            .Cast<Coord?>()
            .FirstOrDefault(coord => pred(grid[coord!.Value]), null);
    
    public static Coord? FirstValueCell(this Grid grid, Rect rect) =>
        grid.FirstCell(rect, s => s != "");
    
    public static JsonNonStr MakeArray(IEnumerable<JsonVal> elmts) =>
        new("[" + string.Join(',', elmts.Select(ev => ev.Text)) + "]");
    
    public static JsonNonStr MakeObject(IEnumerable<(JsonStr k, JsonVal v)> kvps) =>
        new("{" + string.Join(',', kvps.Select(kvp => $"{kvp.k.Text}:{kvp.v.Text}")) + "}");
}

sealed record class JsonFromFfjg_Doer(
        Grid Grid,
        Delegates.JsonStrFromBareText JsonStrFromBareText,
        Delegates.JsonValFromJsonText JsonValFromJsonText
)
{
    public static JsonFromFfjg_Doer Create(JmonSheet jmonSheet, ApiV0.JsonFromJmon_Options options) =>
        new(
            new(jmonSheet.Cells),
            options.StrFromBareText ?? Defaults.JsonStrFromTxt,
            options.ValFromJsonText ?? Defaults.JsonAnyFromCode
            );

    public JsonVal Do()
    {
        var outerRect = new Rect((0, 0), (Grid.NRows, Grid.NCols));
        var valueRect = Grid.FindValueRect(outerRect);
        if (valueRect is null) { throw JmonException.General("Document contains no Value Cells."); }
        return MakeVal(valueRect.Value);
    }

    JsonVal MakeVal(Rect rect)
    {
        var begCellCode = Grid[rect.Beg];
        var (cellType, isTransposed) = Logic.GetCellType(begCellCode);

        switch (cellType)
        {
            case CellType.Invalid:
                throw JmonException.AtCoord(
                    rect.Beg,
                    $"Cell text `{begCellCode}` starts with `{Glyphs.NonStrCellPrefix} but is not a valid glyph."
                    );
            case CellType.String:
                return JsonStrFromBareText(begCellCode);
            case CellType.JsonLit:
                return JsonValFromJsonText(begCellCode.Substring(Glyphs.JsonPrefix.Length));
            case CellType.Array:
            {
                var elmtRects = Grid.GetElmtRects(rect.OffsetBeg((1,1)), isTransposed);
                return Logic.MakeArray(elmtRects.Select(MakeVal));
            }
            case CellType.Object:
            {
                var keysAndSearchRects = MakeKeysAndSearchRects(rect, isTransposed);
                var kvps = keysAndSearchRects.Select(t =>
                {
                    var (key, searchRect) = t;
                    var valRect = Grid.FindValueRect(searchRect);
                    if (valRect is null)
                    {
                        throw JmonException.AtCoord(searchRect.Beg, $"No value found for key {key.Text}.");
                    }

                    return (key, MakeVal(valRect.Value));
                });

                return Logic.MakeObject(kvps);
            }
            case CellType.ArrayOfArrays:
            {
                JsonNonStr MakeInnerArray(Rect outerElmtRect)
                {
                    var innerElmtRects = Grid.GetElmtRects(outerElmtRect, !isTransposed);
                    return Logic.MakeArray(innerElmtRects.Select(MakeVal));
                }

                var outerElmtRects = Grid.GetElmtRects(rect.OffsetBeg((1,1)), isTransposed);
                return Logic.MakeArray(outerElmtRects.Select(MakeInnerArray));
            }
            case CellType.ObjectOfArrays:
            {
                var keysAndSearchRects = MakeKeysAndSearchRects(rect, isTransposed);
                var kvps = keysAndSearchRects.Select(t =>
                {
                    var (key, searchRect) = t;
                    var innerElmtRects = Grid.GetElmtRects(searchRect, !isTransposed);
                    var arr = Logic.MakeArray(innerElmtRects.Select(MakeVal));
                    return (key, arr as JsonVal);
                });

                return Logic.MakeObject(kvps);
            }
            case CellType.Table:
                break;
            case CellType.TableWithKeys:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        throw new UnreachableException();
    }

    public IReadOnlyList<(JsonStr key, Rect searchRect)> MakeKeysAndSearchRects(Rect objRect, bool isTransposed)
    {
        var result = new List<(JsonStr key, Rect searchRect)>();
        var keyRects = Grid.GetElmtRects(objRect.OffsetBeg((1, 1)), isTransposed);
        var seq = keyRects.Select(kr => (kr, MakeVal(kr))).ToList();
        
        foreach (var (keyRect, valInKeyRect) in seq)
        {
            if (valInKeyRect is not JsonStr key)
            {
                var msg = $"Non-string key: {valInKeyRect.Text}.";
                throw JmonException.AtCoord(keyRect.Beg, msg);
            }
            
            if (result.Any(kvp => kvp.key == key))
            {
                var msg = $"Duplicate key: {key.Text}";
                throw JmonException.AtCoord(keyRect.Beg, msg);
            }

            var searchRect = keyRect.OffsetBeg(!isTransposed ? (0, 1) : (1, 0));
            result.Add((key, searchRect));
        }

        return result;
    }
}