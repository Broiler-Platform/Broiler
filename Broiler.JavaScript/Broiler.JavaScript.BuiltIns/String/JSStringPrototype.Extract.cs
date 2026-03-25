using System;
using System.Text;
using Broiler.JavaScript.ExpressionCompiler;
using Broiler.JavaScript.BuiltIns.Generator;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.BuiltIns.String;

public partial class JSString
{
    [JSPrototypeMethod]
    [JSExport("charAt", Length = 1)]
    public static JSValue CharAt(in Arguments a)
    {
        var text = a.This.AsString();
        var pos = a[0]?.IntegerValue ?? 0;

        if (pos < 0 || pos >= text.Length)
            return Empty;

        return new JSString(new string(text[pos], 1));
    }

    [JSPrototypeMethod]
    [JSExport("substring", Length = 2)]
    public static JSValue Substring(in Arguments a)
    {
        var @this = a.This.AsString();
        var start = a[0]?.IntegerValue ?? 0;
        var end = a.TryGetAt(1, out var v) ? (v.IsUndefined ? int.MaxValue : v.IntegerValue) : int.MaxValue;

        var si = Math.Max(Math.Min(start, end), 0);
        var ei = Math.Max(Math.Max(start, end), 0);

        if (si < 0)
            si += @this.Length;

        if (ei < 0)
            ei += @this.Length;

        si = Math.Min(Math.Max(si, 0), @this.Length);
        ei = Math.Min(Math.Max(ei, 0), @this.Length);

        if (ei <= si)
            return Empty;

        return new JSString(@this.Substring(si, ei - si));
    }

    [JSPrototypeMethod]
    [JSExport("substr")]
    public static JSValue Substr(in Arguments a)
    {
        var @this = a.This.AsString();
        var start = a[0]?.IntegerValue ?? 0;
        var length = a.TryGetAt(1, out var v) ? (v.IsUndefined ? @this.Length : Math.Max(v.IntegerValue, 0)) : @this.Length;

        // Per ECMAScript spec: if start is negative, use max(length + start, 0)
        if (start < 0)
            start = Math.Max(@this.Length + start, 0);
        else
            start = Math.Min(start, @this.Length);

        var count = Math.Min(length, @this.Length - start);
        
        if (count <= 0)
            return Empty;
        
        return new JSString(@this.Substring(start, count));
    }

    [JSPrototypeMethod]
    [JSExport("toString")]
    public static JSValue ToString(in Arguments a) => a.This.AsJSString();

    [Symbol("@@iterator")]
    public static JSValue Iterator(in Arguments a) => new JSGenerator(a.This.GetElementEnumerator(), "Array Iterator");

    [JSPrototypeMethod]
    [JSExport("charCodeAt", Length = 1)]
    internal static JSValue CharCodeAt(in Arguments a)
    {
        var text = a.This.AsString();
        var pos = a[0]?.IntegerValue ?? 0;

        if (pos < 0 || pos >= text.Length)
            return JSValue.NumberNaN;

        return JSValue.CreateNumber(text[pos]);
    }

    [JSPrototypeMethod]
    [JSExport("codePointAt", Length = 1)]
    internal static JSValue CodePointAt(in Arguments a)
    {
        var text = a.This.AsString();
        var pos = a[0]?.IntegerValue ?? 0;

        if (pos < 0 || pos >= text.Length)
            return JSValue.NumberNaN;

        int firstCodePoint = text[pos];
        if (firstCodePoint < 0xD800 || firstCodePoint > 0xDBFF || pos + 1 == text.Length)
            return JSValue.CreateNumber(firstCodePoint);

        int secondCodePoint = text[pos + 1];
        if (secondCodePoint < 0xDC00 || secondCodePoint > 0xDFFF)
            return JSValue.CreateNumber(firstCodePoint);

        var output = (double)((firstCodePoint - 0xD800) * 1024 + (secondCodePoint - 0xDC00) + 0x10000);
        return JSValue.CreateNumber(output);

    }

    [JSPrototypeMethod]
    [JSExport("concat", Length = 1)]
    internal static JSValue Concat(in Arguments a)
    {
        var @this = a.This.AsString();
        if (a.Length == 0)
            return a.This;

        StringBuilder sb = new();
        sb.Append(@this);

        for (int i = 0; i < a.Length; i++)
            sb.Append(a.GetAt(i));

        return new JSString(sb.ToString());
    }

    [JSPrototypeMethod]
    [JSExport("slice", Length = 2)]
    internal static JSValue Slice(in Arguments a)
    {
        var @this = a.This.AsString();

        //0th argument, start
        var f = a.Get1();
        var start = f.IntegerValue;
        //1st argument, end
        int end = a[1]?.IntegerValue ?? int.MaxValue;

        if (start < 0)
            start += @this.Length;

        if (end < 0)
            end += @this.Length;

        start = Math.Min(Math.Max(start, 0), @this.Length);
        end = Math.Min(Math.Max(end, 0), @this.Length);

        if (end <= start)
            return Empty;

        var result = @this.Substring(start, end - start);
        return new JSString(result);
    }

    [JSPrototypeMethod]
    [JSExport("valueOf")]
    internal static JSValue ValueOf(in Arguments a) => a.This.AsJSString();
}
