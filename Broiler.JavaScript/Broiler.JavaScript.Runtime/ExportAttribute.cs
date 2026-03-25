using System;

namespace Broiler.JavaScript.Runtime;

/// <summary>
/// Exports the given type as a class.
/// </summary>
/// <param name="name">Asterisk '*' if null</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class ExportAttribute(string name = null) : Attribute
{
    public string Name { get; } = name;
}
