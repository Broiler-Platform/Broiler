using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.ExpressionCompiler;
using System;
using System.Collections.Generic;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine.Extensions;
using Broiler.JavaScript.Engine.Core;

namespace Broiler.JavaScript.BuiltIns.Set;


[JSClassGenerator("Set")]
public partial class JSSet : JSObject
{
    private readonly record struct SetLikeRecord(int Size, Func<JSValue, bool> Has, Func<IElementEnumerator> GetKeys);

    private LinkedList<JSValue> store = new();
    private StringMap<LinkedListNode<JSValue>> index;

    [JSExport]
    public int Size => store?.Count ?? 0;

    public JSSet(in Arguments a) : base(JSEngine.NewTargetPrototype)
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

        if (!index.TryGetValue(in uk, out _))
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
            index.TryRemove(uk.Value, out _);
            return JSBoolean.True;
        }

        return JSBoolean.False;
    }

    private bool Contains(JSValue key)
    {
        HashedString uk = key.ToUniqueID();
        return index.TryGetValue(in uk, out _);
    }

    private bool Remove(JSValue key)
    {
        HashedString uk = key.ToUniqueID();
        if (!index.TryGetValue(in uk, out var node))
            return false;

        store.Remove(node);
        index.TryRemove(uk.Value, out _);
        return true;
    }

    private static SetLikeRecord GetSetLikeRecord(JSValue other, string methodName)
    {
        if (!other.IsObject)
            throw JSEngine.NewTypeError($"Set.prototype.{methodName} requires a Set or set-like object argument");

        if (other is JSSet otherSet)
        {
            return new SetLikeRecord(
                otherSet.Size,
                otherSet.Contains,
                () => new StoreEnumerator(otherSet.store));
        }

        var sizeValue = other["size"];
        var hasMethod = other["has"];
        var keysMethod = other["keys"];

        if (!hasMethod.IsFunction || !keysMethod.IsFunction)
            throw JSEngine.NewTypeError($"Set.prototype.{methodName} requires a Set or set-like object argument");

        return new SetLikeRecord(
            (int)sizeValue.DoubleValue,
            value => hasMethod.Call(other, value).BooleanValue,
            () => keysMethod.Call(other).GetElementEnumerator());
    }

    private sealed class StoreEnumerator(LinkedList<JSValue> source) : IElementEnumerator
    {
        private LinkedListNode<JSValue> current;
        private uint index;

        private bool MoveNextNode(out JSValue value, out uint currentIndex)
        {
            current = current == null ? source.First : current.Next;
            if (current == null)
            {
                value = JSUndefined.Value;
                currentIndex = 0;
                return false;
            }

            value = current.Value;
            currentIndex = index++;
            return true;
        }

        public bool MoveNext(out bool hasValue, out JSValue value, out uint index)
        {
            hasValue = MoveNextNode(out value, out index);
            return hasValue;
        }

        public bool MoveNext(out JSValue value)
            => MoveNextNode(out value, out _);

        public bool MoveNextOrDefault(out JSValue value, JSValue @default)
        {
            if (MoveNextNode(out value, out _))
                return true;

            value = @default;
            return false;
        }

        public JSValue NextOrDefault(JSValue @default)
            => MoveNextNode(out var value, out _) ? value : @default;
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
            throw JSEngine.NewTypeError($"Function parameter expected");

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
        var other = GetSetLikeRecord(a.Get1(), "union");

        var result = new JSSet(Arguments.Empty);
        if (store != null)
        {
            foreach (var item in store)
                result.Add(item);
        }

        var keys = other.GetKeys();
        while (keys.MoveNext(out var item))
        {
            result.Add(item);
        }

        return result;
    }

    [JSExport("intersection")]
    public JSValue Intersection(in Arguments a)
    {
        var other = GetSetLikeRecord(a.Get1(), "intersection");

        var result = new JSSet(Arguments.Empty);
        if (store != null)
        {
            foreach (var item in store)
            {
                if (other.Has(item))
                    result.Add(item);
            }
        }

        return result;
    }

    [JSExport("difference")]
    public JSValue Difference(in Arguments a)
    {
        var other = GetSetLikeRecord(a.Get1(), "difference");

        var result = new JSSet(Arguments.Empty);
        if (store == null)
            return result;

        foreach (var item in store)
        {
            if (!other.Has(item))
                result.Add(item);
        }

        return result;
    }

    [JSExport("symmetricDifference")]
    public JSValue SymmetricDifference(in Arguments a)
    {
        var other = GetSetLikeRecord(a.Get1(), "symmetricDifference");

        var result = new JSSet(Arguments.Empty);
        if (store != null)
        {
            foreach (var item in store)
                result.Add(item);
        }

        var keys = other.GetKeys();
        while (keys.MoveNext(out var item))
        {
            if (!result.Remove(item))
                result.Add(item);
        }

        return result;
    }

    [JSExport("isSubsetOf")]
    public JSValue IsSubsetOf(in Arguments a)
    {
        var other = GetSetLikeRecord(a.Get1(), "isSubsetOf");

        if (store == null)
            return JSBoolean.True;

        foreach (var item in store)
        {
            if (!other.Has(item))
                return JSBoolean.False;
        }

        return JSBoolean.True;
    }

    [JSExport("isSupersetOf")]
    public JSValue IsSupersetOf(in Arguments a)
    {
        var other = GetSetLikeRecord(a.Get1(), "isSupersetOf");

        if (other.Size > Size)
            return JSBoolean.False;

        var keys = other.GetKeys();
        while (keys.MoveNext(out var item))
        {
            if (!Contains(item))
                return JSBoolean.False;
        }

        return JSBoolean.True;
    }

    [JSExport("isDisjointFrom")]
    public JSValue IsDisjointFrom(in Arguments a)
    {
        var other = GetSetLikeRecord(a.Get1(), "isDisjointFrom");

        if (store == null)
            return JSBoolean.True;

        foreach (var item in store)
        {
            if (other.Has(item))
                return JSBoolean.False;
        }

        return JSBoolean.True;
    }
}
