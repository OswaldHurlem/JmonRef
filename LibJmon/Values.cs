namespace LibJmon.Values;

public sealed record class JmonSheet(string[,] Cells);

public abstract record class JsonVal(string Text);