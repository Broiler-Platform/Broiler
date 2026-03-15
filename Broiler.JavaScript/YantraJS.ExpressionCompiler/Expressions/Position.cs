#nullable enable

namespace YantraJS.Expressions;

public readonly struct Position(int line, int column)
{
    public readonly int Line = line;
    public readonly int Column = column;

    public override string ToString() => $"{Line}, {Column}";
}
