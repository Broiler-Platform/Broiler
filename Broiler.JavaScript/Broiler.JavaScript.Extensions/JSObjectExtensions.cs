using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Storage;
using System;
using System.ComponentModel;
using Broiler.JavaScript.Core.Core.Function;
using Broiler.JavaScript.Storage;

namespace Broiler.JavaScript.Core.Extensions;


/// <summary>
/// Static helper methods for fast property addition on JSObject instances.
/// Used by JSObjectBuilder via reflection for expression tree construction.
/// </summary>
public static class JSObjectFastPropertyExtensions
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void FastAddSetter(JSObject target, KeyString key, JSFunction setter, JSPropertyAttributes attributes = JSPropertyAttributes.ConfigurableProperty)
    {
        ref var pr = ref target.GetOwnProperties();
        ref var existing = ref pr.Put(key.Key);

        var getter = existing.get;
        existing = new JSProperty(key, getter, setter, attributes);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void FastAddGetter(JSObject target, KeyString key, JSFunction getter, JSPropertyAttributes attributes = JSPropertyAttributes.ConfigurableProperty)
    {
        ref var pr = ref target.GetOwnProperties();
        ref var existing = ref pr.Put(key.Key);

        var setter = existing.set;
        existing = new JSProperty(key, getter, setter, attributes);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void FastAddSetter(JSObject target, IJSSymbol key, JSFunction setter, JSPropertyAttributes attributes = JSPropertyAttributes.ConfigurableProperty)
    {
        ref var pr = ref target.GetSymbols();
        ref var existing = ref pr.Put(key.Key);

        var getter = existing.get;
        existing = new JSProperty(key.Key, getter, setter, existing.value, attributes);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void FastAddGetter(JSObject target, IJSSymbol key, JSFunction getter, JSPropertyAttributes attributes = JSPropertyAttributes.ConfigurableProperty)
    {
        ref var pr = ref target.GetSymbols();
        ref var existing = ref pr.Put(key.Key);
        var setter = existing.set;
        existing = new JSProperty(key.Key, getter, setter, existing.value, attributes);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void FastAddSetter(JSObject target, uint key, JSFunction setter, JSPropertyAttributes attributes = JSPropertyAttributes.ConfigurableProperty)
    {
        ref var pr = ref target.GetElements(true);
        ref var existing = ref pr.Put(key);

        target.UpdateArrayLengthIfNeeded(key);
        
        var getter = existing.get;
        existing = new JSProperty(key, getter, setter, existing.value, attributes);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void FastAddGetter(JSObject target, uint key, JSFunction getter, JSPropertyAttributes attributes = JSPropertyAttributes.ConfigurableProperty)
    {
        ref var pr = ref target.GetElements(true);
        ref var existing = ref pr.Put(key);

        target.UpdateArrayLengthIfNeeded(key);
        
        var setter = existing.set;
        existing = new JSProperty(key, getter, setter, existing.value, attributes);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void FastAddSetter(JSObject target, JSValue key, JSFunction setter, JSPropertyAttributes attributes = JSPropertyAttributes.ConfigurableProperty)
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
    public static void FastAddGetter(JSObject target, JSValue key, JSFunction getter, JSPropertyAttributes attributes = JSPropertyAttributes.ConfigurableProperty)
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
