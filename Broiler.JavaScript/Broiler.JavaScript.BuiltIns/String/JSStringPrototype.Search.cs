using Broiler.JavaScript.BuiltIns.RegExp;
using System;
using System.Globalization;
using Broiler.JavaScript.ExpressionCompiler;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine.Core;

namespace Broiler.JavaScript.BuiltIns.String;

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
            return JSValue.BooleanTrue;

        return JSValue.BooleanFalse;
    }

    [JSPrototypeMethod]
    [JSExport("endsWith", Length = 1)]
    internal static JSValue EndsWith(in Arguments a)
    {
        var @this = a.This.AsString();
        var f = a.Get1();

        if (f is JSRegExp)
            throw JSEngine.NewTypeError("Substring argument must not be a regular expression.");

        var endPosition = a[1]?.IntegerValue ?? int.MaxValue;
        var fs = f.ToString();

        if (endPosition == int.MaxValue)
            return @this.EndsWith(fs) ? JSValue.BooleanTrue : JSValue.BooleanFalse;

        endPosition = Math.Min(Math.Max(0, endPosition), @this.Length);

        if (fs.Length > endPosition)
            return JSValue.BooleanFalse;

        if (string.Compare(@this, endPosition - fs.Length, fs, 0, fs.Length) == 0)
            return JSValue.BooleanTrue;

        return JSValue.BooleanFalse;
    }

    [JSPrototypeMethod]
    [JSExport("startsWith", Length = 1)]
    internal static JSValue StartsWith(in Arguments a)
    {
        var @this = a.This.AsString();
        var searchStr = a[0] ?? JSUndefined.Value;
        var pos = a[1]?.IntegerValue ?? 0;

        if (searchStr is JSRegExp)
            throw JSEngine.NewTypeError("Substring argument must not be a regular expression.");

        var search = searchStr.ToString();
        if (pos == 0)
            return @this.StartsWith(search) ? JSValue.BooleanTrue : JSValue.BooleanFalse;

        pos = Math.Min(Math.Max(0, pos), @this.Length);
        if (pos + search.Length > @this.Length)
            return JSValue.BooleanFalse;

        int index = @this.IndexOf(search);
        if (index == pos)
            return JSValue.BooleanTrue;

        return JSValue.BooleanFalse;
    }

    [JSPrototypeMethod]
    [JSExport("includes", Length = 1)]
    internal static JSValue Includes(in Arguments a)
    {
        var @this = a.This.AsString();
        var searchStr = a[0] ?? JSUndefined.Value;
        var pos = a[1]?.IntegerValue ?? 0;

        if (searchStr is JSRegExp)
            throw JSEngine.NewTypeError("Substring argument must not be a regular expression.");

        pos = Math.Min(Math.Max(pos, 0), @this.Length);
        return @this.IndexOf(searchStr.ToString(), pos) >= 0 ? JSValue.BooleanTrue : JSValue.BooleanFalse;
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
        return JSValue.CreateNumber(index);
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
                return JSValue.NumberZero;

            return JSValue.NumberMinusOne;
        }

        return JSValue.CreateNumber(@this.LastIndexOf(searchStr.ToString(), startIndex, StringComparison.Ordinal));
    }

    [JSPrototypeMethod]
    [JSExport("localeCompare", Length = 1)]
    internal static JSValue LocaleCompare(in Arguments a)
    {
        var @this = a.This;
        if (@this.IsNullOrUndefined)
            throw JSEngine.NewTypeError("String.prototype.localeCompare called on null or undefined");

        var (compareString, locale, options) = a.Get3();
        var str = compareString.ToString();

        CultureInfo culture = locale.IsNullOrUndefined ? CultureInfo.CurrentCulture : CultureInfo.GetCultureInfo(locale.ToString());

        return JSValue.CreateNumber(string.Compare(@this.ToString(), str, culture, 0));
    }

    [JSPrototypeMethod]
    [JSExport("search", Length = 1)]
    internal static JSValue Search(in Arguments a)
    {
        var @this = a.This.AsString();
        var search = a.Get1();

        //search string not defined
        if (search.IsUndefined)
            return JSValue.NumberZero;

        // is Regex?
        if (search is JSRegExp jSRegExp)
        {
            var reg = jSRegExp.value.Match(@this);

            if (!reg.Success)
                return JSValue.NumberMinusOne;
            return JSValue.CreateNumber(reg.Index);
        }

        //is String
        var index = @this.IndexOf(search.ToString());
        return JSValue.CreateNumber(index);
    }
}
