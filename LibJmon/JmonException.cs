using System.Collections;
using System.Text;
using System.Text.Json;

namespace LibJmon;

public abstract record JmonErr;

public abstract class JmonExceptionBase : Exception
{
    public abstract JmonErr JmonErr { get; }

    public override IDictionary Data => new Dictionary<string, object>
    {
        { "DataObject", JmonErr },
    };

    public override string Message => JmonErr.ToString() ?? "<MISSING>";

    protected JmonExceptionBase(Exception? inner) : base("", inner) { }
}

public sealed class JmonException<TErr> : JmonExceptionBase where TErr : JmonErr
{
    public override TErr JmonErr { get; }

    public JmonException(TErr jmonErr) : base(null) => JmonErr = jmonErr;
    
    public JmonException(TErr jmonErr, Exception inner) : base(inner) => JmonErr = jmonErr;
}

public readonly record struct CellRect(int Top, int Left, int Height, int Width);
public readonly record struct CellCoord(int Row, int Col);

public record LexingErr(string Msg, CellCoord Coord) : JmonErr;

public record ParseErr(string Path, string Msg, CellCoord? FocusCell, CellRect ExprCells) : JmonErr;

public record InternalErr(string Msg) : JmonErr;

public record MultiErr(IReadOnlyList<JmonErr> Errs) : JmonErr
{
    protected override bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(Errs)} = [ ").AppendJoin(", ", Errs).Append(" ]");
        return true;
    }
}