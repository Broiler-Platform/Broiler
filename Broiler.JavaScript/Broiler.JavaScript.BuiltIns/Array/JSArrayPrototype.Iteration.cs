using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.ExpressionCompiler;
using Broiler.JavaScript.BuiltIns.Generator;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.Engine.Core;

namespace Broiler.JavaScript.BuiltIns.Array;

public partial class JSArray
{
    [JSPrototypeMethod]
    [JSExport("every", Length = 1)]
    public static JSValue Every(in Arguments a)
    {
        var array = a.This;
        var (first, thisArg) = a.Get2();

        if (first is not JSFunction fn)
            throw JSEngine.NewTypeError($"First argument is not function");
        
        var en = array.GetElementEnumerator();
        
        while (en.MoveNext(out var hasValue, out var item, out var index))
        {
            var itemArgs = new Arguments(thisArg, item, new JSNumber(index), array);

            if (!fn.f(itemArgs).BooleanValue)
                return JSBoolean.False;
        }

        return JSBoolean.True;
    }

    [JSPrototypeMethod]
    [JSExport("entries")]
    public new static JSValue Entries(in Arguments a)
    {
        var array = a.This as JSArray;
        return new JSGenerator(array.GetEntries(), "Array Iterator");
    }

    [JSPrototypeMethod]
    [JSExport("filter", Length = 1)]
    public static JSValue Filter(in Arguments a)
    {
        var @this = a.This;
        var (callback, thisArg) = a.Get2();

        if (callback is not JSFunction fn)
            throw JSEngine.NewTypeError($"{callback} is not a function in Array.prototype.filter");
        
        var r = new JSArray();
        var en = @this.GetElementEnumerator();
        
        while (en.MoveNext(out var hasValue, out var item, out var index))
        {
            if (!hasValue) continue;
            var itemParams = new Arguments(thisArg, item, new JSNumber(index), @this);

            if (fn.f(itemParams).BooleanValue)
                r.Add(item);
        }
        return r;
    }

    [JSPrototypeMethod]
    [JSExport("find", Length = 1)]
    public static JSValue Find(in Arguments a)
    {
        var @this = a.This;
        var (callback, thisArg) = a.Get2();

        if (callback is not JSFunction fn)
            throw JSEngine.NewTypeError($"{callback} is not a function in Array.prototype.find");
        
        var en = @this.GetElementEnumerator();
        
        while (en.MoveNext(out var hasValue, out var item, out var index))
        {
            // ignore holes...
            if (!hasValue)
                continue;
            
            var itemParams = new Arguments(thisArg, item, new JSNumber(index), @this);
            if (fn.f(itemParams).BooleanValue)
                return item;
        }

        return JSUndefined.Value;
    }

    /// <summary>
    /// Creates a new array with all sub-array elements concatenated into it recursively up to
    /// the specified depth.
    /// </summary>
    /// <param name="thisObj"> The array that is being operated on. </param>
    /// <param name="depth"> The depth level specifying how deep a nested array structure
    /// should be flattened. Defaults to 1. </param>
    /// <returns> A new array with the sub-array elements concatenated into it. </returns>
    [JSPrototypeMethod]
    [JSExport("flat", Length = 0)]
    public static JSValue Flat(in Arguments a)
    {
        var result = new JSArray();
        int depth = a[0]?.IntegerValue ?? 1;
        FlattenTo(result, a.This, null, null, depth);
        return result;
    }

    /// <summary>
    /// Maps each element using a mapping function, then flattens the result into a new array.
    /// </summary>
    /// <param name="thisObj"> The array that is being operated on. </param>
    /// <param name="callback"> A function that produces an element of the new Array, taking
    /// three arguments: currentValue, index, array. </param>
    /// <param name="thisArg"> Value to use as this when executing callback. </param>
    /// <returns> A new array with each element being the result of the callback function and
    /// flattened to a depth of 1. </returns>
    [JSPrototypeMethod]
    [JSExport("flatMap", Length = 1)]
    public static JSValue FlatMap(in Arguments a)
    {
        var result = new JSArray();
        int depth = 1;
        var (callback, thisArg) = a.Get2();
        FlattenTo(result, a.This, callback, thisArg, depth);
        return result;
    }

    private static void FlattenTo(JSArray result, JSValue @this, JSValue callback, JSValue thisArg, int depth)
    {
        for (int i = 0; i < @this.Length; i++)
        {
            // TryGetElement - to check for holes in array
            if (@this.TryGetElement((uint)i, out var elementValue))
            {
                // Transform the value using the mapping function.
                if (callback != null)
                    elementValue = callback.InvokeFunction(new Arguments(thisArg, elementValue, new JSNumber(i), @this));

                // If the element is an array, flatten it.
                if (depth > 0 && elementValue is JSArray childArray)
                    FlattenTo(result, childArray, callback, thisArg, depth - 1);
                else
                    result.Add(elementValue);
            }
        }
    }

