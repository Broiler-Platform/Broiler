using Broiler.Graphics;
﻿using System.Drawing;
using System.Globalization;
using System.Net;
using CssConstants = Broiler.CSS.CssConstants;
using CssValueParser = Broiler.CSS.CssLengthParser;

namespace Broiler.Layout.Engine;

internal partial class CssBox : CssBoxProperties, IDisposable
{
    private CssBox _parentBox;
    protected object _htmlContainer;
    private ILayoutEnvironment _layoutEnvironment;
    private ReadOnlyMemory<char> _text;

    internal bool _tableFixed;

    /// <summary>
    /// The canonical <see cref="Dom.DomElement"/> this box was built from,
    /// when the box tree was generated from a <see cref="Dom.DomDocument"/>
    /// (the <c>SetDocument</c> path). <c>null</c> on the legacy HTML-string parse path.
    /// Phase 5 uses this to run the shared <c>Broiler.CSS.Dom</c> cascade on the
    /// canonical element and project the result onto this box.
    /// </summary>
    internal Dom.DomElement SourceElement { get; set; }

    /// <summary>
    /// When the block-inside-inline correction (CSS2.1 §9.2.1.1) splits a
    /// positioned inline element into sibling anonymous blocks, the hoisted
    /// blocks lose their parent–child relationship with the positioned
    /// inline in the box tree.  This field links back to the original
    /// positioned ancestor so that <see cref="FindPositionedContainingBlock"/>
    /// can still find it.
    /// </summary>
    internal CssBox SplitPositionedAncestor { get; set; }

    /// <summary>
    /// When the block-inside-inline correction splits a positioned inline
    /// element, the original box loses its children to anonymous "left" and
    /// "right" copies.  This list tracks those copies so that
    /// <see cref="GetInlineBoundingBox"/> can compute the bounding box
    /// across <em>all</em> fragments, not just the (now-empty) original.
    /// Only populated on the original box that serves as
    /// <see cref="SplitPositionedAncestor"/> for hoisted descendants.
    /// </summary>
    internal List<CssBox> SplitFragments { get; private set; }

    /// <summary>
    /// Register a box as a fragment of this positioned inline that was
    /// created during the block-inside-inline split.
    /// </summary>
    internal void AddSplitFragment(CssBox fragment)
    {
        SplitFragments ??= new List<CssBox>();
        SplitFragments.Add(fragment);
    }

    protected bool _wordsSizeMeasured;
    private CssBox _listItemBox;
    private List<ILayoutImageLoader?>? _backgroundImageLoadHandlers;
    private bool _backgroundImagesInitialized;

    internal Dictionary<string, string> CustomProperties { get; } = new(StringComparer.OrdinalIgnoreCase);

    internal void SetCustomProperty(string propertyName, string value)
    {
        if (string.IsNullOrEmpty(propertyName))
            return;

        var trimmed = value?.Trim();
        if (string.Equals(trimmed, "initial", StringComparison.OrdinalIgnoreCase))
        {
            CustomProperties[propertyName] = InvalidCustomPropertySentinel;
            return;
        }

        if (string.Equals(trimmed, "inherit", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "unset", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "revert", StringComparison.OrdinalIgnoreCase))
        {
            CustomProperties.Remove(propertyName);
            return;
        }

        CustomProperties[propertyName] = value;
    }

    /// <summary>
    /// Returns the loaded background image handle, or null if no background image is loaded.
    /// Used by <c>FragmentTreeBuilder</c> to capture background images for the new paint path.
    /// </summary>
    internal object? LoadedBackgroundImage
    {
        get
        {
            if (_backgroundImageLoadHandlers == null || _backgroundImageLoadHandlers.Count == 0)
                return null;

            if (_backgroundImageLoadHandlers.Count == 1)
                return _backgroundImageLoadHandlers[0]?.Image;

            var layers = new object?[_backgroundImageLoadHandlers.Count];
            bool hasImage = false;
            for (int i = 0; i < _backgroundImageLoadHandlers.Count; i++)
            {
                var image = _backgroundImageLoadHandlers[i]?.Image;
                layers[i] = image;
                hasImage |= image != null;
            }

            return hasImage ? layers : null;
        }
    }

