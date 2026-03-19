using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Array;
using Broiler.JavaScript.Core.Core.Boolean;
using Broiler.JavaScript.Core.Core.Clr;
using Broiler.JavaScript.Core.Core.Error;
using Broiler.JavaScript.Core.Core.Object;
using Broiler.JavaScript.Core.Core.Primitive;
using Broiler.JavaScript.Core.Core.Storage;
using Broiler.JavaScript.Core.Extensions;
using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Broiler.JavaScript.Core;

public static class JSObjectStatic
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static JSObject ToJSObject(this JSValue value)
    {
        if (value is JSObject @object)
            return @object;

        if (value.IsNullOrUndefined)
            throw JSContext.NewTypeError(JSError.Cannot_convert_undefined_or_null_to_object);

        throw JSContext.NewTypeError(JSError.Parameter_is_not_an_object);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryAsObjectThrowIfNullOrUndefined(this JSValue value, out JSObject @object)
    {
        if (value.IsNullOrUndefined)
            throw JSContext.NewTypeError(JSError.Cannot_convert_undefined_or_null_to_object);

        @object = value as JSObject;
        return @object != null;
    }
}

public partial class JSObject
{
    [JSExport("create")]
    internal static JSValue StaticCreate(in Arguments a)
    {
        var p = a.Get1();
        if (p is not JSObject proto)
        {
            if (!p.IsNull)
                throw JSContext.NewTypeError("Object prototype may only be an Object or null");

            proto = JSContext.Current.ObjectPrototype;
        }

        return new JSObject(proto);
    }

    [JSExport("assign")]
    internal static JSValue Assign(in Arguments a)
    {
        var first = a.Get1();
        if (first.IsNullOrUndefined)
            throw JSContext.NewTypeError(JSError.Cannot_convert_undefined_or_null_to_object);

        if (first is not JSObject firstObject)
            return first;

        for (var i = 1; i < a.Length; i++)
        {
            var ai = a.GetAt(i);
            firstObject.FastAddRange(ai);
        }

        return first;
    }

    [JSExport("entries")]
    internal static JSValue StaticEntries(in Arguments a)
    {
        var target = a.Get1();
        if (target.IsNullOrUndefined)
            throw JSContext.NewTypeError(JSError.Cannot_convert_undefined_or_null_to_object);

        if (!target.IsObject)
            return new JSArray();

        var r = new JSArray();
        ref var rElements = ref r.CreateElements();
        var ownEntries = target.GetElementEnumerator();

        while (ownEntries.MoveNext(out var hasValue, out var item, out var index))
        {
            if (!hasValue)
                continue;

            rElements.Put(r._length++, new JSArray(new JSString(index.ToString()), item));
        }

        var en = (target as JSObject).GetOwnProperties(false).GetEnumerator();
        while (en.MoveNext(out var key, out var property))
            rElements.Put(r._length++, new JSArray(key.ToJSValue(), target.GetValue(property)));

        return r;
    }

    [JSExport("freeze")]
    internal static JSValue Freeze(in Arguments a) => throw new NotImplementedException();

    [JSExport("defineProperties")]
    internal static JSValue DefineProperties(in Arguments a)
    {
        var (a0, a1) = a.Get2();
        if (a0 is not JSObject target)
            throw JSContext.NewTypeError("Object.defineProperty called on non-object");

        var pds = a1;
        if (pds.IsNullOrUndefined)
            throw JSContext.NewTypeError(JSError.Cannot_convert_undefined_or_null_to_object);

        if (pds is not JSObject pdObject)
            return target;

        if (!target.IsExtensible())
            throw JSContext.NewTypeError("Object is not extensible");

        var ownElements = pdObject.GetElementEnumerator();
        while (ownElements.MoveNext(out var hasValue, out var item, out var index))
        {
            if (!hasValue)
                continue;

            if (item is JSObject itemObject)
                target.DefineProperty(index, itemObject);
        }

        var properties = pdObject.GetOwnProperties(false).GetEnumerator();
        while (properties.MoveNext(out var keyString, out var property))
        {
            var item = target.GetValue(property);
            if (item is JSObject itemObject)
                target.DefineProperty(keyString, itemObject);
        }

        return target;
    }

