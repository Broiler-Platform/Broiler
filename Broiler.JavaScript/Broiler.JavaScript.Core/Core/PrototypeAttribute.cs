using System;

namespace YantraJS.Core;


[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class JSRuntimeAttribute(Type staticType, Type prototype) : Attribute
{
    public readonly Type StaticType = staticType;
    public readonly Type Prototype = prototype;

    public bool PreventConstructorInvoke { get; set; }
}

public enum MemberType: int
{
    Method = 1,
    Get = 2,
    Set = 4,
    Constructor = 8,
    StaticMethod = 0xF1,
    StaticGet = 0xF2,
    StaticSet = 0xF4
}

[AttributeUsage(AttributeTargets.Method , AllowMultiple = false, Inherited = false)]

public class ConstructorAttribute: PrototypeAttribute
{
    public ConstructorAttribute(): base(null, JSPropertyAttributes.Empty, MemberType.Constructor)
    {

    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class SymbolAttribute(string name) : Attribute
{
    public readonly string Name = name;
}

/// <summary>
/// Should only be defined on static method and field
/// </summary>
/// <remarks>
/// This is done to reduce number of method calls, checking receiver type and throwing TypeError
/// on instance method will require one more method call and which will slow down if inlining is not supported
/// on AOT platforms
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public class PrototypeAttribute: Attribute
{
    public readonly KeyString Name;

    public readonly MemberType MemberType;

    public readonly JSPropertyAttributes Attributes;

    public readonly bool IsSymbol;

    public int Length { get; set; }

    public bool IsStatic => ((int)MemberType & 0xF0) > 0;

    public bool IsMethod => ((int)MemberType & 0x1) > 0;

    public bool IsGetProperty => ((int)MemberType & 0x2) > 0;
    public bool IsSetProperty => ((int)MemberType & 0x4) > 0;

    public JSPropertyAttributes ConfigurableValue =>
        Attributes == JSPropertyAttributes.Empty
        ? JSPropertyAttributes.ConfigurableValue
        : Attributes;

    public JSPropertyAttributes ReadonlyValue =>
        Attributes == JSPropertyAttributes.Empty
        ? JSPropertyAttributes.ReadonlyValue
        : Attributes;

    public JSPropertyAttributes ConfigurableProperty =>
        Attributes == JSPropertyAttributes.Empty
        ? JSPropertyAttributes.ConfigurableProperty
        : Attributes;

    public JSPropertyAttributes ConfigurableReadonlyValue =>
        Attributes == JSPropertyAttributes.Empty
        ? JSPropertyAttributes.ConfigurableReadonlyValue
        : Attributes;
    public PrototypeAttribute(string name, 
        JSPropertyAttributes attributes = JSPropertyAttributes.Empty, 
        MemberType memberType = MemberType.Method,
        bool isSymbol = false)
    {
        IsSymbol = isSymbol;
        Attributes = attributes;
        if (name != null)
        {
            Name = name;
        }
        MemberType = memberType;
    }
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public class GetProperty(string name, JSPropertyAttributes attributes = JSPropertyAttributes.ConfigurableProperty,
    bool isSymbol = false) : PrototypeAttribute(name, attributes, MemberType.Get, isSymbol)
{
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public class SetProperty(string name, JSPropertyAttributes attributes = JSPropertyAttributes.ConfigurableProperty,
    bool isSymbol = false) : PrototypeAttribute(name, attributes, MemberType.Set, isSymbol)
{
}


[AttributeUsage(AttributeTargets.Method | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public class StaticGetProperty(string name, JSPropertyAttributes attributes = JSPropertyAttributes.ConfigurableProperty, bool isSymbol = false) : PrototypeAttribute(name, attributes, MemberType.StaticGet, isSymbol)
{
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public class StaticSetProperty(string name, JSPropertyAttributes attributes = JSPropertyAttributes.ConfigurableProperty, bool isSymbol = false) : PrototypeAttribute(name, attributes, MemberType.StaticSet, isSymbol)
{
}

/// <summary>
/// Should only be defined on static method and field
/// </summary>
/// <remarks>
/// This is done to reduce number of method calls, checking receiver type and throwing TypeError
/// on instance method will require one more method call and which will slow down if inlining is not supported
/// on AOT platforms
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public class StaticAttribute(string name,
    JSPropertyAttributes attributes = JSPropertyAttributes.Empty,
    bool isSymbol = false) : PrototypeAttribute(name, attributes, MemberType.StaticMethod, isSymbol)
{
}
