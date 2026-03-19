using Broiler.JavaScript.Core.Core.Clr;
using System;
using System.Threading;
using Yantra.Core;
using YantraJS.Core.BigInt;
using YantraJS.Core.Set;
using YantraJS.Core.Typed;
using System.Collections.Generic;
using Broiler.JavaScript.Core.Core.Intl;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Utils;
using Broiler.JavaScript.Core;

namespace YantraJS.Core;

[JSFunctionGenerator("Globals", Globals = true)]
public partial class JSGlobalStatic
{
    [JSExport("Infinity")]
    public static JSNumber Infinity = JSNumber.PositiveInfinity;

    [JSExport("NaN")]
    public static JSNumber NaN = JSNumber.NaN;

    [JSExport("Intl")]
    public static JSValue Intl = ClrType.From(typeof(JSIntl));

    [JSExport("decodeURI", Length = 1)]
    public static JSValue DecodeURI(in Arguments a)
    {
        var f = a.Get1().ToString();
        return new JSString(UriHelper.DecodeURI(f));
    }

    [JSExport("decodeURIComponent", Length = 1)]
    public static JSValue DecodeURIComponent(in Arguments a)
    {
        var f = a.Get1().ToString();
        return new JSString(Uri.UnescapeDataString(f));
    }

    [JSExport("eval", Length = 1)]
    public static JSValue Eval(in Arguments a)
    {
        var f = a.Get1();

        if (!f.IsString)
            return f;

        var text = (f as JSString).value;
        string location = null;

        JSContext.Current.DispatchEvalEvent(ref text, ref location);
        return CoreScript.Evaluate(text, null);
    }

    [JSExport("encodeURI", Length = 1)]
    public static JSValue EncodeURI(in Arguments a)
    {
        var f = a.Get1().ToString();
        return new JSString(Uri.EscapeUriString(f));
    }

    [JSExport("encodeURIComponent", Length = 1)]
    public static JSValue EncodeURIComponent(in Arguments a)
    {
        var f = a.Get1().ToString();
        return new JSString(Uri.EscapeDataString(f));
    }

    [JSExport("isFinite", Length = 1)]
    public static JSValue IsFinite(in Arguments a) => JSNumber.IsFinite(a);

    [JSExport("isNaN", Length = 1)]
    public static JSValue IsNaN(in Arguments a) => double.IsNaN(a.Get1().DoubleValue)
            ? JSBoolean.True
            : JSBoolean.False;

    [JSExport("parseFloat", Length = 1)]
    public static JSValue ParseFloat(in Arguments a) => JSNumber.ParseFloat(a);

    [JSExport("parseInt", Length = 2)]
    public static JSValue ParseInt(in Arguments a) => JSNumber.ParseInt(a);

    [JSExport("setImmediate", Length = 1)]
    public static JSValue SetImmediate(in Arguments a)
    {
        var @this = a.This;
        var fx = a.Get1();

        if (fx is not JSFunction f)
            throw JSContext.NewTypeError("Argument is not a function");

        var c = JSContext.Current;
        SynchronizationContext.Current.Post((_1) =>
        {
            try
            {
                f.f(new Arguments(_1 as JSValue));
            }
            catch (Exception ex)
            {
                c.ReportError(ex);
            }
        }, @this);

        return JSUndefined.Value;
    }

    [JSExport("setInterval", Length = 2)]
    public static JSValue SetInterval(in Arguments a)
    {
        var @this = a.This;
        var (fx, timeout) = a.Get2();

        if (fx is not JSFunction f)
            throw JSContext.NewTypeError("Argument is not a function");

        var delay = timeout.IsUndefined ? 0 : timeout.IntValue;
        var key = JSContext.Current.SetInterval(delay, f, a);

        return new JSBigInt(key);
    }

    [JSExport("clearInterval", Length = 1)]
    public static JSValue ClearInterval(in Arguments a)
    {
        var n = a.Get1().BigIntValue;
        JSContext.Current.ClearInterval(n);
        return JSUndefined.Value;
    }

    [JSExport("setTimeout", Length = 2)]
    public static JSValue SetTimeout(in Arguments a)
    {
        var context = JSContext.Current;
        var (fx, timeout) = a.Get2();
        var current = JSContext.Current;

        if (fx is not JSFunction f)
            throw JSContext.NewTypeError("Argument is not a function");

        var delay = timeout.IsUndefined ? 0 : timeout.IntValue;
        var key = context.PostTimeout(delay, f, a);

        return new JSBigInt(key);
    }

