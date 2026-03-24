using System.Text.RegularExpressions;

namespace Broiler.JavaScript.Parser;

/// <summary>
/// Lightweight regex validation for the lexer.
/// Only checks if the pattern/flags are syntactically valid — full JS-to-.NET
/// regex translation is handled by JSRegExp in Broiler.JavaScript.Core.
/// </summary>
internal static class RegExpValidator
{
    /// <summary>
    /// Returns true when the <paramref name="pattern"/> with the given
    /// <paramref name="flags"/> can be compiled into a .NET Regex.
    /// </summary>
    internal static bool IsValid(string pattern, string flags)
    {
        try
        {
            var options = RegexOptions.None;
            if (flags != null)
            {
                foreach (var ch in flags)
                {
                    switch (ch)
                    {
                        case 'i': options |= RegexOptions.IgnoreCase; break;
                        case 'm': options |= RegexOptions.Multiline; break;
                        case 's': options |= RegexOptions.Singleline; break;
                    }
                }
            }

            _ = new Regex(pattern, options);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
