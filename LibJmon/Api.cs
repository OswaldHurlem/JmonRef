﻿namespace LibJmon;

public static class ApiV0
{
  
    public static Values.JsonVal JsonFromJmon(Values.JmonSheet jmonSheet) => Impl.ApiV0.JsonFromJmon(jmonSheet);
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