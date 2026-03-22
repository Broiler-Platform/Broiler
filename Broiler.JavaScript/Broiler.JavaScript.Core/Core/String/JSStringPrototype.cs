using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Clr;
using Broiler.JavaScript.Core.Core.Primitive;
using Broiler.JavaScript.Core.Core.String;
using System;
using System.Globalization;
using System.Text;
using Broiler.JavaScript.Core.Core.Boolean;
using Broiler.JavaScript.ExpressionCompiler;
using Broiler.JavaScript.Core.Core.Generator;
using Broiler.JavaScript.Core.Core.Array;
using Broiler.JavaScript.Ast.Misc;

namespace Broiler.JavaScript.Core;

public partial class JSString
{
    [JSExport(Length = 1, IsConstructor = true)]
    public static JSValue Constructor(in Arguments a)
    {
        if (a.Length == 0)
            return new JSPrimitiveObject(new JSString(StringSpan.Empty));

        return new JSPrimitiveObject(new JSString(a.Get1().ToString()));
    }

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
            return JSNumber.NaN;

        return new JSNumber(text[pos]);
    }

    [JSPrototypeMethod]
    [JSExport("codePointAt", Length = 1)]
    internal static JSValue CodePointAt(in Arguments a)
    {
        var text = a.This.AsString();
        var pos = a[0]?.IntegerValue ?? 0;

        if (pos < 0 || pos >= text.Length)
            return JSNumber.NaN;

        int firstCodePoint = text[pos];
        if (firstCodePoint < 0xD800 || firstCodePoint > 0xDBFF || pos + 1 == text.Length)
            return new JSNumber(firstCodePoint);

        int secondCodePoint = text[pos + 1];
        if (secondCodePoint < 0xDC00 || secondCodePoint > 0xDFFF)
            return new JSNumber(firstCodePoint);

        var output = (double)((firstCodePoint - 0xD800) * 1024 + (secondCodePoint - 0xDC00) + 0x10000);
        return new JSNumber(output);

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
    [JSExport("match", Length = 1)]
    internal static JSValue Match(in Arguments a)
    {
        var @this = a.This;
        if (@this.IsNullOrUndefined)
            throw JSContext.NewTypeError("String.prototype.match called on null or undefined");
        
        var reg = a.Get1();
        if (reg is JSRegExp jSRegExp)
            return jSRegExp.Match(@this);

        var pattern = reg.IsNullOrUndefined ? "" : reg.ToString();
        return new JSRegExp(pattern, "").Match(@this);
    }

    [JSPrototypeMethod]
    [JSExport("normalize")]
    internal static JSValue Normalize(in Arguments a)
    {
        var @this = a.This.AsString();
        var input = a.Get1();

        string form = input.IsNullOrUndefined ? "NFC" : input.ToString();

        return form switch
        {
            "NFC" => new JSString(@this.Normalize(NormalizationForm.FormC)),
            "NFD" => new JSString(@this.Normalize(NormalizationForm.FormD)),
            "NFKC" => new JSString(@this.Normalize(NormalizationForm.FormKC)),
            "NFKD" => new JSString(@this.Normalize(NormalizationForm.FormKD)),
            _ => throw JSContext.NewRangeError($"The normalization form should be one of NFC, NFD, NFKC, NFKD."),
        };
    }

    [JSPrototypeMethod]
    [JSExport("padEnd")]
    internal static JSValue PadEnd(in Arguments a)
    {
        var @this = a.This.AsString();
        var (s, c) = a.Get2();
        var size = s.IntValue;
        var ch = c.ToString().ToCharArray()[0];

        return new JSString(@this.PadRight(s.IntValue, ch));
    }

    [JSPrototypeMethod]
    [JSExport("padStart")]
    internal static JSValue PadStart(in Arguments a)
    {
        var @this = a.This.AsString();
        var (s, c) = a.Get2();
        var ch = c.ToString().ToCharArray()[0];

        return new JSString(@this.PadLeft(s.IntValue, ch));
    }

    [JSPrototypeMethod]
    [JSExport("repeat", Length = 1)]
    internal static JSValue Repeat(in Arguments a)
    {
        var @this = a.This.AsString();
        var c = a[0]?.IntegerValue ?? int.MaxValue;
        
        if (c < 0 || c == int.MaxValue)
            throw JSContext.NewRangeError($"Invalid count value");
        
        var result = new StringBuilder(c * @this.Length);
        for (var i = 0; i < c; i++)
            result.Append(@this);

        return new JSString(result.ToString());

    }

    [JSPrototypeMethod]
    [JSExport("replace", Length = 2)]
    internal static JSValue Replace(in Arguments a)
    {
        var @this = a.This.AsString();
        var (f, s) = a.Get2();
        if (f is JSRegExp jSRegExp)
            return new JSString(jSRegExp.Replace(@this, s));

        // Find the first occurrance of substr.
        var substr = f.ToString();
        var replaceText = s.IsFunction ? s.InvokeFunction(Arguments.Empty).ToString() : s.ToString();
        int start = @this.IndexOf(substr, StringComparison.Ordinal);
        if (start == -1)
            return a.This;

        int end = start + substr.Length;

        // Replace only the first match.
        var result = new StringBuilder(@this.Length + (replaceText.Length - substr.Length));
        result.Append(@this, 0, start);
        result.Append(replaceText);
        result.Append(@this, end, @this.Length - end);
        return new JSString(result.ToString());
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

    /// <summary>
    /// Splits this string into an array of strings by separating the string into substrings.
    /// </summary>
    /// <param name="engine"> The current script environment. </param>
    /// <param name="thisObject"> The string that is being operated on. </param>
    /// <param name="separator"> A string or regular expression that indicates where to split the string. </param>
    /// <param name="limit"> The maximum number of array items to return.  Defaults to unlimited. </param>
    /// <returns> An array containing the split strings. </returns>
    [JSPrototypeMethod]
    [JSExport("split", Length = 2)]
    internal static JSValue Split(in Arguments a)
    {
        var @this = a.This.AsString();
        var (_separator, limit) = a.Get2();
        // Limit defaults to unlimited.  Note the ToUint32() conversion.
        var limitMax = uint.MaxValue;

        if (!limit.IsUndefined)
            limitMax = limit.UIntValue;

        if (_separator is JSRegExp jSRegExp)
            return jSRegExp.Split(@this, limitMax);

        var separator = _separator.ToString();
        var result = new JSArray();
        if (string.IsNullOrEmpty(separator))
        {
            for (int i = 0; i < @this.Length; i++)
                result[(uint)i] = new JSString(@this[i]);

            return result;
        }

        // .NET Split is buggy, it should not remove empty string entries
        // when StringSplitOptions is None
        var splitStrings = @this.Split([separator], StringSplitOptions.None);
        if (limitMax < splitStrings.Length)
        {
            var splitStrings2 = new string[limitMax];
            Array.Copy(splitStrings, splitStrings2, (int)limitMax);
            splitStrings = splitStrings2;
        }

        foreach (var item in splitStrings)
            result.Add(new JSString(item));

        return result;
    }

    [JSPrototypeMethod]
    [JSExport("toLocaleLowerCase")]
    internal static JSValue ToLocaleLowerCase(in Arguments a)
    {
        var @this = a.This.AsString();
        var locale = a.Get1();

        try
        {
            CultureInfo culture = locale.IsNullOrUndefined ? CultureInfo.CurrentCulture : CultureInfo.GetCultureInfo(locale.ToString());
            return new JSString(@this.ToLower(culture));
        }
        catch (CultureNotFoundException)
        {
            throw JSContext.NewRangeError($"Incorrect locale information provided");
        }
    }

    [JSPrototypeMethod]
    [JSExport("toLocaleUpperCase")]
    internal static JSValue ToLocaleUpperCase(in Arguments a)
    {
        var @this = a.This.AsString();
        var locale = a.Get1();

        try
        {
            CultureInfo culture = locale.IsNullOrUndefined ? CultureInfo.CurrentCulture : CultureInfo.GetCultureInfo(locale.ToString());
            return new JSString(@this.ToUpper(culture));
        }
        catch (CultureNotFoundException)
        {
            throw JSContext.NewRangeError($"Incorrect locale information provided");
        }
    }

    [JSPrototypeMethod]
    [JSExport("toLowerCase")]
    internal static JSValue ToLowerCase(in Arguments a)
    {
        var @this = a.This.AsString();
        return new JSString(@this.ToLowerInvariant());
    }

    [JSPrototypeMethod]
    [JSExport("toUpperCase")]
    internal static JSValue ToUpperCase(in Arguments a)
    {
        var @this = a.This.AsString();
        return new JSString(@this.ToUpperInvariant());
    }

    private static readonly char[] trimCharacters = [
        // Whitespace
        '\x09', '\x0B', '\x0C', '\x20', '\xA0', '\xFEFF',

        // Unicode space separator
        '\u1680', '\u180E', '\u2000', '\u2001',
        '\u2002', '\u2003', '\u2004', '\u2005',
        '\u2006', '\u2007', '\u2008', '\u2009',
        '\u200A', '\u202F', '\u205F', '\u3000', 

        // Line terminators
        '\x0A', '\x0D', '\u2028', '\u2029',
    ];

    [JSPrototypeMethod]
    [JSExport("trim")]
    internal static JSValue Trim(in Arguments a)
    {
        var @this = a.This.AsString();
        return new JSString(@this.Trim(trimCharacters));
    }

    [JSPrototypeMethod]
    [JSExport("trimEnd")]
    internal static JSValue TrimEnd(in Arguments a)
    {
        var @this = a.This.AsString();
        return new JSString(@this.TrimEnd(trimCharacters));
    }

    [JSPrototypeMethod]
    [JSExport("trimStart")]
    internal static JSValue TrimStart(in Arguments a)
    {
        var @this = a.This.AsString();
        return new JSString(@this.TrimStart(trimCharacters));
    }

    [JSPrototypeMethod]
    [JSExport("valueOf")]
    internal static JSValue ValueOf(in Arguments a) => a.This.AsJSString();
}
