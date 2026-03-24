using Broiler.JavaScript.Core.Core.Clr;
using Broiler.JavaScript.Core.Core.Primitive;
using System.Text;
using System.Text.RegularExpressions;
using System;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.ExpressionCompiler;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.Core;


[JSClassGenerator("RegExp")]
public partial class JSRegExp : JSObject
{
    [JSExport("escape")]
    internal static JSValue Escape(in Arguments a)
    {
        var input = a.Get1();
        if (input.IsNullOrUndefined)
            throw JSContext.NewTypeError("RegExp.escape called on null or undefined");

        var str = input.ToString();
        var sb = new StringBuilder(str.Length + 4);

        for (int i = 0; i < str.Length; i++)
        {
            var c = str[i];
            if (c == '^' || c == '$' || c == '\\' || c == '.' || c == '*' ||
                c == '+' || c == '?' || c == '(' || c == ')' || c == '[' ||
                c == ']' || c == '{' || c == '}' || c == '|' || c == '/')
            {
                sb.Append('\\');
            }

            sb.Append(c);
        }

        return JSValue.CreateString(sb.ToString());
    }

    [JSExport("source")]
    public readonly string pattern;

    [JSExport]
    public readonly string flags;

    [JSExport("global")]
    public readonly bool globalSearch;

    [JSExport]
    public readonly bool multiline;
    [JSExport]
    public readonly bool ignoreCase;

    internal Regex value;

    [JSExport]
    public int lastIndex = 0;

    public JSRegExp(in Arguments a) : base(JSContext.NewTargetPrototype)
    {
        var pattern = "";
        var flags = "";

        if (a.Length > 0)
            pattern = a.GetAt(0).ToString();

        if (a.Length > 1)
            flags = a.GetAt(1).ToString();

        this.flags = flags;
        this.pattern = pattern;

        (value, globalSearch, ignoreCase, multiline) = CreateRegex(pattern, flags);
    }

    public JSRegExp(string pattern, string flags) : this()
    {
        this.flags = flags;
        this.pattern = pattern;

        (value, globalSearch, ignoreCase, multiline) = CreateRegex(pattern, flags);
    }

    /// <summary>
    /// Finds all regular expression matches within the given string.
    /// </summary>
    /// <param name="input"> The string on which to perform the search. </param>
    /// <returns> An array containing the matched strings. </returns>
    public JSValue Match(JSValue input)
    {
        // If the global flag is not set, returns a single match.
        if (globalSearch == false)
        {
            var arg = new Arguments(this, input);
            return Exec(arg);
        }

        // Otherwise, find all matches.
        var matches = value.Matches(input.ToString());
        if (matches.Count == 0)
            return JSValue.NullValue;

        // Construct the array to return.
        var matchValues = JSValue.CreateArray((uint)matches.Count);
        for (int i = 0; i < matches.Count; i++)
            matchValues[(uint)i] = JSValue.CreateString(matches[i].Value);

        return matchValues;
    }

    /// <summary>
    /// Splits the given string into an array of strings by separating the string into substrings.
    /// </summary>
    /// <param name="input"> The string to split. </param>
    /// <param name="limit"> The maximum number of array items to return.  Defaults to unlimited. </param>
    /// <returns> An array containing the split strings. </returns>
    public JSValue Split(string input, uint limit = uint.MaxValue)
    {
        // Return an empty array if limit = 0.
        if (limit == 0)
            return JSValue.CreateArray();

        // Find the first match.
        Match match = value.Match(input, 0);


        var results = JSValue.CreateArray();
        int startIndex = 0;
        Match lastMatch = null;

        while (match.Success == true)
        {
            // Do not match the an empty substring at the start or end of the string or at the
            // end of the previous match.
            if (match.Length == 0 && (match.Index == 0 || match.Index == input.Length || match.Index == startIndex))
            {
                // Find the next match.
                match = match.NextMatch();
                continue;
            }

            // Add the match results to the array.
            var element = input.Substring(startIndex, match.Index - startIndex);
            results.AddArrayItem(JSValue.CreateString(element));

            if (results.Length >= limit)
                return results;

            startIndex = match.Index + match.Length;

            for (int i = 1; i < match.Groups.Count; i++)
            {
                var group = match.Groups[i];
                if (group.Captures.Count == 0)
                    results.AddArrayItem(JSUndefined.Value);       // Non-capturing groups return "undefined".
                else
                    results.AddArrayItem(JSValue.CreateString(match.Groups[i].Value));

                if (results.Length >= limit)
                    return results;
            }

            // Record the last match.
            lastMatch = match;

            // Find the next match.
            match = match.NextMatch();
        }
        var ele = input.Substring(startIndex, input.Length - startIndex);
        results.AddArrayItem(JSValue.CreateString(ele));
        return results;
    }

