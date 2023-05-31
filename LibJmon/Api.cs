using System.Text.Json;
using LibJmon.Types;
using LibJmon.Impl;

namespace LibJmon;

public static class ApiV0
{
    public static string ParseJmon(string[,] cells, JsonSerializerOptions jsonOptions)
        => ApiV0Impl.ParseJmon(cells, jsonOptions);
}