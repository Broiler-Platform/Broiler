using System;

namespace Broiler.JavaScript.Core.Core.Module;

/// <summary>
/// Exports given Type as class.
/// </summary>
/// <param name="name">Asterix '*' if null</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class ExportAttribute(string name = null) : Attribute
{
    public string Name { get; } = name;
}
