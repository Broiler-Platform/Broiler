using System;
using System.Threading;
using System.Collections.Generic;
using Broiler.JavaScript.BuiltIns.Error;
using Broiler.JavaScript.BuiltIns.RegExp;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.Array.Typed;
using Broiler.JavaScript.ExpressionCompiler;
using Broiler.JavaScript.Extensions;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.Engine.Extensions;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.Engine.Core;

namespace Broiler.JavaScript.Globals;

[JSFunctionGenerator("Globals", Globals = true)]
public partial class JSGlobalStatic
{
    [JSExport("Infinity")]
    public static JSValue Infinity = JSValue.NumberPositiveInfinity;

    [JSExport("NaN")]
    public static JSValue NaN = JSValue.NumberNaN;

    [JSExport("Intl")]
    public static JSValue Intl => DefaultBuiltInRegistry.IntlFactory?.Invoke() ?? JSUndefined.Value;

    [JSExport("decodeURI", Length = 1)]
    public static JSValue DecodeURI(in Arguments a)
    {
        var f = a.Get1().ToString();
        return JSValue.CreateString(UriHelper.DecodeURI(f));
    }

    [JSExport("decodeURIComponent", Length = 1)]
    public static JSValue DecodeURIComponent(in Arguments a)
    {
        var f = a.Get1().ToString();
        return JSValue.CreateString(Uri.UnescapeDataString(f));
    }

    [JSExport("eval", Length = 1)]
    public static JSValue Eval(in Arguments a)
    {
        var f = a.Get1();

        if (!f.IsString)
            return f;

        var text = f.StringValue;
        string location = null;

        (JSEngine.Current as IJSExecutionContext)?.DispatchEvalEvent(ref text, ref location);
        return CoreScript.Evaluate(text, null);
    }

    [JSExport("encodeURI", Length = 1)]
    public static JSValue EncodeURI(in Arguments a)
    {
        var f = a.Get1().ToString();
        return JSValue.CreateString(Uri.EscapeUriString(f));
    }

    [JSExport("encodeURIComponent", Length = 1)]
    public static JSValue EncodeURIComponent(in Arguments a)
    {
        var f = a.Get1().ToString();
        return JSValue.CreateString(Uri.EscapeDataString(f));
    }

    [JSExport("isFinite", Length = 1)]
    public static JSValue IsFinite(in Arguments a)
    {
        var first = a.Get1();
        if (first.IsNumber)
        {
            var v = first.DoubleValue;
            if (!double.IsNaN(v) && v > double.NegativeInfinity && v < double.PositiveInfinity)
                return JSValue.BooleanTrue;
        }
        return JSValue.BooleanFalse;
    }

    [JSExport("isNaN", Length = 1)]
    public static JSValue IsNaN(in Arguments a) => double.IsNaN(a.Get1().DoubleValue)
            ? JSValue.BooleanTrue
            : JSValue.BooleanFalse;

    [JSExport("parseFloat", Length = 1)]
    public static JSValue ParseFloat(in Arguments a)
    {
        var result = NumberParser.ParseFloat(a.Get1().ToString());
        return JSValue.CreateNumber(result);
    }

    [JSExport("parseInt", Length = 2)]
    public static JSValue ParseInt(in Arguments a)
    {
        var nan = JSValue.NumberNaN;

        if (a.Length <= 0)
            return nan;

        var p = a.Get1();
        if (p.IsNumber)
            return p;

        if (p.IsNull || p.IsUndefined)
            return nan;

        var text = p.JSTrim();
        if (text.Length == 0)
            return nan;

        var radix = 0;
        if (a.Length > 1)
        {
            var (_, a1) = a.Get2();
            if (a1.IsNull || a1.IsUndefined)
            {
                radix = 0;
            }
            else
            {
                var n = a1.DoubleValue;
                if (!double.IsNaN(n))
                {
                    radix = a1.IntValue;
                    if (radix < 0 || radix == 1 || radix > 36)
                        return nan;
                }
            }
        }

        var d = NumberParser.ParseInt(text.Trim(), radix, false);
        return JSValue.CreateNumber(d);
    }