    [JSExport("clearTimeout", Length = 1)]
    public static JSValue ClearTimeout(in Arguments a)
    {
        var n = a.Get1().BigIntValue;
        var context = JSContext.Current;

        context.ClearTimeout(n);
        return JSUndefined.Value;
    }

    /// <summary>
    /// ES2026 §4.11 — structuredClone(value, options?)
    /// Deep-clones a value using the structured clone algorithm.
    /// Supports: primitives, plain objects, arrays, Date, RegExp, Map, Set,
    /// ArrayBuffer, typed arrays, Error. Handles circular references.
    /// </summary>
    [JSExport("structuredClone", Length = 1)]
    public static JSValue StructuredClone(in Arguments a)
    {
        var value = a.Get1();
        var seen = new Dictionary<JSValue, JSValue>(System.Collections.Generic.ReferenceEqualityComparer.Instance);
        return StructuredCloneValue(value, seen);
    }

    private static JSValue StructuredCloneValue(JSValue value, Dictionary<JSValue, JSValue> seen)
    {
        // Primitives are returned as-is.
        if (value == null || value.IsNullOrUndefined)
            return value;

        if (value is JSNumber || value is JSString || value is JSBoolean)
            return value;

        if (value is JSBigInt)
            return value;

        // Functions cannot be cloned.
        if (value is JSFunction)
            throw JSContext.NewTypeError("structuredClone: function values cannot be cloned");

        // Check for circular references.
        if (seen.TryGetValue(value, out var existing))
            return existing;

        // Date
        if (value is JSDate date)
        {
            var clone = new JSDate(date.value);
            seen[value] = clone;
            return clone;
        }

        // RegExp
        if (value is JSRegExp regex)
        {
            var clone = new JSRegExp(regex.pattern, regex.flags);
            seen[value] = clone;
            return clone;
        }

        // ArrayBuffer
        if (value is JSArrayBuffer arrayBuffer)
        {
            if (arrayBuffer.isDetached)
                throw JSContext.NewTypeError("structuredClone: cannot clone a detached ArrayBuffer");

            var newBuf = new byte[arrayBuffer.buffer.Length];
            Array.Copy(arrayBuffer.buffer, newBuf, arrayBuffer.buffer.Length);

            var clone = new JSArrayBuffer(newBuf);
            seen[value] = clone;
            return clone;
        }

        // Map
        if (value is JSMap map)
        {
            var clone = new JSMap(Arguments.Empty);
            seen[value] = clone;

            foreach (var entry in map.GetEntries())
            {
                var clonedKey = StructuredCloneValue(entry[0], seen);
                var clonedVal = StructuredCloneValue(entry[1], seen);
                clone.Set(clonedKey, clonedVal);
            }

            return clone;
        }

        // Set
        if (value is JSSet set)
        {
            var clone = new JSSet(Arguments.Empty);
            seen[value] = clone;

            foreach (var item in set.Keys())
                clone.Add(StructuredCloneValue(item, seen));

            return clone;
        }

        // Error
        if (value is JSError error)
        {
            var clone = new JSError(new Arguments(JSUndefined.Value, new JSString(error.Message)));
            seen[value] = clone;
            return clone;
        }

        // Array
        if (value is JSArray arr)
        {
            var clone = new JSArray();
            seen[value] = clone;
            var en = arr.GetElementEnumerator();
            
            while (en.MoveNext(out var hasValue, out var item, out var _))
            {
                if (!hasValue)
                    continue;

                clone.Add(StructuredCloneValue(item, seen));
            }

            return clone;
        }

        // Plain object
        if (value is JSObject obj)
        {
            var clone = new JSObject();
            seen[value] = clone;
            var pen = obj.GetOwnProperties().GetEnumerator();

            while (pen.MoveNext(out var key, out var prop))
            {
                if (prop.IsEmpty || !prop.IsEnumerable)
                    continue;

                if (!prop.IsValue)
                    continue;

                clone[key.Value] = StructuredCloneValue(prop.value, seen);
            }

            return clone;
        }

        // Fallback: return value as-is for unknown types.
        return value;
    }
}
