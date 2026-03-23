using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Clr;
using Broiler.JavaScript.Core;
using System.Globalization;
using System.Text;
using Broiler.JavaScript.ExpressionCompiler;

namespace Broiler.JavaScript.BuiltIns.String;

public partial class JSString
{
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
}
