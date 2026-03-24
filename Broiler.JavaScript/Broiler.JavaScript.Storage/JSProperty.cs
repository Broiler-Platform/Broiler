using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Broiler.JavaScript.Ast.Misc;

namespace Broiler.JavaScript.Storage;

[StructLayout(LayoutKind.Sequential)]
[DebuggerDisplay("{key}={get},{set},{value}")]
public readonly struct JSProperty
{
    public static JSProperty Empty = new();
    public readonly JSPropertyAttributes Attributes;
    public readonly uint key;

    public readonly IPropertyAccessor get;
    public readonly IPropertyAccessor set;
    public readonly IPropertyValue value;

    public JSProperty ToNotReadOnly() => new(key, get, set, value, Attributes & (~JSPropertyAttributes.Readonly));

    public JSProperty(in KeyString key, IPropertyAccessor get, IPropertyAccessor set, JSPropertyAttributes attributes)
    {
        this.key = key.Key;
        this.get = get;
        this.set = set;
        value = get;
        Attributes = attributes;
    }
    public JSProperty(uint key, IPropertyAccessor get, IPropertyAccessor set, IPropertyValue value, JSPropertyAttributes attributes)
    {
        this.key = key;
        this.get = get ?? value as IPropertyAccessor;
        this.set = set;
        this.value = value;
        Attributes = attributes;
    }

    public JSProperty(in KeyString key, IPropertyAccessor get, IPropertyAccessor set, IPropertyValue value, JSPropertyAttributes attributes)
    {
        this.key = key.Key;
        this.get = get;
        this.set = set;
        this.value = value;
        Attributes = attributes;
    }

    public JSProperty(uint key, IPropertyValue get, JSPropertyAttributes attributes)
    {
        this.key = key;
        this.get = get as IPropertyAccessor;
        set = null;
        value = get;
        Attributes = attributes;
    }

    public JSProperty(in KeyString key, IPropertyValue get, JSPropertyAttributes attributes)
    {
        this.key = key.Key;
        this.get = get as IPropertyAccessor;
        set = null;
        value = get;
        Attributes = attributes;
    }

    public bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Attributes == JSPropertyAttributes.Empty;
    }

    public bool IsConfigurable
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (Attributes & JSPropertyAttributes.Configurable) > 0;
    }

    public bool IsEnumerable
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (Attributes & JSPropertyAttributes.Enumerable) > 0;
    }

    public bool IsReadOnly
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (Attributes & JSPropertyAttributes.Readonly) > 0;
    }

    public bool IsValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (Attributes & JSPropertyAttributes.Value) > 0;
    }

    public bool IsProperty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (Attributes & JSPropertyAttributes.Property) > 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSProperty Property(IPropertyValue d, JSPropertyAttributes attributes = JSPropertyAttributes.EnumerableConfigurableValue) => new(KeyString.Empty, d, attributes);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSProperty Property(uint key, IPropertyValue d, JSPropertyAttributes attributes = JSPropertyAttributes.EnumerableConfigurableValue) => new(key, d, attributes);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSProperty Property(in KeyString key, IPropertyValue d, JSPropertyAttributes attributes = JSPropertyAttributes.EnumerableConfigurableValue) => new(key, d, attributes);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSProperty Property(in KeyString key, IPropertyAccessor d, JSPropertyAttributes attributes = JSPropertyAttributes.EnumerableConfigurableValue) => new(key, d, attributes);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSProperty Property(in KeyString key, IPropertyAccessor get, IPropertyAccessor set = null, JSPropertyAttributes attributes = JSPropertyAttributes.EnumerableConfigurableProperty) => new(key, get, set, attributes);

    public JSProperty With(in KeyString key) => new(key, get, set, value, Attributes);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSProperty Property(IPropertyAccessor get, IPropertyAccessor set = null, JSPropertyAttributes attributes = JSPropertyAttributes.EnumerableConfigurableProperty) => new(KeyString.Empty, get, set, attributes);

}
