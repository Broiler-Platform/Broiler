using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.ExpressionCompiler;
using System;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine.Extensions;
using Broiler.JavaScript.Engine.Core;

namespace Broiler.JavaScript.BuiltIns.Map;

internal delegate void UnregisterWeakValue(in HashedString key);

internal class WeakValue(HashedString key, JSValue value, UnregisterWeakValue unregister)
{
    public readonly JSValue value = value;

    ~WeakValue()
    {
        unregister(in key);
    }
}

[JSClassGenerator("WeakMap")]
public partial class JSWeakMap: JSObject
{
    private StringMap<WeakReference<WeakValue>> index;

    public JSWeakMap(in Arguments a) : base(JSEngine.NewTargetPrototype)
    {
        if (a[0] is not JSArray array)
            return;

        var en = array.GetElementEnumerator();
        while (en.MoveNext(out var value))
            Set((JSObject)value[0], value[1]);
    }

    [JSExport("set")]
    public JSValue Set(JSObject key, JSValue value)
    {
        HashedString uk = key.ToUniqueID();

        lock (this)
        {
            if (!index.TryGetValue(uk, out var w))
                index.Put(uk) = new(new(uk, value, Unregister));
        }

        return value;
    }

    private void Unregister(in HashedString key) => index.RemoveAt(key.Value);

    [JSExport("delete")]
    public JSValue Delete(in Arguments a)
    {
        var key = a.Get1().ToUniqueID();
        lock (this)
        {
            if (index.TryRemove(key, out var w))
            {
                if (w.TryGetTarget(out var target))
                    GC.SuppressFinalize(target);

                return JSBoolean.True;
            }
        }

        return JSBoolean.False;
    }

    [JSExport("has")]
    public JSValue Has(in Arguments a)
    {
        var key = a.Get1().ToUniqueID();
        lock (this)
        {
            if (index.TryGetValue(key, out var v))
            {
                if (v.TryGetTarget(out var target))
                    return JSBoolean.True;
            }
        }

        return JSUndefined.Value;
    }


    [JSExport("get")]
    public JSValue Get(JSObject key)
    {
        var uk = key.ToUniqueID();
        lock (this)
        {
            if (index.TryGetValue(uk, out var v))
            {
                if (v.TryGetTarget(out var target))
                    return target.value;
            }
        }

        return JSUndefined.Value;
    }

    /// <summary>
    /// ES2026 §4.9.3 — WeakMap.prototype.getOrInsert(key, defaultValue)
    /// Returns the value for key if present, otherwise inserts defaultValue
    /// and returns it.
    /// </summary>
    [JSExport("getOrInsert")]
    public JSValue GetOrInsert(in Arguments a)
    {
        var (keyVal, defaultValue) = a.Get2();
        if (keyVal is not JSObject key)
            throw JSEngine.NewTypeError("WeakMap key must be an object");

        var uk = key.ToUniqueID();
        lock (this)
        {
            if (index.TryGetValue(uk, out var v))
            {
                if (v.TryGetTarget(out var target))
                    return target.value;
            }

            index.Put(uk) = new WeakReference<WeakValue>(new WeakValue(uk, defaultValue, Unregister));
        }

        return defaultValue;
    }

    /// <summary>
    /// ES2026 §4.9.3 — WeakMap.prototype.getOrInsertComputed(key, callback)
    /// Returns the value for key if present, otherwise calls callback(key),
    /// inserts the result, and returns it.
    /// </summary>
    [JSExport("getOrInsertComputed")]
    public JSValue GetOrInsertComputed(in Arguments a)
    {
        var (keyVal, callbackfn) = a.Get2();
        if (keyVal is not JSObject key)
            throw JSEngine.NewTypeError("WeakMap key must be an object");

        if (!callbackfn.IsFunction)
            throw JSEngine.NewTypeError("getOrInsertComputed requires a callback function");

        var uk = key.ToUniqueID();
        lock (this)
        {
            if (index.TryGetValue(uk, out var v))
            {
                if (v.TryGetTarget(out var target))
                    return target.value;
            }

            var value = callbackfn.Call(JSUndefined.Value, key);
            index.Put(uk) = new WeakReference<WeakValue>(new WeakValue(uk, value, Unregister));
            return value;
        }
    }
}
