using System;
using System.Text;
using Broiler.JavaScript.Core.Typed;
using System.Globalization;
using Broiler.JavaScript.Core.Core.Primitive;
using Broiler.JavaScript.Core.Core.Storage;
using Broiler.JavaScript.Core.Extensions;
using Broiler.JavaScript.Core.Core.Clr;
using Broiler.JavaScript.Core.Core.Boolean;
using Broiler.JavaScript.Core.Core.Function;
using Broiler.JavaScript.ExpressionCompiler;
using Broiler.JavaScript.Core.Core.Generator;

namespace Broiler.JavaScript.Core.Core.Array;

public partial class JSArray
{
    [JSExport(IsConstructor = true, Length = 1)]
    public new static JSValue Constructor(in Arguments a)
    {
        var @this = a.This;
        var arg = a.Get1();
        var result = new JSArray();

        if (a.Length == 0)
            return new JSArray();

        if (a.Length == 1 && arg.IsNumber)
        {
            double val = arg.DoubleValue;
            if (double.IsNaN(val) || val < 0 || val > UInt32.MaxValue || Math.Floor(val) != val)
                throw JSContext.NewRangeError($"Invalid array length");
            return new JSArray((uint)arg.DoubleValue);
        }

        for (int i = 0; i < a.Length; i++)
        {
            var ele = a.GetAt(i);
            result.Add(ele);
        }

        return result;
    }

    [JSPrototypeMethod]
    [JSExport("at", Length = 1)]
    public static JSValue At(in Arguments a)
    {
        var index = a[0];
        var @this = a.This;
        var length = a.Length;
        var i = index.IntegerValue;

        if (i < 0)
        {
            if (i < -length)
                return JSUndefined.Value;

            i += length;
        }

        if (i >= length)
            return JSUndefined.Value;

        return @this.GetOwnProperty((uint)i);
    }

    [JSPrototypeMethod]
    [JSExport("concat", Length = 1)]
    public static JSValue Concat(in Arguments a)
    {
        var r = new JSArray();

        if (a.This.IsArray)
            r.AddRange(a.This);
        else
            r.Add(a.This);

        for (int i = 0; i < a.Length; i++)
        {
            var f = a.GetAt(i);

            if (f.IsArray)
                r.AddRange(f);
            else
                r.Add(f);
        }

        return r;
    }

