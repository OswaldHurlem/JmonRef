using System.Collections;
using System.Text.Json;

namespace LibJmon;

public abstract record JmonExcData;

public abstract class JmonExceptionBase : Exception
{
    public abstract JmonExcData JmonExcData { get; }

    public override IDictionary Data => new Dictionary<string, object>
    {
        { "DataObject", JmonExcData },
    };

    public override string Message => JmonExcData.ToString() ?? "<MISSING>";

    protected JmonExceptionBase(Exception? inner) : base("", inner) { }
}

public sealed class JmonException<TData> : JmonExceptionBase where TData : JmonExcData
{
    public override TData JmonExcData { get; }

    public JmonException(TData jmonExcData) : base(null) => JmonExcData = jmonExcData;
    
    public JmonException(TData jmonExcData, Exception inner) : base(inner) => JmonExcData = jmonExcData;
}

public record LexingError(int Row, int Col, string Msg) : JmonExcData;

public record ParsingError : JmonExcData;