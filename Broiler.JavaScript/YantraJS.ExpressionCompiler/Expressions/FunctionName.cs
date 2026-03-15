#nullable enable

namespace YantraJS.Expressions;

public readonly struct FunctionName(string? name, string? location = null, int line = 0, int column = 0)
{
    public readonly string Name = name ?? "Unnamed";
    public readonly string? Location = location;
    public readonly int Line = line;
    public readonly int Column = column;

    public string FullName =>
        $"{Name}-{Location}:{Line},{Column}";

    public static implicit operator FunctionName(string name) => new(name);

    public override string ToString() => Name;
}