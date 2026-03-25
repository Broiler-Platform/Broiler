using System.Text.RegularExpressions;

namespace Broiler.JavaScript.Runtime;

/// <summary>
/// Abstraction over JavaScript RegExp objects, allowing assemblies
/// to detect and inspect regular expressions without depending on the
/// concrete <c>JSRegExp</c> class in BuiltIns.
/// </summary>
public interface IJSRegExp
{
    /// <summary>Gets the regular expression pattern string.</summary>
    string Pattern { get; }

    /// <summary>Gets the flags string (e.g. "gi").</summary>
    string Flags { get; }

    /// <summary>Gets the underlying .NET <see cref="Regex"/> instance.</summary>
    Regex Value { get; }
}
