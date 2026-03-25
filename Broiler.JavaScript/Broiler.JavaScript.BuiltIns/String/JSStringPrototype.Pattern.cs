using Broiler.JavaScript.BuiltIns.RegExp;
using System;
using System.Text;
using Broiler.JavaScript.ExpressionCompiler;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine.Core;

namespace Broiler.JavaScript.BuiltIns.String;

public partial class JSString
{
    [JSPrototypeMethod]
    [JSExport("match", Length = 1)]
    internal static JSValue Match(in Arguments a)
    {
        var @this = a.This;
        if (@this.IsNullOrUndefined)
            throw JSEngine.NewTypeError("String.prototype.match called on null or undefined");
        
        var reg = a.Get1();
        if (reg is JSRegExp jSRegExp)
            return jSRegExp.Match(@this);

        var pattern = reg.IsNullOrUndefined ? "" : reg.ToString();
        return new JSRegExp(pattern, "").Match(@this);
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
        var result = JSValue.CreateArray();
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
            System.Array.Copy(splitStrings, splitStrings2, (int)limitMax);
            splitStrings = splitStrings2;
        }

        foreach (var item in splitStrings)
            result.AddArrayItem(new JSString(item));

        return result;
    }
}
