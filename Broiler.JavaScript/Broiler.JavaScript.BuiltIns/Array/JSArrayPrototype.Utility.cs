using System;
using System.Text;
using System.Globalization;
using Broiler.JavaScript.ExpressionCompiler;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine.Extensions;
using Broiler.JavaScript.Engine.Core;

namespace Broiler.JavaScript.BuiltIns.Array;

public partial class JSArray
{
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
    [JSExport("toLocaleString", Length = 0)]
    internal static JSValue ToLocaleString(in Arguments a)
    {
        var @this = a.This as JSArray;
        var (locale, format) = a.Get2();
        StringBuilder sb = new();

        var def = "N0";

        string strFormat = format.IsNullOrUndefined ? def : (format.IsString ? format.ToString() :
            throw JSEngine.NewTypeError("Options not supported, use .Net String Formats")
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

}
