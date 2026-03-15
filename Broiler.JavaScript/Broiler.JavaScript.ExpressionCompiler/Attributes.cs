#nullable enable
using System;

namespace Yantra.Core;

public class JSInternalObjectAttribute: Attribute
{

}
public class JSBaseClassAttribute(string name) : Attribute
{
    public readonly string Name = name;
}

public class JSPrototypeMethodAttribute: Attribute {
}

public class JSGlobalFunctionAttribute: Attribute
{

}

public class JSFunctionGeneratorAttribute(string? name = null, string keysClass = "KeyStrings") : Attribute
{
    public readonly string? Name = name;

    public readonly string KeysClass = keysClass;

    public bool Register { get; set; } = true;

    public bool Globals { get; set; }
}

public class JSClassGeneratorAttribute(string? name = null, string keysClass = "KeyStrings") : Attribute
{
    public readonly string? Name = name;

    public readonly string KeysClass = keysClass;

    public bool Register { get; set; } = true;
}

public class JSRegistrationGeneratorAttribute : Attribute
{
}
