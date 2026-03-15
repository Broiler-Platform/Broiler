#nullable enable
using System;

namespace YantraJS.Core.Clr;

public class JSExportAttribute(
    string? name = null) : Attribute {

    public readonly string? Name = name;

    public bool AsCamel = true;

    public int Length { get; set; }

    public bool Pure { get; set; }

    public bool IsConstructor { get; set; }
}