    [JSPrototypeMethod]
    [JSExport("findIndex", Length = 1)]
    public static JSValue FindIndex(in Arguments a)
    {
        var @this = a.This;
        var (callback, thisArg) = a.Get2();

        if (callback is not JSFunction fn)
            throw JSEngine.NewTypeError($"{callback} is not a function in Array.prototype.find");

        var en = @this.GetElementEnumerator();

        while (en.MoveNext(out var hasValue, out var item, out var n))
        {
            // ignore holes...
            if (!hasValue)
                continue;

            var index = new JSNumber(n);
            var itemParams = new Arguments(thisArg, item, index, @this);

            if (fn.f(itemParams).BooleanValue)
                return index;
        }

        return JSNumber.MinusOne;
    }

    [JSPrototypeMethod]
    [JSExport("forEach", Length = 1)]
    public static JSValue ForEach(in Arguments a)
    {
        var @this = a.This;
        var (callback, thisArg) = a.Get2();

        if (callback is not JSFunction fn)
            throw JSEngine.NewTypeError($"{callback} is not a function in Array.prototype.find");

        var en = @this.GetElementEnumerator();

        while (en.MoveNext(out var hasValue, out var item, out var index))
        {
            // ignore holes...
            if (!hasValue)
                continue;

            var n = new JSNumber(index);
            var itemParams = new Arguments(thisArg, item, n, @this);

            fn.f(itemParams);
        }

        return JSUndefined.Value;
    }

    [JSPrototypeMethod]
    [JSExport("keys")]
    public new static JSValue Keys(in Arguments a)
    {
        var @this = a.This;
        return new JSGenerator(new IntKeyEnumerator(@this.Length), "Array Iterator");
    }

    [JSPrototypeMethod]
    [JSExport("map", Length = 1)]
    public static JSValue Map(in Arguments a)
    {
        if (a.This is not JSObject @this)
            throw JSEngine.NewTypeError($"{a.This} is not an object or an array");

        var callback = a.Get1();

        if (callback is not JSFunction fn)
            throw JSEngine.NewTypeError($"{callback} is not a function in Array.prototype.find");

        ref var te = ref @this.GetElements();
        var r = new JSArray();
        ref var relements = ref r.GetElements();
        var length = (uint)@this.Length;

        for (uint i = 0; i < length; i++)
        {
            ref var e = ref te.Get(i);

            if (e.IsEmpty)
                continue;

            var item = @this.GetValue(e);
            var itemArgs = new Arguments(@this, item, new JSNumber(i), @this);

            relements.Put(i, fn.f(itemArgs));
        }

        r._length = length;

        return r;
    }

    [JSPrototypeMethod]
    [JSExport("reduce", Length = 1)]
    public static JSValue Reduce(in Arguments a)
    {
        var r = new JSArray();
        var @this = a.This;
        var (callback, initialValue) = a.Get2();

        if (callback is not JSFunction fn)
            throw JSEngine.NewTypeError($"{callback} is not a function in Array.prototype.reduce");

        var en = @this.GetElementEnumerator();
        uint index = 0;

        if (a.Length == 1)
        {
            if (!en.MoveNext(out initialValue))
                throw JSEngine.NewTypeError($"No initial value provided and array is empty");
        }

        while (en.MoveNext(out var hasValue, out var item, out index))
        {
            if (!hasValue)
                continue;

            var itemArgs = new Arguments(JSUndefined.Value, initialValue, item, new JSNumber(index), @this);
            initialValue = fn.f(itemArgs);
        }

        return initialValue;
    }

    [JSPrototypeMethod]
    [JSExport("reduceRight", Length = 1)]
    public static JSValue ReduceRight(in Arguments a)
    {
        var r = new JSArray();
        var @this = a.This;
        var (callback, initialValue) = a.Get2();

        if (callback is not JSFunction fn)
            throw JSEngine.NewTypeError($"{callback} is not a function in Array.prototype.reduce");

        var start = @this.Length - 1;

        if (a.Length == 1)
        {
            if (@this.Length == 0)
                throw JSEngine.NewTypeError($"No initial value provided and array is empty");

            initialValue = @this[(uint)start];
            start--;
        }

        for (int i = start; i >= 0; i--)
        {
            var item = @this[(uint)i];
            var itemArgs = new Arguments(JSUndefined.Value, initialValue, item, new JSNumber(i), @this);
            initialValue = fn.f(itemArgs);
        }

        return initialValue;
    }

    [JSPrototypeMethod]
    [JSExport("some", Length = 1)]
    public static JSValue Some(in Arguments a)
    {
        var array = a.This;
        var (first, thisArg) = a.Get2();

        if (first is not JSFunction fn)
            throw JSEngine.NewTypeError($"First argument is not function");

        var en = array.GetElementEnumerator();

        while (en.MoveNext(out var hasValue, out var item, out var index))
        {
            if (!hasValue)
                continue;

            var itemArgs = new Arguments(thisArg, item, new JSNumber(index), array);

            if (fn.f(itemArgs).BooleanValue)
                return JSBoolean.True;
        }

        return JSBoolean.False;
    }

    [JSPrototypeMethod]
    [JSExport("values", Length = 2)]
    [Symbol("@@iterator")]
    public new static JSValue Values(in Arguments a) => new JSGenerator(a.This.GetElementEnumerator(), "Array Iterator");

}
