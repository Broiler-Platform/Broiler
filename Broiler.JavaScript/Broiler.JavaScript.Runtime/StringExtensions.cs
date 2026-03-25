using System;
using System.Runtime.CompilerServices;

namespace Broiler.JavaScript.Runtime;

public static class StringExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEmpty(this string str) => string.IsNullOrEmpty(str);

    public static bool Greater(this string left, string right) => string.CompareOrdinal(left, right) > 0;

    public static bool GreaterOrEqual(this string left, string right) => string.CompareOrdinal(left, right) >= 0;


    public static bool Less(this string left, string right) => string.CompareOrdinal(left, right) < 0;

    public static bool LessOrEqual(this string left, string right) => string.CompareOrdinal(left, right) <= 0;


    public static string ToCamelCase(this string text)
    {
        int i = 0;
        foreach (char ch in text)
        {
            if (char.IsUpper(ch))
            {
                i++;
                continue;
            }

            break;
        }

        return string.Concat(text.Substring(0, i).ToLower(), text.AsSpan(i));
    }
}
