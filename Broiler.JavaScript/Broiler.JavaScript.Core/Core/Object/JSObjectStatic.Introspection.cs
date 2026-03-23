using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Array;
using Broiler.JavaScript.Core.Core.Boolean;
using Broiler.JavaScript.Core.Core.Clr;
using Broiler.JavaScript.Core.Core.Error;
using Broiler.JavaScript.Core.Core.Primitive;
using Broiler.JavaScript.Core.Extensions;
using System.Linq;

namespace Broiler.JavaScript.Core;

public partial class JSObject
{
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
        var keys = symbols.AllValues().Select(x => KeyStringCoreExtensions.GetJSString(x.Value.key));

        return new JSArray(keys);
    }

    [JSExport("getPrototypeOf")]
    internal static JSValue GetPrototypeOf(in Arguments a) => a.Get1().GetPrototypeOf();
}
