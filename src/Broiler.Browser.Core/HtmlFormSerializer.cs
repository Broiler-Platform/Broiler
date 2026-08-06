using System.Text;
using Broiler.Dom;

namespace Broiler.Browser;

/// <summary>
/// Builds a form's submission target from the page's DOM: the HTML "form data set"
/// of its successful controls, encoded as <c>application/x-www-form-urlencoded</c>
/// and applied to the form's action.
/// </summary>
/// <remarks>
/// <para>
/// The renderer resolves a submit control to its enclosing <c>&lt;form&gt;</c>'s
/// <c>action</c> and navigates there verbatim — no field values are collected — so a
/// search form went to <c>/search</c> rather than <c>/search?q=…</c>, and typing into
/// a page could not actually submit anything.
/// </para>
/// <para>
/// This is deliberately pure and DOM-only: it takes a parsed document, so it can be
/// tested directly and does not depend on layout, hit testing or the renderer's
/// internals. Values the user typed are supplied by the caller through a lookup,
/// since the page's markup still carries the values it was loaded with.
/// </para>
/// </remarks>
internal static class HtmlFormSerializer
{
    /// <summary>Input types whose value is submitted as-is when the control has a name.</summary>
    private static readonly HashSet<string> PlainValueInputTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text", "search", "url", "tel", "email", "password", "number", "range", "color",
        "date", "month", "week", "time", "datetime-local", "hidden",
    };

    /// <summary>Input types that are buttons: submitted only when they are the submitter.</summary>
    private static readonly HashSet<string> ButtonInputTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "submit", "image",
    };

    /// <summary>
    /// The <c>&lt;form&gt;</c> ancestor of <paramref name="control"/>, or <c>null</c>
    /// when it is not in one.
    /// </summary>
    public static DomElement? FindEnclosingForm(DomElement? control)
    {
        for (DomNode? node = control?.ParentNode; node is not null; node = node.ParentNode)
        {
            if (node is DomElement element && IsTag(element, "form"))
                return element;
        }

        return null;
    }

    /// <summary>
    /// Finds the form control in <paramref name="root"/> that the given attributes
    /// describe — how a clicked submit button, reported by the renderer as a bare
    /// attribute bag, is located in the parsed document. Matches on <c>id</c> first,
    /// then on the tag/type/name/value combination.
    /// </summary>
    public static DomElement? FindControl(DomNode? root, IReadOnlyDictionary<string, string>? attributes)
    {
        if (root is null || attributes is null)
            return null;

        if (TryGet(attributes, "id") is { Length: > 0 } id)
        {
            DomElement? byId = FindElement(root, e => string.Equals(e.GetAttribute("id"), id, StringComparison.Ordinal));
            if (byId is not null)
                return byId;
        }

        string? name = TryGet(attributes, "name");
        string? type = TryGet(attributes, "type");
        string? value = TryGet(attributes, "value");

        return FindElement(root, e =>
            (IsTag(e, "input") || IsTag(e, "button")) &&
            Matches(e.GetAttribute("name"), name) &&
            Matches(e.GetAttribute("type"), type) &&
            Matches(e.GetAttribute("value"), value));

        static bool Matches(string? actual, string? expected) =>
            expected is null || string.Equals(actual ?? string.Empty, expected, StringComparison.Ordinal);
    }

    /// <summary>
    /// Builds the <c>application/x-www-form-urlencoded</c> body of
    /// <paramref name="form"/>'s form data set.
    /// </summary>
    /// <param name="submitter">
    /// The control that triggered submission, if any. Only the submitter contributes
    /// its own name/value, which is how <c>&lt;input type="submit" name="…"&gt;</c>
    /// tells the server which button was pressed.
    /// </param>
    /// <param name="valueOverride">
    /// Returns the live value of a control when the user has edited it, or <c>null</c>
    /// to use the markup's value. Checkbox and radio state is expressed by returning
    /// <see cref="CheckedOverride"/> / <see cref="UncheckedOverride"/>.
    /// </param>
    public static string BuildFormData(
        DomElement form,
        DomElement? submitter = null,
        Func<DomElement, string?>? valueOverride = null)
    {
        ArgumentNullException.ThrowIfNull(form);

        StringBuilder body = new();
        foreach (DomElement control in Descendants(form))
            AppendControl(body, control, submitter, valueOverride);

        return body.ToString();
    }

    /// <summary>Sentinel a <paramref name="valueOverride"/> returns to mark a checkbox/radio checked.</summary>
    public const string CheckedOverride = "checked";

    /// <summary>Sentinel a <paramref name="valueOverride"/> returns to mark a checkbox/radio unchecked.</summary>
    public const string UncheckedOverride = "unchecked";

    /// <summary>
    /// Applies <paramref name="formData"/> to <paramref name="action"/> as a query
    /// string, replacing any query the action already carries and dropping its
    /// fragment — the navigation a <c>method="get"</c> submission performs.
    /// </summary>
    public static string ApplyQuery(string action, string formData)
    {
        action ??= string.Empty;

        int fragment = action.IndexOf('#', StringComparison.Ordinal);
        if (fragment >= 0)
            action = action[..fragment];

        int query = action.IndexOf('?', StringComparison.Ordinal);
        if (query >= 0)
            action = action[..query];

        return formData.Length == 0 ? action : action + "?" + formData;
    }

    /// <summary>
    /// Whether the form submits with <c>method="get"</c> — the only method the
    /// browser's navigation path can carry, since it fetches by URL.
    /// </summary>
    public static bool IsGetSubmission(DomElement form) =>
        !string.Equals(form.GetAttribute("method"), "post", StringComparison.OrdinalIgnoreCase);

    private static void AppendControl(
        StringBuilder body,
        DomElement control,
        DomElement? submitter,
        Func<DomElement, string?>? valueOverride)
    {
        // A disabled control is never successful.
        if (control.HasAttribute("disabled"))
            return;

        string name = control.GetAttribute("name") ?? string.Empty;
        string? live = valueOverride?.Invoke(control);

        if (IsTag(control, "textarea"))
        {
            if (name.Length > 0)
                Append(body, name, live ?? control.TextContent ?? string.Empty);
            return;
        }

        if (IsTag(control, "select"))
        {
            if (name.Length > 0)
                AppendSelect(body, control, name, live);
            return;
        }

        if (IsTag(control, "button"))
        {
            // Only a submit button that is the submitter is successful.
            string buttonType = control.GetAttribute("type") ?? "submit";
            if (ReferenceEquals(control, submitter) &&
                name.Length > 0 &&
                string.Equals(buttonType, "submit", StringComparison.OrdinalIgnoreCase))
            {
                Append(body, name, live ?? control.GetAttribute("value") ?? string.Empty);
            }

            return;
        }

        if (!IsTag(control, "input"))
            return;

        string type = control.GetAttribute("type") ?? "text";

        if (string.Equals(type, "checkbox", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type, "radio", StringComparison.OrdinalIgnoreCase))
        {
            bool isChecked = live switch
            {
                CheckedOverride => true,
                UncheckedOverride => false,
                _ => control.HasAttribute("checked"),
            };

            // An unchecked checkbox or radio contributes nothing at all.
            if (isChecked && name.Length > 0)
                Append(body, name, control.GetAttribute("value") ?? "on");

            return;
        }

        if (ButtonInputTypes.Contains(type))
        {
            if (ReferenceEquals(control, submitter) && name.Length > 0)
                Append(body, name, live ?? control.GetAttribute("value") ?? string.Empty);
            return;
        }

        // reset and button never submit; file needs a body this path cannot carry.
        if (!PlainValueInputTypes.Contains(type))
            return;

        if (name.Length > 0)
            Append(body, name, live ?? control.GetAttribute("value") ?? string.Empty);
    }

    private static void AppendSelect(StringBuilder body, DomElement select, string name, string? live)
    {
        if (live is not null)
        {
            Append(body, name, live);
            return;
        }

        bool multiple = select.HasAttribute("multiple");
        DomElement? first = null;
        bool appended = false;

        foreach (DomElement option in Descendants(select))
        {
            if (!IsTag(option, "option") || option.HasAttribute("disabled"))
                continue;

            first ??= option;
            if (!option.HasAttribute("selected"))
                continue;

            Append(body, name, OptionValue(option));
            appended = true;
            if (!multiple)
                return;
        }

        // A single-select with nothing marked selected submits its first option.
        if (!appended && !multiple && first is not null)
            Append(body, name, OptionValue(first));
    }

    private static string OptionValue(DomElement option) =>
        option.GetAttribute("value") ?? (option.TextContent ?? string.Empty).Trim();

    private static void Append(StringBuilder body, string name, string value)
    {
        if (body.Length > 0)
            body.Append('&');

        body.Append(Encode(name)).Append('=').Append(Encode(value));
    }

    /// <summary>
    /// Percent-encodes for <c>application/x-www-form-urlencoded</c>, where a space is
    /// <c>+</c> rather than <c>%20</c>.
    /// </summary>
    private static string Encode(string text) =>
        Uri.EscapeDataString(text).Replace("%20", "+", StringComparison.Ordinal);

    private static IEnumerable<DomElement> Descendants(DomNode root)
    {
        foreach (DomNode child in root.ChildNodes)
        {
            if (child is DomElement element)
                yield return element;

            foreach (DomElement nested in Descendants(child))
                yield return nested;
        }
    }

    private static DomElement? FindElement(DomNode root, Func<DomElement, bool> predicate)
    {
        foreach (DomElement element in Descendants(root))
        {
            if (predicate(element))
                return element;
        }

        return null;
    }

    private static bool IsTag(DomElement element, string name) =>
        string.Equals(element.TagName, name, StringComparison.OrdinalIgnoreCase);

    private static string? TryGet(IReadOnlyDictionary<string, string> attributes, string key)
    {
        foreach (KeyValuePair<string, string> pair in attributes)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                return pair.Value;
        }

        return null;
    }
}
