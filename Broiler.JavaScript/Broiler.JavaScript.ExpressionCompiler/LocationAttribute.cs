using System;

namespace Broiler.JavaScript.ExpressionCompiler;

[AttributeUsage(AttributeTargets.Method)]
public class LocationAttribute(string location, string name, int line, int column) : Attribute
{
    public readonly string Location = location;
    public readonly string Name = name;
    public readonly int Line = line;
    public readonly int Column = column;
}
