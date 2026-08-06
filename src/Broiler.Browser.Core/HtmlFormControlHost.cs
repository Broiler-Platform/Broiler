using System.Drawing;
using Broiler.Dom;
using Broiler.Dom.Html;
using Broiler.Graphics;
using Broiler.HtmlBridge;
using Broiler.UI;
using Broiler.UI.CheckBox;
using Broiler.UI.CheckBox.Standard;
using Broiler.UI.ComboBox;
using Broiler.UI.ComboBox.Standard;
using Broiler.UI.RadioButton;
using Broiler.UI.RadioButton.Standard;
using HtmlContainer = Broiler.HTML.Image.HtmlContainer;

namespace Broiler.Browser;

/// <summary>
/// Hosts real Broiler.UI controls over the page's checkboxes, radios and
/// <c>&lt;select&gt;</c>s — a <see cref="StandardCheckBox"/>,
/// <see cref="StandardRadioButton"/> or <see cref="StandardComboBox"/> apiece. The
/// renderer draws all three as empty bordered boxes: no check, no dot, no selected
/// option text, no popup, and no way to change any of them.
/// </summary>
/// <remarks>
/// <para>
/// Unlike the text editor — which hosts one control on demand, at the point the user
/// clicked — these have to show their state whether or not they are being interacted
/// with, so every one on the page is hosted up front.
/// </para>
/// <para>
/// That needs each control's geometry, and the renderer's only public geometry query
/// for an arbitrary element is <c>GetElementRectangle(id)</c>. Controls are therefore
/// given synthetic ids by <see cref="HtmlPostProcessor.StampFormControlIds"/> before
/// the page reaches the renderer, identified from the parsed document, and located by
/// id after each layout.
/// </para>
/// <para>
/// Changes record into <see cref="HtmlFormState"/> rather than the document: the
/// renderer has no API to write a <c>checked</c> attribute or a selected option back,
/// and form submission layers this state over the markup anyway.
/// </para>
/// </remarks>
internal sealed class HtmlFormControlHost
{
    /// <summary>
    /// Ceiling on hosted controls per page. Each one costs an id lookup — a box-tree
    /// walk — on every layout, so a pathological page is capped rather than allowed
    /// to make scrolling quadratic.
    /// </summary>
    private const int MaxHostedControls = 64;

    /// <summary>UA default control font (see <c>CssDefaults</c>), so hosted controls match the page.</summary>
    private const string UaControlFontFamily = "Arial";
    private const double UaControlFontSize = 13.3333;

    private readonly UiElement _owner;
    private readonly HtmlFormState _formState;
    private readonly List<HostedToggle> _hosted = [];
    private readonly Dictionary<string, UiRadioGroupScope> _radioGroups = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<ToggleIdentity>> _radioMembers = new(StringComparer.Ordinal);