    [JSExport("setImmediate", Length = 1)]
    public static JSValue SetImmediate(in Arguments a)
    {
        var @this = a.This;
        var fx = a.Get1();

        if (fx is not JSFunction f)
            throw JSEngine.NewTypeError("Argument is not a function");

        var c = JSEngine.Current as JSContext;
        SynchronizationContext.Current.Post((_1) =>
        {
            try
            {
                f.Delegate(new Arguments(_1 as JSValue));
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
            throw JSEngine.NewTypeError("Argument is not a function");

        var delay = timeout.IsUndefined ? 0 : timeout.IntValue;
        var key = (JSEngine.Current as JSContext).SetInterval(delay, f, a);

        return JSValue.CreateBigInt(key);
    }

    [JSExport("clearInterval", Length = 1)]
    public static JSValue ClearInterval(in Arguments a)
    {
        var n = a.Get1().BigIntValue;
        (JSEngine.Current as JSContext).ClearInterval(n);
        return JSUndefined.Value;
    }

    [JSExport("setTimeout", Length = 2)]
    public static JSValue SetTimeout(in Arguments a)
    {
        var context = JSEngine.Current as JSContext;
        var (fx, timeout) = a.Get2();
        var current = JSEngine.Current as JSContext;

        if (fx is not JSFunction f)
            throw JSEngine.NewTypeError("Argument is not a function");

        var delay = timeout.IsUndefined ? 0 : timeout.IntValue;
        var key = context.PostTimeout(delay, f, a);

        return JSValue.CreateBigInt(key);
    }

    [JSExport("clearTimeout", Length = 1)]
    public static JSValue ClearTimeout(in Arguments a)
    {
        var n = a.Get1().BigIntValue;
        var context = JSEngine.Current as JSContext;

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
        var transferredBuffers = GetTransferredArrayBuffers(a.Length > 1 ? a[1] : JSUndefined.Value);
        foreach (var (source, clone) in transferredBuffers)
            seen[source] = clone;

        var result = StructuredCloneValue(value, seen);
        DetachTransferredArrayBuffers(transferredBuffers);

        return result;
    }

    private static Dictionary<JSValue, JSValue> GetTransferredArrayBuffers(JSValue options)
    {
        var transferredBuffers = new Dictionary<JSValue, JSValue>(System.Collections.Generic.ReferenceEqualityComparer.Instance);
        if (options is not JSObject optionsObject)
            return transferredBuffers;

        var transferValue = optionsObject[(KeyString)"transfer"];
        if (transferValue == null || transferValue.IsNullOrUndefined)
            return transferredBuffers;

        if (transferValue is not JSArray transferArray)
            throw JSEngine.NewTypeError("structuredClone: transfer must be an array");

        foreach (var (_, item) in transferArray.GetArrayElements(withHoles: false))
        {
            if (item is not JSArrayBuffer arrayBuffer)
                throw JSEngine.NewTypeError("structuredClone: transfer list entries must be ArrayBuffers");

            if (arrayBuffer.Detached)
                throw JSEngine.NewTypeError("structuredClone: cannot transfer a detached ArrayBuffer");

            if (transferredBuffers.ContainsKey(arrayBuffer))
                throw JSEngine.NewTypeError("structuredClone: duplicate ArrayBuffer in transfer list");

            var sourceBuffer = arrayBuffer.Buffer;
            var clonedBuffer = new byte[sourceBuffer.Length];
            Array.Copy(sourceBuffer, clonedBuffer, sourceBuffer.Length);
            transferredBuffers[arrayBuffer] = new JSArrayBuffer(clonedBuffer);
        }

        return transferredBuffers;
    }

    private static void DetachTransferredArrayBuffers(Dictionary<JSValue, JSValue> transferredBuffers)
    {
        foreach (var source in transferredBuffers.Keys)
        {
            if (source is not JSArrayBuffer arrayBuffer)
                continue;

            arrayBuffer.InvokeMethod((KeyString)"transfer", new Arguments(arrayBuffer));
        }
    }

    private static JSValue StructuredCloneValue(JSValue value, Dictionary<JSValue, JSValue> seen)
    {
        // Primitives are returned as-is.
        if (value == null || value.IsNullOrUndefined)
            return value;

        if (value.IsNumber || value.IsString || value.IsBoolean)
            return value;

        if (value.TypeOf() == JSConstants.BigInt)
            return value;

        // Functions cannot be cloned.
        if (value is JSFunction)
            throw JSEngine.NewTypeError("structuredClone: function values cannot be cloned");

        // Check for circular references.
        if (seen.TryGetValue(value, out var existing))
            return existing;

        // RegExp
        if (value is JSRegExp regex)
        {
            var clone = new JSRegExp(regex.pattern, regex.flags);
            seen[value] = clone;
            return clone;
        }

        // Map, Set, ArrayBuffer, and other satellite assembly types
        var extResult = DefaultBuiltInRegistry.StructuredCloneExtension?.Invoke(value, seen, StructuredCloneValue);
        if (extResult != null) return extResult;

        // Error
        if (value is JSError error)
        {
            var clone = new JSError(new Arguments(JSUndefined.Value, JSValue.CreateString(error.Message)));
            seen[value] = clone;
            return clone;
        }

        // Array
        if (value.IsArray)
        {
            var clone = JSValue.CreateArray();
            seen[value] = clone;
            var en = value.GetElementEnumerator();
            
            while (en.MoveNext(out var hasValue, out var item, out var _))
            {
                if (!hasValue)
                    continue;

                clone.AddArrayItem(StructuredCloneValue(item, seen));
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

                clone[key.Value] = StructuredCloneValue((JSValue)prop.value, seen);
            }

            return clone;
        }

        // Fallback: return value as-is for unknown types.
        return value;
    }
}
