using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Error;
using Broiler.JavaScript.BuiltIns.Promise;
using System;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.Engine.Core;

namespace Broiler.JavaScript.BuiltIns.Array;


public partial class JSArray
{
    [JSExport("from", Length = 1)]
    public static JSValue StaticFrom(in Arguments a)
    {
        var r = new JSArray();
        var (f, map, mapThis) = a.Get3();
        var t = a.This;
        var en = f.GetElementEnumerator();
        uint length = 0;
        ref var elements = ref r.GetElements();

        if (map is JSFunction fx)
        {
            var cb = fx.f;
            while (en.MoveNext(out var hasValue, out var item, out var index))
                elements.Put(length++, cb(new Arguments(mapThis, item, new JSNumber(index))));
        }
        else
        {
            while (en.MoveNext(out var hasValue, out var item, out var index))
                elements.Put(length++, item);
        }

        r._length = length;
        return r;
    }

    [JSExport("isArray", Length = 1)]
    public static JSValue StaticIsArray(in Arguments a) => a.Get1() is JSArray ? JSBoolean.True : JSBoolean.False;

    [JSExport("of")]
    public static JSValue StaticOf(in Arguments a)
    {
        var r = new JSArray();
        var al = a.Length;
        ref var rElements = ref r.CreateElements();

        for (var ai = 0; ai < al; ai++)
            rElements.Put(r._length++, a.GetAt(ai));

        return r;
    }

    /// <summary>
    /// §4.5  Array.fromAsync(asyncIterable, mapFn?, thisArg?)
    /// Creates an array from an async iterable or iterable/array-like,
    /// returning a Promise that resolves to the new array.
    /// </summary>
    [JSExport("fromAsync")]
    public static JSValue StaticFromAsync(in Arguments a)
    {
        var (items, mapFn, thisArg) = a.Get3();

        if (items.IsNullOrUndefined)
            throw JSEngine.NewTypeError(JSException.Cannot_convert_undefined_or_null_to_object);

        bool hasMap = mapFn.IsFunction;

        if (!mapFn.IsNullOrUndefined && !hasMap)
            throw JSEngine.NewTypeError("mapFn must be a function");

        try
        {
            var result = new JSArray();
            var en = items.GetElementEnumerator();
            uint length = 0;
            ref var elements = ref result.GetElements();

            while (en.MoveNext(out var hasValue, out var item, out var _))
            {
                if (!hasValue) continue;

                // Await-like: if the element is a promise/thenable, resolve it
                // synchronously for the current implementation.
                if (item is JSPromise p && p.state == JSPromise.PromiseState.Resolved)
                    item = p.result ?? item;

                if (hasMap)
                    item = mapFn.InvokeFunction(new Arguments(thisArg, item, new JSNumber(length)));

                elements.Put(length++, item);
            }

            result._length = length;

            return new JSPromise(result, JSPromise.PromiseState.Resolved);
        }
        catch (JSException ex)
        {
            return new JSPromise(ex.Error ?? JSError.From(ex), JSPromise.PromiseState.Rejected);
        }
        catch (Exception ex)
        {
            return new JSPromise(JSError.From(ex), JSPromise.PromiseState.Rejected);
        }
    }
}
