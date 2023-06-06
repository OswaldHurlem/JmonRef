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
        { "JmonErr", JmonErr },
    };

    public override string Message => JmonErr.ToString() ?? "<MISSING>";

    protected JmonExceptionBase(Exception? inner) : base("", inner) { }
}

public sealed class JmonException : Exception
{
    public JmonErr JmonErr { get; }
    
    public override IDictionary Data => new Dictionary<string, object>
    {
        { "JmonErr", JmonErr },
    };
    
    public override string Message => JmonErr.ToString();

    public JmonException(JmonErr jmonErr) : base(jmonErr.ToString()) => JmonErr = jmonErr;
    
    public JmonException(JmonErr jmonErr, Exception inner) : base(jmonErr.ToString(), inner) => JmonErr = jmonErr;
}

public readonly record struct CellRect(Range Rows, Range Cols);
public readonly record struct CellCoord(int Row, int Col);

public record JmonLexErr(string Msg, CellCoord Coord, string? LexedExpression) : JmonErr;

public record JmonParseErr(string Path, string Msg, CellCoord? FocusCell, CellRect ExprCells) : JmonErr;

public record JmonInternalErr(string Msg) : JmonErr;

public record JmonMultiErr(IReadOnlyList<JmonErr> Errs) : JmonErr
{
    protected override bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(Errs)} = [ ").AppendJoin(", ", Errs).Append(" ]");
        return true;
    }
}

public static class JmonErrExtensions
{
    public static JmonException ToExc(this JmonErr err) => new(err);
    
    public static JmonException ToExc(this JmonErr err, Exception inner) => new(err, inner);
}