
namespace Broiler.JavaScript.Runtime;

public partial class JSObject
{
    [JSExport("entries")]
    internal static JSValue StaticEntries(in Arguments a)
    {
        var target = a.Get1();
        if (target.IsNullOrUndefined)
            throw NewTypeError(Cannot_convert_undefined_or_null_to_object);

        if (!target.IsObject)
            return JSValue.CreateArray();

        var r = JSValue.CreateArray();
        var ownEntries = target.GetElementEnumerator();

        while (ownEntries.MoveNext(out var hasValue, out var item, out var index))
        {
            if (!hasValue)
                continue;

            var entry = JSValue.CreateArray();
            entry.AddArrayItem(JSValue.CreateString(index.ToString()));
            entry.AddArrayItem(item);
            r.AddArrayItem(entry);
        }

        var en = (target as JSObject).GetOwnProperties(false).GetEnumerator();
        while (en.MoveNext(out var key, out var property))
        {
            var entry = JSValue.CreateArray();
            entry.AddArrayItem(JSObjectCoreExtensions.KeyStringToJSValue(key));
            entry.AddArrayItem(target.GetValue(property));
            r.AddArrayItem(entry);
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
            return JSValue.BooleanTrue;

        return JSValue.BooleanFalse;
    }

    [JSExport("isFrozen")]
    internal static JSValue IsFrozen(in Arguments a)
    {
        if ((a.Get1() is JSObject @object) && @object.IsFrozen())
            return JSValue.BooleanTrue;

        return JSValue.BooleanFalse;
    }

    [JSExport("isSealed")]
    internal static JSValue IsSealed(in Arguments a)
    {
        if ((a.Get1() is JSObject @object) && @object.IsSealed())
            return JSValue.BooleanTrue;

        return JSValue.BooleanFalse;
    }

    [JSExport("keys")]
    internal static JSValue Keys(in Arguments a)
    {
        var first = a.Get1();

        if (first.IsNullOrUndefined)
            throw NewTypeError(Cannot_convert_undefined_or_null_to_object);

        if (first is not JSObject jobj)
            return JSValue.CreateArray();

        var en = jobj.GetAllKeys(true, false);
        var r = JSValue.CreateArray();

        while (en.MoveNext(out var hasValue, out var value, out var index))
        {
            if (hasValue)
                r.AddArrayItem(value);
        }

        return r;
    }

    [JSExport("values")]
    internal static JSValue Values(in Arguments a)
    {
        var first = a.Get1();

        if (first.IsNullOrUndefined)
            throw NewTypeError(Cannot_convert_undefined_or_null_to_object);

        if (first is not JSObject target)
            return JSValue.CreateArray();

        var r = JSValue.CreateArray();
        var ownEntries = target.GetElementEnumerator();

        while (ownEntries.MoveNext(out var hasValue, out var item, out var index))
        {
            if (!hasValue)
                continue;

            r.AddArrayItem(item);
        }

        var en = target.GetOwnProperties(false).GetEnumerator();
        while (en.MoveNext(out var property))
            r.AddArrayItem(target.GetValue(property));

        return r;
    }

    [JSExport("getOwnPropertyDescriptor")]
    internal static JSValue GetOwnPropertyDescriptor(in Arguments a)
    {
        var (first, name) = a.Get2();

        if (first.IsNullOrUndefined)
            throw NewTypeError(Cannot_convert_undefined_or_null_to_object);

        if (first is not JSObject jobj)
            return JSValue.UndefinedValue;

        return jobj.GetOwnPropertyDescriptor(name);
    }

    [JSExport("getOwnPropertyDescriptors")]
    internal static JSValue GetOwnPropertyDescriptors(in Arguments a)
    {
        var first = a.Get1();

        if (first.IsNullOrUndefined)
            throw NewTypeError(Cannot_convert_undefined_or_null_to_object);

        if (first is not JSObject jobj)
            return JSValue.CreateArray();

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
            throw NewTypeError(Cannot_convert_undefined_or_null_to_object);

        if (first is not JSObject jobj)
            return JSValue.CreateArray();

        var en = jobj.GetAllKeys(false, false);
        var r = JSValue.CreateArray();
        while (en.MoveNext(out var hasValue, out var value, out var index))
        {
            if (hasValue)
                r.AddArrayItem(value);
        }
        return r;
    }

    [JSExport("getOwnPropertySymbols")]
    internal static JSValue GetOwnPropertySymbols(in Arguments a)
    {
        var first = a.Get1();
        if (first.IsNullOrUndefined)
            throw NewTypeError(Cannot_convert_undefined_or_null_to_object);

        if (first is not JSObject jobj)
            return JSValue.CreateArray();

        ref var symbols = ref jobj.GetSymbols();
        var r = JSValue.CreateArray();
        foreach (var x in symbols.AllValues())
            r.AddArrayItem(JSObjectCoreExtensions.GetJSString(x.Value.key));

        return r;
    }

    [JSExport("getPrototypeOf")]
    internal static JSValue GetPrototypeOf(in Arguments a) => a.Get1().GetPrototypeOf();
}