    public CssBox(CssBox parentBox, HtmlTag tag, Uri baseUrl)
    {
        if (parentBox != null)
        {
            _parentBox = parentBox;
            _parentBox.Boxes.Add(this);
        }

        HtmlTag = tag;
        BaseUrl = baseUrl;
    }

    /// <summary>
    /// Opaque host-container handle (the renderer's <c>IHtmlContainerInt</c>),
    /// kept as the box→container link for renderer/serialization code that reads
    /// it externally (cast back to the concrete type). Layout itself no longer
    /// uses it — host services flow through <see cref="LayoutEnvironment"/>.
    /// Typed as <see cref="object"/> so layout stays free of the renderer's
    /// container interface (roadmap §4, Phase 4).
    /// </summary>
    internal object ContainerInt
    {
        get { return _htmlContainer ??= _parentBox?.ContainerInt; }
        set { _htmlContainer = value; }
    }

    private bool? _documentQuirksMode;

    /// <summary>
    /// Whether the document being laid out is in quirks mode. Captured once —
    /// from <see cref="Dom.DocumentModeContext"/>, which the HTML parser publishes
    /// on the parse thread — onto the tree root the first time layout consults it,
    /// then read from the root by every box. Caching on the root makes the value
    /// survive re-layout passes (which may run on a different thread, where the
    /// thread-local would have reset) without threading it through the box builder.
    /// </summary>
    internal bool DocumentQuirksMode
    {
        get
        {
            var root = this;
            while (root._parentBox != null)
                root = root._parentBox;
            root._documentQuirksMode ??= DocumentModeContext.CurrentQuirksMode;
            return root._documentQuirksMode.Value;
        }
    }

    /// <summary>
    /// Host-injected layout environment (see <see cref="ILayoutEnvironment"/>),
    /// resolved up the box tree like <see cref="ContainerInt"/>. Set on the root
    /// box at the start of each layout pass; supplies the initial-containing-block
    /// inputs (<see cref="ILayoutEnvironment.ViewportSize"/>,
    /// <see cref="ILayoutEnvironment.RootLocation"/>) and the
    /// <see cref="ILayoutEnvironment.ActualSize"/> output that layout reads/writes
    /// outside the <c>g</c>-threaded methods (roadmap §5, Phase 3).
    /// </summary>
    internal ILayoutEnvironment LayoutEnvironment
    {
        get { return _layoutEnvironment ??= _parentBox?.LayoutEnvironment; }
        set { _layoutEnvironment = value; }
    }

    public CssBox ParentBox
    {
        get { return _parentBox; }
        set
        {
            _parentBox?.Boxes.Remove(this);
            _parentBox = value;

            if (value != null)
                _parentBox.Boxes.Add(this);
        }
    }

    public List<CssBox> Boxes { get; } = [];

    public override bool AvoidGeometryAntialias => LayoutEnvironment?.AvoidGeometryAntialias ?? false;

    protected override bool TryGetCustomPropertyValue(string propertyName, out string value)
    {
        if (CustomProperties.TryGetValue(propertyName, out value))
            return true;

        if (ParentBox != null)
            return ParentBox.TryGetCustomPropertyValue(propertyName, out value);

        value = string.Empty;
        return false;
    }

    public bool IsBrElement => HtmlTag != null && HtmlTag.Name.Equals("br", StringComparison.InvariantCultureIgnoreCase);
    public bool IsInline => (Display == CssConstants.Inline || Display == CssConstants.InlineBlock
        || Display == "inline-flex" || Display == "inline-grid") && !IsBrElement;
    public bool IsBlock => Display == CssConstants.Block || Display == "flex"
        || Display == "grid";
    public virtual bool IsClickable
    {
        get
        {
            if (HtmlTag == null)
                return false;

            // <a> links (without only an id anchor)
            if (HtmlTag.Name == HtmlConstants.A && !HtmlTag.HasAttribute("id"))
                return true;

            // <button> elements
            if (HtmlTag.Name.Equals("button", StringComparison.OrdinalIgnoreCase))
                return true;

            // <input type="submit|button|reset"> elements
            if (HtmlTag.Name.Equals("input", StringComparison.OrdinalIgnoreCase))
            {
                var inputType = HtmlTag.TryGetAttribute("type")?.ToLowerInvariant() ?? "text";
                if (inputType is "submit" or "button" or "reset")
                    return true;
            }

            return false;
        }
    }