    /// <summary>
    /// Returns a copy of the given string with text replaced using a regular expression.
    /// </summary>
    /// <param name="input"> The string on which to perform the search. </param>
    /// <param name="replaceFunction"> A function that is called to produce the text to replace
    /// for every successful match. </param>
    /// <returns> A copy of the given string with text replaced using a regular expression. </returns>
    public string Replace(string input, JSValue replaceFunction)
    {
        if (!replaceFunction.IsFunction)
            return Replace(input, replaceFunction.ToString());

        return value.Replace(input, match =>
        {
            // Set the deprecated RegExp properties.
            //this.Engine.RegExp.SetDeprecatedProperties(input, match);

            JSValue[] parameters = new JSValue[match.Groups.Count + 2];
            for (int i = 0; i < match.Groups.Count; i++)
            {
                if (match.Groups[i].Success == false)
                    parameters[i] = JSUndefined.Value;
                else
                    parameters[i] = JSValue.CreateString(match.Groups[i].Value);
            }

            parameters[match.Groups.Count] = JSValue.CreateNumber(match.Index);
            parameters[match.Groups.Count + 1] = JSValue.CreateString(input);

            var a = new Arguments(JSValue.NullValue, parameters);
            return replaceFunction.InvokeFunction(a).ToString();
        }, globalSearch == true ? int.MaxValue : 1);
    }

    /// <summary>
    /// Returns a copy of the given string with text replaced using a regular expression.
    /// </summary>
    /// <param name="input"> The string on which to perform the search. </param>
    /// <param name="replaceText"> A string containing the text to replace for every successful match. </param>
    /// <returns> A copy of the given string with text replaced using a regular expression. </returns>
    public string Replace(string input, string replaceText)
    {
        // Check if the replacement string contains any patterns.
        bool replaceTextContainsPattern = replaceText.IndexOf('$') >= 0;

        // Replace the input string with replaceText, recording the last match found.
        Match lastMatch = null;
        string result = value.Replace(input, match =>
        {
            lastMatch = match;

            // If there is no pattern, replace the pattern as is.
            if (replaceTextContainsPattern == false)
                return replaceText;

            // Patterns
            // $$	Inserts a "$".
            // $&	Inserts the matched substring.
            // $`	Inserts the portion of the string that precedes the matched substring.
            // $'	Inserts the portion of the string that follows the matched substring.
            // $n or $nn	Where n or nn are decimal digits, inserts the nth parenthesized submatch string, provided the first argument was a RegExp object.
            var replacementBuilder = new StringBuilder();
            for (int i = 0; i < replaceText.Length; i++)
            {
                char c = replaceText[i];
                if (c == '$' && i < replaceText.Length - 1)
                {
                    c = replaceText[++i];
                    if (c == '$')
                        replacementBuilder.Append('$');
                    else if (c == '&')
                        replacementBuilder.Append(match.Value);
                    else if (c == '`')
                        replacementBuilder.Append(input.AsSpan(0, match.Index));
                    else if (c == '\'')
                        replacementBuilder.Append(input.AsSpan(match.Index + match.Length));
                    else if (c >= '0' && c <= '9')
                    {
                        int matchNumber1 = c - '0';

                        // The match number can be one or two digits long.
                        int matchNumber2 = 0;
                        if (i < replaceText.Length - 1 && replaceText[i + 1] >= '0' && replaceText[i + 1] <= '9')
                            matchNumber2 = matchNumber1 * 10 + (replaceText[i + 1] - '0');

                        // Try the two digit capture first.
                        if (matchNumber2 > 0 && matchNumber2 < match.Groups.Count)
                        {
                            // Two digit capture replacement.
                            replacementBuilder.Append(match.Groups[matchNumber2].Value);
                            i++;
                        }
                        else if (matchNumber1 > 0 && matchNumber1 < match.Groups.Count)
                        {
                            // Single digit capture replacement.
                            replacementBuilder.Append(match.Groups[matchNumber1].Value);
                        }
                        else
                        {
                            // Capture does not exist.
                            replacementBuilder.Append('$');
                            i--;
                        }
                    }
                    else
                    {
                        // Unknown replacement pattern.
                        replacementBuilder.Append('$');
                        replacementBuilder.Append(c);
                    }
                }
                else
                    replacementBuilder.Append(c);
            }

            return replacementBuilder.ToString();
        }, globalSearch == true ? -1 : 1);

        return result;
    }

