using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using YantraJS.Core.Clr;
using YantraJS.Core.Enumerators;
using YantraJS.Core.Storage;

namespace YantraJS.Core;

public static class MarshalExtensions
{

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue Marshal(this object value) => ClrProxy.Marshal(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue Marshal(this Type type) => ClrType.From(type);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue Marshal(this string value) => new JSString(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue Marshal(this bool value) => value ? JSBoolean.True : JSBoolean.False;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue Marshal(this int value) => new JSNumber(value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue Marshal(this uint value) => new JSNumber(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue Marshal(this double value) => new JSNumber(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue Marshal(this float value) => new JSNumber(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue Marshal(this short value) => new JSNumber(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue Marshal(this byte value) => new JSNumber(value);



    internal static bool TryUnmarshal(this JSObject @object, Type type, out object result) => cache[type](@object, out result);

    delegate bool UnmarshalDelegate(JSObject @object, out object result);

    static ConcurrentTypeTrie<UnmarshalDelegate> cache = new(UnmarshalDelegateFactory);

    static UnmarshalDelegate UnmarshalDelegateFactory (Type type)
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
                if (gt == typeof(List<>)) {
                    // add all items...
                    var et = type.GetGenericArguments()[0];
                    // get enumerator...
                    bool UnmarshalList(JSObject @object, out object result) {
                        var list = (result = c.Invoke([])) as System.Collections.IList;
                        var en = @object.GetElementEnumerator();
                        while(en.MoveNext(out var item))
                        {
                            list.Add(item.ForceConvert(et));
                        }
                        return true;
                    }

                    return UnmarshalList;
                }

                // check if it is a Dictionary<T>...

                if (gt == typeof(Dictionary<,>)) {
                    var keys = gt.GetGenericArguments();
                    var keyType = keys[0];
                    var valueType = keys[1];

                    bool UnmarshalList(JSObject @object, out object result)
                    {
                        var list = (result = c.Invoke([])) as System.Collections.IDictionary;
                        var en = new PropertyEnumerator(@object, true, true);
                        while (en.MoveNext(out var key, out var value))
                        {
                            list.Add(
                                Convert.ChangeType(key.ToString(), keyType),
                                value.ForceConvert(valueType));
                        }
                        return true;
                    }

                    return UnmarshalList;
                }

            }
            


            // change this logic to support case insensitive property match
            var properties = type.GetProperties()
                .Where(x => x.CanWrite)
                .ToDictionary(x => x.Name.ToLower(), x => x);
            bool Unmarshal(JSObject @object, out object result)
            {
                result = c.Invoke([]);
                var en = new PropertyEnumerator(@object, true, true);
                while(en.MoveNext(out var key, out var value))
                {
                    if (properties.TryGetValue(key.ToString().ToLower(), out var p))
                    {
                        p.SetValue(result, value.ForceConvert(p.PropertyType));
                    }
                }
                return true;
            }
            return Unmarshal;
        }

        bool NotSupported(JSObject  @object, out object result)
        {
            result = null;
            return false;
        }
        return NotSupported;
    }
}
