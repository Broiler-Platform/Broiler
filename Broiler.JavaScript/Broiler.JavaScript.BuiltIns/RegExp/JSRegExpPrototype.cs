using Broiler.JavaScript.Runtime;
using System.Runtime.CompilerServices;

namespace Broiler.JavaScript.BuiltIns.RegExp;

public partial class JSRegExp
{
    [JSExport]
    public int LastIndex
    {
        get => lastIndex; set => lastIndex = value;
    }

    [JSExport("test")]
    public JSValue Test(in Arguments a)
    {
        var text = a.Get1().ToString();
        var match = value.Match(text, CalculateStartPosition(text));

        if (match.Success)
        {
            if (globalSearch)
                lastIndex = match.Index + match.Length;

            return JSValue.BooleanTrue;
        }

        return JSValue.BooleanFalse;
    }

    [JSExport("exec")]
    public JSValue Exec(in Arguments a)
    {
        var input = a.Get1().ToString();
        // Perform the regular expression matching.
        var match = value.Match(input, CalculateStartPosition(input));

        // Return null if no match was found.
        if (match.Success == false)
        {
            // Reset the lastIndex property.
            if (globalSearch == true)
                lastIndex = 0;

            return JSValue.NullValue;
        }

        if (globalSearch)
            lastIndex = match.Index + match.Length;

        var groups = match.Groups;
        var c = groups.Count;
        var result = JSValue.CreateArray((uint)c);

        for (int i = 0; i < c; i++)
        {
            var group = groups[i];
            if (group.Captures.Count == 0)
            {
                result[(uint)i] = JSUndefined.Value;
            } 
            else
            {
                result[(uint)i] = JSValue.CreateString(group.Value);
            }
        }

        result[KeyStrings.index] = JSValue.CreateNumber(match.Index);
        result[KeyStrings.input] = a.Get1();

        // Populate named groups (§2.7 — duplicate named capture groups support).
        var groupNames = value.GetGroupNames();
        if (groupNames.Length > 1) // First name is always "0" (the whole match)
        {
            var namedGroups = new JSObject();
            bool hasNamedGroups = false;
            
            for (int i = 0; i < groupNames.Length; i++)
            {
                var name = groupNames[i];
                // Skip numeric group names (they're positional, not named)
                if (name.Length > 0 && (name[0] < '0' || name[0] > '9'))
                {
                    hasNamedGroups = true;
                    var g = match.Groups[name];
                    namedGroups[name] = g.Success
                        ? JSValue.CreateString(g.Value)
                        : JSUndefined.Value;
                }
            }
            
            if (hasNamedGroups)
                result[KeyStrings.GetOrCreate("groups")] = namedGroups;
        }

        return result;
    }

    /// <summary>
    /// Calculates the position to start searching.
    /// </summary>
    /// <param name="input"> The string on which to perform the search. </param>
    /// <returns> The character position to start searching. </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CalculateStartPosition(string input)
    {
        if (globalSearch == false)
            return 0;

        var maxIndex = lastIndex > 0 ? lastIndex : 0;
        var minIndex = maxIndex < input.Length ? maxIndex : input.Length;

        return minIndex;
    }
}
