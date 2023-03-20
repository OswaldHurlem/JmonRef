namespace LibJmon.Values;

public sealed record class JmonSheet(string[,] Cells);
public abstract record class JsonVal(string Text);
public sealed record class JsonStr(string Text) : JsonVal(Text);
public sealed record class JsonNonStr(string Text) : JsonVal(Text);