    [JSExport("defineProperty")]
    internal static JSValue DefineProperty(in Arguments a)
    {
        var (target, key, desc) = a.Get3();

        if (target is not JSObject targetObject)
            throw JSContext.NewTypeError("Object.defineProperty called on non-object");

        if (!targetObject.IsExtensible())
            throw JSContext.NewTypeError("Object is not extensible");

        if (desc is not JSObject pd)
            throw JSContext.NewTypeError("Property Description must be an object");

        return targetObject.DefineProperty(key, pd);
    }

    [JSExport("entries")]
    internal static JSValue GetEntries(in Arguments a)
    {
        if (a[0] is not JSObject obj)
            throw JSContext.NewTypeError(JSTypeError.NotIterable("undefined"));

        var r = new JSArray();

        var es = obj.GetElementEnumerator();
        while (es.MoveNext(out var hasValue, out var value, out var index))
        {
            if (hasValue)
                r[r._length++] = new JSArray(new JSNumber(index), value);
        }

        var vp = new PropertySequence.ValueEnumerator(obj, false);
        while (vp.MoveNext(out var value, out var key))
            r[r._length++] = new JSArray(key.ToJSValue(), value);

        return r;
    }

    [JSExport("fromEntries")]
    internal static JSValue FromEntries(in Arguments a)
    {
        var v = a.Get1();
        if (v.IsNullOrUndefined)
            throw JSContext.NewTypeError(JSTypeError.NotIterable("undefined"));

        var r = new JSObject();
        if (v is JSArray va)
        {
            ref var vaElements = ref va.GetElements();
            for (uint i = 0; i < va._length; i++)
            {
                var vi = vaElements[i];
                if (vi.value is not JSArray ia)
                    throw JSContext.NewTypeError(JSTypeError.NotEntry(vi));

                var first = ia[0];
                var second = ia[1];
                r.FastAddValue(first, second, JSPropertyAttributes.EnumerableConfigurableValue);
            }
        }

        return r;
    }

    [JSExport("is")]
    internal static JSValue Is(in Arguments a)
    {
        var (first, second) = a.Get2();
        return first.Is(second);
    }

    [JSExport("isExtensible")]
    internal static JSValue IsExtensible(in Arguments a)
    {
        if (a.Get1() is JSObject @object && @object.IsExtensible())
            return JSBoolean.True;

        return JSBoolean.False;
    }

    [JSExport("isFrozen")]
    internal static JSValue IsFrozen(in Arguments a)
    {
        if ((a.Get1() is JSObject @object) && @object.IsFrozen())
            return JSBoolean.True;

        return JSBoolean.False;
    }

    [JSExport("isSealed")]
    internal static JSValue IsSealed(in Arguments a)
    {
        if ((a.Get1() is JSObject @object) && @object.IsSealed())
            return JSBoolean.True;

        return JSBoolean.False;
    }

    [JSExport("keys")]
    internal static JSValue Keys(in Arguments a)
    {
        var first = a.Get1();

        if (first.IsNullOrUndefined)
            throw JSContext.NewTypeError(JSError.Cannot_convert_undefined_or_null_to_object);

        if (first is not JSObject jobj)
            return new JSArray();

        var en = jobj.GetAllKeys(true, false);
        var r = new JSArray();
        ref var e = ref r.GetElements();

        while (en.MoveNext(out var hasValue, out var value, out var index))
        {
            if (hasValue)
                e.Put(r._length++, value);
        }

        return r;
    }

    [JSExport("preventExtensions")]
    internal static JSValue PreventExtensions(in Arguments a)
    {
        var first = a.Get1();
        if (first is not JSObject @object)
            return first;

        @object.status |= ObjectStatus.NonExtensible;
        return @object;
    }

    [JSExport("seal")]
    internal static JSValue Seal(in Arguments a)
    {
        var first = a.Get1();
        if (first is not JSObject @object)
            return first;

        @object.status |= ObjectStatus.Sealed;
        @object.GetOwnProperties().Update((uint x, ref JSProperty v) => v = new JSProperty(x, v.get, v.set, v.value, v.Attributes & (~JSPropertyAttributes.Configurable)));
        return first;
    }

    [JSExport("setPrototypeOf")]
    internal static JSValue SetPrototypeOf(in Arguments a)
    {
        var (first, second) = a.Get2();
        first.SetPrototypeOf(second);
        return first;
    }