    [JSPrototypeMethod]
    [JSExport("every", Length = 1)]
    public static JSValue Every(in Arguments a)
    {
        var array = a.This;
        var (first, thisArg) = a.Get2();

        if (first is not JSFunction fn)
            throw JSContext.NewTypeError($"First argument is not function");
        
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
    [JSExport("copyWithin", Length = 2)]
    public static JSValue CopyWithin(in Arguments a)
    {
        var (t, s) = a.Get2();
        var target = t.IntValue;
        var start = s.IntValue;
        var end = a.TryGetAt(2, out var e) ? e.IntValue : int.MaxValue;
        var @this = a.This as JSArray;

        target = target < 0 ? Math.Max(@this.Length + target, 0) : Math.Min(target, @this.Length);
        start = start < 0 ? Math.Max(@this.Length + start, 0) : Math.Min(start, @this.Length);
        end = end < 0 ? Math.Max(@this.Length + end, 0) : Math.Min(end, @this.Length);

        // Calculate the number of values to copy.
        int count = Math.Min(end - start, @this.Length - target);

        // Check if we need to copy in reverse due to an overlap.
        int direction = 1;
        if (start < target && target < start + count)
        {
            direction = -1;
            start += count - 1;
            target += count - 1;
        }

        ref var elements = ref @this.GetElements(true);

        while (count > 0)
        {
            // Get the value of the array element.
            var elementValue = elements[(uint)start];

            if (!elementValue.IsEmpty)
            {
                // Copy the value to the new position.
                elements.Put((uint)target) = elementValue;
            }
            else
            {
                // Delete the element at the new position.
                // Delete((uint)target);
                elements.RemoveAt((uint)target);
            }

            // Progress to the next element.
            start += direction;
            target += direction;
            count--;
        }

        return @this;
    }

    [JSPrototypeMethod]
    [JSExport("entries")]
    public new static JSValue Entries(in Arguments a)
    {
        var array = a.This as JSArray;
        return new JSGenerator(array.GetEntries(), "Array Iterator");
    }

    /// <summary>
    /// Fills all the elements of a typed array from a start index to an end index with a
    /// static value.
    /// </summary>
    /// <param name="value"> The value to fill the typed array with. </param>
    /// <param name="start"> Optional. Start index. Defaults to 0. </param>
    /// <param name="end"> Optional. End index (exclusive). Defaults to the length of the array. </param>
    /// <returns> The array that is being operated on. </returns>
    [JSPrototypeMethod]
    [JSExport("fill", Length = 1)]
    public static JSValue Fill(in Arguments a)
    {
        var @this = a.This;
        var (value, start, end) = a.Get3();

        var len = @this.Length;
        var relativeStart = start.AsInt32OrDefault();
        var relativeEnd = end.AsInt32OrDefault(len);

        relativeStart = relativeStart < 0 ? Math.Max(len + relativeStart, 0) : Math.Min(relativeStart, len);
        relativeEnd = relativeEnd < 0 ? Math.Max(len + relativeEnd, 0) : Math.Min(relativeEnd, len);

        for (; relativeStart < relativeEnd; relativeStart++)
            @this[(uint)relativeStart] = value;

        return @this;
    }

    [JSPrototypeMethod]
    [JSExport("filter", Length = 1)]
    public static JSValue Filter(in Arguments a)
    {
        var @this = a.This;
        var (callback, thisArg) = a.Get2();

        if (callback is not JSFunction fn)
            throw JSContext.NewTypeError($"{callback} is not a function in Array.prototype.filter");
        
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
            throw JSContext.NewTypeError($"{callback} is not a function in Array.prototype.find");
        
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
            throw JSContext.NewTypeError($"{callback} is not a function in Array.prototype.find");

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
            throw JSContext.NewTypeError($"{callback} is not a function in Array.prototype.find");

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
    [JSExport("includes", Length = 1)]
    public static JSValue Includes(in Arguments a)
    {
        var @this = a.This;
        var first = a.Get1();

        var fromIndex = a[1]?.IntValue ?? 0;
        if (fromIndex < 0)
            fromIndex += @this.Length;

        bool isUndefined = first.IsUndefined;
        var en = @this.GetElementEnumerator();

        while (en.MoveNext(out var hasValue, out var item, out var index))
        {
            if (fromIndex > index)
                continue;

            if (hasValue)
            {
                if (item.SameValueZero(first))
                    return JSBoolean.True;
            }
            else
            {
                if (isUndefined)
                    return JSBoolean.True;
            }
        }

        return JSBoolean.False;
    }

    [JSPrototypeMethod]
    [JSExport("indexOf", Length = 1)]
    public static JSValue IndexOf(in Arguments a)
    {
        var @this = a.This;
        var first = a.Get1();
        var fromIndex = a[1]?.IntValue ?? 0;

        if (fromIndex < 0)
            fromIndex += @this.Length;

        var en = @this.GetElementEnumerator();

        while (en.MoveNext(out var hasValue, out var item, out var index))
        {
            if (fromIndex > index)
                continue;

            if (!hasValue)
                continue;

            if (first.StrictEquals(item))
                return new JSNumber(index);
        }

        return JSNumber.MinusOne;
    }

    [JSPrototypeMethod]
    [JSExport("join", Length = 1)]
    public static JSValue Join(in Arguments a)
    {
        var @this = a.This as JSObject;
        var first = a.Get1();
        var sep = first.IsUndefined ? "," : first.ToString();
        var sb = new StringBuilder();
        var length = (uint)@this.Length;

        for (uint i = 0; i < length; i++)
        {
            var item = @this[i];
            if (i != 0)
                sb.Append(sep);

            if (item.IsNullOrUndefined)
                continue;

            sb.Append(item.ToString());
        }

        return new JSString(sb.ToString());
    }

    [JSPrototypeMethod]
    [JSExport("keys")]
    public new static JSValue Keys(in Arguments a)
    {
        var @this = a.This;
        return new JSGenerator(new KeyEnumerator(@this.Length), "Array Iterator");
    }

    [JSPrototypeMethod]
    [JSExport("lastIndexOf", Length = 1)]
    public static JSValue LastIndexOf(in Arguments a)
    {
        var @this = a.This;
        var first = a.Get1();
        var n = @this.Length;
        var fromIndex = a[1]?.IntValue ?? int.MaxValue;

        if (fromIndex < 0)
            fromIndex += @this.Length;

        if (n == 0)
            return JSNumber.MinusOne;

        for (int i = Math.Min(n - 1, fromIndex); i >= 0; i--)
        {
            if (!@this.TryGetElement((uint)i, out var item))
                continue;

            if (item.StrictEquals(first))
                return new JSNumber(i);
        }

        return JSNumber.MinusOne;
    }

    [JSPrototypeMethod]
    [JSExport("map", Length = 1)]
    public static JSValue Map(in Arguments a)
    {
        if (a.This is not JSObject @this)
            throw JSContext.NewTypeError($"{a.This} is not an object or an array");

        var callback = a.Get1();

        if (callback is not JSFunction fn)
            throw JSContext.NewTypeError($"{callback} is not a function in Array.prototype.find");

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
    [JSExport("push", Length = 1)]
    public static JSValue Push(in Arguments a)
    {
        var t = a.This as JSObject;

        if (t == null)
            return JSNumber.Zero;

        if (t.IsSealedOrFrozen())
            throw JSContext.NewTypeError($"Cannot modify property length");

        int ai, al;

        if (t is JSArray ta)
        {
            var i = ta._length;
            var l = (long)i;
            var max = (long)uint.MaxValue;

            al = a.Length;

            ref var taElements = ref ta.GetElements();

            for (ai = 0; ai < al; ai++)
            {
                var item = a.GetAt(ai);
                if (l < max)
                {
                    taElements.Put(i++, item);
                    ta._length = i;
                }
                else
                {
                    ta[l.ToString()] = item;
                }

                l++;
            }

            if (l > max)
                throw JSContext.NewTypeError($"Invalid array length");

            ta._length = i;

            return new JSNumber(ta._length);
        }

        var oldLength = t[KeyStrings.length];
        uint ln = oldLength.IsUndefined ? 0 : (uint)oldLength.DoubleValue;

        al = a.Length;

        for (ai = 0; ai < al; ai++)
            t[ln++] = a.GetAt(ai);

        var n = new JSNumber(ln);
        t[KeyStrings.length] = n;

        return n;
    }

    [JSPrototypeMethod]
    [JSExport("pop")]
    public static JSValue Pop(in Arguments a)
    {
        var @this = a.This;

        if (@this == null)
            return JSUndefined.Value;

        var length = @this.Length;

        if (length <= 0)
            return JSUndefined.Value;

        var index = length - 1;

        if (@this.TryRemove((uint)index, out JSProperty r))
        {
            @this.Length = index;
            return r.value;
        }

        return JSUndefined.Value;
    }

    [JSPrototypeMethod]
    [JSExport("reduce", Length = 1)]
    public static JSValue Reduce(in Arguments a)
    {
        var r = new JSArray();
        var @this = a.This;
        var (callback, initialValue) = a.Get2();

        if (callback is not JSFunction fn)
            throw JSContext.NewTypeError($"{callback} is not a function in Array.prototype.reduce");

        var en = @this.GetElementEnumerator();
        uint index = 0;

        if (a.Length == 1)
        {
            if (!en.MoveNext(out initialValue))
                throw JSContext.NewTypeError($"No initial value provided and array is empty");
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
            throw JSContext.NewTypeError($"{callback} is not a function in Array.prototype.reduce");

        var start = @this.Length - 1;

        if (a.Length == 1)
        {
            if (@this.Length == 0)
                throw JSContext.NewTypeError($"No initial value provided and array is empty");

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
    [JSExport("reverse")]
    public static JSValue Reverse(in Arguments a)
    {
        var @this = a.This as JSObject;
        var i = 0;
        var j = @this.Length - 1;
        ref var elements = ref @this.GetElements();

        while (i < j)
        {
            var swap = elements[(uint)i];
            elements.Put((uint)i++) = elements[(uint)j];
            elements.Put((uint)j--) = swap;
        }

        return @this;
    }

    [JSPrototypeMethod]
    [JSExport("shift", Length = 0)]
    public static JSValue Shift(in Arguments a)
    {
        var @this = a.This;
        JSValue first = JSUndefined.Value;

        if (@this is not JSObject @object)
            return first;

        if (@object.IsSealedOrFrozen())
            throw JSContext.NewTypeError("Cannot modify property length");

        var n = (uint)@this.Length;
        if (n == 0)
            return first;

        ref var oe = ref @object.GetElements();
        if (oe.IsNull)
            return first;

        first = @this[0];
        var last = n - 1;
        for (uint i = 1; i < n; i++)
            oe.Put(i - 1) = oe[i];

        oe.RemoveAt(last);
        @this.Length = (int)last;

        return first;
    }

    [JSPrototypeMethod]
    [JSExport("slice", Length = 2)]
    public static JSArray Slice(in Arguments a)
    {
        var start = a.TryGetAt(0, out var a1) ? a1.IntegerValue : 0;
        var end = a.TryGetAt(1, out var a2)
            ? (a2.IsUndefined ? int.MaxValue : a2.IntegerValue)
            : int.MaxValue;

        var @this = a.This;

        // Fix the arguments so they are positive and within the bounds of the array.
        if (start < 0)
            start += @this.Length;

        if (end < 0)
            end += @this.Length;

        // return empty array
        if (end <= start)
            return new JSArray();

        start = Math.Min(Math.Max(start, 0), @this.Length);
        end = Math.Min(Math.Max(end, 0), @this.Length);

        var resultLength = end - start;
        JSArray r = new((uint)resultLength);
        ref var rElements = ref r.CreateElements();
        uint ni;

        ni = 0;
        //r.length is int
        for (uint i = 0; i < r.Length; i++)
        {
            var index = (uint)start + i;

            if (@this.TryGetValue(index, out var val))
            {
                rElements.Put(ni++) = val;
            }
            else
            {
                ni++;
            }
        }

        //_length is uint for internal calculation
        r._length = ni;
        return r;
    }

    [JSPrototypeMethod]
    [JSExport("some", Length = 1)]
    public static JSValue Some(in Arguments a)
    {
        var array = a.This;
        var (first, thisArg) = a.Get2();

        if (first is not JSFunction fn)
            throw JSContext.NewTypeError($"First argument is not function");

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
    [JSExport("sort", Length = 1)]
    public static JSValue Sort(in Arguments a)
    {
        // To be modified by Akash
        var fx = a.Get1();
        var @this = a.This as JSObject;

        if (@this == null)
            throw JSContext.NewTypeError($"Sort can only be called with an Array or an Object");

        var length = @this.Length;
        if (length <= 1)
            return @this;

        Comparison<JSValue> cx = null;
        if (fx is JSFunction fn)
        {
            cx = (left, right) =>
            {
                left = left ?? JSNull.Value;
                right = right ?? JSNull.Value;

                if (left == JSNull.Value)
                {
                    if (right == JSNull.Value)
                        return 0;

                    return 1;
                }

                if (right == JSNull.Value)
                    return -1;

                if (left == JSUndefined.Value)
                {
                    if (right == JSUndefined.Value)
                        return 0;

                    return 1;
                }

                if (right == JSUndefined.Value)
                    return -1;

                var arg = new Arguments(JSUndefined.Value, left, right);
                var r = fn.f(arg).DoubleValue;

                if (double.IsNaN(r))
                    return 0;

                return Math.Sign(r);
            };
        }
        else
        {
            if (!fx.IsUndefined)
                throw JSContext.NewTypeError($"Argument is not a function");

            cx = (left, right) =>
            {
                left = left ?? JSNull.Value;
                right = right ?? JSNull.Value;

                if (left == JSNull.Value)
                {
                    if (right == JSNull.Value)
                        return 0;

                    return 1;
                }

                if (right == JSNull.Value)
                    return -1;

                if (left == JSUndefined.Value)
                {
                    if (right == JSUndefined.Value)
                        return 0;
                    return 1;
                }

                if (right == JSUndefined.Value)
                    return -1;

                return string.CompareOrdinal(
                    left.IsUndefined ? string.Empty : left.ToString(),
                    right.IsUndefined ? string.Empty : right.ToString());
            };
        }

        ref var elements = ref @this.GetElements();
        elements.QuickSort(cx, 0, (uint)(length - 1));

        return @this;
    }

    [JSPrototypeMethod]
    [JSExport("splice", Length = 2)]
    public static JSValue Splice(in Arguments a)
    {
        var r = new JSArray();

        var start = a.TryGetAt(0, out var startP)
            ? startP.IntegerValue
            : 0;
        var deleteCount = a.TryGetAt(1, out var deleteCountP)
            ? deleteCountP.IntegerValue
            : (a.Length == 0 ? 0 : int.MaxValue);

        var @this = a.This as JSObject;

        if (@this == null)
            return r;

        if (@this.IsSealedOrFrozen())
            throw JSContext.NewTypeError("Cannot modify property length");

        // Get the length of the array.
        int arrayLength = @this.Length;

        // This method only supports arrays of length up to 2^31 - 1.
        if (@this.Length > int.MaxValue)
            throw JSContext.NewRangeError("The array is too long");

        // Fix the arguments so they are positive and within the bounds of the array.
        if (start < 0)
            start = Math.Max(arrayLength + start, 0);
        else
            start = Math.Min(start, arrayLength);

        deleteCount = Math.Min(Math.Max(deleteCount, 0), arrayLength - start);

        ref var elements = ref @this.GetElements();

        // Get the deleted items.
        var deletedItems = new JSArray((uint)deleteCount);
        ref var deletedItemsElements = ref deletedItems.GetElements();

        for (uint i = 0; i < deleteCount; i++)
        {
            ref var property = ref elements.Get((uint)(start + i));

            if (property.IsProperty)
            {
                deletedItemsElements.Put(i) = JSProperty.Property(@this.GetValue(in property));
                continue;
            }

            deletedItemsElements.Put(i) = property;
        }

        var itemsLength = a.Length > 1 ? a.Length - 2 : 0;

        // Move the trailing elements.
        int offset = itemsLength - deleteCount;
        int newLength = arrayLength + offset;

        if (deleteCount > itemsLength)
        {
            for (int i = start + itemsLength; i < newLength; i++)
                elements.Put((uint)i) = elements.Get((uint)(i - offset));

            // Delete the trailing elements.
            for (int i = newLength; i < arrayLength; i++)
                elements.RemoveAt((uint)i);
        }
        else
        {
            for (int i = newLength - 1; i >= start + itemsLength; i--)
                elements.Put((uint)i) = elements.Get((uint)(i - offset));
        }

        @this.Length = newLength;

        // Insert the new elements.
        for (int i = 0; i < itemsLength; i++)
        {
            elements.Put((uint)(start + i)) = JSProperty.Property(a[i + 2]);
        }

        // Return the deleted items.
        return deletedItems;
    }

    [JSPrototypeMethod]
    [JSExport("unshift", Length = 1)]
    public static JSValue Unshift(in Arguments a)
    {
        var @this = a.This as JSObject;
        if (@this == null)
            return JSUndefined.Value;

        if (@this.IsSealedOrFrozen())
            throw JSContext.NewTypeError("Cannot modify property length");

        var l = a.This.Length;
        if (l > 0)
        {
            // move.. 
            @this.MoveElements(0, a.Length);
            ref var elements = ref @this.GetElements();

            for (uint i = 0; i < a.Length; i++)
            {
                elements.Put(i) = JSProperty.Property(a.GetAt((int)i));
            }
        }
        return new JSNumber(a.This.Length);
    }

    [JSPrototypeMethod]
    [JSExport("toLocaleString", Length = 0)]
    internal static JSValue ToLocaleString(in Arguments a)
    {
        var @this = a.This as JSArray;
        var (locale, format) = a.Get2();
        StringBuilder sb = new();

        var def = "N0";

        string strFormat = format.IsNullOrUndefined ? def : (format.IsString ? format.ToString() :
            throw JSContext.NewTypeError("Options not supported, use .Net String Formats")
            );

        CultureInfo culture = locale.IsNullOrUndefined ? CultureInfo.CurrentCulture : CultureInfo.GetCultureInfo(locale.ToString());

        // Group separator based on Culture Info.
        var separator = culture.TextInfo.ListSeparator;

        bool first = true;
        var en = @this.GetElementEnumerator();

        while (en.MoveNext(out var n))
        {
            if (!first)
            {
                //sb.Append(',');
                sb.Append(separator);
            }

            first = false;
            sb.Append(n.ToLocaleString(strFormat, culture));
        }

        return new JSString(sb.ToString());
    }

    [JSPrototypeMethod]
    [JSExport("toString")]
    internal new static JSValue ToString(in Arguments args)
    {
        if (args.This.IsArray)
            return Join(in args);

        return args.This.InvokeMethod(KeyStrings.join, in args);
    }

    [JSPrototypeMethod]
    [JSExport("values", Length = 2)]
    [Symbol("@@iterator")]
    public new static JSValue Values(in Arguments a) => new JSGenerator(a.This.GetElementEnumerator(), "Array Iterator");
}
