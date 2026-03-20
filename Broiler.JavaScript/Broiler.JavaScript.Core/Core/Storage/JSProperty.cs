using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Broiler.JavaScript.Core.Core.Function;

namespace Broiler.JavaScript.Core.Core.Storage;

[StructLayout(LayoutKind.Sequential)]
[DebuggerDisplay("{key}={get},{set},{value}")]
public readonly struct JSProperty
{
    public static JSProperty Empty = new();
    public readonly JSPropertyAttributes Attributes;
    public readonly uint key;

    // this slot will be used for getting method as well...
    // to avoid casting at runtime...
    public readonly JSFunction get;
    public readonly JSFunction set;
    public readonly JSValue value;

    public JSProperty ToNotReadOnly() => new(key, get, set, value, Attributes & (~JSPropertyAttributes.Readonly));

    public JSProperty(in KeyString key, JSFunction get, JSFunction set, JSPropertyAttributes attributes)
    {
        this.key = key.Key;
        this.get = get;
        this.set = set;
        value = get;
        Attributes = attributes;
    }
    public JSProperty(uint key, JSFunction get, JSFunction set, JSValue value, JSPropertyAttributes attributes)
    {
        this.key = key;
        this.get = get ?? value as JSFunction;
        this.set = set;
        this.value = value;
        Attributes = attributes;
    }

    public JSProperty(in KeyString key, JSFunction get, JSFunction set, JSValue value, JSPropertyAttributes attributes)
    {
        this.key = key.Key;
        this.get = get;
        this.set = set;
        this.value = value;
        Attributes = attributes;
    }

    public JSProperty(uint key, JSValue get, JSPropertyAttributes attributes)
    {
        this.key = key;
        this.get = get as JSFunction;
        set = null;
        value = get;
        Attributes = attributes;
    }

    public JSProperty(in KeyString key, JSValue get, JSPropertyAttributes attributes)
    {
        this.key = key.Key;
        this.get = get as JSFunction;
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
    public static JSProperty Property(JSValue d, JSPropertyAttributes attributes = JSPropertyAttributes.EnumerableConfigurableValue) => new(KeyString.Empty, d, attributes);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSProperty Function(in KeyString key, JSFunctionDelegate d, JSPropertyAttributes attributes = JSPropertyAttributes.ConfigurableValue, int length = 0)
    {
        var fx = new JSFunction(d, key.ToString(), null, length);
        return new JSProperty(key, fx, null, attributes);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSProperty Property(uint key, JSValue d, JSPropertyAttributes attributes = JSPropertyAttributes.EnumerableConfigurableValue) => new(key, d, attributes);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSProperty Property(in KeyString key, JSValue d, JSPropertyAttributes attributes = JSPropertyAttributes.EnumerableConfigurableValue) => new(key, d, attributes);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSProperty Property(in KeyString key, JSFunction d, JSPropertyAttributes attributes = JSPropertyAttributes.EnumerableConfigurableValue) => new(key, d, attributes);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSProperty Property(in KeyString key, JSFunctionDelegate get, JSFunctionDelegate set = null, JSPropertyAttributes attributes = JSPropertyAttributes.EnumerableConfigurableProperty)
    {
        var fget = get == null ? null : new JSFunction(get, "get " + key.ToString());
        var fset = set == null ? null : new JSFunction(set, "set " + key.ToString());

        return new JSProperty(key, fget, fset, attributes);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSProperty Property(in KeyString key, JSFunction get, JSFunction set = null, JSPropertyAttributes attributes = JSPropertyAttributes.EnumerableConfigurableProperty) => new(key, get, set, attributes);
    public JSProperty With(in KeyString key) => new(key, get, set, value, Attributes);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSProperty Property(JSFunction get, JSFunction set = null, JSPropertyAttributes attributes = JSPropertyAttributes.EnumerableConfigurableProperty) => new(KeyString.Empty, get, set, attributes);

}
