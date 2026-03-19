#nullable enable
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Clr;
using Broiler.JavaScript.Core.Core.Storage;
using System.Collections.Generic;
using Yantra.Core;

namespace YantraJS.Core.Set;


[JSClassGenerator("Set")]
public partial class JSSet : JSObject
{
    private LinkedList<JSValue> store = new();
    private StringMap<LinkedListNode<JSValue>> index;

    [JSExport]
    public int Size => store?.Count ?? 0;

    public JSSet(in Arguments a) : base(JSContext.NewTargetPrototype)
    {
        if (a[0] is not JSArray array)
            return;

        var en = array.GetElementEnumerator();
        while (en.MoveNext(out var item))
            Add(item);
    }

    [JSExport("add")]
    public JSValue Add(JSValue key)
    {
        HashedString uk = key.ToUniqueID();

        if (!index.TryGetValue(in uk, out var i))
        {
            var node = store.AddLast(key);
            index.Put(in uk) = node;
        }

        return key;
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

        foreach (var entry in store)
            yield return new JSArray(entry, entry);
    }

    [JSExport("forEach")]
    public JSValue ForEach(in Arguments a)
    {
        var fx = a.Get1();
        if (!fx.IsFunction)
            throw JSContext.NewTypeError($"Function parameter expected");

        var @this = a.This ?? this;
        if (store == null)
            return JSUndefined.Value;
        
        foreach (var e in store)
            fx.Call(@this, e, e, this);
        
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

    [JSExport("keys")]
    public IEnumerable<JSValue> Keys()
    {
        if (store == null)
            yield break;

        foreach (var entry in store)
            yield return entry;
    }


    [JSExport("values")]
    public IEnumerable<JSValue> Values()
    {
        if (store == null)
            yield break;

        foreach (var entry in store)
            yield return entry;
    }

    [JSExport("union")]
    public JSValue Union(in Arguments a)
    {
        var other = a.Get1();
        if (other is not JSSet otherSet)
            throw JSContext.NewTypeError("Set.prototype.union requires a Set argument");

        var result = new JSSet(Arguments.Empty);
        if (store != null)
        {
            foreach (var item in store)
                result.Add(item);
        }

        if (otherSet.store != null)
        {
            foreach (var item in otherSet.store)
                result.Add(item);
        }

        return result;
    }

    [JSExport("intersection")]
    public JSValue Intersection(in Arguments a)
    {
        var other = a.Get1();
        if (other is not JSSet otherSet)
            throw JSContext.NewTypeError("Set.prototype.intersection requires a Set argument");

        var result = new JSSet(Arguments.Empty);
        if (store != null)
        {
            foreach (var item in store)
            {
                HashedString uk = item.ToUniqueID();
                if (otherSet.index.TryGetValue(in uk, out _))
                    result.Add(item);
            }
        }

        return result;
    }

    [JSExport("difference")]
    public JSValue Difference(in Arguments a)
    {
        var other = a.Get1();
        if (other is not JSSet otherSet)
            throw JSContext.NewTypeError("Set.prototype.difference requires a Set argument");

        var result = new JSSet(Arguments.Empty);
        if (store == null)
            return result;

        foreach (var item in store)
        {
            HashedString uk = item.ToUniqueID();
            if (!otherSet.index.TryGetValue(in uk, out _))
                result.Add(item);
        }

        return result;
    }

    [JSExport("symmetricDifference")]
    public JSValue SymmetricDifference(in Arguments a)
    {
        var other = a.Get1();
        if (other is not JSSet otherSet)
            throw JSContext.NewTypeError("Set.prototype.symmetricDifference requires a Set argument");

        var result = new JSSet(Arguments.Empty);
        if (store != null)
        {
            foreach (var item in store)
            {
                HashedString uk = item.ToUniqueID();
                if (!otherSet.index.TryGetValue(in uk, out _))
                    result.Add(item);
            }
        }

        if (otherSet.store == null)
            return result;

        foreach (var item in otherSet.store)
        {
            HashedString uk = item.ToUniqueID();
            if (!index.TryGetValue(in uk, out _))
                result.Add(item);
        }

        return result;
    }

    [JSExport("isSubsetOf")]
    public JSValue IsSubsetOf(in Arguments a)
    {
        var other = a.Get1();
        if (other is not JSSet otherSet)
            throw JSContext.NewTypeError("Set.prototype.isSubsetOf requires a Set argument");

        if (store == null)
            return JSBoolean.True;

        foreach (var item in store)
        {
            HashedString uk = item.ToUniqueID();
            if (!otherSet.index.TryGetValue(in uk, out _))
                return JSBoolean.False;
        }

        return JSBoolean.True;
    }

    [JSExport("isSupersetOf")]
    public JSValue IsSupersetOf(in Arguments a)
    {
        var other = a.Get1();
        if (other is not JSSet otherSet)
            throw JSContext.NewTypeError("Set.prototype.isSupersetOf requires a Set argument");

        if (otherSet.store == null)
            return JSBoolean.True;

        foreach (var item in otherSet.store)
        {
            HashedString uk = item.ToUniqueID();
            if (!index.TryGetValue(in uk, out _))
                return JSBoolean.False;
        }

        return JSBoolean.True;
    }

    [JSExport("isDisjointFrom")]
    public JSValue IsDisjointFrom(in Arguments a)
    {
        var other = a.Get1();
        if (other is not JSSet otherSet)
            throw JSContext.NewTypeError("Set.prototype.isDisjointFrom requires a Set argument");

        if (store != null)
        {
            foreach (var item in store)
            {
                HashedString uk = item.ToUniqueID();
                if (otherSet.index.TryGetValue(in uk, out _))
                    return JSBoolean.False;
            }
        }

        return JSBoolean.True;
    }
}