    /// <summary>
    /// Parses the flags parameter into an enum.
    /// </summary>
    /// <param name="flags"> Available flags, which may be combined, are:
    /// g (global search for all occurrences of pattern)
    /// i (ignore case)
    /// m (multiline search)
    /// s (dotAll – dot matches newlines)
    /// u (unicode)
    /// y (sticky)
    /// v (unicodeSets)
    /// d (hasIndices)</param>
    /// <returns> RegexOptions flags that correspond to the given flags. </returns>
    private static (RegexOptions, bool, bool, bool, bool) ParseFlags(string flags)
    {
        bool globalSearch = false;
        bool ignoreCase = false;
        bool multiline = false;
        bool dotAll = false;

        var options = RegexOptions.ECMAScript;

        if (flags == null)
            return (options, globalSearch, ignoreCase, multiline, dotAll);

        for (int i = 0; i < flags.Length; i++)
        {
            char flag = flags[i];
            if (flag == 'g')
            {
                if (globalSearch == true)
                    throw JSContext.NewSyntaxError("The 'g' flag cannot be specified twice");
                globalSearch = true;
            }
            else if (flag == 'i')
            {
                if ((options & RegexOptions.IgnoreCase) == RegexOptions.IgnoreCase)
                    throw JSContext.NewSyntaxError("The 'i' flag cannot be specified twice");
                options |= RegexOptions.IgnoreCase;
                ignoreCase = true;
            }
            else if (flag == 'm')
            {
                if ((options & RegexOptions.Multiline) == RegexOptions.Multiline)
                    throw JSContext.NewSyntaxError("The 'm' flag cannot be specified twice");
                options |= RegexOptions.Multiline;
                multiline = true;
            }
            else if (flag == 's')
            {
                if (dotAll)
                    throw JSContext.NewSyntaxError("The 's' flag cannot be specified twice");
                dotAll = true;
                // Singleline makes . match \n as well.
                // We remove ECMAScript mode because it does not support Singleline.
                options &= ~RegexOptions.ECMAScript;
                options |= RegexOptions.Singleline;
            }
            else if (flag == 'u' || flag == 'v' || flag == 'y' || flag == 'd')
            {
                // Accepted but not enforced in this implementation.
            }
            else
            {
                throw JSContext.NewSyntaxError($"Unknown flag {flag}");
            }
        }

        return (options, globalSearch, ignoreCase, multiline, dotAll);
    }

