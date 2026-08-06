using Broiler.Dom;
using Broiler.Dom.Html;

namespace Broiler.Browser;

/// <summary>
/// The browser's view of the page's forms: it parses the live document when a
/// submission needs serializing, and holds the control state the renderer's DOM
/// does not carry (checkbox and radio checked state).
/// </summary>
/// <remarks>
/// The renderer's serialized HTML is the source of truth for values — text the user
/// typed is committed into it before a submission, and hidden inputs, <c>select</c>
/// options and their <c>selected</c> flags all survive serialization. Checked state
/// is the exception: nothing writes it back into the document, so hosted checkboxes
/// and radios report theirs here and it is layered over the markup at submit time.
/// </remarks>
internal sealed class HtmlFormState
{
    /// <summary>Checked state by control key, for controls the user has toggled.</summary>
    private readonly Dictionary<string, bool> _checkedState = new(StringComparer.Ordinal);

    /// <summary>Forgets per-page state. Called when the page is replaced.</summary>
    public void Reset() => _checkedState.Clear();

    /// <summary>Records the state of a checkbox or radio the user toggled.</summary>
    public void SetChecked(string id, string name, string value, bool isChecked) =>
        _checkedState[ControlKey(id, name, value)] = isChecked;

    /// <summary>The recorded state of a checkbox or radio, or <c>null</c> if untouched.</summary>
    public bool? GetChecked(string id, string name, string value) =>
        _checkedState.TryGetValue(ControlKey(id, name, value), out bool state) ? state : null;

    /// <summary>
    /// Builds the URL a submission should navigate to, or <c>null</c> when the click
    /// is not a form submission this can serialize — an ordinary link, a control
    /// outside any form, or a <c>method="post"</c> form, which the browser's
    /// fetch-by-URL navigation cannot carry.
    /// </summary>
    /// <param name="pageHtml">The live document, as serialized by the renderer.</param>
    /// <param name="attributes">Attributes of the clicked control, as reported by the renderer.</param>
    /// <param name="resolvedAction">The action URL the renderer already resolved.</param>
    public string? TryBuildSubmitTarget(
        string pageHtml,
        IReadOnlyDictionary<string, string>? attributes,
        string resolvedAction)
    {
        if (string.IsNullOrEmpty(pageHtml) || attributes is null)
            return null;

        if (!IsSubmitControl(attributes))
            return null;

        DomElement? root = TryParse(pageHtml);
        if (root is null)
            return null;

        DomElement? submitter = HtmlFormSerializer.FindControl(root, attributes);
        DomElement? form = HtmlFormSerializer.FindEnclosingForm(submitter);
        if (form is null || !HtmlFormSerializer.IsGetSubmission(form))
            return null;

        return HtmlFormSerializer.ApplyQuery(
            resolvedAction,
            HtmlFormSerializer.BuildFormData(form, submitter, ResolveOverride));
    }

    /// <summary>
    /// Builds the URL for a submission triggered from a field rather than a button —
    /// pressing Enter in a text control submits its form. Returns <c>null</c> when the
    /// field is not in a serializable form.
    /// </summary>
    /// <param name="baseUrl">Page URL, used to resolve a relative form action.</param>
    public string? TryBuildFieldSubmitTarget(string pageHtml, string fieldId, string fieldName, string baseUrl)
    {
        if (string.IsNullOrEmpty(pageHtml))
            return null;

        DomElement? root = TryParse(pageHtml);
        if (root is null)
            return null;

        Dictionary<string, string> attributes = [];
        if (fieldId.Length > 0)
            attributes["id"] = fieldId;
        if (fieldName.Length > 0)
            attributes["name"] = fieldName;
        if (attributes.Count == 0)
            return null;

        DomElement? field = HtmlFormSerializer.FindControl(root, attributes);
        DomElement? form = HtmlFormSerializer.FindEnclosingForm(field);
        if (form is null || !HtmlFormSerializer.IsGetSubmission(form))
            return null;

        // No submitter: pressing Enter in a field submits the form without any
        // button contributing its own name and value.
        string query = HtmlFormSerializer.BuildFormData(form, submitter: null, ResolveOverride);
        return HtmlFormSerializer.ApplyQuery(ResolveAction(form.GetAttribute("action"), baseUrl), query);
    }

    /// <summary>Resolves a form action against the page URL, leaving it as-is if that fails.</summary>
    internal static string ResolveAction(string? action, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(action))
            return baseUrl;

        // Resolve against the page first: an absolute action survives that unchanged,
        // while treating the action as absolute first would take "/search" for a
        // rooted file path on Unix and produce file:///search.
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? root) &&
            Uri.TryCreate(root, action, out Uri? resolved))
        {
            return resolved.ToString();
        }

        return Uri.TryCreate(action, UriKind.Absolute, out Uri? absolute) ? absolute.ToString() : action;
    }

    private string? ResolveOverride(DomElement control)
    {
        if (!string.Equals(control.TagName, "input", StringComparison.OrdinalIgnoreCase))
            return null;

        string type = control.GetAttribute("type") ?? "text";
        if (!string.Equals(type, "checkbox", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(type, "radio", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        bool? state = GetChecked(
            control.GetAttribute("id") ?? string.Empty,
            control.GetAttribute("name") ?? string.Empty,
            control.GetAttribute("value") ?? string.Empty);

        return state switch
        {
            true => HtmlFormSerializer.CheckedOverride,
            false => HtmlFormSerializer.UncheckedOverride,
            null => null,
        };
    }

    /// <summary>
    /// Whether the clicked element is a submit control. The renderer raises the same
    /// event for ordinary links, and a link inside a <c>&lt;form&gt;</c> must not be
    /// turned into a submission.
    /// </summary>
    private static bool IsSubmitControl(IReadOnlyDictionary<string, string> attributes)
    {
        foreach (KeyValuePair<string, string> pair in attributes)
        {
            // An <a href> is a navigation, never a submission.
            if (string.Equals(pair.Key, "href", StringComparison.OrdinalIgnoreCase))
                return false;
        }

        foreach (KeyValuePair<string, string> pair in attributes)
        {
            if (!string.Equals(pair.Key, "type", StringComparison.OrdinalIgnoreCase))
                continue;

            return string.Equals(pair.Value, "submit", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(pair.Value, "image", StringComparison.OrdinalIgnoreCase);
        }

        // A <button> with no type defaults to submit, and so does a bare control the
        // renderer only resolves to a form action because it is a submit control.
        return true;
    }

    private static DomElement? TryParse(string html)
    {
        try
        {
            return new HtmlDocumentParser().ParseDocument(html).Document.DocumentElement;
        }
        catch (Exception)
        {
            // A page the shared parser cannot take is not a reason to break the click;
            // the caller falls back to navigating the action verbatim.
            return null;
        }
    }

    /// <summary>
    /// Identifies a toggle across a page. The browsing path stamps an id on every
    /// checkbox and radio, so the id form is the normal one; the name/value form is
    /// the fallback for a control reached without one.
    /// </summary>
    private static string ControlKey(string id, string name, string value) =>
        id.Length > 0 ? "#" + id : name + "=" + value;
}