    public virtual bool IsFixed
    {
        get
        {
            if (Position == CssConstants.Fixed)
                return true;

            if (ParentBox == null)
                return false;

            CssBox parent = this;

            while (!(parent.ParentBox == null || parent == parent.ParentBox))
            {
                parent = parent.ParentBox;

                if (parent.Position == CssConstants.Fixed)
                    return true;
            }

            return false;
        }
    }

    public virtual string HrefLink => GetAttribute(HtmlConstants.Href);

    public HtmlTag HtmlTag { get; }

    public bool IsImage => Words.Count == 1 && Words[0].IsImage;

    public ReadOnlyMemory<char> Text
    {
        get { return _text; }
        set
        {
            _text = value;
            Words.Clear();
        }
    }

    internal List<CssLineBox> LineBoxes { get; } = [];
    internal Dictionary<CssLineBox, RectangleF> Rectangles { get; } = [];
    internal List<CssRect> Words { get; } = [];
    internal CssRect FirstWord => Words[0];

    internal CssLineBox FirstHostingLineBox { get; set; }

    internal CssLineBox LastHostingLineBox { get; set; }

    internal Uri BaseUrl { get; set; }

    public void PerformLayout(ILayoutEnvironment g)
    {
        try
        {
            // PROTOTYPE (BROILER_VERTICAL_FLOW): a vertical-writing-mode rotation
            // root lays its whole subtree out in a logical (horizontal) frame.
            // While that frame layout runs, a transposed box's physical
            // border/padding insets are read as LOGICAL (frame) insets so they
            // land on the correct axis after the rotation swaps width↔height
            // (PushVerticalFrameLayout / UsesLogicalFrameInsets). The flag is
            // cleared before the rotation and the later paint pass, which read
            // the box's authored physical borders.
            bool isVerticalRoot = VerticalFlowPrototype.Enabled
                && IsVerticalWritingMode(WritingMode)
                && (ParentBox == null || !IsVerticalWritingMode(ParentBox.WritingMode));

            if (isVerticalRoot)
                PushVerticalFrameLayout();
            try
            {
                PerformLayoutImp(g);
            }
            finally
            {
                if (isVerticalRoot)
                    PopVerticalFrameLayout();
            }

            // Once a vertical-writing-mode root and its whole subtree have been
            // laid out in the logical (horizontal) frame, rotate the result into
            // physical space.
            if (isVerticalRoot)
            {
                ApplyVerticalWritingModeFlow();
            }
        }
        catch (Exception ex)
        {
            LayoutEnvironment.ReportLayoutError("Exception in box layout", ex);
        }
    }

    public void SetBeforeBox(CssBox before)
    {
        int index = _parentBox.Boxes.IndexOf(before);

        if (index < 0)
            throw new Exception("before box doesn't exist on parent");

        _parentBox.Boxes.Remove(this);
        _parentBox.Boxes.Insert(index, this);
    }

    public void SetAllBoxes(CssBox fromBox)
    {
        foreach (var childBox in fromBox.Boxes)
            childBox._parentBox = this;

        Boxes.AddRange(fromBox.Boxes);
        fromBox.Boxes.Clear();
    }

    public virtual void Dispose()
    {
        if (_backgroundImageLoadHandlers != null)
        {
            foreach (var imageLoadHandler in _backgroundImageLoadHandlers)
                imageLoadHandler?.Dispose();
        }

        foreach (var childBox in Boxes)
            childBox.Dispose();
    }

    protected override sealed CssBoxProperties GetParent() => _parentBox;

    private int GetIndexForList()
    {
        // Phase 2: Read list attributes from CssBoxProperties instead of GetAttribute().
        bool reversed = ParentBox.ListReversed;

        int index;
        if (ParentBox.ListStart.HasValue)
        {
            index = ParentBox.ListStart.Value;
        }
        else if (reversed)
        {
            index = 0;
            foreach (CssBox b in ParentBox.Boxes)
            {
                if (b.Display == CssConstants.ListItem)
                    index++;
            }
        }
        else
        {
            index = 1;
        }

        foreach (CssBox b in ParentBox.Boxes)
        {
            if (b.Equals(this))
                return index;

            if (b.Display == CssConstants.ListItem)
                index += reversed ? -1 : 1;
        }

        return index;
    }

