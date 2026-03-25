using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Storage;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Broiler.JavaScript.Engine.Internal;

/// <summary>
/// Minimal internal helpers for Core call sites that cannot reference the
/// Broiler.JavaScript.Extensions assembly.  The full
/// <c>InternalExtensionHelpers</c> class has been moved to Extensions.
/// </summary>
internal static class CoreInternalHelpers
{
    // ── JSPropertyExtensions (ToJSValue) ────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static JSValue ToJSValue(in this JSProperty px)
    {
        var t = JSValue.BooleanTrue;
        var f = JSValue.BooleanFalse;
        JSObject obj;

        if (px.IsValue)
        {
            obj = JSObject.NewWithProperties()
                .AddProperty(KeyStrings.configurable, px.IsConfigurable ? t : f)
                .AddProperty(KeyStrings.enumerable, px.IsEnumerable ? t : f)
                .AddProperty(KeyStrings.writable, !px.IsReadOnly ? t : f)
                .AddProperty(KeyStrings.value, (JSValue)px.value);
        }
        else
        {
            obj = JSObject.NewWithProperties()
                .AddProperty(KeyStrings.configurable, px.IsConfigurable ? t : f)
                .AddProperty(KeyStrings.enumerable, px.IsEnumerable ? t : f)
                .AddProperty(KeyStrings.@get, (JSValue)px.get)
                .AddProperty(KeyStrings.@set, (JSValue)px.set);
        }

        return obj;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    private static JSObject AddProperty(this JSObject target, in KeyString key, JSValue value, JSPropertyAttributes attributes = JSPropertyAttributes.EnumerableConfigurableValue)
    {
        target.GetOwnProperties().Put(in key, value, attributes);
        return target;
    }

    // ── ClrProxyExtensions (TryGetClrEnumerator) ────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static object CreateClrEnumerator(JSValue target, Type elementType)
    {
        Type type = typeof(ClrObjectEnumerator<>).MakeGenericType(elementType);
        return Activator.CreateInstance(type, target);
    }

    internal static bool TryGetClrEnumerator(this JSValue value, Type type, out object clrObject)
    {
        if (type.IsConstructedGenericType)
        {
            var gt = type.GetGenericTypeDefinition();
            if (gt == typeof(IEnumerator<>))
            {
                clrObject = CreateClrEnumerator(value, type.GetGenericArguments()[0]);
                return true;
            }
        }

        if (type == typeof(System.Collections.IEnumerable))
        {
            clrObject = CreateClrEnumerator(value, typeof(object));
            return true;
        }

        clrObject = null;
        return false;
    }

    // ── MarshalExtensions (TryUnmarshal) ────────────────────────────

    internal static bool TryUnmarshal(this JSObject @object, Type type, out object result) => cache[type](@object, out result);

    delegate bool UnmarshalDelegate(JSObject @object, out object result);

    static readonly ConcurrentTypeTrie<UnmarshalDelegate> cache = new(UnmarshalDelegateFactory);

    static UnmarshalDelegate UnmarshalDelegateFactory(Type type)
    {
        var c = type.GetConstructor([typeof(JSValue)]);
        if (c != null)
        {
            bool Unmarshal(JSObject @object, out object result)
            {
                result = c.Invoke([@object]);
                return true;
            }

            return Unmarshal;
        }

        c = type.GetConstructor([]);
        if (c != null)
        {
            if (type.IsConstructedGenericType)
            {
                var gt = type.GetGenericTypeDefinition();
                // check if it is List<T> ....
                if (gt == typeof(List<>))
                {
                    // add all items...
                    var et = type.GetGenericArguments()[0];
                    // get enumerator...
                    bool UnmarshalList(JSObject @object, out object result)
                    {
                        var list = (result = c.Invoke([])) as System.Collections.IList;
                        var en = @object.GetElementEnumerator();
                        while (en.MoveNext(out var item))
                            list.Add(item.ForceConvert(et));

                        return true;
                    }

                    return UnmarshalList;
                }

                // check if it is a Dictionary<T>...

                if (gt == typeof(Dictionary<,>))
                {
                    var keys = gt.GetGenericArguments();
                    var keyType = keys[0];
                    var valueType = keys[1];

                    bool UnmarshalList(JSObject @object, out object result)
                    {
                        var list = (result = c.Invoke([])) as System.Collections.IDictionary;
                        var en = new PropertyEnumerator(@object, true, true);

                        while (en.MoveNext(out var key, out var value))
                            list.Add(Convert.ChangeType(key.ToString(), keyType), value.ForceConvert(valueType));

                        return true;
                    }

                    return UnmarshalList;
                }

            }

            // change this logic to support case insensitive property match
            var properties = type.GetProperties().Where(x => x.CanWrite).ToDictionary(x => x.Name.ToLower(), x => x);
            bool Unmarshal(JSObject @object, out object result)
            {
                result = c.Invoke([]);
                var en = new PropertyEnumerator(@object, true, true);

                while (en.MoveNext(out var key, out var value))
                {
                    if (properties.TryGetValue(key.ToString().ToLower(), out var p))
                        p.SetValue(result, value.ForceConvert(p.PropertyType));
                }

                return true;
            }

            return Unmarshal;
        }

        bool NotSupported(JSObject @object, out object result)
        {
            result = null;
            return false;
        }

        return NotSupported;
    }
}
