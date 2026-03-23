using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Clr;
using Broiler.JavaScript.Core.Core.Primitive;
using Broiler.JavaScript.Core.Core.String;
using System;
using System.Globalization;
using Broiler.JavaScript.Core.Core.Boolean;
using Broiler.JavaScript.ExpressionCompiler;

namespace Broiler.JavaScript.Core;

public partial class JSString
{
    [JSPrototypeMethod]
    [JSExport("contains", Length = 1)]
    internal static JSValue Contains(in Arguments a)
    {
        var @this = a.This.AsString();
        var arg = a.Get1().ToString();
        int position = a.GetIntAt(1, 0);

        position = Math.Min(Math.Max(0, position), @this.Length);

        if (@this.IndexOf(arg, position) >= 0)
            return JSBoolean.True;

        return JSBoolean.False;
    }

    [JSPrototypeMethod]
    [JSExport("endsWith", Length = 1)]
    internal static JSValue EndsWith(in Arguments a)
    {
        var @this = a.This.AsString();
        var f = a.Get1();

        if (f is JSRegExp)
            throw JSContext.NewTypeError("Substring argument must not be a regular expression.");

        var endPosition = a[1]?.IntegerValue ?? int.MaxValue;
        var fs = f.ToString();

        if (endPosition == int.MaxValue)
            return @this.EndsWith(fs) ? JSBoolean.True : JSBoolean.False;

        endPosition = Math.Min(Math.Max(0, endPosition), @this.Length);

        if (fs.Length > endPosition)
            return JSBoolean.False;

        if (string.Compare(@this, endPosition - fs.Length, fs, 0, fs.Length) == 0)
            return JSBoolean.True;

        return JSBoolean.False;
    }

    [JSPrototypeMethod]
    [JSExport("startsWith", Length = 1)]
    internal static JSValue StartsWith(in Arguments a)
    {
        var @this = a.This.AsString();
        var searchStr = a[0] ?? JSUndefined.Value;
        var pos = a[1]?.IntegerValue ?? 0;

        if (searchStr is JSRegExp)
            throw JSContext.NewTypeError("Substring argument must not be a regular expression.");

        var search = searchStr.ToString();
        if (pos == 0)
            return @this.StartsWith(search) ? JSBoolean.True : JSBoolean.False;

        pos = Math.Min(Math.Max(0, pos), @this.Length);
        if (pos + search.Length > @this.Length)
            return JSBoolean.False;

        int index = @this.IndexOf(search);
        if (index == pos)
            return JSBoolean.True;

        return JSBoolean.False;
    }

    [JSPrototypeMethod]
    [JSExport("includes", Length = 1)]
    internal static JSValue Includes(in Arguments a)
    {
        var @this = a.This.AsString();
        var searchStr = a[0] ?? JSUndefined.Value;
        var pos = a[1]?.IntegerValue ?? 0;

        if (searchStr is JSRegExp)
            throw JSContext.NewTypeError("Substring argument must not be a regular expression.");

        pos = Math.Min(Math.Max(pos, 0), @this.Length);
        return @this.IndexOf(searchStr.ToString(), pos) >= 0 ? JSBoolean.True : JSBoolean.False;
    }

    [JSPrototypeMethod]
    [JSExport("indexOf", Length = 1)]
    internal static JSValue IndexOf(in Arguments a)
    {
        var @this = a.This.AsString();
        var searchStr = a[0] ?? JSUndefined.Value;
        var pos = a[1]?.IntegerValue ?? 0;

        pos = Math.Min(Math.Max(pos, 0), @this.Length);

        var index = @this.IndexOf(searchStr.ToString(), pos);
        return new JSNumber(index);
    }

    [JSPrototypeMethod]
    [JSExport("lastIndexOf", Length = 1)]
    internal static JSValue LastIndexOF(in Arguments a)
    {
        var @this = a.This.AsString();
        var searchStr = a[0] ?? JSUndefined.Value;
        var fromIndex = a[1]?.DoubleValue ?? int.MaxValue;
        var startIndex = double.IsNaN(fromIndex) ? int.MaxValue : (int)(((long)fromIndex << 32) >> 32);

        startIndex = Math.Min(startIndex, @this.Length - 1);
        startIndex = Math.Min(startIndex + searchStr.Length - 1, @this.Length - 1);

        if (startIndex < 0)
        {
            if (@this == string.Empty && searchStr.Length == 0)
                return JSNumber.Zero;

            return JSNumber.MinusOne;
        }

        return new JSNumber(@this.LastIndexOf(searchStr.ToString(), startIndex, StringComparison.Ordinal));
    }

    [JSPrototypeMethod]
    [JSExport("localeCompare", Length = 1)]
    internal static JSValue LocaleCompare(in Arguments a)
    {
        var @this = a.This;
        if (@this.IsNullOrUndefined)
            throw JSContext.NewTypeError("String.prototype.localeCompare called on null or undefined");

        var (compareString, locale, options) = a.Get3();
        var str = compareString.ToString();

        CultureInfo culture = locale.IsNullOrUndefined ? CultureInfo.CurrentCulture : CultureInfo.GetCultureInfo(locale.ToString());

        return new JSNumber(string.Compare(@this.ToString(), str, culture, 0));
    }

    [JSPrototypeMethod]
    [JSExport("search", Length = 1)]
    internal static JSValue Search(in Arguments a)
    {
        var @this = a.This.AsString();
        var search = a.Get1();

        //search string not defined
        if (search.IsUndefined)
            return JSNumber.Zero;

        // is Regex?
        if (search is JSRegExp jSRegExp)
        {
            var reg = jSRegExp.value.Match(@this);

            if (!reg.Success)
                return JSNumber.MinusOne;
            return new JSNumber(reg.Index);
        }

        //is String
        var index = @this.IndexOf(search.ToString());
        return new JSNumber(index);
    }
}
