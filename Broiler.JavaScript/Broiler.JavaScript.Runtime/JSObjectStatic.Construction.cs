using Broiler.JavaScript.Storage;
using System;

namespace Broiler.JavaScript.Runtime;

public partial class JSObject
{
    [JSExport("create")]
    internal static JSValue StaticCreate(in Arguments a)
    {
        var p = a.Get1();
        if (p is not JSObject proto)
        {
            if (!p.IsNull)
                throw NewTypeError("Object prototype may only be an Object or null");

            proto = GetCurrentObjectPrototype();
        }

        return new JSObject(proto);
    }

    [JSExport("assign")]
    internal static JSValue Assign(in Arguments a)
    {
        var first = a.Get1();
        if (first.IsNullOrUndefined)
            throw NewTypeError(Cannot_convert_undefined_or_null_to_object);

        if (first is not JSObject firstObject)
            return first;

        for (var i = 1; i < a.Length; i++)
        {
            var ai = a.GetAt(i);
            firstObject.FastAddRange(ai);
        }

        return first;
    }

    [JSExport("defineProperties")]
    internal static JSValue DefineProperties(in Arguments a)
    {
        var (a0, a1) = a.Get2();
        if (a0 is not JSObject target)
            throw NewTypeError("Object.defineProperty called on non-object");

        var pds = a1;
        if (pds.IsNullOrUndefined)
            throw NewTypeError(Cannot_convert_undefined_or_null_to_object);

        if (pds is not JSObject pdObject)
            return target;

        if (!target.IsExtensible())
            throw NewTypeError("Object is not extensible");

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
            throw NewTypeError("Object.defineProperty called on non-object");

        if (!targetObject.IsExtensible())
            throw NewTypeError("Object is not extensible");

        if (desc is not JSObject pd)
            throw NewTypeError("Property Description must be an object");

        return targetObject.DefineProperty(key, pd);
    }

    [JSExport("entries")]
    internal static JSValue GetEntries(in Arguments a)
    {
        if (a[0] is not JSObject obj)
            throw NewTypeError(NotIterable("undefined"));

        var r = JSValue.CreateArray();

        var es = obj.GetElementEnumerator();
        while (es.MoveNext(out var hasValue, out var value, out var index))
        {
            if (hasValue)
            {
                var entry = JSValue.CreateArray();
                entry.AddArrayItem(JSValue.CreateNumber(index));
                entry.AddArrayItem(value);
                r.AddArrayItem(entry);
            }
        }

        var vp = new PropertyValueEnumerator(obj, false);
        while (vp.MoveNext(out var value, out var key))
        {
            var entry = JSValue.CreateArray();
            entry.AddArrayItem(JSObjectCoreExtensions.KeyStringToJSValue(key));
            entry.AddArrayItem(value);
            r.AddArrayItem(entry);
        }

        return r;
    }

    [JSExport("freeze")]
    internal static JSValue Freeze(in Arguments a) => throw new NotImplementedException();

    [JSExport("fromEntries")]
    internal static JSValue FromEntries(in Arguments a)
    {
        var v = a.Get1();
        if (v.IsNullOrUndefined)
            throw NewTypeError(NotIterable("undefined"));

        var r = new JSObject();
        if (v.IsArray && v is JSObject va)
        {
            ref var vaElements = ref va.GetElements();
            for (uint i = 0; i < (uint)v.Length; i++)
            {
                var vi = vaElements[i];
                var iaValue = vi.value as JSValue;
                if (iaValue == null || !iaValue.IsArray)
                    throw NewTypeError(NotEntry(vi));

                var first = iaValue[0];
                var second = iaValue[1];
                r.FastAddValue(first, second, JSPropertyAttributes.EnumerableConfigurableValue);
            }
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

    [JSExport("groupBy")]
    internal static JSValue GroupBy(in Arguments a)
    {
        var (items, callbackfn) = a.Get2();

        if (items.IsNullOrUndefined)
            throw NewTypeError(Cannot_convert_undefined_or_null_to_object);

        if (!callbackfn.IsFunction)
            throw NewTypeError("CallbackFn must be a function");

        var result = new JSObject();
        var en = items.GetElementEnumerator();
        int index = 0;

        while (en.MoveNext(out var hasValue, out var item, out var _))
        {
            if (!hasValue)
                continue;

            var key = JSObjectCoreExtensions.CallWith(callbackfn, JSValue.UndefinedValue, item, JSValue.CreateNumber(index));
            var keyStr = key.ToString();
            var group = result[keyStr];

            if (group.IsNullOrUndefined)
            {
                group = JSValue.CreateArray();
                result.FastAddValue(keyStr, group, JSPropertyAttributes.EnumerableConfigurableValue);
            }

            group.AddArrayItem(item);
            index++;
        }

        return result;
    }
}
