namespace YantraJS.Core.FastParser;

public readonly struct SpanLocation(int line, int column)
{
    public readonly int Line = line;
    public readonly int Column = column;

    public override string ToString() => $"{Line}, {Column}";
}