    private void CreateListItemBox(ILayoutEnvironment g)
    {
        if (Display != CssConstants.ListItem || ListStyleType == CssConstants.None)
            return;

        if (_listItemBox == null)
        {
            _listItemBox = new CssBox(null, null, BaseUrl);
            _listItemBox.InheritStyle(this);
            _listItemBox.Display = CssConstants.Inline;
            _listItemBox._htmlContainer = ContainerInt;
            _listItemBox._layoutEnvironment = LayoutEnvironment;

            if (ListStyleType.Equals(CssConstants.Disc, StringComparison.InvariantCultureIgnoreCase))
            {
                _listItemBox.Text = "•".AsMemory();
            }
            else if (ListStyleType.Equals(CssConstants.Circle, StringComparison.InvariantCultureIgnoreCase))
            {
                _listItemBox.Text = "o".AsMemory();
            }
            else if (ListStyleType.Equals(CssConstants.Square, StringComparison.InvariantCultureIgnoreCase))
            {
                _listItemBox.Text = "♠".AsMemory();
            }
            else if (ListStyleType.Equals(CssConstants.Decimal, StringComparison.InvariantCultureIgnoreCase))
            {
                _listItemBox.Text = (GetIndexForList().ToString(CultureInfo.InvariantCulture) + ".").AsMemory();
            }
            else if (ListStyleType.Equals(CssConstants.DecimalLeadingZero, StringComparison.InvariantCultureIgnoreCase))
            {
                _listItemBox.Text = (GetIndexForList().ToString("00", CultureInfo.InvariantCulture) + ".").AsMemory();
            }
            else
            {
                _listItemBox.Text = (LayoutEnvironment.FormatListMarker(GetIndexForList(), ListStyleType) + ".").AsMemory();
            }

            _listItemBox.ParseToWords();

            _listItemBox.PerformLayoutImp(g);
            _listItemBox.Size = new SizeF((float)_listItemBox.Words[0].Width, (float)_listItemBox.Words[0].Height);
        }

        _listItemBox.Words[0].Left = Location.X - _listItemBox.Size.Width - 5;
        _listItemBox.Words[0].Top = Location.Y + ActualPaddingTop; // +FontAscent;
    }

    internal string GetAttribute(string attribute) => GetAttribute(attribute, string.Empty);
    internal string GetAttribute(string attribute, string defaultValue) => HtmlTag != null ? HtmlTag.TryGetAttribute(attribute, defaultValue) : defaultValue;

    internal new void InheritStyle(CssBox box = null, bool everything = false) => base.InheritStyle(box ?? ParentBox, everything);

    protected override ILayoutFont GetCachedFont(string fontFamily, double fsize, LayoutFontStyle st, string fontFeatures) => LayoutEnvironment.GetFont(fontFamily, fsize, st, fontFeatures);

    /// <summary>
    /// Resolves the CSS <c>ch</c> unit by measuring the advance of the "0" glyph
    /// in this box's font (CSS Values 3 §5.1.1). Returns <see cref="double.NaN"/>
    /// when no layout environment is wired up yet so the caller can fall back to
    /// the generic approximation.
    /// </summary>
    protected override double GetChWidth()
    {
        var env = LayoutEnvironment;
        if (env is null)
            return double.NaN;

        double width = env.MeasureText(ActualFont, "0").Width;
        return width > 0 ? width : double.NaN;
    }

    protected override BColor GetActualColor(string colorStr) => LayoutEnvironment.ParseColor(colorStr);

    protected override PointF GetActualLocation(string X, string Y)
    {
        var vpSize = LayoutEnvironment.ViewportSize;
        var left = CssValueParser.ParseLength(X, vpSize.Width, GetEmHeight(), null);
        var top = CssValueParser.ParseLength(Y, vpSize.Height, GetEmHeight(), null);

        return new PointF((float)left, (float)top);
    }

    public override string ToString()
    {
        var tag = HtmlTag != null ? $"<{HtmlTag.Name}>" : "anon";

        if (IsBlock)
        {
            return $"{(ParentBox == null ? "Root: " : string.Empty)}{tag} Block {FontSize}, Children:{Boxes.Count}";
        }
        else if (Display == CssConstants.None)
        {
            return $"{(ParentBox == null ? "Root: " : string.Empty)}{tag} None";
        }
        else
        {
            return $"{(ParentBox == null ? "Root: " : string.Empty)}{tag} {Display}: {Text}";
        }
    }
}
