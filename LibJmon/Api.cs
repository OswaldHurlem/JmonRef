namespace LibJmon;

public static class ApiV0
{
    public sealed record class JsonFromJmon_Options(
        Delegates.JsonStrFromBareText? StrFromBareText = null,
        Delegates.JsonValFromJsonText? ValFromJsonText = null
    )
    {
        public static JsonFromJmon_Options Default { get; } = new();
    }
    
    public static Values.JsonVal JsonFromJmon(Values.JmonSheet jmonSheet, JsonFromJmon_Options options)
    {
        var doer = Impl.JsonFromFfjg_Doer.Create(jmonSheet, options);
        return doer.Do();
    }
}

public sealed class JmonException : Exception
{
    public (int row, int col)? Coord { get; } = null;
    
    private JmonException(string message, Exception? innerException)
        : base(message, innerException) { }

    private JmonException(string message, Exception? innerException, (int row, int col)? coord = null)
        : this(message, innerException) => Coord = coord;

    public static JmonException AtCoord((int row, int col) coord, string message) =>
        new ($"Error @{coord}: {message}", null, coord);
    
    public static JmonException General(string message) =>
        new (message, null);
}