    public HtmlFormControlHost(UiElement owner, HtmlFormState formState)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _formState = formState ?? throw new ArgumentNullException(nameof(formState));
    }

    /// <summary>Raised when the user changed a control, so the host can repaint.</summary>
    public event EventHandler? Changed;

    /// <summary>The controls hosted for the current page.</summary>
    public IReadOnlyList<UiElement> Controls => _hosted.ConvertAll(h => h.Control);

    /// <summary>Number of controls hosted, for tests and diagnostics.</summary>
    public int Count => _hosted.Count;

    /// <summary>
    /// Rebuilds the hosted controls from <paramref name="pageHtml"/>. Call once per
    /// page, not per layout — the parse is the expensive half.
    /// </summary>
    public void Rebuild(string pageHtml)
    {
        Clear();

        DomElement? root = TryParse(pageHtml);
        if (root is null)
            return;

        foreach (DomElement element in Descendants(root))
        {
            if (_hosted.Count >= MaxHostedControls)
                break;

            bool isSelect = string.Equals(element.TagName, "select", StringComparison.OrdinalIgnoreCase);
            bool isInput = string.Equals(element.TagName, "input", StringComparison.OrdinalIgnoreCase);
            if (!isSelect && !isInput)
                continue;

            // A multi-select is not a combo box; leave it painted and let submission
            // use the markup's selection rather than misrepresent it as single-choice.
            if (isSelect && element.HasAttribute("multiple"))
                continue;

            string type = element.GetAttribute("type") ?? string.Empty;
            bool isRadio = isInput && string.Equals(type, "radio", StringComparison.OrdinalIgnoreCase);
            bool isCheckBox = isInput && string.Equals(type, "checkbox", StringComparison.OrdinalIgnoreCase);
            if (!isSelect && !isRadio && !isCheckBox)
                continue;

            string id = element.GetAttribute("id") ?? string.Empty;
            if (id.Length == 0)
                continue;

            Add(element, id, isSelect, isRadio);
        }
    }

    /// <summary>Removes every hosted control. Called when the page is replaced.</summary>
    public void Clear()
    {
        foreach (HostedToggle hosted in _hosted)
        {
            _owner.RemoveChild(hosted.Control);
            hosted.Control.Dispose();
        }

        _hosted.Clear();
        _radioGroups.Clear();
        _radioMembers.Clear();
    }

    /// <summary>
    /// Places every hosted control over its element for the current viewport
    /// transform, hiding those that are scrolled out of view or no longer laid out.
    /// </summary>
    public void UpdateViewport(HtmlContainer? container, BRect viewportBounds, double zoom, double scrollY)
    {
        if (_hosted.Count == 0)
            return;

        foreach (HostedToggle hosted in _hosted)
        {
            RectangleF? rect = container is null || zoom <= 0 || viewportBounds.IsEmpty
                ? null
                : TryGetRectangle(container, hosted.ElementId);

            if (rect is not { Width: > 0, Height: > 0 } box)
            {
                hosted.Control.Visibility = UiVisibility.Collapsed;
                continue;
            }

            BRect target = new(
                viewportBounds.Left + (box.X * zoom),
                viewportBounds.Top + (box.Y * zoom) - scrollY,
                box.Width * zoom,
                box.Height * zoom);

            if (target.Intersect(viewportBounds).IsEmpty)
            {
                hosted.Control.Visibility = UiVisibility.Collapsed;
                continue;
            }

            hosted.Control.Visibility = UiVisibility.Visible;
            ApplyMarkSize(hosted.Control, target);
            hosted.Control.Measure(target.Size);
            hosted.Control.Arrange(target);
        }
    }

    private void Add(DomElement element, string id, bool isSelect, bool isRadio)
    {
        string name = element.GetAttribute("name") ?? string.Empty;
        string value = element.GetAttribute("value") ?? string.Empty;

        UiElement control;
        if (isSelect)
        {
            control = CreateComboBox(element, id, name);
        }
        else
        {
            bool isChecked = _formState.GetChecked(id, name, value) ?? element.HasAttribute("checked");
            control = isRadio
                ? CreateRadio(id, name, value, isChecked)
                : CreateCheckBox(id, name, value, isChecked);
        }

        control.Visibility = UiVisibility.Collapsed;
        _owner.AddChild(control);
        _hosted.Add(new HostedToggle(id, control));
    }

    private UiElement CreateCheckBox(string id, string name, string value, bool isChecked)
    {
        StandardCheckBox box = new()
        {
            IsChecked = isChecked,
            Text = string.Empty,
            PaddingX = 0,
            PaddingY = 0,
            Background = BColor.White,
        };

        box.CheckStateChanged += (_, _) =>
        {
            _formState.SetChecked(id, name, value, box.CheckState == UiCheckState.Checked);
            Changed?.Invoke(this, EventArgs.Empty);
        };

        return box;
    }

    private UiElement CreateRadio(string id, string name, string value, bool isChecked)
    {
        if (!_radioGroups.TryGetValue(name, out UiRadioGroupScope? scope))
        {
            scope = new UiRadioGroupScope(name);
            _radioGroups[name] = scope;
        }

        StandardRadioButton radio = new()
        {
            GroupScope = scope,
            IsChecked = isChecked,
            Text = string.Empty,
            PaddingX = 0,
            PaddingY = 0,
            Background = BColor.White,
        };

        if (!_radioMembers.TryGetValue(name, out List<ToggleIdentity>? members))
        {
            members = [];
            _radioMembers[name] = members;
        }

        ToggleIdentity identity = new(id, name, value);
        members.Add(identity);

        radio.CheckedChanged += (_, _) =>
        {
            // A radio group is single-choice, and the markup's `checked` on a sibling
            // would otherwise still be submitted. Selecting one records the whole
            // group, so the untouched siblings are explicitly unchecked rather than
            // falling back to the markup.
            if (radio.IsChecked)
            {
                foreach (ToggleIdentity member in members)
                    _formState.SetChecked(member.Id, member.Name, member.Value, member == identity);
            }

            Changed?.Invoke(this, EventArgs.Empty);
        };

        return radio;
    }

    /// <summary>
    /// Builds a combo box from a <c>&lt;select&gt;</c>'s options, seeded with the
    /// selection the page declares (or the first option, which is what a single-select
    /// with nothing marked shows and submits).
    /// </summary>
    private UiElement CreateComboBox(DomElement select, string id, string name)
    {
        List<UiComboBoxItem> items = [];
        List<string> values = [];
        int selected = -1;

        foreach (DomElement option in Descendants(select))
        {
            if (!string.Equals(option.TagName, "option", StringComparison.OrdinalIgnoreCase))
                continue;

            string text = (option.TextContent ?? string.Empty).Trim();
            // An option with no value attribute submits its text.
            string value = option.GetAttribute("value") ?? text;

            if (option.HasAttribute("selected") && selected < 0)
                selected = items.Count;

            values.Add(value);
            items.Add(new UiComboBoxItem(value, text));
        }

        if (selected < 0 && items.Count > 0)
            selected = 0;

        // A selection the user already made on this page outranks the markup.
        string? recorded = _formState.GetSelectedValue(id, name);
        if (recorded is not null)
        {
            int index = values.IndexOf(recorded);
            if (index >= 0)
                selected = index;
        }

        StandardComboBox combo = new()
        {
            Font = new BFontStyle(UaControlFontFamily, UaControlFontSize),
            CornerRadius = 0,
        };
        combo.SetItems(items);
        if (selected >= 0)
            combo.SelectIndex(selected);

        combo.SelectionChanged += (_, _) =>
        {
            int index = combo.SelectedIndex;
            if (index >= 0 && index < values.Count)
                _formState.SetSelectedValue(id, name, values[index]);

            Changed?.Invoke(this, EventArgs.Empty);
        };

        return combo;
    }

    /// <summary>
    /// Sizes the control's mark to the box the page laid out, so a hosted control
    /// occupies exactly the 13×13 (or author-styled) area the renderer reserved.
    /// </summary>
    private static void ApplyMarkSize(UiElement control, BRect target)
    {
        double size = Math.Max(1, Math.Min(target.Width, target.Height));
        switch (control)
        {
            case StandardCheckBox box:
                box.BoxSize = size;
                box.Spacing = 0;
                break;
            case StandardRadioButton radio:
                radio.MarkSize = size;
                radio.Spacing = 0;
                break;
            case StandardComboBox combo:
                // A combo fills its box rather than sizing a mark inside it, and its
                // row height has to match or the closed control clips its own text.
                combo.ItemHeight = Math.Max(1, target.Height);
                break;
        }
    }

    private static RectangleF? TryGetRectangle(HtmlContainer container, string elementId)
    {
        try
        {
            return container.GetElementRectangle(elementId);
        }
        catch (Exception)
        {
            // An id the box tree no longer carries (the page was rewritten under us)
            // simply has no geometry; the control hides until it comes back.
            return null;
        }
    }

    private static DomElement? TryParse(string html)
    {
        if (string.IsNullOrEmpty(html))
            return null;

        try
        {
            return new HtmlDocumentParser().ParseDocument(html).Document.DocumentElement;
        }
        catch (Exception)
        {
            return null;
        }
    }

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

    private sealed record HostedToggle(string ElementId, UiElement Control);

    /// <summary>Identifies a toggle for <see cref="HtmlFormState"/>.</summary>
    private sealed record ToggleIdentity(string Id, string Name, string Value);
}
