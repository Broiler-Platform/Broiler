#nullable enable
using Broiler.JavaScript.Ast.Misc;
using System.Text;

// Moved from Broiler.JavaScript.Core to Broiler.JavaScript.Extensions.
// Rationale: StringSpanExtensions provides utility extension methods for
// StringSpan (which lives in the Ast assembly) and logically belongs with
// the other extension helper classes in the Extensions assembly.
// Namespace preserved for binary compatibility.

namespace Broiler.JavaScript.Extensions;

public static class StringSpanExtensions
{
    public static StringSpan ToStringSpan(this string text, int offset, int length) => new(text, offset, length);

    public unsafe static void Append(this StringBuilder sb, in StringSpan span)
    {
        fixed (char* start = span.Source)
        {
            char* ch1 = start + span.Offset;
            sb.Append(ch1, span.Length);
        }
    }

    public static string ToSnakeCase(this StringSpan text, string prefix = "")
    {
        var sb = new StringBuilder(text.Length + prefix.Length);
        if (prefix.Length > 0) {
            sb.Append(prefix);
        }
        foreach(var ch in text)
        {
            if (char.IsUpper(ch))
            {
                sb.Append('-');
                sb.Append(char.ToLower(ch));
                continue;
            }
            sb.Append(ch);
        }
        return sb.ToString();
    }
}
