# JmonRef
Reference implementation of a JMON (JSON-building Matrix Object Notation) parser

## [Introduction to the JMON Format](LibJmon/JmonByExample.md)

## How to use:

### Command-line
`JmonCmd.exe your_jmon_file.csv`

### Library
- Link to LibJmon
- Use `ApiV0.ParseJmon(string[,] cells, JsonSerializerOptions jsonOptions)` to parse JMON to JSON.
  - Catch exceptions of type `JmonException` to get JMON evaluation errors.