    /// <summary>
    /// Creates a .NET Regex object using the given pattern and options.
    /// Supports ES2025 inline pattern modifiers (§2.6) and duplicate
    /// named capturing groups (§2.7).
    /// </summary>
    public static (Regex, bool, bool, bool) CreateRegex(string pattern, string flags)
    {
        try
        {
            var (options, globalSearch, ignoreCase, multiline, dotAll) = ParseFlags(flags);

            // BROILER-PATCH: Transform ES3 empty character classes and forward backreferences
            // for .NET compatibility (tests 89, 90)
            pattern = TransformES3Patterns(pattern);

            // §2.6 — Detect inline pattern modifiers (?i:...) / (?-i:...) / (?ims:...) etc.
            // .NET ECMAScript mode does not support them, so switch to default mode.
            if ((options & RegexOptions.ECMAScript) != 0 && HasInlineModifiers(pattern))
                options &= ~RegexOptions.ECMAScript;

            // §2.7 — Detect duplicate named capturing groups.
            // .NET ECMAScript mode does not allow them; default mode does.
            if ((options & RegexOptions.ECMAScript) != 0 && HasDuplicateNamedGroups(pattern))
                options &= ~RegexOptions.ECMAScript;

            if ((options & RegexOptions.Multiline) == RegexOptions.Multiline)
            {
                // In the .NET Regex implementation with multiline mode:
                // '.' matches any character except \n
                // '^' matches the start of the string or \n (positive lookbehind)
                // '$' matches the end of the string or \n (positive lookahead)
                // In Javascript, we want all three characters to also match \r in the same way they match \n.

                StringBuilder builder = null;
                int start = 0, end = -1;
                while (end < pattern.Length)
                {
                    end = pattern.IndexOfAny(['.', '^', '$', '\\'], end + 1);
                    if (end == -1)
                        break;
                    
                    builder ??= new StringBuilder();
                    builder.Append(pattern.AsSpan(start, end - start));
                    
                    start = end + 1;
                    switch (pattern[end])
                    {
                        case '.':
                            builder.Append(@"[^\r\n]");
                            break;

                        case '^':
                            // [^abc] is a thing. The ^ does NOT match the start of the line in this case.
                            if (end > 0 && pattern[end - 1] == '[')
                                builder.Append('^');
                            else
                                builder.Append(@"(?<=^|\r)");
                            break;

                        case '$':
                            builder.Append(@"(?=$|\r)");
                            break;

                        case '\\':
                            // $ is an anchor. \$ matches the literal dollar sign. \\$ is a backslash then an anchor.
                            if (end < pattern.Length - 1)
                            {
                                builder.Append(pattern[end]);
                                builder.Append(pattern[end + 1]);
                                start++;
                                end++;
                            }
                            break;
                    }
                }

                if (builder != null)
                {
                    builder.Append(pattern.AsSpan(start));
                    pattern = builder.ToString();
                }
            }

            return (new Regex(pattern, options), globalSearch, ignoreCase, multiline);
        }
        catch
        {
            return (null, false, false, false);
        }
    }

    /// <summary>
    /// Returns <c>true</c> when the pattern contains inline modifier
    /// groups such as <c>(?i:...)</c>, <c>(?-m:...)</c>, <c>(?si:...)</c>.
    /// These are not supported in .NET ECMAScript mode.
    /// </summary>
    private static bool HasInlineModifiers(string pattern)
    {
        // Look for (?[imsx-]+: which is the inline-modifier syntax.
        for (int i = 0; i < pattern.Length - 3; i++)
        {
            if (pattern[i] == '(' && pattern[i + 1] == '?')
            {
                int j = i + 2;
                // Skip valid modifier characters
                while (j < pattern.Length && (pattern[j] == 'i' || pattern[j] == 'm' ||
                       pattern[j] == 's' || pattern[j] == 'x' || pattern[j] == '-'))
                {
                    j++;
                }

                // If followed by ':' and we consumed at least one modifier char, it's an inline modifier
                if (j > i + 2 && j < pattern.Length && pattern[j] == ':')
                    return true;
            }

            // Skip escaped characters
            if (pattern[i] == '\\' && i + 1 < pattern.Length)
                i++;
        }

        return false;
    }

    /// <summary>
    /// Returns <c>true</c> when the pattern contains more than one named
    /// capturing group with the same name (ES2025 §2.7).
    /// </summary>
    private static bool HasDuplicateNamedGroups(string pattern)
    {
        var names = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < pattern.Length - 3; i++)
        {
            if (pattern[i] == '(' && i + 2 < pattern.Length &&
                pattern[i + 1] == '?' && pattern[i + 2] == '<' &&
                (i + 3 >= pattern.Length || (pattern[i + 3] != '=' && pattern[i + 3] != '!')))
            {
                // Extract the group name
                int start = i + 3;
                int end = pattern.IndexOf('>', start);
                if (end > start)
                {
                    var name = pattern.Substring(start, end - start);
                    if (!names.Add(name))
                        return true;
                    i = end;
                }
            }

            // Skip escaped characters
            if (pattern[i] == '\\' && i + 1 < pattern.Length)
                i++;
        }

