using System;
using System.ComponentModel;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.Extensions;


/// <summary>
/// Static helper methods for fast property addition on JSObject instances.
/// Used by JSObjectBuilder via reflection for expression tree construction.
/// </summary>
public static class JSObjectFastPropertyExtensions
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void FastAddSetter(JSObject target, KeyString key, JSValue setter, JSPropertyAttributes attributes = JSPropertyAttributes.ConfigurableProperty)
    {
        ref var pr = ref target.GetOwnProperties();
        ref var existing = ref pr.Put(key.Key);

        var getter = existing.get;
        existing = new JSProperty(key, getter, setter, attributes);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void FastAddGetter(JSObject target, KeyString key, JSValue getter, JSPropertyAttributes attributes = JSPropertyAttributes.ConfigurableProperty)
    {
        ref var pr = ref target.GetOwnProperties();
        ref var existing = ref pr.Put(key.Key);

        var setter = existing.set;
        existing = new JSProperty(key, getter, setter, attributes);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void FastAddSetter(JSObject target, IJSSymbol key, JSValue setter, JSPropertyAttributes attributes = JSPropertyAttributes.ConfigurableProperty)
    {
        ref var pr = ref target.GetSymbols();
        ref var existing = ref pr.Put(key.Key);

        var getter = existing.get;
        existing = new JSProperty(key.Key, getter, setter, existing.value, attributes);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void FastAddGetter(JSObject target, IJSSymbol key, JSValue getter, JSPropertyAttributes attributes = JSPropertyAttributes.ConfigurableProperty)
    {
        ref var pr = ref target.GetSymbols();
        ref var existing = ref pr.Put(key.Key);
        var setter = existing.set;
        existing = new JSProperty(key.Key, getter, setter, existing.value, attributes);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void FastAddSetter(JSObject target, uint key, JSValue setter, JSPropertyAttributes attributes = JSPropertyAttributes.ConfigurableProperty)
    {
        ref var pr = ref target.GetElements(true);
        ref var existing = ref pr.Put(key);

        target.UpdateArrayLengthIfNeeded(key);
        
        var getter = existing.get;
        existing = new JSProperty(key, getter, setter, existing.value, attributes);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void FastAddGetter(JSObject target, uint key, JSValue getter, JSPropertyAttributes attributes = JSPropertyAttributes.ConfigurableProperty)
    {
        ref var pr = ref target.GetElements(true);
        ref var existing = ref pr.Put(key);

        target.UpdateArrayLengthIfNeeded(key);
        
        var setter = existing.set;
        existing = new JSProperty(key, getter, setter, existing.value, attributes);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void FastAddSetter(JSObject target, JSValue key, JSValue setter, JSPropertyAttributes attributes = JSPropertyAttributes.ConfigurableProperty)
    {
        var k = key.ToKey();
        switch (k.Type)
        {
            case KeyType.String:
                FastAddSetter(target, k.KeyString, setter, attributes);
                return;
            case KeyType.UInt:
                FastAddSetter(target, k.Index, setter, attributes);
                return;
            case KeyType.Symbol:
                FastAddSetter(target, k.Symbol, setter, attributes);
                return;
            default:
                throw new NotSupportedException();
        }
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void FastAddGetter(JSObject target, JSValue key, JSValue getter, JSPropertyAttributes attributes = JSPropertyAttributes.ConfigurableProperty)
    {
        var k = key.ToKey();
        switch (k.Type)
        {
            case KeyType.String:
                FastAddGetter(target, k.KeyString, getter, attributes);
                return;
            case KeyType.UInt:
                FastAddGetter(target, k.Index, getter, attributes);
                return;
            case KeyType.Symbol:
                FastAddGetter(target, k.Symbol, getter, attributes);
                return;
            default:
                throw new NotSupportedException();
        }
    }
}