    [JSExport("values")]
    internal static JSValue Values(in Arguments a)
    {
        var first = a.Get1();

        if (first.IsNullOrUndefined)
            throw JSContext.NewTypeError(JSError.Cannot_convert_undefined_or_null_to_object);

        if (first is not JSObject target)
            return new JSArray();

        var r = new JSArray();
        ref var rElements = ref r.CreateElements();
        var ownEntries = target.GetElementEnumerator();

        while (ownEntries.MoveNext(out var hasValue, out var item, out var index))
        {
            if (!hasValue)
                continue;

            rElements.Put(r._length++, item);
        }

        var en = target.GetOwnProperties(false).GetEnumerator();
        while (en.MoveNext(out var property))
            rElements.Put(r._length++, target.GetValue(property));

        return r;
    }

    [JSExport("getOwnPropertyDescriptor")]
    internal static JSValue GetOwnPropertyDescriptor(in Arguments a)
    {
        var (first, name) = a.Get2();

        if (first.IsNullOrUndefined)
            throw JSContext.NewTypeError(JSError.Cannot_convert_undefined_or_null_to_object);

        if (first is not JSObject jobj)
            return JSUndefined.Value;

        return jobj.GetOwnPropertyDescriptor(name);
    }

    [JSExport("getOwnPropertyDescriptors")]
    internal static JSValue GetOwnPropertyDescriptors(in Arguments a)
    {
        var first = a.Get1();

        if (first.IsNullOrUndefined)
            throw JSContext.NewTypeError(JSError.Cannot_convert_undefined_or_null_to_object);

        if (first is not JSObject jobj)
            return new JSArray();

        var r = new JSObject();
        ref var p = ref r.GetOwnProperties(true);
        var en = jobj.GetOwnProperties(false).GetEnumerator();

        while (en.MoveNext(out var key, out var property))
            p.Put(key.Key) = property;

        return r;
    }

    /// <summary>
    /// The Object.getOwnPropertyNames() method returns an array of all properties 
    /// (including non-enumerable properties except for those which use Symbol) 
    /// found directly in a given object.
    /// </summary>
    /// <param name="a"></param>
    /// <returns></returns>
    [JSExport("getOwnPropertyNames")]
    internal static JSValue GetOwnPropertyNames(in Arguments a)
    {
        var first = a.Get1();

        if (first.IsNullOrUndefined)
            throw JSContext.NewTypeError(JSError.Cannot_convert_undefined_or_null_to_object);

        if (first is not JSObject jobj)
            return new JSArray();

        var r = new JSArray(jobj.GetAllKeys(false, false));
        return r;
    }

    [JSExport("getOwnPropertySymbols")]
    internal static JSValue GetOwnPropertySymbols(in Arguments a)
    {
        var first = a.Get1();
        if (first.IsNullOrUndefined)
            throw JSContext.NewTypeError(JSError.Cannot_convert_undefined_or_null_to_object);

        if (first is not JSObject jobj)
            return new JSArray();

        ref var symbols = ref jobj.GetSymbols();
        var keys = symbols.AllValues().Select(x => KeyStrings.GetJSString(x.Value.key));

        return new JSArray(keys);
    }

    [JSExport("getPrototypeOf")]
    internal static JSValue GetPrototypeOf(in Arguments a) => a.Get1().GetPrototypeOf();

    [JSExport("groupBy")]
    internal static JSValue GroupBy(in Arguments a)
    {
        var (items, callbackfn) = a.Get2();

        if (items.IsNullOrUndefined)
            throw JSContext.NewTypeError(JSError.Cannot_convert_undefined_or_null_to_object);

        if (!callbackfn.IsFunction)
            throw JSContext.NewTypeError("CallbackFn must be a function");

        var result = new JSObject();
        var en = items.GetElementEnumerator();
        int index = 0;

        while (en.MoveNext(out var hasValue, out var item, out var _))
        {
            if (!hasValue)
                continue;

            var key = callbackfn.Call(JSUndefined.Value, item, new JSNumber(index));
            var keyStr = key.ToString();
            var group = result[keyStr];

            if (group.IsNullOrUndefined)
            {
                group = new JSArray();
                result.FastAddValue(keyStr, group, JSPropertyAttributes.EnumerableConfigurableValue);
            }

            (group as JSArray)?.Add(item);
            index++;
        }

        return result;
    }
}
