using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.ExpressionCompiler;
using System.Collections.Generic;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine.Extensions;
using Broiler.JavaScript.Engine.Core;

namespace Broiler.JavaScript.BuiltIns.Map;

[JSClassGenerator("Map")]
public partial class JSMap : JSObject
{
    private readonly LinkedList<(JSValue key, JSValue value)> store = new();
    private StringMap<LinkedListNode<(JSValue key, JSValue value)>> index = new();

    [JSExport]
    public int Size => store.Count;

    public JSMap(in Arguments a) : base(JSEngine.NewTargetPrototype)
    {
        if (a[0] is not JSArray array)
            return;

        var en = array.GetElementEnumerator();
        while (en.MoveNext(out var item))
            Set(item[0], item[1]);
    }

    [JSExport("groupBy")]
    internal static new JSValue GroupBy(in Arguments a)
    {
        var (items, callbackfn) = a.Get2();
        if (items.IsNullOrUndefined)
            throw JSEngine.NewTypeError(JSException.Cannot_convert_undefined_or_null_to_object);

        if (!callbackfn.IsFunction)
            throw JSEngine.NewTypeError("CallbackFn must be a function");

        var result = new JSMap(Arguments.Empty);
        var en = items.GetElementEnumerator();
        int index = 0;

        while (en.MoveNext(out var hasValue, out var item, out var _))
        {
            if (!hasValue)
                continue;

            var key = callbackfn.Call(JSUndefined.Value, item, new JSNumber(index));
            var existing = result.Get(key);

            if (existing.IsNullOrUndefined)
            {
                var arr = new JSArray();
                arr.Add(item);
                result.Set(key, arr);
            }
            else
            {
                (existing as JSArray)?.Add(item);
            }

            index++;
        }

        return result;
    }

    [JSExport("set")]
    public JSValue Set(JSValue key, JSValue value)
    {
        HashedString uk = key.ToUniqueID();

        if (index.TryGetValue(in uk, out var i))
        {
            i.Value = (key, value);
        }
        else
        {
            var node = store.AddLast((key, value));
            index.Put(in uk) = node;
        }

        return value;
    }

    [JSExport("clear")]
    public JSValue Set(in Arguments a)
    {
        index = new();
        store.Clear();

        return JSUndefined.Value;
    }

    [JSExport("delete")]
    public JSValue Delete(in Arguments a)
    {
        var f = a[0];
        HashedString uk = f.ToUniqueID();

        if (index.TryGetValue(in uk, out var i))
        {
            store.Remove(i);
            return JSBoolean.True;
        }

        return JSBoolean.False;
    }

    [JSExport("entries")]
    public IEnumerable<JSValue> GetEntries()
    {
        if (store == null)
            yield break;

        foreach (var (key, value) in store)
            yield return new JSArray(key, value);
    }

    [JSExport("forEach")]
    public JSValue ForEach(in Arguments a)
    {
        var fx = a.Get1();
        if (!fx.IsFunction)
            throw JSEngine.NewTypeError($"Function parameter expected");

        var @this = a.This ?? this;
        if (store == null)
            return JSUndefined.Value;
        
        foreach (var e in store)
            fx.Call(@this, e.key, e.value, this);
        
        return JSUndefined.Value;
    }

    [JSExport("has")]
    public JSValue Has(in Arguments a)
    {
        var f = a.Get1();
        HashedString uk = f.ToUniqueID();
        if (index.TryGetValue(in uk, out var i))
            return JSBoolean.True;

        return JSBoolean.False;
    }


    [JSExport("get")]
    public JSValue Get(JSValue key)
    {
        HashedString uk = key.ToUniqueID();
        if (index.TryGetValue(in uk, out var i))
            return i.Value.value;

        return JSUndefined.Value;
    }

    [JSExport("keys")]
    public IEnumerable<JSValue> Keys()
    {
        if (store == null)
            yield break;

        foreach (var (key, _) in store)
            yield return key;
    }


    [JSExport("values")]
    public IEnumerable<JSValue> Values()
    {
        if (store == null)
            yield break;

        foreach (var (_, value) in store)
            yield return value;
    }

    /// <summary>
    /// ES2026 §4.9.1 — Map.prototype.getOrInsert(key, defaultValue)
    /// Returns the value for key if present, otherwise inserts defaultValue
    /// and returns it.
    /// </summary>
    [JSExport("getOrInsert")]
    public JSValue GetOrInsert(in Arguments a)
    {
        var (key, defaultValue) = a.Get2();
        HashedString uk = key.ToUniqueID();

        if (index.TryGetValue(in uk, out var i))
            return i.Value.value;

        var node = store.AddLast((key, defaultValue));
        index.Put(in uk) = node;

        return defaultValue;
    }

    /// <summary>
    /// ES2026 §4.9.2 — Map.prototype.getOrInsertComputed(key, callback)
    /// Returns the value for key if present, otherwise calls callback(key),
    /// inserts the result, and returns it.
    /// </summary>
    [JSExport("getOrInsertComputed")]
    public JSValue GetOrInsertComputed(in Arguments a)
    {
        var (key, callbackfn) = a.Get2();
        if (!callbackfn.IsFunction)
            throw JSEngine.NewTypeError("getOrInsertComputed requires a callback function");
        
        HashedString uk = key.ToUniqueID();
        if (index.TryGetValue(in uk, out var i))
            return i.Value.value;

        var value = callbackfn.Call(JSUndefined.Value, key);
        var node = store.AddLast((key, value));
        index.Put(in uk) = node;

        return value;
    }
}