        return false;
    }

    // BROILER-PATCH: Transform ES3-specific regex patterns for .NET compatibility
    // Handles empty character classes, forward backreferences, and NUL escapes.
    private static string TransformES3Patterns(string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
            return pattern;

        // Pass 1: Count total capturing groups for forward backreference detection
        int totalGroups = CountCapturingGroups(pattern);

        var sb = new StringBuilder(pattern.Length + 8);
        bool inClass = false;
        int groupsSeen = 0;

        for (int i = 0; i < pattern.Length; i++)
        {
            char c = pattern[i];

            if (c == '\\' && i + 1 < pattern.Length)
            {
                char next = pattern[i + 1];
                if (!inClass && next >= '1' && next <= '9')
                {
                    // Backreference \N — check if it's a forward reference
                    int refNum = 0;
                    int j = i + 1;
                    
                    while (j < pattern.Length && pattern[j] >= '0' && pattern[j] <= '9')
                    {
                        refNum = refNum * 10 + (pattern[j] - '0');
                        j++;
                    }
                    
                    if (refNum > totalGroups)
                    {
                        // Reference to non-existent group — treat as empty match
                        sb.Append("(?:)");
                        i = j - 1;
                        continue;
                    }
                    
                    if (refNum > groupsSeen)
                    {
                        // Forward reference to not-yet-captured group — matches empty string per ES3
                        sb.Append("(?:)");
                        i = j - 1;
                        continue;
                    }
                    
                    // Normal backreference — pass through
                    sb.Append(pattern, i, j - i);
                    i = j - 1;
                    
                    continue;
                }
                
                if (next == '0')
                {
                    // \0 — NUL escape. Check if followed by an octal digit.
                    if (i + 2 < pattern.Length && pattern[i + 2] >= '0' && pattern[i + 2] <= '7')
                    {
                        // \0N — octal escape, pass through to .NET
                        sb.Append(c);
                        continue;
                    }

                    // \0 alone — NUL character. Use \x00 for .NET compatibility.
                    sb.Append("\\x00");
                    i++; // skip the '0'
                    continue;
                }

                // Other escapes — pass through
                sb.Append(c);
                sb.Append(next);
                i++;
                continue;
            }

            if (inClass)
            {
                if (c == ']')
                    inClass = false;

                sb.Append(c);
                continue;
            }

            if (c == '[')
            {
                // Check for ES3 empty character class [] or [^]
                if (i + 1 < pattern.Length && pattern[i + 1] == ']')
                {
                    // [] — empty character class, matches nothing
                    sb.Append("[^\\s\\S]");
                    i++; // skip ']'
                    continue;
                }

                if (i + 2 < pattern.Length && pattern[i + 1] == '^' && pattern[i + 2] == ']')
                {
                    // [^] — complement of empty class, matches any character
                    sb.Append("[\\s\\S]");
                    i += 2; // skip '^]'
                    continue;
                }

                inClass = true;
                sb.Append(c);
                continue;
            }

            if (c == '(' && (i + 1 >= pattern.Length || pattern[i + 1] != '?'))
                groupsSeen++;

            sb.Append(c);
        }

        return sb.ToString();
    }

    // Count the total number of capturing groups in the pattern
    private static int CountCapturingGroups(string pattern)
    {
        int count = 0;
        bool inClass = false;

        for (int i = 0; i < pattern.Length; i++)
        {
            char c = pattern[i];
            if (c == '\\' && i + 1 < pattern.Length)
            {
                i++; // skip escaped char
                continue;
            }

            if (inClass)
            {
                if (c == ']') inClass = false;
                continue;
            }

            if (c == '[') { inClass = true; continue; }
            if (c == '(' && (i + 1 >= pattern.Length || pattern[i + 1] != '?'))
            {
                count++;
            }
        }

        return count;
    }

    public override string ToString() => $"/{pattern}/{flags}";
}
