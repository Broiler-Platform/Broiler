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

    private bool UsesBorderBoxSizing =>
        BoxSizing != null && BoxSizing.Equals("border-box", StringComparison.OrdinalIgnoreCase);

    private double ResolveSpecifiedWidthToBorderBox(double cssWidth)
    {
        if (!UsesBorderBoxSizing)
            cssWidth += ActualPaddingLeft + ActualPaddingRight + ActualBorderLeftWidth + ActualBorderRightWidth;

        return Math.Max(0, cssWidth);
    }

    /// <summary>CSS Sizing 3: <c>true</c> for a content-based intrinsic width
    /// keyword (<c>min-content</c>, <c>max-content</c>, <c>fit-content</c> /
    /// <c>fit-content()</c>) that resolves to the box's content size rather than a
    /// length against the containing block.</summary>
    private static bool IsIntrinsicSizingWidthKeyword(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        string v = value.Trim();
        return v.Equals("min-content", StringComparison.OrdinalIgnoreCase)
            || v.Equals("max-content", StringComparison.OrdinalIgnoreCase)
            || v.Equals("fit-content", StringComparison.OrdinalIgnoreCase)
            || v.StartsWith("fit-content(", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>CSS Sizing 3: <c>true</c> for a content-based intrinsic <c>height</c>
    /// keyword. A block box's min-/max-/fit-content block size is its content
    /// height, so such a height must not be treated as a specified length (which,
    /// under <c>box-sizing:border-box</c>, would wrongly reinterpret the already
    /// content-derived height as a border-box value and drop the border/padding);
    /// leave the content-computed <c>ActualBottom</c> in place and let the §10.7
    /// min-/max-height clamp apply.</summary>
    private static bool IsIntrinsicSizingHeightKeyword(string value) =>
        IsIntrinsicSizingWidthKeyword(value);

    private double ResolveSpecifiedHeightToBorderBox(double cssHeight)
    {
        if (!UsesBorderBoxSizing)
            cssHeight += ActualPaddingTop + ActualPaddingBottom + ActualBorderTopWidth + ActualBorderBottomWidth;

        return Math.Max(0, cssHeight);
    }

    /// <summary>
    /// CSS2.1 §10.7: clamp a specified (author-declared) height to
    /// <c>min-height</c>/<c>max-height</c> in the same box-sizing frame (both share
    /// it), returning the clamped specified value — the caller normalizes to the
    /// border box via <see cref="ResolveSpecifiedHeightToBorderBox"/>. A percentage
    /// min-/max-height against an indefinite (auto-height) flow containing block is
    /// treated as its initial value (<c>0</c>/<c>none</c>), per §10.7.
    /// </summary>
    private double ClampSpecifiedHeightToMinMax(double specifiedHeight)
    {
        double cbHeight = (Position == CssConstants.Fixed && LayoutEnvironment != null)
            ? LayoutEnvironment.ViewportSize.Height
            : ContainingBlock?.Size.Height ?? 0;
        bool cbIndefinite = Position is not (CssConstants.Absolute or CssConstants.Fixed)
            && ContainingBlock?.ParentBox != null
            && (ContainingBlock.Height == CssConstants.Auto || string.IsNullOrEmpty(ContainingBlock.Height));

        if (MaxHeight != "none" && !string.IsNullOrEmpty(MaxHeight)
            && !(MaxHeight.Contains('%') && cbIndefinite))
        {
            double maxH = CssValueParser.ParseLength(MaxHeight, cbHeight, GetEmHeight());
            if (specifiedHeight > maxH) specifiedHeight = maxH;
        }
        if (MinHeight != "0" && !string.IsNullOrEmpty(MinHeight)
            && !(MinHeight.Contains('%') && cbIndefinite))
        {
            double minH = CssValueParser.ParseLength(MinHeight, cbHeight, GetEmHeight());
            if (specifiedHeight < minH) specifiedHeight = minH;
        }
        return specifiedHeight;
    }

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

    /// <summary>
    /// PROTOTYPE (BROILER_VERTICAL_FLOW): see
    /// <see cref="CssBoxProperties.WillBeVerticalTransposed"/>.  Walk up the box
    /// tree: the first vertical-writing-mode ancestor (or this box) whose own
    /// parent is NOT vertical is a rotation root and transposes this box.  An
    /// out-of-flow ancestor establishes its own rotation context, so if one is
    /// reached before any vertical root, the rotation of a further vertical
    /// ancestor does not reach this box — matching the runtime, where an abspos
    /// item in a vertical container is left untransposed (its container's
    /// rotation skips it and it is not a root itself).
    /// </summary>
    protected override bool WillBeVerticalTransposed()
    {
        if (!VerticalFlowPrototype.Enabled)
            return false;
        for (CssBox ctx = this; ctx != null; ctx = ctx.ParentBox)
        {
            bool ctxVertical = IsVerticalWritingMode(ctx.WritingMode);
            bool parentVertical = ctx.ParentBox != null
                && IsVerticalWritingMode(ctx.ParentBox.WritingMode);
            if (ctxVertical && !parentVertical)
                return true;
            if (ctx.Position == CssConstants.Absolute || ctx.Position == CssConstants.Fixed)
                return false;
        }
        return false;
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

    public CssBox ContainingBlock
    {
        get
        {
            if (ParentBox == null)
                return this; //This is the initial containing block.

            var box = ParentBox;

            // CSS2.1 §10.1: The containing block for a box is the nearest
            // ancestor that is a block container.  Block containers include:
            //   - block-level boxes (display:block, flex, grid)
            //   - inline-block boxes (display:inline-block)
            //   - list-item boxes
            //   - table cells (display:table-cell)
            //   - table boxes (display:table)
            // Inline-block establishes a BFC (§9.4.1), so its block-level
            // children must use it as their containing block.
            while (!box.IsBlock
                   && box.Display != CssConstants.InlineBlock
                   && box.Display != CssConstants.ListItem
                   && box.Display != CssConstants.Table
                   && box.Display != CssConstants.TableCell
                   && box.ParentBox != null)
            {
                box = box.ParentBox;
            }

            //Comment this following line to treat always superior box as block
            if (box == null)
                throw new Exception("There's no containing block on the chain");

            return box;
        }
    }

    /// <summary>
    /// CSS2.1 §10.1: For absolutely positioned elements, the containing
    /// block is the padding-box of the nearest ancestor with a computed
    /// position of <c>absolute</c>, <c>relative</c>, or <c>fixed</c>.
    /// Falls back to <see cref="ContainingBlock"/> if none is found.
    /// Also checks <see cref="SplitPositionedAncestor"/> which links back
    /// to positioned inlines that were restructured by the block-inside-
    /// inline correction (CSS2.1 §9.2.1.1).
    /// </summary>
    private CssBox FindPositionedContainingBlock()
    {
        var box = ParentBox;
        while (box != null)
        {
            if (box.Position is CssConstants.Relative or CssConstants.Absolute or CssConstants.Fixed || box.ParentBox == null)
                return box;

            // If the block-inside-inline correction split a positioned inline
            // and hoisted this branch out, SplitPositionedAncestor links back
            // to the original positioned inline ancestor.
            if (box.SplitPositionedAncestor is { } spa
                && spa.Position is CssConstants.Relative or CssConstants.Absolute or CssConstants.Fixed)
                return spa;

            box = box.ParentBox;
        }

        return ContainingBlock;
    }

    private bool IsInitialContainingBlock(CssBox cb) =>
        cb.ParentBox == null && LayoutEnvironment != null;

    private void GetAbsoluteContainingBlockPaddingBox(CssBox cb,
        out double cbPadLeft,
        out double cbPadTop,
        out double cbPadWidth,
        out double cbPadHeight)
    {
        // CSS Grid §9: an absolutely-positioned grid item's containing block is
        // the grid area the grid container's track-sizing pass resolved for it,
        // not the container's padding box. All abspos size/offset resolution
        // routes through here, so returning the area makes width/height/inset
        // percentages and the static position use it uniformly.
        if (GridAreaContainingBlock is { } gridArea)
        {
            cbPadLeft = gridArea.Left;
            cbPadTop = gridArea.Top;
            cbPadWidth = gridArea.Width;
            cbPadHeight = gridArea.Height;
            return;
        }

        if (IsInlineContainingBlock(cb))
        {
            var bbox = GetInlineBoundingBox(cb);
            if (bbox != RectangleF.Empty)
            {
                cbPadLeft = bbox.Left;
                cbPadTop = bbox.Top;
                cbPadWidth = bbox.Width;
                cbPadHeight = bbox.Height;
                return;
            }
        }

        if (IsInitialContainingBlock(cb))
        {
            cbPadLeft = 0;
            cbPadTop = 0;
            cbPadWidth = LayoutEnvironment.ViewportSize.Width;
            cbPadHeight = LayoutEnvironment.ViewportSize.Height;
            return;
        }

        cbPadLeft = cb.Location.X + cb.ActualBorderLeftWidth;
        cbPadTop = cb.Location.Y + cb.ActualBorderTopWidth;
        cbPadWidth = cb.Size.Width - cb.ActualBorderLeftWidth - cb.ActualBorderRightWidth;
        cbPadHeight = (cb.ActualBottom - cb.Location.Y) - cb.ActualBorderTopWidth - cb.ActualBorderBottomWidth;

        // Block-axis self-alignment of an absolutely positioned descendant can
        // run before the containing block has resolved its own block size:
        // heights resolve bottom-up, yet abspos children are positioned during
        // the CB's layout, so cb.ActualBottom may still equal cb.Location.Y and
        // cbPadHeight collapses to ~0 — leaving align-self with no IMCB to work
        // within (the box stays at its static position).  Widths resolve
        // top-down, so cbPadWidth is already correct; this only patches the
        // height.  When the CB carries a definite (non-percentage) specified
        // height, derive the padding-box height from it directly.
        if (cbPadHeight <= 0
            && cb.Height != CssConstants.Auto && !string.IsNullOrEmpty(cb.Height)
            && !cb.Height.Contains('%'))
        {
            double cssHeight = CssValueParser.ParseLength(cb.Height, 0, cb.GetEmHeight());
            double borderBoxHeight = cb.ResolveSpecifiedHeightToBorderBox(cssHeight);
            double candidate = borderBoxHeight - cb.ActualBorderTopWidth - cb.ActualBorderBottomWidth;
            if (candidate > cbPadHeight)
                cbPadHeight = candidate;
        }
    }

    /// <summary>
    /// CSS2.1 §10.1: When the containing block for an absolutely positioned
    /// element is formed by an inline-level element, the containing block is
    /// the bounding box around the padding boxes of the first and last inline
    /// boxes generated for that element.  Returns the bounding rectangle in
    /// absolute coordinates, or <see cref="RectangleF.Empty"/> if the inline
    /// has no line-box rectangles and no laid-out children.
    /// </summary>
    private static RectangleF GetInlineBoundingBox(CssBox cb)
    {
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        // Accumulate extents from one box (the original or a fragment).
        void AccumulateBox(CssBox box)
        {
            // Try the inline's own Rectangles (populated when the
            // inline element has direct text words).
            foreach (var rect in box.Rectangles.Values)
            {
                if (rect.Left < minX) minX = rect.Left;
                if (rect.Top < minY) minY = rect.Top;
                if (rect.Right > maxX) maxX = rect.Right;
                if (rect.Bottom > maxY) maxY = rect.Bottom;
            }

            // Also scan child boxes (inline-blocks etc.) for their
            // laid-out positions and sizes.
            foreach (var child in box.Boxes)
            {
                if (child.Size.Width <= 0 && child.Size.Height <= 0)
                    continue;
                float left = child.Location.X;
                float top = child.Location.Y;
                float right = left + child.Size.Width;
                float bottom = (float)child.ActualBottom;
                if (bottom <= top) bottom = top + child.Size.Height;

                if (left < minX) minX = left;
                if (top < minY) minY = top;
                if (right > maxX) maxX = right;
                if (bottom > maxY) maxY = bottom;
            }
        }

        // Scan the original box.
        AccumulateBox(cb);

        // If the positioned inline was split by the block-inside-inline
        // correction, also scan inline fragment copies that received its
        // children so the bounding box covers the full inline extent.
        // Only include fragments that are still inline — block-level
        // anonymous wrappers created during the split are structural
        // containers, not inline fragments.
        if (cb.SplitFragments != null)
        {
            foreach (var frag in cb.SplitFragments)
            {
                if (frag.Display == CssConstants.Inline)
                    AccumulateBox(frag);
            }
        }

        if (minX > maxX || minY > maxY)
            return RectangleF.Empty;

        return new RectangleF(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    /// Returns <c>true</c> when the given box is a pure inline element
    /// (not inline-block/inline-table etc.) whose containing-block extent
    /// must be computed from its line-box rectangles per CSS2.1 §10.1.
    /// </summary>
    private static bool IsInlineContainingBlock(CssBox cb) =>
        cb.Display == CssConstants.Inline;

    /// <summary>
    /// Returns true when <see cref="Height"/> is a percentage that resolves
    /// to auto because the containing block's height is not explicitly
    /// specified (CSS 2.1 §10.5).  Callers must still verify that Height is
    /// not auto/empty before using this — the check only tests whether a
    /// non-auto percentage value should be treated as auto.
    /// </summary>
    internal bool HeightPercentageResolvesToAuto()
    {
        if (!Height.Contains('%'))
            return false;

        // CSS 2.1 §10.5: "A percentage height on the root element is
        // relative to the initial containing block."  The initial
        // containing block always has a definite height (the viewport),
        // so percentage heights on the root element never resolve to auto.
        if (ContainingBlock?.ParentBox == null)
            return false;

        // CSS 2.1 §10.5: the "resolves to auto when the containing block's
        // height is indefinite" rule applies only when "this element is not
        // absolutely positioned".  An absolutely (or fixed) positioned box's
        // containing block is the padding box of its positioned ancestor (or
        // the viewport for the initial containing block), whose height is
        // always definite — so a percentage height never resolves to auto.
        if (Position == CssConstants.Absolute || Position == CssConstants.Fixed)
            return false;

        // CSS Sizing 4 §4: a containing block whose height is auto but that has a
        // preferred aspect-ratio and a definite used width has a definite used
        // block size (its transferred aspect-ratio height), so a percentage height
        // resolves against it rather than to auto — matching the reference browser,
        // which sizes a filling child to the aspect-ratio square.
        if (ContainingBlock.HasDefiniteAspectRatioBlockHeight())
            return false;

        return ContainingBlock.Height == CssConstants.Auto
            || string.IsNullOrEmpty(ContainingBlock.Height);
    }

    /// <summary>CSS Sizing 4 §4: <c>true</c> when this box's block (height) axis is
    /// <c>auto</c> but resolvable from its used width and preferred
    /// <c>aspect-ratio</c>, so its used height is definite for percentage-height
    /// descendants. Scoped to in-flow block-level boxes, matching
    /// <see cref="TryResolveAspectRatioBlockHeight"/>'s applicability.</summary>
    internal bool HasDefiniteAspectRatioBlockHeight() =>
        (Height == CssConstants.Auto || string.IsNullOrEmpty(Height))
        && Display == CssConstants.Block
        && Float == CssConstants.None
        && Position != CssConstants.Absolute && Position != CssConstants.Fixed
        && !IsImage
        && TryResolveAspectRatioBlockHeight(out _);

    /// <summary>
    /// CSS2.1 §10.5: the containing-block height a percentage <c>height</c>
    /// (or percentage <c>min-/max-height</c>) resolves against.  For
    /// fixed-position boxes this is the viewport; for other absolutely
    /// positioned boxes it is the height of the <em>positioned</em> containing
    /// block's padding box (the viewport when that is the initial containing
    /// block) — an abspos box's containing block always has a definite height,
    /// unlike the flow containing block, whose height may be auto/indefinite.
    /// Otherwise the flow containing block's used height is returned.
    /// </summary>
    private double PercentageHeightContainingBlockHeight()
    {
        if (Position == CssConstants.Fixed && LayoutEnvironment != null)
            return LayoutEnvironment.ViewportSize.Height;

        if (Position == CssConstants.Absolute)
        {
            var cb = FindPositionedContainingBlock();
            GetAbsoluteContainingBlockPaddingBox(cb, out _, out _, out _, out double cbHeight);
            return cbHeight;
        }

        if (ContainingBlock?.ParentBox == null && LayoutEnvironment != null)
            return LayoutEnvironment.ViewportSize.Height;

        // A flow containing block with a definite (non-auto, non-percentage)
        // specified height exposes that height to its percentage-height
        // children even before its own block size is applied. Block heights
        // resolve bottom-up — children lay out (and resolve their percentages)
        // before the containing block sets its used height — and a fixed-height
        // box, unlike a percentage-height one, is not pre-resolved into
        // Size.Height (see the §10.5 pre-resolution in the layout pass). Reading
        // Size.Height here would then yield 0 and collapse every percentage-height
        // child. Derive the basis straight from the specification instead, the
        // same way the abspos IMCB fallback does for a definite containing block.
        var flowCb = ContainingBlock;
        if (flowCb != null && flowCb.Height != CssConstants.Auto && !string.IsNullOrEmpty(flowCb.Height)
            && !flowCb.Height.Contains('%'))
        {
            double cssHeight = CssValueParser.ParseLength(flowCb.Height, 0, flowCb.GetEmHeight());
            if (cssHeight > 0)
                return flowCb.ResolveSpecifiedHeightToBorderBox(cssHeight);
        }

        return flowCb?.Size.Height ?? 0;
    }

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

    public void SetGeneratedTextContent(string text)
    {
        CssBox textBox = null;
        foreach (CssBox child in Boxes)
        {
            if (child.HtmlTag == null)
            {
                textBox = child;
                break;
            }
        }

        if (textBox == null)
        {
            textBox = new CssBox(this, null, BaseUrl);
            textBox.InheritStyle();
        }

        textBox.Text = (text ?? string.Empty).AsMemory();
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
            PerformLayoutImp(g);

            // PROTOTYPE (BROILER_VERTICAL_FLOW): once a vertical-writing-mode
            // root and its whole subtree have been laid out in the logical
            // (horizontal) frame, rotate the result into physical space.
            if (VerticalFlowPrototype.Enabled
                && IsVerticalWritingMode(WritingMode)
                && (ParentBox == null || !IsVerticalWritingMode(ParentBox.WritingMode)))
            {
                ApplyVerticalWritingModeFlow();
            }
        }
        catch (Exception ex)
        {
            LayoutEnvironment.ReportLayoutError("Exception in box layout", ex);
        }
    }

    /// <summary>
    /// PROTOTYPE (BROILER_VERTICAL_FLOW), Stage 1: rotate this vertical
    /// writing-mode root's subtree from the logical horizontal layout frame
    /// into physical space.  The inline axis (laid out left→right) becomes
    /// top→bottom; the block axis (laid out top→bottom) becomes left→right
    /// for <c>vertical-lr</c> or right→left for <c>vertical-rl</c>.
    ///
    /// Positions and box/line rectangle extents are swapped; glyph runs keep
    /// their horizontal size (no glyph rotation yet — Stage 2), so this is
    /// positionally correct for square fonts and an approximation otherwise.
    /// </summary>
    internal void ApplyVerticalWritingModeFlow()
    {
        // Capture the root's logical origin and block extent (its logical
        // height) before any coordinate is rewritten.  For vertical-rl the
        // block extent is mirrored so the first line sits at the right edge.
        float rootX = Location.X;
        float rootY = Location.Y;
        double logicalBlockExtent = ActualBottom - Location.Y;
        bool mirror = WritingMode is "vertical-rl" or "sideways-rl";

        // Where the rotated root's border-box sits horizontally depends on whether
        // its writing mode is the *principal* (viewport) writing mode or a local
        // orthogonal flow:
        //
        //  • Principal writing mode — a vertical-rl root/body whose value
        //    propagates to the viewport (CSS Writing Modes §3.1). The whole page's
        //    block flow runs right→left, so its content begins at the viewport's
        //    right edge. This logical frame shrink-wrapped the root to its content
        //    width and left-aligned it, so shift the rotated subtree right until
        //    the root's block-start (right) edge meets the containing block's
        //    content-right — putting the first block at the top-right corner.
        //
        //  • Local orthogonal flow — a vertical-rl block nested inside a
        //    horizontal-tb (or vertical-lr) containing block. Its border-box is
        //    placed by that containing block's own flow (inline-start / left for an
        //    LTR horizontal container), independent of the box's own writing mode;
        //    Chromium left-aligns such a box even with a definite width. Its
        //    block-start being on the right only governs where its *content* flows
        //    (right→left, handled by the `mirror` transform on descendants and
        //    words below), not where the box itself is positioned — so no shift.
        //
        // The rotation-root test (this box vertical, its parent not) already
        // guarantees a `<body>` reaches here only when its parent `<html>` is
        // horizontal-tb, i.e. exactly when the body's writing mode propagates to
        // the viewport — so "root element or body" is the principal-WM signal.
        // Out-of-flow and inline-level roots are positioned by their own machinery
        // (abspos self-alignment, the inline formatting context) and keep offset 0.
        bool establishesPrincipalWm = ParentBox == null
            || (HtmlTag is { } tag
                && (tag.Name.Equals("body", StringComparison.OrdinalIgnoreCase)
                    || tag.Name.Equals("html", StringComparison.OrdinalIgnoreCase)));

        // A right-floated orthogonal box is a second right-anchored case. Float
        // placement ran in the logical horizontal frame, where it pinned the box's
        // *logical* right (its physical bottom) to the container's content-right —
        // leaving the rotated border-box short of that edge by the difference
        // between its logical and physical widths. Re-pin its physical right edge
        // to the container's content-right so it renders flush against it, matching
        // the block-start alignment the principal-WM shift performs. (Left floats
        // and in-flow boxes are already at their correct physical left, offset 0.)
        bool rightAnchored = establishesPrincipalWm || Float == CssConstants.Right;

        double blockOffset = 0;
        if (mirror && rightAnchored
            && Position != CssConstants.Absolute && Position != CssConstants.Fixed
            && Display != CssConstants.InlineBlock
            && Display != "inline-flex" && Display != "inline-grid"
            && ContainingBlock is { } cb)
        {
            double cbContentRight = cb.Location.X + cb.Size.Width
                - cb.ActualPaddingRight - cb.ActualBorderRightWidth;
            double desiredRootRight = cbContentRight - ActualMarginRight;
            double currentRootRight = rootX + logicalBlockExtent;
            blockOffset = desiredRootRight - currentRootRight;
        }

        TransformVerticalSubtree(this, rootX, rootY, logicalBlockExtent, mirror, blockOffset, isRoot: true);
    }

    private static void TransformVerticalSubtree(
        CssBox box, float rootX, float rootY, double blockExtent, bool mirror, double blockOffset, bool isRoot)
    {
        // --- Box border-box: Location + Size ---
        // logical (x,y) measured from root origin → physical (y,x): the inline
        // offset becomes vertical, the block offset becomes horizontal.
        double logicalLeft = box.Location.X - rootX;
        double logicalTop = box.Location.Y - rootY;
        double logicalWidth = box.Size.Width;
        double logicalHeight = box.ActualBottom - box.Location.Y;

        // The root keeps its own physical origin (the parent placed it in the
        // horizontal frame), apart from the right-alignment shift; only its size
        // is rotated.  Descendants rotate their position relative to the root.
        if (!isRoot)
        {
            double physLeft = mirror
                ? blockExtent - logicalTop - logicalHeight
                : logicalTop;
            box.Location = new PointF(rootX + (float)(blockOffset + physLeft), rootY + (float)logicalLeft);
        }
        else if (blockOffset != 0)
        {
            box.Location = new PointF(rootX + (float)blockOffset, rootY);
        }

        box.Size = new SizeF((float)logicalHeight, (float)logicalWidth);
        box.ActualBottom = box.Location.Y + logicalWidth;

        // Per-line rectangles cached on the box itself (inline backgrounds).
        var boxRectKeys = new List<CssLineBox>(box.Rectangles.Keys);
        foreach (var k in boxRectKeys)
            box.Rectangles[k] = RotateRect(box.Rectangles[k], rootX, rootY, blockExtent, mirror, blockOffset);

        // --- Line boxes owned by this box: words + per-box rectangles ---
        foreach (var line in box.LineBoxes)
        {
            var rotatedWords = new List<CssRect>(line.Words.Count);
            foreach (var word in line.Words)
            {
                // Column X = the line's block offset (lines advance left→right
                // for vertical-lr, right→left for vertical-rl).  Column Y top =
                // the word's inline offset (the inline axis runs top→bottom).
                double wLeft = word.Left - rootX;
                double wTop = word.Top - rootY;
                double colX = rootX + blockOffset + (mirror ? blockExtent - wTop - word.Height : wTop);
                double colTop = rootY + wLeft;

                // Stage 3: decompose a multi-glyph run into per-glyph cells
                // stacked along the inline (vertical) axis.  Each glyph advances
                // by its inline advance (≈ run width / glyph count — exact for
                // monospace/square fonts, approximate otherwise).  This is what
                // the position-only transform alone could not do: without it a
                // run paints horizontally and overlaps the next column.
                string text = word.Text;
                if (text != null && text.Length > 1 && !word.IsLineBreak)
                {
                    double advance = word.Width / text.Length;
                    for (int i = 0; i < text.Length; i++)
                    {
                        var glyph = new CssRectWord(word.OwnerBox, text[i].ToString(), false, false)
                        {
                            Left = colX,
                            Top = colTop + i * advance,
                            Width = advance,
                            Height = word.Height,
                        };
                        rotatedWords.Add(glyph);
                    }
                }
                else
                {
                    word.Left = colX;
                    word.Top = colTop;
                    rotatedWords.Add(word);
                }
            }

            line.Words.Clear();
            line.Words.AddRange(rotatedWords);

            var keys = new List<CssBox>(line.Rectangles.Keys);
            foreach (var k in keys)
                line.Rectangles[k] = RotateRect(line.Rectangles[k], rootX, rootY, blockExtent, mirror, blockOffset);
        }

        foreach (var child in box.Boxes)
        {
            if (child.Display == CssConstants.None)
                continue;
            // Out-of-flow descendants are excluded from the rotation: an
            // absolutely/fixed-positioned box is placed in *physical* space by
            // the abspos self-alignment (which is already writing-mode aware and,
            // per WillBeVerticalTransposed, treats abspos boxes as not
            // transposed). Rotating it here would apply the transform twice and
            // tear it away from its resolved position (WPT css-align/abspos
            // *-default-overflow-vrl-* regressed when an inline-block vertical
            // container began rotating its abspos children).
            if (child.Position is CssConstants.Absolute or CssConstants.Fixed)
                continue;
            TransformVerticalSubtree(child, rootX, rootY, blockExtent, mirror, blockOffset, isRoot: false);
        }
    }

    private static RectangleF RotateRect(RectangleF r, float rootX, float rootY, double blockExtent, bool mirror, double blockOffset)
    {
        double logicalLeft = r.X - rootX;
        double logicalTop = r.Y - rootY;
        double physLeft = mirror ? blockExtent - logicalTop - r.Height : logicalTop;
        return new RectangleF(
            rootX + (float)(blockOffset + physLeft),
            rootY + (float)logicalLeft,
            r.Height,
            r.Width);
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

    public void ParseToWords()
    {
        Words.Clear();

        // CSS2.1 §4.3.8: UAs should not render characters from the Unicode
        // "control characters" category (C0 U+0000–U+001F except tab/LF/CR,
        // and C1 U+007F–U+009F).  Strip them before word splitting.
        // Per HTML spec §13.2.2, U+0000 (NULL) is replaced with U+FFFD
        // (REPLACEMENT CHARACTER) so it remains visible.
        var textSpan = _text.Span;
        bool hasControl = false;
        for (int i = 0; i < textSpan.Length; i++)
        {
            char c = textSpan[i];
            if (c != '\t' && c != '\n' && c != '\r'
                && (char.IsControl(c) || (c >= '\u007F' && c <= '\u009F')))
            {
                hasControl = true;
                break;
            }
        }
        if (hasControl)
        {
            var sb = new System.Text.StringBuilder(textSpan.Length);
            for (int i = 0; i < textSpan.Length; i++)
            {
                char c = textSpan[i];
                if (c == '\0')
                    sb.Append('\uFFFD'); // HTML spec: NULL → REPLACEMENT CHARACTER
                else if (c == '\t' || c == '\n' || c == '\r'
                    || (!char.IsControl(c) && (c < '\u007F' || c > '\u009F')))
                    sb.Append(c);
            }
            _text = sb.ToString().AsMemory();
        }

        int startIdx = 0;
        bool preserveSpaces = WhiteSpace == CssConstants.Pre || WhiteSpace == CssConstants.PreWrap;
        bool respoctNewline = preserveSpaces || WhiteSpace == CssConstants.PreLine;

        textSpan = _text.Span;
        while (startIdx < textSpan.Length)
        {
            while (startIdx < textSpan.Length && textSpan[startIdx] == '\r')
                startIdx++;

            if (startIdx >= textSpan.Length)
                continue;

            var endIdx = startIdx;

            while (endIdx < textSpan.Length && char.IsWhiteSpace(textSpan[endIdx]) && textSpan[endIdx] != '\n')
                endIdx++;

            if (endIdx > startIdx)
            {
                if (preserveSpaces)
                {
                    // CSS2.1 §16.6: For pre-wrap, emit each space as a
                    // separate word so the layout engine can break lines
                    // at any space position.  For pre, emit the entire
                    // whitespace run as one word (no wrapping allowed).
                    if (WhiteSpace == CssConstants.PreWrap)
                    {
                        // Cache " " string to avoid per-char allocation
                        const string singleSpace = " ";
                        for (int i = startIdx; i < endIdx; i++)
                        {
                            var ch = _text.Slice(i, 1).ToString();
                            Words.Add(new CssRectWord(this, ch == " " ? singleSpace : ch, false, false));
                        }
                    }
                    else
                    {
                        Words.Add(new CssRectWord(this, WebUtility.HtmlDecode(_text.Slice(startIdx, endIdx - startIdx).ToString()), false, false));
                    }
                }
            }
            else
            {
                endIdx = startIdx;

                while (endIdx < textSpan.Length && !char.IsWhiteSpace(textSpan[endIdx]) && textSpan[endIdx] != '-' && WordBreak != CssConstants.BreakAll && !CommonUtils.IsAsianCharecter(textSpan[endIdx]))
                    endIdx++;

                if (endIdx < textSpan.Length && (textSpan[endIdx] == '-' || WordBreak == CssConstants.BreakAll || CommonUtils.IsAsianCharecter(textSpan[endIdx])))
                {
                    endIdx++;
                    if (endIdx < textSpan.Length &&
                        char.IsHighSurrogate(textSpan[endIdx - 1]) &&
                        char.IsLowSurrogate(textSpan[endIdx]))
                    {
                        endIdx++;
                    }
                }

                if (endIdx > startIdx)
                {
                    var hasSpaceBefore = !preserveSpaces && startIdx > 0 && Words.Count == 0 && char.IsWhiteSpace(textSpan[startIdx - 1]);
                    var hasSpaceAfter = !preserveSpaces && endIdx < textSpan.Length && char.IsWhiteSpace(textSpan[endIdx]);

                    Words.Add(new CssRectWord(this, WebUtility.HtmlDecode(_text.Slice(startIdx, endIdx - startIdx).ToString()), hasSpaceBefore, hasSpaceAfter));
                }
            }

            // create new-line word so it will effect the layout
            if (endIdx < textSpan.Length && textSpan[endIdx] == '\n')
            {
                endIdx++;

                if (respoctNewline)
                    Words.Add(new CssRectWord(this, "\n", false, false));
            }

            startIdx = endIdx;
        }
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

    /// <summary>
    /// Whether a concrete <c>justify-self</c> alignment (one that actually shifts
    /// the box) is in effect, after resolving <c>auto</c> to the parent's
    /// <c>justify-items</c> and the legacy <c>text-align:-webkit-*</c> fallback.
    /// Mirrors the resolution in <see cref="PerformLayoutImp"/>'s block
    /// justify-self step; used to avoid double-applying alignment with the
    /// CSS2.1 §10.3.3 over-constrained-margin positioning.
    /// </summary>
    private bool HasConcreteJustifySelf()
    {
        string js = JustifySelf?.Trim().ToLowerInvariant() ?? "auto";
        if (js == "auto")
            js = ParentBox?.JustifyItems?.Trim().ToLowerInvariant() ?? "normal";
        if (js is "normal" or "stretch" or "auto" or "legacy")
        {
            js = (ParentBox?.TextAlign?.Trim().ToLowerInvariant()) switch
            {
                "-webkit-right" => "right",
                "-webkit-center" => "center",
                "-webkit-left" => "left",
                _ => js,
            };
        }
        return js is "center" or "end" or "flex-end" or "self-end" or "right"
            or "start" or "flex-start" or "self-start" or "left";
    }

    protected virtual void PerformLayoutImp(ILayoutEnvironment g)
    {
        if (Display != CssConstants.None)
        {
            RectanglesReset();
            MeasureWordsSize(g);
        }

        // CI fallback for the Broiler.HTML submodule <br> patch
        // (patches/0002-broiler-html-br-after-inline-block.patch): DomParser
        // gives a <br> a ".95em" empty-line height when it "follows a block".
        // An atomic inline-block carries no text words, so it is misclassified
        // as block-level and a <br> after it spuriously inserts a full empty
        // line, pushing every following block sibling ~1em down.  Such a <br>
        // merely ends the inline-block's line, so drop its empty-line height.
        // The previous in-flow sibling (an anonymous block wrapping the
        // inline-block, or the inline-block itself) is already laid out by the
        // time this block runs.  Harmless once the submodule patch lands (the
        // <br> then carries no .95em height to drop).
        if (IsBrElement && !string.IsNullOrEmpty(Height) && Height != CssConstants.Auto
            && CssLayoutEngine.EndsWithAtomicInlineBlock(LayoutBoxUtils.GetPreviousSibling(this)))
        {
            Height = CssConstants.Auto;
        }

        // CSS Box Model 4 §6.2: margin-trim zeroes the block-axis margins of
        // this container's first/last in-flow block-level children before they
        // are laid out, so the trimmed margins collapse to nothing.
        ApplyMarginTrim();

        if (IsBlock || Display == CssConstants.ListItem || Display == CssConstants.Table || Display == CssConstants.InlineTable || Display == CssConstants.TableCell)
        {
            // Because their width and height are set by CssTable
            if (Display != CssConstants.TableCell && Display != CssConstants.Table)
            {
                // CSS2.1 §9.6.1: The containing block for a fixed-position
                // element is the viewport (initial containing block).
                // CSS2.1 §10.1: For absolutely positioned elements, the
                // containing block is the padding-box of the nearest
                // positioned ancestor.
                // Use the viewport width for percentage/auto resolution.
                double width;
                if (Position == CssConstants.Fixed && LayoutEnvironment != null)
                {
                    width = LayoutEnvironment.ViewportSize.Width;
                }
                else if (Position == CssConstants.Absolute)
                {
                    var cb = FindPositionedContainingBlock();
                    GetAbsoluteContainingBlockPaddingBox(cb, out _, out _, out width, out _);
                }
                else
                {
                    width = ContainingBlock.Size.Width
                            - ContainingBlock.ActualPaddingLeft - ContainingBlock.ActualPaddingRight
                            - ContainingBlock.ActualBorderLeftWidth - ContainingBlock.ActualBorderRightWidth;
                }

                if (IsIntrinsicWidthKeyword(Width)
                    && Float == CssConstants.None
                    && Position != CssConstants.Absolute && Position != CssConstants.Fixed)
                {
                    // CSS Sizing 3 §5: width resolves to an intrinsic size
                    // (min-content / max-content / fit-content).
                    width = ResolveIntrinsicWidth(g, Width, width);
                }
                else if (Width != CssConstants.Auto && !string.IsNullOrEmpty(Width)
                    && !IsIntrinsicWidthKeyword(Width))
                {
                    double containingWidth = width;
                    width = string.Equals(Width, "inherit", StringComparison.OrdinalIgnoreCase) && GetParent() != null
                        ? GetParent().ActualWidth
                        : ParseLengthWithLineHeight(Width, containingWidth);

                    // CSS2.1 §10.4: Apply max-width constraint
                    if (MaxWidth != "none" && !string.IsNullOrEmpty(MaxWidth))
                    {
                        double maxW = ParseLengthWithLineHeight(MaxWidth, containingWidth);
                        if (width > maxW) width = maxW;
                    }

                    // CSS2.1 §10.4: Apply min-width constraint (min wins over max per §10.4)
                    if (MinWidth != "0" && !string.IsNullOrEmpty(MinWidth))
                    {
                        double minW = ParseLengthWithLineHeight(MinWidth, containingWidth);
                        if (width < minW) width = minW;
                    }

                    width = ResolveSpecifiedWidthToBorderBox(width);
                }
                else if ((Position == CssConstants.Absolute || Position == CssConstants.Fixed)
                    && Left != null && Left != CssConstants.Auto
                    && Right != null && Right != CssConstants.Auto)
                {
                    // CSS2.1 §10.3.7: For absolutely positioned, non-replaced
                    // elements when width is auto and both left and right are
                    // specified, compute width from the constraint equation:
                    // left + margin-left + width + margin-right + right = CB width
                    double cbContentWidth = width;
                    if (Position == CssConstants.Fixed && LayoutEnvironment != null)
                        cbContentWidth = LayoutEnvironment.ViewportSize.Width;
                    double cssLeft = CssValueParser.ParseLength(Left, cbContentWidth, GetEmHeight());
                    double cssRight = CssValueParser.ParseLength(Right, cbContentWidth, GetEmHeight());
                    width = cbContentWidth - cssLeft - cssRight - ActualMarginLeft - ActualMarginRight;
                    if (width < 0) width = 0;
                    width = ResolveSpecifiedWidthToBorderBox(width);
                }

                // CSS2.1 §10.4: Apply max-width constraint even when
                // Width is auto — the tentative used width must not exceed
                // max-width.
                if (MaxWidth != "none" && !string.IsNullOrEmpty(MaxWidth))
                {
                    double maxW = ParseLengthWithLineHeight(MaxWidth, width);
                    maxW = ResolveSpecifiedWidthToBorderBox(maxW);
                    if (width > maxW) width = maxW;
                }

                // CSS2.1 §10.4: Apply min-width constraint (min wins over
                // max per §10.4) — also when Width is auto.
                if (MinWidth != "0" && !string.IsNullOrEmpty(MinWidth))
                {
                    double minW = ParseLengthWithLineHeight(MinWidth, width);
                    minW = ResolveSpecifiedWidthToBorderBox(minW);
                    if (width < minW) width = minW;
                }

                Size = new SizeF((float)width, Size.Height);

                // CSS2.1 §10.3.3: For block-level, non-replaced elements in
                // normal flow with an explicit width and auto margins, resolve
                // the auto margins so the element is centered horizontally.
                if (Width != CssConstants.Auto && !string.IsNullOrEmpty(Width)
                    && Float == CssConstants.None
                    && Position != CssConstants.Absolute && Position != CssConstants.Fixed)
                {
                    double containingContentWidth = ContainingBlock.Size.Width
                        - ContainingBlock.ActualPaddingLeft - ContainingBlock.ActualPaddingRight
                        - ContainingBlock.ActualBorderLeftWidth - ContainingBlock.ActualBorderRightWidth;
                    double remainingSpace = containingContentWidth - Size.Width;

                    if (MarginLeft == CssConstants.Auto && MarginRight == CssConstants.Auto)
                    {
                        if (remainingSpace >= 0)
                        {
                            string halfMargin = (remainingSpace / 2).ToString("F4",
                                CultureInfo.InvariantCulture) + "px";
                            MarginLeft = halfMargin;
                            MarginRight = halfMargin;
                        }
                        else
                        {
                            MarginLeft = "0";
                            MarginRight = "0";
                        }
                    }
                    else if (MarginLeft == CssConstants.Auto)
                    {
                        double rightMargin = ActualMarginRight;
                        double leftMargin = Math.Max(0, remainingSpace - rightMargin);
                        MarginLeft = leftMargin.ToString("F4",
                            CultureInfo.InvariantCulture) + "px";
                    }
                    else if (MarginRight == CssConstants.Auto)
                    {
                        double leftMargin = ActualMarginLeft;
                        double rightMargin = Math.Max(0, remainingSpace - leftMargin);
                        MarginRight = rightMargin.ToString("F4",
                            CultureInfo.InvariantCulture) + "px";
                    }
                    else if ((IsBlock || Display == CssConstants.ListItem)
                             && remainingSpace >= 0
                             && ContainingBlock?.Position != CssConstants.Absolute
                             && ContainingBlock?.Position != CssConstants.Fixed
                             && !IsVerticalWritingMode(ContainingBlock?.WritingMode ?? WritingMode)
                             && (ContainingBlock?.Direction ?? Direction) == "rtl"
                             && !HasConcreteJustifySelf())
                    {
                        // CSS2.1 §10.3.3: when width and both margins are
                        // specified the box is over-constrained, so one used
                        // margin is ignored and solved for. In a left-to-right
                        // containing block that is margin-right (and the box
                        // stays at its margin-left, which the X computation
                        // already honours, so no adjustment is needed). In a
                        // right-to-left containing block margin-LEFT is the one
                        // ignored, so recompute it from the remaining space —
                        // this positions the box against the right edge instead
                        // of the left (e.g. a fixed-width block in a dir=rtl
                        // container; WPT css-anchor-position/anchor-position-borders).
                        // Skipped when a concrete justify-self alignment applies,
                        // because that is resolved later (see ApplyBlockJustifySelf)
                        // and would otherwise be double-applied.
                        double leftMargin = remainingSpace - ActualMarginRight;
                        MarginLeft = leftMargin.ToString("F4",
                            CultureInfo.InvariantCulture) + "px";
                    }
                }

                // CSS2.1 §10.3.7: Absolutely positioned non-replaced elements
                // with auto width use shrink-to-fit when at least one of
                // left/right is auto.  Shrink-to-fit =
                //   min(max(preferred_minimum, available), preferred)
                if ((Width == CssConstants.Auto || string.IsNullOrEmpty(Width))
                    && (Position == CssConstants.Absolute || Position == CssConstants.Fixed)
                    && (Left == null || Left == CssConstants.Auto
                     || Right == null || Right == CssConstants.Auto))
                {
                    // Ensure descendant word sizes (and ActualWordSpacing) are
                    // measured before computing intrinsic min/max widths.
                    // Without this, word.FullWidth may be NaN because
                    // ActualWordSpacing defaults to NaN until MeasureWordSpacing
                    // runs, causing the entire shrink-to-fit result to be NaN.
                    EnsureDescendantWordsMeasured(g);

                    // Compute preferred width by independently measuring each
                    // direct child and taking the maximum.  This correctly
                    // treats each block/float child as its own "line" and avoids
                    // the additive accumulation in GetMinMaxSumWords where a
                    // float's width would incorrectly sum with a preceding
                    // block child's width.
                    double preferred = ComputeShrinkToFitWidth();
                    double available = width - ActualMarginLeft - ActualMarginRight;

                    GetMinMaxWidth(out double prefMin, out _);
                    // Guard against NaN from unmeasured descendants
                    if (double.IsNaN(prefMin)) prefMin = 0;
                    if (double.IsNaN(preferred)) preferred = 0;
                    double stfWidth = Math.Min(Math.Max(prefMin, available), preferred);

                    if (MaxWidth != "none" && !string.IsNullOrEmpty(MaxWidth))
                    {
                        double maxW = ParseLengthWithLineHeight(MaxWidth, width);
                        if (stfWidth > maxW) stfWidth = maxW;
                    }
                    if (MinWidth != "0" && !string.IsNullOrEmpty(MinWidth))
                    {
                        double minW = ParseLengthWithLineHeight(MinWidth, width);
                        if (stfWidth < minW) stfWidth = minW;
                    }

                    // CSS2.1 §10.3.7: Shrink-to-fit gives the content
                    // width; add own borders and padding for the border-box
                    // width that Size.Width represents.
                    stfWidth += ActualBorderLeftWidth + ActualBorderRightWidth
                              + ActualPaddingLeft + ActualPaddingRight;

                    Size = new SizeF((float)stfWidth, Size.Height);
                }
                else if ((Width == CssConstants.Auto || string.IsNullOrEmpty(Width))
                    && Float != CssConstants.None)
                {
                    // CSS2.1 §10.3.5: Floating non-replaced elements with
                    // 'width: auto' use shrink-to-fit width.
                    EnsureDescendantWordsMeasured(g);

                    double preferred = ComputeShrinkToFitWidth();
                    double available = width - ActualMarginLeft - ActualMarginRight;

                    GetMinMaxWidth(out double prefMin, out _);
                    if (double.IsNaN(prefMin)) prefMin = 0;
                    if (double.IsNaN(preferred)) preferred = 0;
                    double stfWidth = Math.Min(Math.Max(prefMin, available), preferred);

                    if (MaxWidth != "none" && !string.IsNullOrEmpty(MaxWidth))
                    {
                        double maxW = ParseLengthWithLineHeight(MaxWidth, width);
                        if (stfWidth > maxW) stfWidth = maxW;
                    }
                    if (MinWidth != "0" && !string.IsNullOrEmpty(MinWidth))
                    {
                        double minW = ParseLengthWithLineHeight(MinWidth, width);
                        if (stfWidth < minW) stfWidth = minW;
                    }

                    stfWidth += ActualBorderLeftWidth + ActualBorderRightWidth
                              + ActualPaddingLeft + ActualPaddingRight;

                    Size = new SizeF((float)stfWidth, Size.Height);
                }
                else if ((Width == CssConstants.Auto || string.IsNullOrEmpty(Width))
                    && Float == CssConstants.None
                    && Position != CssConstants.Absolute && Position != CssConstants.Fixed
                    && VerticalFlowPrototype.Enabled
                    && IsVerticalWritingMode(WritingMode)
                    && (ParentBox == null || !IsVerticalWritingMode(ParentBox.WritingMode))
                    && ContainingBlock is { ParentBox: not null } orthoCb
                    && (string.IsNullOrEmpty(orthoCb.Height) || orthoCb.Height == CssConstants.Auto))
                {
                    // CSS Writing Modes 4 §7.3 (auto-sizing in orthogonal flows):
                    // a box establishing an orthogonal flow — here a vertical
                    // writing-mode box inside a non-vertical containing block — with
                    // an auto inline size is sized to fit-content, NOT stretched to
                    // the containing block's (perpendicular) inline size. In the
                    // vertical-flow prototype this box is laid out in a logical
                    // horizontal frame where its logical width IS its inline size, so
                    // compute that width as shrink-to-fit; the post-layout rotation
                    // (ApplyVerticalWritingModeFlow) then maps it onto physical height.
                    // Gated on an indefinite containing-block block size (an auto-height
                    // in-flow ancestor) so a definite orthogonal size — a root box
                    // filling the viewport, or an explicit-height container — keeps the
                    // existing fill behaviour. Without this, an empty (or short)
                    // vertical box fills the container width and rotates into a
                    // viewport-tall strip instead of collapsing to its content
                    // (WPT css-grid/grid-lanes row-subgrid-auto-fill-007).
                    EnsureDescendantWordsMeasured(g);

                    double preferred = ComputeShrinkToFitWidth();
                    // Indefinite orthogonal available inline size falls back to the
                    // initial containing block (viewport) block size — here the
                    // viewport height, the vertical inline axis's extent.
                    double available = LayoutEnvironment?.ViewportSize.Height ?? width;

                    GetMinMaxWidth(out double prefMin, out _);
                    if (double.IsNaN(prefMin)) prefMin = 0;
                    if (double.IsNaN(preferred)) preferred = 0;
                    double stfWidth = Math.Min(Math.Max(prefMin, available), preferred);

                    // Border/padding added for the border-box width Size.Width holds
                    // (shrink-to-fit yields a content width). max-width/min-width are
                    // physical-width (block-size) constraints and do not clamp the
                    // inline size resolved here.
                    stfWidth += ActualBorderLeftWidth + ActualBorderRightWidth
                              + ActualPaddingLeft + ActualPaddingRight;

                    Size = new SizeF((float)stfWidth, Size.Height);
                }
                else if (IsIntrinsicSizingWidthKeyword(Width))
                {
                    // CSS Sizing 3 §5.1: an intrinsic-sizing keyword width resolves
                    // to the box's content-based size, not the containing block.
                    //   min-content → the min-content (preferred-minimum) width,
                    //   max-content → the max-content (preferred) width,
                    //   fit-content → min(max(min-content, available), max-content).
                    // Without this these keywords fell through to the stretched
                    // container width (e.g. a shrink-to-fit grid stayed 1024 instead
                    // of its min-width — WPT css-grid grid-auto-repeat-min-size-001).
                    // Mirrors the float shrink-to-fit path (content widths + own
                    // border/padding for the border-box Size.Width, then min/max-width).
                    EnsureDescendantWordsMeasured(g);

                    double ownPadBorder = ActualBorderLeftWidth + ActualBorderRightWidth
                                        + ActualPaddingLeft + ActualPaddingRight;
                    // Both contributions must be in the same frame: ComputeShrinkToFitWidth
                    // returns a content-box width, but GetMinMaxWidth returns a border-box
                    // one, so strip this box's own padding/border off the min side before
                    // combining — otherwise fit-content double-counts it.
                    double maxContent = ComputeShrinkToFitWidth();
                    GetMinMaxWidth(out double minContentBorderBox, out _);
                    if (double.IsNaN(minContentBorderBox)) minContentBorderBox = 0;
                    if (double.IsNaN(maxContent)) maxContent = 0;
                    double minContent = Math.Max(0, minContentBorderBox - ownPadBorder);
                    double available = width - ActualMarginLeft - ActualMarginRight;

                    double resolved = Width.StartsWith("min-content", StringComparison.OrdinalIgnoreCase)
                        ? minContent
                        : Width.StartsWith("max-content", StringComparison.OrdinalIgnoreCase)
                            ? maxContent
                            : Math.Min(Math.Max(minContent, available), maxContent); // fit-content

                    if (MaxWidth != "none" && !string.IsNullOrEmpty(MaxWidth))
                    {
                        double maxW = ParseLengthWithLineHeight(MaxWidth, width);
                        if (resolved > maxW) resolved = maxW;
                    }
                    if (MinWidth != "0" && !string.IsNullOrEmpty(MinWidth))
                    {
                        double minW = ParseLengthWithLineHeight(MinWidth, width);
                        if (resolved < minW) resolved = minW;
                    }

                    resolved += ownPadBorder;
                    Size = new SizeF((float)resolved, Size.Height);
                }
                else if (Width == CssConstants.Auto || string.IsNullOrEmpty(Width))
                {
                    // Margins reduce the box width only for auto-width elements.
                    // For explicit widths, margins affect position only (CSS1 box model).
                    Size = new SizeF((float)(width - ActualMarginLeft - ActualMarginRight), Size.Height);
                }
            }

            if (Display != CssConstants.TableCell)
            {
                var prevSibling = LayoutBoxUtils.GetPreviousSibling(this);

                // Compute the static position for all elements (including
                // position:fixed).  Fixed elements need the static position
                // as fallback when offset properties are auto (CSS2.1 §10.6.4).
                {
                    double left = ContainingBlock.Location.X + ContainingBlock.ActualPaddingLeft + ActualMarginLeft + ContainingBlock.ActualBorderLeftWidth;

                    // CSS2.1 §9.5: floats are out of normal flow. Non-floated
                    // blocks must be positioned as if preceding floats do not
                    // exist.  For cleared elements this also prevents margin
                    // collapsing with the float (CSS2.1 §8.3.1).
                    var flowPrev = prevSibling;
                    if (Float == CssConstants.None
                        && flowPrev != null && flowPrev.Float != CssConstants.None)
                    {
                        flowPrev = LayoutBoxUtils.GetPreviousInFlowSibling(flowPrev);
                    }

                    // CSS2.1 §9.4.3: Relative positioning is visual-only.
                    // Use the flow-position bottom (before relative offset)
                    // when computing the next sibling's position.
                    double flowPrevBottom = flowPrev?.ActualBottom ?? 0;
                    if (flowPrev is CssBox flowPrevBox && flowPrevBox.Position == CssConstants.Relative)
                        flowPrevBottom -= CssBoxHelper.GetRelativeOffsetY(flowPrevBox);

                    // CSS2.1 §8.3.1: MarginTopCollapse may propagate margins
                    // and update the parent's Location, so compute it before
                    // reading ParentBox.ClientTop.
                    double marginCollapse = MarginTopCollapse(flowPrev);
                    double top = (flowPrev == null && ParentBox != null ? ParentBox.ClientTop : ParentBox == null ? Location.Y : 0) + marginCollapse + flowPrevBottom;

                    // --- Float positioning ---
                    if (Float != CssConstants.None)
                    {
                        // Align Y with previous float sibling if consecutive
                        if (prevSibling != null && prevSibling.Float != CssConstants.None)
                            top = prevSibling.Location.Y;

                        double containerLeft = ContainingBlock.Location.X + ContainingBlock.ActualPaddingLeft + ContainingBlock.ActualBorderLeftWidth;
                        double containerRight = ContainingBlock.ClientLeft + ContainingBlock.AvailableWidth;
                        double floatHeight = Math.Max(ActualHeight + ActualPaddingTop + ActualPaddingBottom + ActualBorderTopWidth + ActualBorderBottomWidth, 1);

                        // Collect all preceding floats in the BFC, including
                        // those nested inside non-BFC siblings (CSS2.1 §9.5.1).
                        var precedingFloats = CssBoxHelper.CollectPrecedingFloatsInBfc(this);

                        // CSS2.1 §9.5.1 rule 4: A floating box's outer top
                        // (margin edge) may not be higher than the top of its
                        // containing block.  `top` already includes the margin
                        // contribution (from MarginTopCollapse), so the outer
                        // (margin-edge) top = top - ActualMarginTop.  The
                        // constraint outer_top >= ClientTop translates to:
                        //   top >= ClientTop + ActualMarginTop
                        // This allows negative margins to pull the float above
                        // the content-area edge while still honoring the rule.
                        if (ParentBox != null)
                            top = Math.Max(top, ParentBox.ClientTop + ActualMarginTop);

                        // CSS2.1 §9.5.1 rule 6: The outer top of a floating
                        // box may not be higher than the outer top of any
                        // block or floated box generated by an element earlier
                        // in the source document.
                        foreach (var pf in precedingFloats)
                            top = Math.Max(top, pf.Location.Y);

                        if (Float == CssConstants.Left)
                        {
                            // Iteratively resolve collisions with all prior floats (CSS1 §5.5.25)
                            for (int iter = 0; iter < 100; iter++)
                            {
                                left = containerLeft + ActualMarginLeft;

                                foreach (var floatBox in precedingFloats)
                                {
                                    if (floatBox.Float == CssConstants.Left)
                                    {
                                        double fBottom = floatBox.ActualBottom;
                                        if (top < fBottom && top + floatHeight > floatBox.Location.Y)
                                            left = Math.Max(left, floatBox.Location.X + floatBox.Size.Width + floatBox.ActualMarginRight + ActualMarginLeft);
                                    }
                                }

                                // Also ensure left float doesn't overlap with right floats
                                double effectiveRight = containerRight;
                                foreach (var floatBox in precedingFloats)
                                {
                                    if (floatBox.Float == CssConstants.Right)
                                    {
                                        double fBottom = floatBox.ActualBottom;
                                        if (top < fBottom && top + floatHeight > floatBox.Location.Y)
                                            effectiveRight = Math.Min(effectiveRight, floatBox.Location.X - floatBox.ActualMarginLeft);
                                    }
                                }

                                if (left + Size.Width <= effectiveRight)
                                    break;

                                // Move below the lowest overlapping float
                                double maxBottom = top;
                                foreach (var floatBox in precedingFloats)
                                {
                                    double fBottom = floatBox.ActualBottom;
                                    if (top < fBottom && top + floatHeight > floatBox.Location.Y)
                                        maxBottom = Math.Max(maxBottom, fBottom);
                                }

                                if (maxBottom <= top) break;
                                top = maxBottom;
                            }
                        }
                        else if (Float == CssConstants.Right)
                        {
                            // Iteratively resolve collisions with all prior floats (CSS1 §5.5.26)
                            for (int iter = 0; iter < 100; iter++)
                            {
                                left = containerRight - Size.Width - ActualMarginRight;

                                // Avoid overlapping with preceding right floats
                                foreach (var floatBox in precedingFloats)
                                {
                                    if (floatBox.Float == CssConstants.Right)
                                    {
                                        double fBottom = floatBox.ActualBottom;
                                        if (top < fBottom && top + floatHeight > floatBox.Location.Y)
                                            left = Math.Min(left, floatBox.Location.X - floatBox.ActualMarginLeft - Size.Width - ActualMarginRight);
                                    }
                                }

                                // Ensure right float doesn't overlap with left floats
                                double leftFloatEdge = containerLeft;
                                foreach (var floatBox in precedingFloats)
                                {
                                    if (floatBox.Float == CssConstants.Left)
                                    {
                                        double fBottom = floatBox.ActualBottom;
                                        if (top < fBottom && top + floatHeight > floatBox.Location.Y)
                                            leftFloatEdge = Math.Max(leftFloatEdge, floatBox.Location.X + floatBox.Size.Width + floatBox.ActualMarginRight);
                                    }
                                }

                                if (left >= leftFloatEdge)
                                    break;

                                // Move below the lowest overlapping float
                                double maxBottom = top;
                                foreach (var floatBox in precedingFloats)
                                {
                                    double fBottom = floatBox.ActualBottom;
                                    if (top < fBottom && top + floatHeight > floatBox.Location.Y)
                                        maxBottom = Math.Max(maxBottom, fBottom);
                                }

                                if (maxBottom <= top) break;
                                top = maxBottom;
                            }
                        }
                    }

                    // CSS2.1 §8.3.1/§9.5.2: Handle clear property.  Clearance
                    // inhibits margin collapsing and pushes the border edge of the
                    // cleared element below the bottom outer edge of the relevant
                    // floats.  Clearance can be negative when the uncollapsed
                    // position is already past the float.
                    if (Clear != CssConstants.None)
                    {
                        double maxFloatBottom = CssBoxHelper.GetMaxFloatBottom(this);
                        if (maxFloatBottom > 0)
                        {
                            double hypotheticalTop = top;

                            // Compute uncollapsed position: margins are NOT
                            // collapsed when clearance is present (§8.3.1).
                            // Use the effective margin for empty collapsible
                            // boxes (§8.3.1 margin-through-collapse).
                            double uncollapsedTop;
                            if (flowPrev != null)
                            {
                                double prevMarginBottom = (flowPrev is CssBox fpb)
                                    ? CssBoxHelper.GetEffectiveMarginBottom(fpb)
                                    : flowPrev.ActualMarginBottom;
                                uncollapsedTop = flowPrevBottom
                                    + prevMarginBottom
                                    + ActualMarginTop;
                            }
                            else if (ParentBox != null)
                            {
                                uncollapsedTop = ParentBox.ClientTop + ActualMarginTop;
                            }
                            else
                            {
                                uncollapsedTop = hypotheticalTop;
                            }

                            // CSS2.2 §9.5.2: Only introduce clearance when the
                            // hypothetical position (where the top border edge
                            // would be if 'clear' were 'none') is NOT past the
                            // relevant floats.  When the margin alone already
                            // places the element past the float, no clearance is
                            // needed and margin collapsing is preserved.
                            if (hypotheticalTop < maxFloatBottom)
                            {
                                // clearance = max(amount to clear float, amount to
                                // reach hypothetical position).  This can be negative.
                                double clearance = Math.Max(
                                    maxFloatBottom - uncollapsedTop,
                                    hypotheticalTop - uncollapsedTop);

                                top = uncollapsedTop + clearance;
                            }
                        }
                    }

                    // CSS2.1 §9.5: The border box of an element in normal
                    // flow that establishes a new BFC must not overlap the
                    // margin box of any floats in the same BFC.  Shift the
                    // block right past left floats and narrow it to avoid
                    // right floats.  If it cannot fit beside the floats,
                    // clear below them.
                    if (Float == CssConstants.None
                        && Position != CssConstants.Absolute && Position != CssConstants.Fixed)
                    {
                        bool isBfcRoot = Display == CssConstants.InlineBlock
                            || Display == CssConstants.TableCell
                            || Display is "flex" or "inline-flex" or "grid" or "inline-grid"
                            || (Overflow != null && Overflow != CssConstants.Visible)
                            || (AlignContent != null && AlignContent != "normal");

                        if (isBfcRoot)
                        {
                            var precedingFloats = CssBoxHelper.CollectPrecedingFloatsInBfc(this);
                            if (precedingFloats.Count > 0)
                            {
                                double containerLeft = ContainingBlock.Location.X + ContainingBlock.ActualPaddingLeft + ContainingBlock.ActualBorderLeftWidth;
                                double containerRight = ContainingBlock.ClientLeft + ContainingBlock.AvailableWidth;
                                double boxHeight = Math.Max(Size.Height, GetEmHeight());

                                // Try to fit beside floats; if not possible, clear
                                // below them.  100 iterations is a safe upper bound
                                // since each iteration advances past at least one
                                // float's bottom edge.
                                for (int bfcIter = 0; bfcIter < 100; bfcIter++)
                                {
                                    double leftEdge = containerLeft + ActualMarginLeft;
                                    double rightEdge = containerRight - ActualMarginRight;

                                    foreach (var fb in precedingFloats)
                                    {
                                        double fbBottom = fb.ActualBottom + fb.ActualMarginBottom;
                                        if (top < fbBottom && top + boxHeight > fb.Location.Y - fb.ActualMarginTop)
                                        {
                                            if (fb.Float == CssConstants.Left)
                                                leftEdge = Math.Max(leftEdge, fb.Location.X + fb.Size.Width + fb.ActualMarginRight + ActualMarginLeft);
                                            else if (fb.Float == CssConstants.Right)
                                                rightEdge = Math.Min(rightEdge, fb.Location.X - fb.ActualMarginLeft - ActualMarginRight);
                                        }
                                    }

                                    double availableWidth = rightEdge - leftEdge;
                                    if (availableWidth >= Size.Width || availableWidth >= 0)
                                    {
                                        left = leftEdge;
                                        if (availableWidth < Size.Width && (Width == CssConstants.Auto || string.IsNullOrEmpty(Width)))
                                            Size = new SizeF((float)availableWidth, Size.Height);
                                        break;
                                    }

                                    // Cannot fit beside floats — clear below them.
                                    double maxFb = top;
                                    foreach (var fb in precedingFloats)
                                    {
                                        double fbBottom = fb.ActualBottom + fb.ActualMarginBottom;
                                        if (top < fbBottom && top + boxHeight > fb.Location.Y - fb.ActualMarginTop)
                                            maxFb = Math.Max(maxFb, fbBottom);
                                    }
                                    if (maxFb <= top) break;
                                    top = maxFb;
                                }
                            }
                        }
                    }

                    Location = new PointF((float)left, (float)top);
                    ActualBottom = top;
                    AbsposLocationFinalized = false;

                    // CSS2.1 §10.3.7 / §10.6.4: For absolutely positioned
                    // elements with explicit 'top'/'left', override the static
                    // position with the CSS-specified offset from the containing
                    // block's padding edge.
                    if (Position == CssConstants.Absolute)
                    {
                        var cb = FindPositionedContainingBlock();
                        GetAbsoluteContainingBlockPaddingBox(cb, out double cbPadLeft, out double cbPadTop, out double cbPadWidth, out double cbPadHeight);

                        float newX = Location.X, newY = Location.Y;

                        if (Left != null && Left != CssConstants.Auto)
                        {
                            double cssLeft = CssValueParser.ParseLength(Left, cbPadWidth, GetEmHeight());
                            newX = (float)(cbPadLeft + cssLeft + ActualMarginLeft);
                        }
                        else if (Right != null && Right != CssConstants.Auto)
                        {
                            // CSS2.1 §10.3.7: When left is auto and right is
                            // specified, position from the right padding edge.
                            double cssRight = CssValueParser.ParseLength(Right, cbPadWidth, GetEmHeight());
                            newX = (float)(cbPadLeft + cbPadWidth - cssRight - ActualMarginRight - Size.Width);
                        }

                        if (Top != null && Top != CssConstants.Auto)
                        {
                            double cssTop = CssValueParser.ParseLength(Top, cbPadHeight, GetEmHeight());
                            newY = (float)(cbPadTop + cssTop + ActualMarginTop);
                        }
                        else if (Bottom != null && Bottom != CssConstants.Auto)
                        {
                            // CSS2.1 §10.6.4: When top is auto and bottom is
                            // specified, position from the bottom padding edge.
                            double cssBottom = CssValueParser.ParseLength(Bottom, cbPadHeight, GetEmHeight());
                            double boxHeight = ActualBottom - Location.Y;
                            // boxHeight may be zero when the box position was
                            // just initialised and children have not yet been
                            // laid out.  Fall back to Size.Height which reflects
                            // any explicit CSS height already applied.
                            if (boxHeight <= 0) boxHeight = Size.Height;
                            newY = (float)(cbPadTop + cbPadHeight - cssBottom - ActualMarginBottom - boxHeight);
                        }

                        Location = new PointF(newX, newY);
                        ActualBottom = newY;
                        // Location now holds the final left/top offset; the content
                        // flow below starts here, so AdjustAbsolutePosition must not
                        // add the offset again (WPT css-anchor-position anchor-scroll).
                        if (Left != null && Left != CssConstants.Auto
                            || Top != null && Top != CssConstants.Auto)
                            AbsposLocationFinalized = true;
                    }

                    // CSS2.1 §10.6.4 / §9.6.1: For fixed-position elements,
                    // the containing block is the viewport.  When top/left/
                    // bottom/right are explicitly set, use those offsets from
                    // the viewport edge.  When they are auto, the static
                    // position (computed above) is kept.
                    if (Position == CssConstants.Fixed && LayoutEnvironment != null)
                    {
                        bool hasLeft = Left != null && Left != CssConstants.Auto;
                        bool hasRight = Right != null && Right != CssConstants.Auto;
                        bool hasTop = Top != null && Top != CssConstants.Auto;
                        bool hasBottom = Bottom != null && Bottom != CssConstants.Auto;

                        if (hasLeft || hasRight || hasTop || hasBottom)
                        {
                            var vpSize = LayoutEnvironment.ViewportSize;
                            float newX = Location.X, newY = Location.Y;

                            if (hasLeft)
                            {
                                double cssLeft = CssValueParser.ParseLength(Left, vpSize.Width, GetEmHeight());
                                newX = (float)(cssLeft + ActualMarginLeft);
                            }
                            else if (hasRight)
                            {
                                double cssRight = CssValueParser.ParseLength(Right, vpSize.Width, GetEmHeight());
                                newX = (float)(vpSize.Width - cssRight - ActualMarginRight - Size.Width);
                            }

                            if (hasTop)
                            {
                                double cssTop = CssValueParser.ParseLength(Top, vpSize.Height, GetEmHeight());
                                newY = (float)(cssTop + ActualMarginTop);
                            }
                            else if (hasBottom)
                            {
                                double cssBottom = CssValueParser.ParseLength(Bottom, vpSize.Height, GetEmHeight());
                                double boxHeight = ActualBottom - Location.Y;
                                if (boxHeight <= 0) boxHeight = Size.Height;
                                newY = (float)(vpSize.Height - cssBottom - ActualMarginBottom - boxHeight);
                            }

                            Location = new PointF(newX, newY);
                            ActualBottom = newY;
                            if (hasLeft || hasTop)
                                AbsposLocationFinalized = true;
                        }
                        // When all offsets are auto, keep the static position
                        // (Location is already set from normal-flow
                        // calculation above).
                    }
                }
            }

            // CSS2.1 §10.5: Pre-resolve percentage heights so that children
            // can use ContainingBlock.Size.Height for their own percentage
            // height resolution.  This must run AFTER position assignment
            // (which resets Size.Height to 0 via ActualBottom = top) but
            // BEFORE child layout so descendants see the correct height.
            if (Height != CssConstants.Auto && !string.IsNullOrEmpty(Height)
                && Height.Contains('%') && !HeightPercentageResolvesToAuto())
            {
                double cbHeight = PercentageHeightContainingBlockHeight();
                double preHeight = ResolveSpecifiedHeightToBorderBox(
                    CssValueParser.ParseLength(Height, cbHeight, GetEmHeight()));
                Size = new SizeF(Size.Width, (float)preHeight);
            }
            // CSS Sizing 4 §4: likewise pre-resolve an aspect-ratio block size from
            // the used width so a percentage-height child (e.g. a filling
            // background element) can resolve against the container's definite
            // aspect-ratio height. The final ActualBottom is re-established after
            // child layout below; this only makes the height visible to
            // descendants beforehand, mirroring the §10.5 pre-resolution above.
            else if ((Height == CssConstants.Auto || string.IsNullOrEmpty(Height))
                && Display == CssConstants.Block
                && Float == CssConstants.None
                && Position != CssConstants.Absolute && Position != CssConstants.Fixed
                && !IsImage
                && TryResolveAspectRatioBlockHeight(out double aspectRatioPreHeight))
            {
                Size = new SizeF(Size.Width, (float)aspectRatioPreHeight);
            }

            //If we're talking about a table here..
            if (Display == CssConstants.Table || Display == CssConstants.InlineTable)
            {
                CssLayoutEngineTable.PerformLayout(g, this, BaseUrl);
            }
            else
            {
                // CSS Flexbox §8.2/§8.4: Map flex alignment properties to
                // CSS2.1 text-align so that the inline formatting context
                // fallback (FlowInlineBlock) produces visually aligned items.
                // This only applies when the author has not set text-align
                // explicitly (i.e. it still has the default 'left' value).
                if (Display is "flex" or "inline-flex" or "grid" or "inline-grid")
                {
                    if (JustifyContent is "center" &&
                        TextAlign is CssConstants.Left or "start" or "")
                    {
                        TextAlign = CssConstants.Center;
                    }
                    else if (JustifyContent is "flex-end" or "end" &&
                        TextAlign is CssConstants.Left or "start" or "")
                    {
                        TextAlign = CssConstants.Right;
                    }
                }

                if (IsRowFlexContainer())
                {
                    PerformFlexRowLayout(g);
                }
                //If there's just inline boxes, create LineBoxes
                else if (LayoutBoxUtils.ContainsInlinesOnly(this))
                {
                    ActualBottom = Location.Y;
                    CssLayoutEngine.CreateLineBoxes(g, this); //This will automatically set the bottom of this block

                    // CSS2.1 §9.5: Floated children were skipped by
                    // CreateLineBoxes (they are out-of-flow).  Lay them out
                    // now so they are positioned and painted.
                    foreach (var childBox in Boxes)
                    {
                        if (childBox.Float != CssConstants.None)
                        {
                            childBox.PerformLayout(g);

                            // CSS2.1 §13.3.1: When page-break-inside:avoid is
                            // set on a float's containing block, move the float
                            // to the next page if it would otherwise cross a
                            // page boundary.
                            if (PageBreakInside == CssConstants.Avoid)
                                childBox.BreakPage();
                        }
                    }

                    // CSS2.1 §10.6.7: Elements that establish a new block
                    // formatting context (BFC) must include descendant floats
                    // in their auto-height calculation.  The inline path above
                    // does not call MarginBottomCollapse(), so BFC elements
                    // with only floated children would otherwise have zero
                    // content height.
                    bool isBfc = Float != CssConstants.None
                        || Display == CssConstants.InlineBlock
                        || Display == CssConstants.TableCell
                        || Display is "flex" or "inline-flex" or "grid" or "inline-grid"
                        || (Overflow != null && Overflow != CssConstants.Visible)
                        || Position == CssConstants.Absolute
                        || Position == CssConstants.Fixed
                        || (AlignContent != null && AlignContent != "normal");
                    if (isBfc)
                    {
                        ActualBottom = MarginBottomCollapse();
                    }

                    // CSS Grid Level 1 §8.5: When all grid items share
                    // the same grid-row and grid-column, reposition them
                    // to the container's content-area origin so they
                    // overlap visually.  (This duplicates the same logic
                    // in the block path below; it is needed here because
                    // ContainsInlinesOnly() forces grid containers into
                    // the inline layout path for shrink-to-fit sizing.)
                    if (Display is "grid" or "inline-grid")
                        ApplyGridLayoutAfterInline();

                    // CSS Box Alignment §6.2: distribute flex/grid items along
                    // the block (cross) axis per align-items / align-self.
                    ApplyFlexGridCrossAxisAlignment();
                    ApplyFlexColumnInlineAxisAlignment();
                }
                else if (Boxes.Count > 0)
                {
                    // CSS Multi-column: Pre-constrain width so children
                    // lay out at column width instead of full container width.
                    float savedWidth = Size.Width;
                    int preColCount = 0;
                    bool hasExplicitColCount = ColumnCount != null && ColumnCount != "auto"
                        && int.TryParse(ColumnCount, out preColCount) && preColCount > 1;
                    bool hasColWidth = ColumnWidth != null && ColumnWidth != "auto"
                        && !string.IsNullOrEmpty(ColumnWidth);

                    bool isMultiColumn = hasExplicitColCount || hasColWidth;
                    if (isMultiColumn && !hasExplicitColCount && hasColWidth)
                    {
                        // Auto column-count from column-width: compute the
                        // number of columns so we can pre-constrain width.
                        double cwVal = CssValueParser.ParseLength(ColumnWidth, Size.Width, GetEmHeight());
                        double gap = ResolveColumnGap();
                        double available = Size.Width - ActualPaddingLeft - ActualPaddingRight
                            - ActualBorderLeftWidth - ActualBorderRightWidth;
                        if (cwVal > 0 && available > 0)
                            preColCount = Math.Max(1, (int)Math.Floor((available + gap) / (cwVal + gap)));
                        isMultiColumn = preColCount > 1;
                    }

                    if (isMultiColumn && preColCount > 1)
                    {
                        double columnGap = ResolveColumnGap();
                        double cw = Size.Width - ActualPaddingLeft - ActualPaddingRight
                            - ActualBorderLeftWidth - ActualBorderRightWidth;
                        double colWidth = (cw - (preColCount - 1) * columnGap) / preColCount;
                        if (colWidth > 0)
                            Size = new SizeF((float)colWidth, Size.Height);
                    }

                    foreach (var childBox in Boxes)
                    {
                        childBox.PerformLayout(g);

                        // CSS2.1 §13.3.1: When page-break-inside:avoid is
                        // set, move floated children to the next page if they
                        // would cross a page boundary.
                        if (childBox.Float != CssConstants.None
                            && PageBreakInside == CssConstants.Avoid)
                            childBox.BreakPage();
                    }

                    // Restore original width after children are laid out.
                    if (isMultiColumn)
                        Size = new SizeF(savedWidth, Size.Height);

                    ActualRight = CalculateActualRight();
                    ActualBottom = MarginBottomCollapse();

                    if (Display is "grid" or "inline-grid")
                        ApplyGridLayoutAfterInline();
                }
            }
        }
        else
        {
            var prevSibling = LayoutBoxUtils.GetPreviousSibling(this);
            if (prevSibling != null)
            {
                if (Location == PointF.Empty)
                    Location = prevSibling.Location;

                ActualBottom = prevSibling.ActualBottom;
            }
        }

        // CSS Multi-column Layout §3: When column-count > 1 or column-width
        // is specified, redistribute in-flow children into multiple columns.
        // This is a post-layout transformation that moves children
        // horizontally and vertically to simulate multi-column flow.
        {
            int colCount = 0;
            bool hasExplicitCount = ColumnCount != null && ColumnCount != "auto"
                && int.TryParse(ColumnCount, out colCount) && colCount > 1;
            bool hasColumnWidth = ColumnWidth != null && ColumnWidth != "auto"
                && !string.IsNullOrEmpty(ColumnWidth);

            if (!hasExplicitCount && hasColumnWidth)
            {
                // Auto column-count from column-width: CSS Multi-column §3.4
                double cw = CssValueParser.ParseLength(ColumnWidth, Size.Width, GetEmHeight());
                double gap = GetEmHeight();
                double available = Size.Width - ActualPaddingLeft - ActualPaddingRight
                    - ActualBorderLeftWidth - ActualBorderRightWidth;
                if (cw > 0 && available > 0)
                    colCount = Math.Max(1, (int)Math.Floor((available + gap) / (cw + gap)));
            }

            if (colCount > 1 && Boxes.Count > 0)
            {
                ApplyMultiColumnLayout(colCount);
            }
        }

        // CSS content-box model: 'height' specifies the content height only;
        // padding and border are additive (CSS2.1 §10.6.3). An intrinsic-sizing
        // height keyword (min-/max-/fit-content) is not a length — the content
        // height already in ActualBottom is its used value, so leave it be.
        if (Height != CssConstants.Auto && !string.IsNullOrEmpty(Height)
            && !IsIntrinsicSizingHeightKeyword(Height))
        {
            // CSS2.1 §10.5: If height is a percentage and the containing
            // block's height is not explicitly specified (auto), the
            // percentage resolves to auto and this constraint is skipped.
            if (!HeightPercentageResolvesToAuto())
            {
                // CSS2.1 §10.5: Percentage heights resolve against the
                // containing block's height, not the element's own size.
                // ActualHeight uses Size.Height (the element's own height
                // from child layout), which is wrong for percentage values.
                // Resolve against the containing block's height instead.
                double contentHeight;
                if (Height.Contains('%'))
                {
                    double cbHeight = PercentageHeightContainingBlockHeight();
                    contentHeight = CssValueParser.ParseLength(Height, cbHeight, GetEmHeight());
                }
                else
                {
                    contentHeight = string.Equals(Height, "inherit", StringComparison.OrdinalIgnoreCase) && GetParent() != null
                        ? GetParent().ActualHeight
                        : ActualHeight;
                }

                double borderBoxHeight = ResolveSpecifiedHeightToBorderBox(contentHeight);

                // CSS2.1 §10.6.3: An explicit height sets the content box
                // height.  Content that exceeds this height overflows
                // (visible by default) but does not affect sibling
                // positioning.  Use direct assignment so that explicit
                // height (e.g. height:0) can override the height computed
                // by CreateLineBoxes (e.g. from line-height).
                ActualBottom = Location.Y + borderBoxHeight;
            }
        }
        else if ((Position == CssConstants.Absolute || Position == CssConstants.Fixed)
            && Top != null && Top != CssConstants.Auto
            && Bottom != null && Bottom != CssConstants.Auto
            && (Height == CssConstants.Auto || string.IsNullOrEmpty(Height)))
        {
            // CSS2.1 §10.6.4: For absolutely positioned, non-replaced
            // elements when height is auto and both top and bottom are
            // specified, compute height from the constraint equation:
            // top + margin-top + height + margin-bottom + bottom = CB height
            double cbHeight;
            if (Position == CssConstants.Fixed && LayoutEnvironment != null)
                cbHeight = LayoutEnvironment.ViewportSize.Height;
            else
            {
                var cb = FindPositionedContainingBlock();
                GetAbsoluteContainingBlockPaddingBox(cb, out _, out _, out _, out cbHeight);
            }
            double cssTop = CssValueParser.ParseLength(Top, cbHeight, GetEmHeight());
            double cssBottom = CssValueParser.ParseLength(Bottom, cbHeight, GetEmHeight());
            double resolvedHeight = cbHeight - cssTop - cssBottom - ActualMarginTop - ActualMarginBottom
                - ActualPaddingTop - ActualPaddingBottom - ActualBorderTopWidth - ActualBorderBottomWidth;
            if (resolvedHeight < 0) resolvedHeight = 0;
            double borderBoxH = resolvedHeight + ActualPaddingTop + ActualPaddingBottom + ActualBorderTopWidth + ActualBorderBottomWidth;
            ActualBottom = Location.Y + borderBoxH;
        }

        // CSS Sizing 4 §4: a box with a preferred aspect-ratio and an auto block
        // (height) axis derives its used height from its used inline (width) size.
        // Runs after the explicit-height paths above (so an author height still
        // wins) and before the §10.7 min-/max-height clamp below (so e.g. a
        // min-height floors the transferred square). Scoped to in-flow block-level
        // boxes, whose used width is already resolved and does not itself depend on
        // the aspect ratio; replaced elements keep their intrinsic-ratio sizing.
        if ((Height == CssConstants.Auto || string.IsNullOrEmpty(Height))
            && Display == CssConstants.Block
            && Float == CssConstants.None
            && Position != CssConstants.Absolute && Position != CssConstants.Fixed
            && !IsImage
            && TryResolveAspectRatioBlockHeight(out double aspectRatioBorderBoxHeight))
        {
            ActualBottom = Location.Y + aspectRatioBorderBoxHeight;
        }

        // CSS2.1 §10.7: Apply min-height / max-height constraints.
        // When min-height > max-height, min-height wins.
        {
            double contentHeight = ActualBottom - Location.Y - ActualPaddingTop - ActualPaddingBottom - ActualBorderTopWidth - ActualBorderBottomWidth;
            bool constrained = false;

            // CSS2.1 §9.6.1: For fixed-position elements, percentage
            // heights resolve against the viewport, not the parent.
            double cbHeight = (Position == CssConstants.Fixed && LayoutEnvironment != null)
                ? LayoutEnvironment.ViewportSize.Height
                : ContainingBlock.Size.Height;

            if (MaxHeight != "none" && !string.IsNullOrEmpty(MaxHeight))
            {
                // CSS2.1 §10.7: If the containing block's height is not
                // specified explicitly and this element is not absolutely
                // positioned, a percentage max-height is treated as 'none'.
                // Exception: the initial containing block always has a
                // definite height (the viewport), per §10.5.
                bool maxIsPercentageAuto = MaxHeight.Contains('%')
                    && Position != CssConstants.Absolute && Position != CssConstants.Fixed
                    && ContainingBlock?.ParentBox != null
                    && (ContainingBlock.Height == CssConstants.Auto || string.IsNullOrEmpty(ContainingBlock.Height));

                if (!maxIsPercentageAuto)
                {
                    double maxH = CssValueParser.ParseLength(MaxHeight, cbHeight, GetEmHeight());
                    if (contentHeight > maxH)
                    {
                        contentHeight = maxH;
                        constrained = true;
                    }
                }
            }

            if (MinHeight != "0" && !string.IsNullOrEmpty(MinHeight))
            {
                // CSS2.1 §10.7: If the containing block's height is not
                // specified explicitly and this element is not absolutely
                // positioned, a percentage min-height is treated as '0'.
                // Exception: the initial containing block always has a
                // definite height (the viewport), per §10.5.
                bool minIsPercentageAuto = MinHeight.Contains('%')
                    && Position != CssConstants.Absolute && Position != CssConstants.Fixed
                    && ContainingBlock?.ParentBox != null
                    && (ContainingBlock.Height == CssConstants.Auto || string.IsNullOrEmpty(ContainingBlock.Height));

                if (!minIsPercentageAuto)
                {
                    double minH = CssValueParser.ParseLength(MinHeight, cbHeight, GetEmHeight());
                    if (contentHeight < minH)
                    {
                        contentHeight = minH;
                        constrained = true;
                    }
                }
            }

            if (constrained)
            {
                ActualBottom = Location.Y + ResolveSpecifiedHeightToBorderBox(contentHeight);
            }
        }

        // Floats with an explicit CSS height establish a new BFC.
        // Their ActualBottom should reflect the stated height, not
        // content overflow from child floats (CSS2.1 §10.6.1).
        // CSS2.1 §10.5: Percentage heights resolve to auto when
        // the containing block's height is not explicitly specified.
        if (Float != CssConstants.None && Height != CssConstants.Auto && !string.IsNullOrEmpty(Height)
            && !IsIntrinsicSizingHeightKeyword(Height))
        {
            if (!HeightPercentageResolvesToAuto())
            {
                // For percentage heights, resolve against the containing
                // block's height directly.  ActualHeight resolves against
                // Size.Height which may have been cached before the
                // percentage height pre-resolution step set the correct
                // Size.Height (CSS2.1 §10.5).
                double contentHeight;
                if (Height.Contains('%'))
                {
                    double cbHeight = PercentageHeightContainingBlockHeight();
                    contentHeight = CssValueParser.ParseLength(Height, cbHeight, GetEmHeight());
                }
                else
                {
                    contentHeight = ActualHeight;
                }
                // CSS2.1 §10.7: min-height/max-height also constrain a float's
                // explicit height. This override runs after the §10.7 clamp above,
                // so without re-clamping here a float with height:100; min-height:200
                // kept 100 (e.g. a float:left grid whose auto-fill row count already
                // grew to min-height — WPT css-grid grid-auto-repeat-min-size-001).
                // height and min/max-height share the box-sizing frame, so clamp the
                // specified value; ResolveSpecifiedHeightToBorderBox normalizes it.
                contentHeight = ClampSpecifiedHeightToMinMax(contentHeight);
                double borderBoxHeight = ResolveSpecifiedHeightToBorderBox(contentHeight);
                ActualBottom = Location.Y + borderBoxHeight;
            }
        }

        if (Position == CssConstants.Absolute)
        {
            bool hasLeft = Left != null && Left != CssConstants.Auto;
            bool hasRight = Right != null && Right != CssConstants.Auto;
            bool hasTop = Top != null && Top != CssConstants.Auto;
            bool hasBottom = Bottom != null && Bottom != CssConstants.Auto;

            if ((!hasLeft && hasRight) || (!hasTop && hasBottom))
            {
                var cb = FindPositionedContainingBlock();
                GetAbsoluteContainingBlockPaddingBox(cb, out double cbPadLeft, out double cbPadTop, out double cbPadWidth, out double cbPadHeight);

                float newX = Location.X;
                float newY = Location.Y;

                if (!hasLeft && hasRight)
                {
                    double boxWidth = ActualRight - Location.X;
                    if (boxWidth <= 0)
                        boxWidth = Size.Width;

                    double cssRight = CssValueParser.ParseLength(Right, cbPadWidth, GetEmHeight());
                    newX = (float)(cbPadLeft + cbPadWidth - cssRight - ActualMarginRight - boxWidth);
                }

                if (!hasTop && hasBottom)
                {
                    double boxHeight = ActualBottom - Location.Y;
                    if (boxHeight <= 0)
                        boxHeight = Size.Height;

                    double cssBottom = CssValueParser.ParseLength(Bottom, cbPadHeight, GetEmHeight());
                    newY = (float)(cbPadTop + cbPadHeight - cssBottom - ActualMarginBottom - boxHeight);
                }

                float deltaX = newX - Location.X;
                float deltaY = newY - Location.Y;
                if (deltaX != 0)
                    OffsetLeft(deltaX);
                if (deltaY != 0)
                {
                    // OffsetTop already shifts Location.Y, and ActualBottom is a
                    // derived value (ActualBottom => Location.Y + Size.Height), so the
                    // box's bottom edge follows the move automatically.  A further
                    // "ActualBottom += deltaY" would double-apply the shift — its
                    // setter writes Size.Height = ActualBottom - Location.Y, growing
                    // (or, as here for a bottom-anchored full-height abspos box,
                    // collapsing) the height by deltaY.  Mirror the horizontal branch
                    // above, which offsets without touching ActualRight.
                    OffsetTop(deltaY);
                }
            }

            // CSS Box Alignment Level 3 §6.1: Post-layout self-alignment for
            // absolutely positioned elements.  After children are laid out,
            // shrink the box to fit-content size and align within the IMCB.
            // This must run after child layout so content dimensions are known.
            string jsPost = JustifySelf?.Trim().ToLowerInvariant() ?? "auto";
            bool jsPostNonDefault = jsPost != "auto" && jsPost != "normal" && jsPost != "stretch";
            string asPost = AlignSelf?.Trim().ToLowerInvariant() ?? "auto";
            bool asPostNonDefault = asPost != "auto" && asPost != "normal" && asPost != "stretch";

            if (jsPostNonDefault || asPostNonDefault)
            {
                var cb = FindPositionedContainingBlock();
                GetAbsoluteContainingBlockPaddingBox(cb, out double cbPadLeft, out double cbPadTop, out double cbPadWidth, out double cbPadHeight);

                bool hasL = Left != null && Left != CssConstants.Auto;
                bool hasR = Right != null && Right != CssConstants.Auto;
                bool hasT = Top != null && Top != CssConstants.Auto;
                bool hasB = Bottom != null && Bottom != CssConstants.Auto;

                // CSS Writing Modes Level 4: the containing block's writing mode
                // determines which physical axis corresponds to justify-self (inline)
                // and align-self (block).
                bool cbVertical = cb.WritingMode == "vertical-rl" || cb.WritingMode == "vertical-lr";

                float newX = Location.X, newY = Location.Y;

                // When align-self resolves the block axis to a non-stretch value,
                // the box uses its content (shrink-to-fit) block size rather than
                // the stretched inset size; record the resolved border-box height
                // so the apply step can shrink it (mirrors how the inline branch
                // sets Size.Width).  Null = leave the block size untouched.
                double? alignBlockBorderBoxHeight = null;

                // justify-self controls the inline axis:
                //   horizontal-tb → horizontal (L/R insets)
                //   vertical-rl/lr → vertical (T/B insets)
                if (jsPostNonDefault)
                {
                    if (!cbVertical && hasL && hasR)
                    {
                        double cssLeft = CssValueParser.ParseLength(Left, cbPadWidth, GetEmHeight());
                        double cssRight = CssValueParser.ParseLength(Right, cbPadWidth, GetEmHeight());
                        double imcbLeft = cbPadLeft + cssLeft;
                        double imcbWidth = cbPadWidth - cssLeft - cssRight;

                        double boxWidth = GetShrinkToFitWidth();
                        Size = new SizeF((float)boxWidth, Size.Height);

                        // For a box the vertical-flow rotation will transpose, the
                        // alignment runs on the CB's inline (horizontal) axis but
                        // the item's PHYSICAL width is its logical HEIGHT (the
                        // rotation swaps them). Align with the physical extent so
                        // an overflowing vrl item (laid out with a small logical
                        // width) is centered/clamped by its true width.
                        double alignWidth = WillBeVerticalTransposed()
                            ? GetShrinkToFitHeight() : boxWidth;

                        // Inline-axis start edge follows the CB's direction (start/end);
                        // self-start/self-end follow the ITEM's start in this horizontal
                        // axis — its inline axis when horizontal-tb (right under rtl), or
                        // its block axis when vertical (vertical-rl starts on the right).
                        bool startIsLow = cb.Direction != "rtl";
                        bool itemStartIsHigh = WritingMode switch
                        {
                            "vertical-rl" => true,
                            "vertical-lr" => false,
                            _ => Direction == "rtl",
                        };
                        double dx = ResolveAbsposSelfAlignment(
                            jsPost, imcbLeft, imcbWidth, cbPadLeft, cbPadWidth,
                            alignWidth, isRtl: !startIsLow, startIsLow,
                            selfStartIsHigh: itemStartIsHigh);
                        newX = (float)(imcbLeft + dx + ActualMarginLeft);
                    }
                    else if (cbVertical && hasT && hasB)
                    {
                        double cssTop = CssValueParser.ParseLength(Top, cbPadHeight, GetEmHeight());
                        double cssBottom = CssValueParser.ParseLength(Bottom, cbPadHeight, GetEmHeight());
                        double imcbTop = cbPadTop + cssTop;
                        double imcbHeight = cbPadHeight - cssTop - cssBottom;

                        double boxHeight = GetShrinkToFitHeight();
                        // Non-stretch justify-self on the vertical inline axis →
                        // the box uses its content (shrink-to-fit) height, not the
                        // top-to-bottom inset-stretched height. Record it so the
                        // shared apply step restores the height after the offset
                        // (mirrors the width un-stretch in the !cbVertical inline
                        // branch and the height un-stretch in the align-self
                        // block-axis branch). Without this the box stays stretched
                        // to the IMCB height and renders as a tall bar.
                        alignBlockBorderBoxHeight = boxHeight;

                        // Inline axis is vertical here; its start runs top→bottom
                        // unless the CB's direction is rtl. start/end follow the CB's
                        // inline direction (so the flip is !startIsLow, mirroring the
                        // align-self block-axis branch); self-start/self-end follow the
                        // ITEM's inline direction — for a vertical-wm item the vertical
                        // axis is its inline axis (start at the bottom under rtl), while
                        // for a horizontal-tb item it is the block axis (start at top).
                        bool startIsLow = cb.Direction != "rtl";
                        bool itemStartIsHigh =
                            (WritingMode == "vertical-lr" || WritingMode == "vertical-rl")
                            && Direction == "rtl";
                        double dy = ResolveAbsposSelfAlignment(
                            jsPost, imcbTop, imcbHeight, cbPadTop, cbPadHeight,
                            boxHeight, isRtl: !startIsLow, startIsLow,
                            selfStartIsHigh: itemStartIsHigh);
                        newY = (float)(imcbTop + dy + ActualMarginTop);
                    }
                    else if (!cbVertical && !hasL && !hasR && ParentBox != null)
                    {
                        // Inline insets are auto → the box is at its static
                        // position; justify-self aligns it within the
                        // static-position rectangle, whose inline extent is the
                        // in-flow parent's content box (CSS Position 3
                        // §abspos-alignment). The box keeps its own inline size.
                        double rectStart = ParentBox.ClientLeft;
                        double rectWidth = ParentBox.ClientRight - ParentBox.ClientLeft;
                        double marginBoxWidth = Size.Width + ActualMarginLeft + ActualMarginRight;
                        bool isRtl = Direction == "rtl";
                        bool startIsLow = cb.Direction != "rtl";
                        double dx = ResolveAbsposSelfAlignment(
                            "unsafe " + StripSafeUnsafe(jsPost),
                            rectStart, rectWidth, rectStart, rectWidth,
                            marginBoxWidth, isRtl, startIsLow);
                        newX = (float)(rectStart + dx + ActualMarginLeft);
                    }
                }
                else if (!cbVertical && !hasL && !hasR && ParentBox != null
                         && cb.Direction == "rtl")
                {
                    // justify-self:auto (default) + auto inline insets → the box
                    // rests at its static position: the inline-START edge of the
                    // static-position rectangle (the in-flow parent's content
                    // box). That start edge follows the containing block's
                    // direction — for ltr it is the left edge (already set by
                    // base layout), for rtl it is the RIGHT edge. Without this,
                    // abspos items in rtl containers render flush-left, shifted
                    // left by the free inline width
                    // (WPT css-align/abspos/*-rtl-*, issue #1131).
                    double rectStart = ParentBox.ClientLeft;
                    double rectWidth = ParentBox.ClientRight - ParentBox.ClientLeft;
                    // Use the physical width for a box the rotation will transpose
                    // (its physical width is the logical height).
                    double boxW = WillBeVerticalTransposed() ? GetShrinkToFitHeight() : Size.Width;
                    double marginBoxWidth = boxW + ActualMarginLeft + ActualMarginRight;
                    double dx = ResolveAbsposSelfAlignment(
                        "unsafe start", rectStart, rectWidth, rectStart, rectWidth,
                        marginBoxWidth, isRtl: true, startIsLow: false);
                    newX = (float)(rectStart + dx + ActualMarginLeft);
                }
                else if (cbVertical && !hasT && !hasB
                         && cb.Direction == "rtl")
                {
                    // vertical-rl/lr container: the inline axis is VERTICAL.
                    // justify-self:auto + auto block insets (top/bottom) → the box
                    // rests at its static position: the inline-START edge of the
                    // static-position rectangle. That start follows the inline
                    // direction — for ltr the top (Broiler's default), for rtl the
                    // inline axis is reversed so the start is the BOTTOM. Use the
                    // CB padding box (cbPadTop/cbPadHeight) for the vertical
                    // extent: block-axis sizes resolve bottom-up, so ParentBox's
                    // ActualBottom is not final here, whereas cbPad* carries the
                    // definite-height patch. Without this, abspos items in
                    // vertical-rl+rtl containers render flush-top
                    // (WPT css-align/abspos/*-vrl-rtl-*, issue #1131).
                    double marginBoxHeight = Size.Height + ActualMarginTop + ActualMarginBottom;
                    double dy = ResolveAbsposSelfAlignment(
                        "unsafe start", cbPadTop, cbPadHeight, cbPadTop, cbPadHeight,
                        marginBoxHeight, isRtl: true, startIsLow: false);
                    newY = (float)(cbPadTop + dy + ActualMarginTop);
                    // Preserve the box's own (shrink-to-fit) block size: the apply
                    // step shifts ActualBottom by the same delta as Location, so
                    // record the height to restore it (mirrors the align-self
                    // block-axis un-stretch).
                    alignBlockBorderBoxHeight = Size.Height;
                }

                // align-self controls the block axis:
                //   horizontal-tb → vertical (T/B insets)
                //   vertical-rl/lr → horizontal (L/R insets)
                if (asPostNonDefault)
                {
                    if (!cbVertical && hasT && hasB)
                    {
                        double cssTop = CssValueParser.ParseLength(Top, cbPadHeight, GetEmHeight());
                        double cssBottom = CssValueParser.ParseLength(Bottom, cbPadHeight, GetEmHeight());
                        double imcbTop = cbPadTop + cssTop;
                        double imcbHeight = cbPadHeight - cssTop - cssBottom;

                        double boxHeight = GetShrinkToFitHeight();
                        // Non-stretch align-self → the box is its content height,
                        // not the stretched top-to-bottom inset height.
                        alignBlockBorderBoxHeight = boxHeight;

                        // For a box the vertical-flow rotation will transpose, the
                        // alignment runs on the CB's block (vertical) axis but the
                        // item's PHYSICAL height is its logical WIDTH (the rotation
                        // swaps them); align with the physical extent.
                        double alignHeight = WillBeVerticalTransposed()
                            ? GetShrinkToFitWidth() : boxHeight;

                        // Block-axis start is the top edge for horizontal-tb. self-start/
                        // self-end use the ITEM's start in this vertical axis: its block
                        // axis when horizontal-tb (top), or its inline axis when vertical
                        // (bottom under direction:rtl).
                        bool itemStartIsHigh =
                            (WritingMode == "vertical-lr" || WritingMode == "vertical-rl")
                            && Direction == "rtl";
                        double dy = ResolveAbsposSelfAlignment(
                            asPost, imcbTop, imcbHeight, cbPadTop, cbPadHeight,
                            alignHeight, isRtl: false, startIsLow: true,
                            selfStartIsHigh: itemStartIsHigh);
                        newY = (float)(imcbTop + dy + ActualMarginTop);
                    }
                    else if (cbVertical && hasL && hasR)
                    {
                        double cssLeft = CssValueParser.ParseLength(Left, cbPadWidth, GetEmHeight());
                        double cssRight = CssValueParser.ParseLength(Right, cbPadWidth, GetEmHeight());
                        double imcbLeft = cbPadLeft + cssLeft;
                        double imcbWidth = cbPadWidth - cssLeft - cssRight;

                        double boxWidth = GetShrinkToFitWidth();
                        Size = new SizeF((float)boxWidth, Size.Height);

                        // Block-axis start runs L→R for vertical-lr, R→L for
                        // vertical-rl (so the low/left edge is the start only for lr).
                        // align-self acts on the containing block's BLOCK axis, whose
                        // flow is fixed by writing-mode; `direction` (rtl/ltr) is an
                        // inline-axis property and must NOT flip block-axis start/end
                        // (WPT css-align/abspos/align-self-{vlr,vrl}-*). So the start↔end
                        // flip is driven purely by the writing mode: start sits on the
                        // high edge exactly when the block start is not the low edge.
                        bool startIsLow = cb.WritingMode == "vertical-lr";

                        // self-start/self-end use the ITEM's start edge in this
                        // (horizontal) alignment axis: for a vertical-wm item the
                        // horizontal axis is its block axis (vertical-rl starts on the
                        // right/high edge, vertical-lr on the left/low edge); for a
                        // horizontal-tb item it is the inline axis, whose start is the
                        // right/high edge under direction:rtl.
                        bool itemStartIsHigh = WritingMode switch
                        {
                            "vertical-rl" => true,
                            "vertical-lr" => false,
                            _ => Direction == "rtl",
                        };
                        double dx = ResolveAbsposSelfAlignment(
                            asPost, imcbLeft, imcbWidth, cbPadLeft, cbPadWidth,
                            boxWidth, isRtl: !startIsLow, startIsLow,
                            selfStartIsHigh: itemStartIsHigh);
                        newX = (float)(imcbLeft + dx + ActualMarginLeft);
                    }
                    else if (!cbVertical && !hasT && !hasB)
                    {
                        // Block insets are auto → the box is at its static
                        // position; align-self aligns it within the
                        // static-position rectangle, which has ZERO block size
                        // at the static position (free space = −margin-box
                        // height), so start keeps the box put while center/end
                        // pull it up by half / all of its height (CSS Position 3
                        // §abspos-alignment). The box keeps its own block size:
                        // record it so the shared apply step's ActualBottom
                        // bookkeeping restores the height after the offset
                        // (otherwise moving up shrinks the box by the delta).
                        alignBlockBorderBoxHeight = Size.Height;
                        double marginBoxStart = Location.Y - ActualMarginTop;
                        double marginBoxHeight = Size.Height + ActualMarginTop + ActualMarginBottom;
                        double dy = ResolveAbsposSelfAlignment(
                            "unsafe " + StripSafeUnsafe(asPost),
                            marginBoxStart, 0, marginBoxStart, 0,
                            marginBoxHeight, false, startIsLow: true);
                        newY = (float)(marginBoxStart + dy + ActualMarginTop);
                    }
                }
                else if (cbVertical && !hasL && !hasR && ParentBox != null
                         && cb.WritingMode == "vertical-rl")
                {
                    // align-self:auto (default) + auto block insets (left/right):
                    // for a vertical-rl container the BLOCK axis is horizontal and
                    // flows right-to-left, so the block-START edge is the RIGHT.
                    // The box rests at that block static position, but Broiler's
                    // base layout placed it flush-left, so flush it right within
                    // the parent content box. (vertical-lr keeps the left edge —
                    // its block start — which is Broiler's default, so no branch
                    // is needed there.) The inline (vertical) axis is handled by
                    // the justify-self branch above. Mirrors the rtl inline-axis
                    // static-position fix (WPT css-align/abspos/justify-self-*-vrl-*,
                    // issue #1131). Widths resolve top-down, so ParentBox's
                    // horizontal extent is reliable here (unlike its vertical one).
                    double rectStart = ParentBox.ClientLeft;
                    double rectWidth = ParentBox.ClientRight - ParentBox.ClientLeft;
                    double marginBoxWidth = Size.Width + ActualMarginLeft + ActualMarginRight;
                    double dx = ResolveAbsposSelfAlignment(
                        "unsafe start", rectStart, rectWidth, rectStart, rectWidth,
                        marginBoxWidth, isRtl: true, startIsLow: false);
                    newX = (float)(rectStart + dx + ActualMarginLeft);
                }

                if (newX != Location.X || newY != Location.Y)
                {
                    float deltaX = newX - Location.X;
                    float deltaY = newY - Location.Y;
                    if (deltaX != 0)
                        OffsetLeft(deltaX);
                    if (deltaY != 0)
                    {
                        OffsetTop(deltaY);
                        ActualBottom += deltaY;
                    }
                }

                // Un-stretch the block axis to the content height for non-stretch
                // align-self.  Runs even when the offset was zero (align-self:start
                // keeps the box at the start edge but still shrinks it), so it is
                // outside the offset guard above.
                if (alignBlockBorderBoxHeight is double abh)
                    ActualBottom = Location.Y + abh;
            }
        }

        // CSS Box Alignment Level 3 §5.4: align-content on block containers
        // shifts the in-flow content vertically when the container has a
        // definite height larger than the content.  Values:
        //   normal/start/baseline/flex-start → no shift (top-aligned)
        //   center                           → center vertically
        //   end/flex-end/last baseline       → bottom-aligned
        //   space-between/space-around/space-evenly → distribute space
        // The "unsafe" and "safe" prefixes are stripped; safe alignment
        // falls back to start when content overflows, but for blocks this
        // is handled implicitly (shift is clamped to ≥ 0).
        if (AlignContent != null && AlignContent != "normal"
            // The definite-track grid pass distributes align-content across its
            // row tracks itself; this block-level shift would double it.
            && !_gridTrackLayoutApplied
            && (IsBlock || Display == CssConstants.ListItem || Display == CssConstants.InlineBlock
                || Display == CssConstants.TableCell)
            && Boxes.Count > 0
            && (Height != CssConstants.Auto && !string.IsNullOrEmpty(Height)
                || Display == CssConstants.TableCell))
        {
            double borderBoxHeight = ActualBottom - Location.Y;
            double containerContentHeight = borderBoxHeight
                - ActualPaddingTop - ActualPaddingBottom
                - ActualBorderTopWidth - ActualBorderBottomWidth;

            // Compute the extent of the in-flow content (excluding absolutely
            // positioned and fixed elements).  Per CSS Box Alignment §5.4 the
            // alignment subject is the content's *margin* box, so the leading
            // child's top margin and the trailing child's bottom margin count
            // toward the consumed space — measuring only border boxes would
            // overstate the free space and shift the content too far.
            double contentTop = double.MaxValue;
            double contentBottom = double.MinValue;
            foreach (var child in Boxes)
            {
                if (child.Position == CssConstants.Absolute || child.Position == CssConstants.Fixed)
                    continue;
                if (child.Display == CssConstants.None)
                    continue;
                double childTop = child.Location.Y - child.ActualMarginTop;
                double childBottom = child.ActualBottom + child.ActualMarginBottom;
                if (childTop < contentTop)
                    contentTop = childTop;
                if (childBottom > contentBottom)
                    contentBottom = childBottom;
            }

            if (contentTop < double.MaxValue && contentBottom > double.MinValue)
            {
                double usedContentHeight = contentBottom - contentTop;
                double freeSpace = containerContentHeight - usedContentHeight;

                // Normalise the align-content value: strip safe/unsafe prefix.
                string ac = AlignContent.Trim();
                bool explicitUnsafe = ac.StartsWith("unsafe ", StringComparison.OrdinalIgnoreCase);
                bool explicitSafe = ac.StartsWith("safe ", StringComparison.OrdinalIgnoreCase);
                if (explicitSafe)
                    ac = ac.Substring(5).Trim();
                else if (explicitUnsafe)
                    ac = ac.Substring(7).Trim();

                // CSS Box Alignment §5.3: when no explicit safe/unsafe keyword
                // is present, the default overflow alignment is "safe".
                bool isSafe = !explicitUnsafe;

                // Only compute shift when there's free space, or when unsafe
                // mode allows shifting even into overflow.
                if (freeSpace > 0.5 || (!isSafe && freeSpace < -0.5))
                {
                    double shift = 0;
                    switch (ac.ToLowerInvariant())
                    {
                        case "center":
                            shift = freeSpace / 2;
                            break;
                        case "end":
                        case "flex-end":
                            shift = freeSpace;
                            break;
                        // baseline / last baseline: with no baseline-sharing group
                        // (each container is independent), both fall back to the
                        // start edge — matching the reference rendering.
                        case "space-between":
                            // Single content group → same as start (no shift).
                            break;
                        case "space-around":
                            shift = freeSpace / 2;
                            break;
                        case "space-evenly":
                            shift = freeSpace / 2;
                            break;
                        // start, flex-start, baseline, normal → no shift.
                    }

                    // Safe alignment: clamp shift to 0 to prevent overflow.
                    if (isSafe && shift < 0)
                        shift = 0;

                    if (Math.Abs(shift) > 0.5)
                    {
                        foreach (var child in Boxes)
                        {
                            if (child.Position == CssConstants.Absolute || child.Position == CssConstants.Fixed)
                                continue;
                            if (child.Display == CssConstants.None)
                                continue;
                            child.OffsetTop(shift);
                        }
                    }
                }
            }
        }

        // CSS Box Alignment Level 3 §6.1: justify-self on block-level boxes.
        // When a non-replaced block has an explicit width narrower than its
        // containing block, 'justify-self' shifts the box horizontally within
        // the containing block's content area.  Values:
        //   auto/normal/stretch → default behaviour (no shift)
        //   start/flex-start/self-start/left → left-aligned (no shift in LTR)
        //   end/flex-end/self-end/right → right-aligned
        //   center → centered
        // Floated and absolutely/fixed positioned boxes are unaffected.
        //
        // 'auto' and 'normal' are not literally no-ops — they resolve:
        //   • justify-self:auto → the containing block's 'justify-items'
        //     (CSS Box Alignment §justify-self).
        //   • a still-unresolved 'normal'/'stretch' on a definite-width block →
        //     the parent's legacy 'text-align:-webkit-{left,right,center}' block
        //     alignment, if any (non-standard but widely supported; WPT
        //     css-align/blocks/justify-self-text-align exercises it).
        if (Float == CssConstants.None
            && Position != CssConstants.Absolute && Position != CssConstants.Fixed
            && (IsBlock || Display == CssConstants.ListItem)
            && ParentBox != null)
        {
            // CSS Box Alignment §5.3 + §6.1: an explicit overflow-alignment
            // keyword (safe/unsafe) on a block-level box. Unlike the legacy
            // path below, this handles the containing block's inline axis when
            // it is VERTICAL (writing-mode: vertical-*) — where justify-self
            // shifts the box along Y — and it honours overflow: `safe` clamps
            // to start when the box is larger than the alignment container,
            // while `unsafe` keeps the requested edge (allowing a negative
            // shift past the start edge). The keyword-less path is left
            // untouched below to avoid perturbing existing block layout.
            string rawJs = JustifySelf?.Trim().ToLowerInvariant() ?? "auto";
            if (rawJs.StartsWith("safe ", StringComparison.Ordinal)
                || rawJs.StartsWith("unsafe ", StringComparison.Ordinal))
            {
                bool explicitSafe = rawJs.StartsWith("safe ", StringComparison.Ordinal);
                string alignKw = StripSafeUnsafe(rawJs);
                if (alignKw is "center" or "end" or "flex-end" or "self-end" or "right"
                    or "start" or "flex-start" or "self-start" or "left")
                {
                    // When the box will be rotated by the vertical-flow transform
                    // (WillBeVerticalTransposed), layout is happening in the logical
                    // (horizontal) frame, so justify-self is applied along the
                    // logical inline axis (X) here and the transform rotates it onto
                    // the physical vertical axis. Only when the transform is NOT in
                    // play (prototype disabled) does a vertical container require a
                    // direct physical-Y shift.
                    bool containerVertical = IsVerticalWritingMode(ParentBox.WritingMode)
                        && !WillBeVerticalTransposed();
                    double boxSize = containerVertical
                        ? ActualBottom - Location.Y
                        : ActualRight - Location.X;
                    double marginStart = containerVertical ? ActualMarginTop : ActualMarginLeft;
                    double marginEnd = containerVertical ? ActualMarginBottom : ActualMarginRight;
                    // The vertical inline-axis extent must come from ActualHeight
                    // (the resolved content height), not ClientRectangle.Height:
                    // block-axis geometry resolves bottom-up, so the container's
                    // ActualBottom — and thus ClientRectangle.Height — is still 0
                    // when its in-flow child is being aligned.
                    double containerSize = containerVertical
                        ? ParentBox.ActualHeight
                        : ParentBox.ClientRectangle.Width;
                    double axisFree = containerSize - boxSize - marginStart - marginEnd;

                    // 'safe' falls back to 'start' when the box overflows.
                    if (explicitSafe && axisFree < 0)
                        alignKw = "start";

                    bool selfRtl = Direction == "rtl";
                    bool cbRtl = ParentBox?.Direction == "rtl";
                    double d = alignKw switch
                    {
                        "center" => axisFree / 2,
                        "end" or "flex-end" => cbRtl ? 0 : axisFree,
                        "self-end" => selfRtl ? 0 : axisFree,
                        "right" => axisFree,
                        "start" or "flex-start" => cbRtl ? axisFree : 0,
                        "self-start" => selfRtl ? axisFree : 0,
                        _ => 0, // left
                    };

                    if (Math.Abs(d) > 0.5)
                    {
                        if (containerVertical)
                            OffsetTop(d);
                        else
                            OffsetLeft(d);
                    }
                }
                // The keyword-less legacy path below is a no-op for an explicit
                // safe/unsafe value ("safe end" etc. is not a concrete keyword,
                // so it resolves to null there); fall through so any
                // position:relative offset later in this method still applies.
            }

            string js = JustifySelf?.Trim().ToLowerInvariant() ?? "auto";
            if (js == "auto")
                js = ParentBox.JustifyItems?.Trim().ToLowerInvariant() ?? "normal";
            if (js is "normal" or "stretch" or "auto" or "legacy")
            {
                js = (ParentBox.TextAlign?.Trim().ToLowerInvariant()) switch
                {
                    "-webkit-right" => "right",
                    "-webkit-center" => "center",
                    "-webkit-left" => "left",
                    _ => js,
                };
            }

            // Only a concrete edge/center alignment actually moves the box;
            // normal/stretch/baseline leave it at its in-flow position.
            if (js is not ("center" or "end" or "flex-end" or "self-end" or "right"
                or "start" or "flex-start" or "self-start" or "left"))
                js = null!;

            double boxWidth = ActualRight - Location.X;
            double containerWidth = ParentBox.ClientRectangle.Width;
            // Free space is what remains AFTER the box's own margins. Auto margins
            // are resolved during block layout (e.g. margin:auto centres the box by
            // splitting the free space), so they leave nothing here — which makes
            // 'justify-self' a no-op, per CSS Box Alignment §justify-abspos ("auto
            // margins make justify-self have no effect"). Accounting for margins
            // also keeps explicit-margin boxes aligned to the correct edge.
            double freeSpace = containerWidth - boxWidth
                - ActualMarginLeft - ActualMarginRight;

            if (js != null && freeSpace > 0.5)
            {
                // CSS Box Alignment §6.1: 'start'/'end' use the containing
                // block's writing direction; 'self-start'/'self-end' use the
                // element's own writing direction.
                bool isElementRtl = Direction == "rtl";
                bool isContainerRtl = ParentBox?.Direction == "rtl";

                double dx = 0;
                switch (js)
                {
                    case "center":
                        dx = freeSpace / 2;
                        break;
                    case "end":
                    case "flex-end":
                        dx = isContainerRtl ? 0 : freeSpace;
                        break;
                    case "self-end":
                        dx = isElementRtl ? 0 : freeSpace;
                        break;
                    case "right":
                        dx = freeSpace;
                        break;
                    case "start":
                    case "flex-start":
                        dx = isContainerRtl ? freeSpace : 0;
                        break;
                    case "self-start":
                        dx = isElementRtl ? freeSpace : 0;
                        break;
                    case "left":
                        dx = 0;
                        break;
                }

                if (dx > 0.5)
                    OffsetLeft(dx);
            }
        }

        // Apply position:relative offset after layout (visual only, does not affect flow)
        // CSS2.1 §9.4.3: For relative positioning, 'left'/'right' and
        // 'top'/'bottom' form constraint pairs.  When 'top' is auto and
        // 'bottom' is not, dy = -bottom.  When both are non-auto, 'bottom'
        // is ignored (in LTR).  Same logic applies to left/right.
        if (Position == CssConstants.Relative)
        {
            double dx = 0, dy = 0;

            bool hasLeft = Left != null && Left != CssConstants.Auto;
            bool hasRight = Right != null && Right != CssConstants.Auto;
            bool hasTop = Top != null && Top != CssConstants.Auto;
            bool hasBottom = Bottom != null && Bottom != CssConstants.Auto;

            if (hasLeft)
                dx = CssValueParser.ParseLength(Left, Size.Width, GetEmHeight());
            else if (hasRight)
                dx = -CssValueParser.ParseLength(Right, Size.Width, GetEmHeight());

            if (hasTop)
                dy = CssValueParser.ParseLength(Top, Size.Height, GetEmHeight());
            else if (hasBottom)
                dy = -CssValueParser.ParseLength(Bottom, Size.Height, GetEmHeight());

            if (dx != 0)
                OffsetLeft(dx);
            if (dy != 0)
                OffsetTop(dy);
        }

        CreateListItemBox(g);

        if (!IsFixed)
        {
            var actualWidth = Math.Max(GetMinimumWidth() + CssBoxHelper.GetWidthMarginDeep(this), Size.Width < 90999 ? ActualRight - LayoutEnvironment.RootLocation.X : 0);
            LayoutEnvironment.ActualSize = CommonUtils.Max(LayoutEnvironment.ActualSize, new SizeF((float)actualWidth, (float)(ActualBottom - LayoutEnvironment.RootLocation.Y)));
        }
    }

    /// <summary>
    /// CSS Multi-column §6.2: resolves <c>column-gap</c>.  The initial value
    /// 'normal' is ≈ 1em; an explicit length (including 0) overrides it.
    /// </summary>
    private double ResolveColumnGap()
    {
        if (!string.IsNullOrEmpty(ColumnGap) && ColumnGap != "normal")
            return CssValueParser.ParseLength(ColumnGap, Size.Width, GetEmHeight());
        return GetEmHeight();
    }

    private sealed class FlexItemLayout
    {
        public CssBox Box { get; init; }
        public double Grow { get; init; }
        public double Shrink { get; init; }
        public double BaseOuterWidth { get; init; }
        public double TargetOuterWidth { get; set; }
    }

    private sealed class FlexLineLayout
    {
        public List<FlexItemLayout> Items { get; } = [];
        public double BaseOuterWidth { get; set; }
        public double CrossSize { get; set; }
    }

    private bool IsFlexContainer() => Display is "flex" or "inline-flex";

    internal bool IsRowFlexContainer()
    {
        if (!IsFlexContainer())
            return false;

        string direction = FlexDirection?.Trim().ToLowerInvariant() ?? "row";
        return direction is not ("column" or "column-reverse");
    }

    internal void PerformFlexRowLayout(ILayoutEnvironment g)
    {
        EnsureDescendantWordsMeasured(g);

        double contentLeft = ClientLeft;
        double contentTop = ClientTop;
        double contentWidth = Math.Max(0, Size.Width
            - ActualBorderLeftWidth - ActualBorderRightWidth
            - ActualPaddingLeft - ActualPaddingRight);

        double columnGap = ResolveFlexGap(ColumnGap, contentWidth);
        double rowGap = ResolveFlexGap(RowGap, Size.Height);
        bool wrap = FlexWrap is "wrap" or "wrap-reverse";

        var lines = new List<FlexLineLayout>();
        var currentLine = new FlexLineLayout();

        foreach (var child in Boxes)
        {
            if (!IsInFlowFlexItem(child))
                continue;

            var item = new FlexItemLayout
            {
                Box = child,
                Grow = ParseFlexFactor(child.FlexGrow, 0),
                Shrink = ParseFlexFactor(child.FlexShrink, 1),
                BaseOuterWidth = ResolveFlexItemBaseOuterWidth(child, contentWidth)
            };
            item.TargetOuterWidth = item.BaseOuterWidth;

            double candidateWidth = currentLine.BaseOuterWidth + item.BaseOuterWidth
                + (currentLine.Items.Count > 0 ? columnGap : 0);

            if (wrap && currentLine.Items.Count > 0 && candidateWidth > contentWidth + 0.5)
            {
                lines.Add(currentLine);
                currentLine = new FlexLineLayout();
            }

            currentLine.Items.Add(item);
            currentLine.BaseOuterWidth += item.BaseOuterWidth;
        }

        if (currentLine.Items.Count > 0 || lines.Count == 0)
            lines.Add(currentLine);

        double cursorY = contentTop;
        bool reverse = FlexDirection?.Trim().Equals("row-reverse", StringComparison.OrdinalIgnoreCase) == true;

        foreach (var line in lines)
        {
            ResolveFlexLineWidths(line, contentWidth, columnGap);

            foreach (var item in line.Items)
            {
                LayoutFlexItemAtTargetWidth(g, item.Box, item.TargetOuterWidth);
                double itemHeight = GetFlexItemOuterHeight(item.Box);
                if (itemHeight > line.CrossSize)
                    line.CrossSize = itemHeight;
            }

            double usedWidth = 0;
            foreach (var item in line.Items)
                usedWidth += item.TargetOuterWidth;

            int itemCount = line.Items.Count;
            double freeSpace = contentWidth - usedWidth - Math.Max(0, itemCount - 1) * columnGap;
            if (freeSpace < 0)
                freeSpace = 0;

            ResolveJustifyContent(itemCount, freeSpace, columnGap,
                out double lineOffset, out double itemGap);

            double cursorX = contentLeft + lineOffset;
            if (reverse)
                cursorX = contentLeft + contentWidth - lineOffset;

            for (int i = 0; i < itemCount; i++)
            {
                int itemIndex = reverse ? itemCount - i - 1 : i;
                var item = line.Items[itemIndex];
                var child = item.Box;

                double marginBoxWidth = item.TargetOuterWidth;
                double borderBoxWidth = Math.Max(0, marginBoxWidth
                    - child.ActualMarginLeft - child.ActualMarginRight);
                double itemLeft = reverse
                    ? cursorX - marginBoxWidth + child.ActualMarginLeft
                    : cursorX + child.ActualMarginLeft;

                double crossOffset = ResolveFlexCrossOffset(child, line.CrossSize);
                double itemTop = cursorY + crossOffset + child.ActualMarginTop;

                double dx = itemLeft - child.Location.X;
                double dy = itemTop - child.Location.Y;
                if (Math.Abs(dx) > 0.1)
                    child.OffsetLeft(dx);
                if (Math.Abs(dy) > 0.1)
                    child.OffsetTop(dy);

                if (reverse)
                    cursorX -= marginBoxWidth + itemGap;
                else
                    cursorX += marginBoxWidth + itemGap;

                // The temporary width is restored after child layout; keep
                // the used border-box width in Size for painting/layout dumps.
                if (borderBoxWidth > 0 && Math.Abs(child.Size.Width - borderBoxWidth) > 0.5)
                    child.Size = new SizeF((float)borderBoxWidth, child.Size.Height);
            }

            cursorY += line.CrossSize + rowGap;
        }

        if (lines.Count > 0)
            cursorY -= rowGap;

        ActualBottom = cursorY + ActualPaddingBottom + ActualBorderBottomWidth;
        ActualRight = Location.X + Size.Width;
    }

    private static bool IsInFlowFlexItem(CssBox child) =>
        child.Display != CssConstants.None
        && child.Position is not (CssConstants.Absolute or CssConstants.Fixed);

    private double ResolveFlexItemBaseOuterWidth(CssBox child, double containerContentWidth)
    {
        double borderBoxWidth;
        string basis = child.FlexBasis?.Trim();

        if (!string.IsNullOrEmpty(basis)
            && !basis.Equals("auto", StringComparison.OrdinalIgnoreCase)
            && !basis.Equals("content", StringComparison.OrdinalIgnoreCase))
        {
            borderBoxWidth = child.ResolveSpecifiedWidthToBorderBox(
                ParseFlexLengthOrZero(child, basis, containerContentWidth));
        }
        else if (child.Width != CssConstants.Auto && !string.IsNullOrEmpty(child.Width)
            && !IsIntrinsicWidthKeyword(child.Width))
        {
            borderBoxWidth = child.ResolveSpecifiedWidthToBorderBox(
                ParseFlexLengthOrZero(child, child.Width, containerContentWidth));
        }
        else
        {
            child.GetMinMaxWidth(out _, out double preferred);
            if (double.IsNaN(preferred) || preferred < 0)
                preferred = 0;

            borderBoxWidth = preferred + child.ActualBorderLeftWidth + child.ActualBorderRightWidth;
        }

        borderBoxWidth = ClampFlexItemBorderBoxWidth(child, borderBoxWidth, containerContentWidth);
        return borderBoxWidth + child.ActualMarginLeft + child.ActualMarginRight;
    }

    private static double ParseFlexLengthOrZero(CssBox box, string value, double percentBase)
    {
        try
        {
            return box.ParseLengthWithLineHeight(value, percentBase);
        }
        catch
        {
            return 0;
        }
    }

    private double ClampFlexItemBorderBoxWidth(CssBox child, double borderBoxWidth, double containerContentWidth)
    {
        if (!string.IsNullOrEmpty(child.MaxWidth)
            && !child.MaxWidth.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            double maxWidth = child.ResolveSpecifiedWidthToBorderBox(
                ParseFlexLengthOrZero(child, child.MaxWidth, containerContentWidth));
            if (borderBoxWidth > maxWidth)
                borderBoxWidth = maxWidth;
        }

        bool useAutomaticMinWidth = !child.IsMinWidthSpecified
            || child.MinWidth.Equals("auto", StringComparison.OrdinalIgnoreCase);
        if (useAutomaticMinWidth)
        {
            child.GetMinMaxWidth(out double minContentWidth, out _);
            if (!double.IsNaN(minContentWidth) && minContentWidth > 0)
            {
                double minBorderBoxWidth = minContentWidth
                    + child.ActualBorderLeftWidth + child.ActualBorderRightWidth;
                if (borderBoxWidth < minBorderBoxWidth)
                    borderBoxWidth = minBorderBoxWidth;
            }
        }
        else if (!string.IsNullOrEmpty(child.MinWidth)
            && !child.MinWidth.Equals("0", StringComparison.OrdinalIgnoreCase))
        {
            double minWidth = child.ResolveSpecifiedWidthToBorderBox(
                ParseFlexLengthOrZero(child, child.MinWidth, containerContentWidth));
            if (borderBoxWidth < minWidth)
                borderBoxWidth = minWidth;
        }

        return Math.Max(0, borderBoxWidth);
    }

    private void ResolveFlexLineWidths(FlexLineLayout line, double contentWidth, double columnGap)
    {
        int itemCount = line.Items.Count;
        if (itemCount == 0)
            return;

        double gapTotal = Math.Max(0, itemCount - 1) * columnGap;
        double freeSpace = contentWidth - line.BaseOuterWidth - gapTotal;

        if (freeSpace > 0.5)
        {
            double growTotal = 0;
            foreach (var item in line.Items)
                growTotal += item.Grow;

            if (growTotal > 0)
            {
                foreach (var item in line.Items)
                    item.TargetOuterWidth = item.BaseOuterWidth + freeSpace * (item.Grow / growTotal);
            }
        }
        else if (freeSpace < -0.5)
        {
            double shrinkTotal = 0;
            foreach (var item in line.Items)
                shrinkTotal += item.Shrink * Math.Max(0, item.BaseOuterWidth);

            if (shrinkTotal > 0)
            {
                foreach (var item in line.Items)
                {
                    double shrinkShare = (item.Shrink * Math.Max(0, item.BaseOuterWidth)) / shrinkTotal;
                    double target = item.BaseOuterWidth + freeSpace * shrinkShare;
                    double marginWidth = item.Box.ActualMarginLeft + item.Box.ActualMarginRight;
                    double borderBoxWidth = ClampFlexItemBorderBoxWidth(
                        item.Box,
                        Math.Max(0, target - marginWidth),
                        contentWidth);
                    item.TargetOuterWidth = borderBoxWidth + marginWidth;
                }
            }
        }
    }

    private void LayoutFlexItemAtTargetWidth(ILayoutEnvironment g, CssBox child, double targetOuterWidth)
    {
        double targetBorderBoxWidth = Math.Max(0, targetOuterWidth
            - child.ActualMarginLeft - child.ActualMarginRight);
        double cssWidth = child.UsesBorderBoxSizing
            ? targetBorderBoxWidth
            : targetBorderBoxWidth
              - child.ActualPaddingLeft - child.ActualPaddingRight
              - child.ActualBorderLeftWidth - child.ActualBorderRightWidth;

        string savedWidth = child.Width;
        child.Width = FormatCssPx(Math.Max(0, cssWidth));
        child.PerformLayout(g);
        child.Width = savedWidth;
    }

    private static string FormatCssPx(double value) =>
        value.ToString("0.####", CultureInfo.InvariantCulture) + "px";

    private static double ParseFlexFactor(string value, double fallback)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            && parsed >= 0)
        {
            return parsed;
        }

        return fallback;
    }

    private double ResolveFlexGap(string value, double percentBase)
    {
        if (string.IsNullOrEmpty(value) || value.Equals("normal", StringComparison.OrdinalIgnoreCase))
            return 0;

        try
        {
            return CssValueParser.ParseLength(value, percentBase, GetEmHeight());
        }
        catch
        {
            return 0;
        }
    }

    private void ResolveJustifyContent(int itemCount, double freeSpace, double baseGap,
        out double lineOffset, out double itemGap)
    {
        lineOffset = 0;
        itemGap = baseGap;

        string justify = NormalizeBoxAlignment(JustifyContent);
        switch (justify)
        {
            case "center":
                lineOffset = freeSpace / 2;
                break;
            case "end":
            case "flex-end":
            case "right":
                lineOffset = freeSpace;
                break;
            case "space-between":
                if (itemCount > 1)
                    itemGap = baseGap + freeSpace / (itemCount - 1);
                break;
            case "space-around":
                if (itemCount > 0)
                {
                    itemGap = baseGap + freeSpace / itemCount;
                    lineOffset = (itemGap - baseGap) / 2;
                }
                break;
            case "space-evenly":
                if (itemCount > 0)
                {
                    double extraGap = freeSpace / (itemCount + 1);
                    itemGap = baseGap + extraGap;
                    lineOffset = extraGap;
                }
                break;
        }
    }

    private double ResolveFlexCrossOffset(CssBox child, double lineCrossSize)
    {
        string align = NormalizeBoxAlignment(child.AlignSelf);
        if (align is "" or "auto" or "normal")
            align = NormalizeBoxAlignment(AlignItems);

        double itemOuterHeight = GetFlexItemOuterHeight(child);
        double freeSpace = lineCrossSize - itemOuterHeight;
        if (freeSpace <= 0)
            return 0;

        return align switch
        {
            "center" => freeSpace / 2,
            "end" or "flex-end" or "self-end" => freeSpace,
            _ => 0
        };
    }

    private static double GetFlexItemOuterHeight(CssBox child) =>
        Math.Max(0, child.ActualBottom - child.Location.Y)
        + child.ActualMarginTop + child.ActualMarginBottom;

    private void ApplyFlexColumnInlineAxisAlignment()
    {
        if (!IsFlexContainer())
            return;

        string direction = FlexDirection?.Trim().ToLowerInvariant() ?? "row";
        if (direction is not ("column" or "column-reverse"))
            return;

        double contentWidth = Math.Max(0, Size.Width
            - ActualBorderLeftWidth - ActualBorderRightWidth
            - ActualPaddingLeft - ActualPaddingRight);
        if (contentWidth <= 0)
            return;

        string containerAlign = NormalizeBoxAlignment(AlignItems);
        foreach (var child in Boxes)
        {
            if (!IsInFlowFlexItem(child))
                continue;

            string align = NormalizeBoxAlignment(child.AlignSelf);
            if (align is "" or "auto" or "normal")
                align = containerAlign;

            double marginBoxWidth = child.Size.Width + child.ActualMarginLeft + child.ActualMarginRight;
            double freeSpace = contentWidth - marginBoxWidth;
            if (freeSpace <= 0.5)
                continue;

            double offset = align switch
            {
                "center" => freeSpace / 2,
                "end" or "flex-end" or "self-end" or "right" => freeSpace,
                _ => 0
            };
            if (offset <= 0.5)
                continue;

            double targetLeft = ClientLeft + offset + child.ActualMarginLeft;
            double dx = targetLeft - child.Location.X;
            if (Math.Abs(dx) > 0.5)
                child.OffsetLeft(dx);
        }
    }

    /// <summary>
    /// CSS Multi-column Layout: Redistributes in-flow child boxes into
    /// multiple columns after single-column layout.  Walks down through
    /// single-child containers (e.g. html to body) to find the actual
    /// fragmentable children.
    /// </summary>
    private void ApplyMultiColumnLayout(int colCount)
    {
        // PROTOTYPE (BROILER_VERTICAL_FLOW), Stage 4: in a vertical writing
        // mode the whole subtree is laid out in a logical horizontal frame and
        // rotated into physical space after layout (ApplyVerticalWritingModeFlow).
        // Multi-column fragmentation runs here, in that logical frame, exactly as
        // it does for horizontal-tb: columns advance along the logical inline
        // axis (X). The post-layout rotation then maps the logical inline axis
        // onto the physical inline axis, so the columns stack along the writing
        // mode's inline direction — logical left→right becomes physical top→bottom.
        // Verified against Chromium for css-position/multicol/static-position/
        // vlr-in-multicol-ref.html: an 80×600 logical run fragmented across
        // 100px-tall columns rotates to a 100px-wide × 480px-tall vertical strip
        // (diff 17.3% → ~1.9% vs the legacy single-block run). Right→left modes
        // (vertical-rl / sideways-rl) fragment identically here and are then
        // block-start (right) aligned by ApplyVerticalWritingModeFlow.
        double columnGap = ResolveColumnGap();
        double contentWidth = Size.Width - ActualPaddingLeft - ActualPaddingRight
            - ActualBorderLeftWidth - ActualBorderRightWidth;
        double columnWidth = (contentWidth - (colCount - 1) * columnGap) / colCount;
        if (columnWidth <= 0) return;

        double containerTop = Location.Y + ActualPaddingTop + ActualBorderTopWidth;
        double columnLeft = Location.X + ActualPaddingLeft + ActualBorderLeftWidth;

        // Walk down through single-child containers (html -> body) to find
        // the level with multiple block children to distribute.
        var fragmentParent = FindMultiColumnFragmentParent();
        if (fragmentParent == null) return;

        var fragments = new List<CssBox>();
        foreach (var child in fragmentParent.Boxes)
        {
            if (child.Position == CssConstants.Absolute || child.Position == CssConstants.Fixed)
                continue;
            if (child.Display == CssConstants.None)
                continue;
            fragments.Add(child);
        }

        if (fragments.Count == 0) return;

        double firstTop = fragments[0].Location.Y;
        double lastBottom = GetVisualBottom(fragments[^1]);
        foreach (var frag in fragments)
        {
            double vb = GetVisualBottom(frag);
            if (vb > lastBottom) lastBottom = vb;
        }
        double totalContentHeight = lastBottom - firstTop;

        if (totalContentHeight <= 0) return;

        // Determine column height: balanced columns for auto/max-height,
        // or explicit height.
        bool hasMaxHeight = MaxHeight != "none" && !string.IsNullOrEmpty(MaxHeight);
        bool hasExplicitHeight = Height != CssConstants.Auto && !string.IsNullOrEmpty(Height);
        double maxAllowedHeight = double.MaxValue;

        if (hasMaxHeight)
        {
            double maxH = CssValueParser.ParseLength(MaxHeight, ContainingBlock?.Size.Height ?? Size.Height, GetEmHeight());
            maxAllowedHeight = maxH - ActualPaddingTop - ActualPaddingBottom - ActualBorderTopWidth - ActualBorderBottomWidth;
        }

        double columnHeight;
        if (hasExplicitHeight)
        {
            double h = CssValueParser.ParseLength(Height, ContainingBlock?.Size.Height ?? Size.Height, GetEmHeight());
            columnHeight = h;
        }
        else if (ColumnFill == "auto" && hasMaxHeight)
        {
            // column-fill: auto — fill columns sequentially up to max-height.
            columnHeight = maxAllowedHeight;
        }
        else
        {
            // Balanced column layout: find the minimum column height that
            // distributes all fragments across colCount columns.  Use a
            // binary search between (tallest fragment) and (total height).
            double lo = 0;
            foreach (var frag in fragments)
            {
                double fh = GetVisualBottom(frag) - frag.Location.Y;
                if (fh > lo) lo = fh;
            }
            double hi = totalContentHeight;

            for (int iter = 0; iter < 20; iter++)
            {
                double mid = (lo + hi) / 2;
                int cols = CountColumnsNeededVisual(fragments, mid);
                if (cols <= colCount)
                    hi = mid;
                else
                    lo = mid + 0.5;
            }
            columnHeight = Math.Ceiling(hi);

            if (columnHeight > maxAllowedHeight)
                columnHeight = maxAllowedHeight;
        }

        if (columnHeight <= 0) return;

        // CSS Fragmentation §3: When fragments contain boxes with visible
        // overflow that exceeds the column height (e.g. height: 0 parents
        // with overflowing children), flatten the hierarchy by collecting
        // the deepest fragmentable blocks from inside those containers.
        bool needsDeepFragment = false;
        foreach (var frag in fragments)
        {
            double visualH = GetVisualBottom(frag) - frag.Location.Y;
            if (visualH > columnHeight + 0.5 && frag.Boxes.Count > 0)
            {
                needsDeepFragment = true;
                break;
            }
        }

        if (needsDeepFragment)
        {
            var deepFragments = new List<CssBox>();
            foreach (var frag in fragments)
            {
                double visualH = GetVisualBottom(frag) - frag.Location.Y;
                if (visualH > columnHeight + 0.5 && frag.Boxes.Count > 0)
                {
                    CollectFragmentableBlocksCore(frag, columnHeight, deepFragments, 0);
                }
                else
                {
                    deepFragments.Add(frag);
                }
            }

            if (deepFragments.Count > fragments.Count)
            {
                fragments = deepFragments;
                firstTop = fragments[0].Location.Y;
                lastBottom = firstTop;
                foreach (var frag in fragments)
                {
                    double vb = GetVisualBottom(frag);
                    if (vb > lastBottom) lastBottom = vb;
                }
                totalContentHeight = lastBottom - firstTop;

                // Re-compute balanced column height for the new fragment set.
                if (!hasExplicitHeight && !(ColumnFill == "auto" && hasMaxHeight))
                {
                    double lo = 0;
                    foreach (var frag in fragments)
                    {
                        double fh = GetVisualBottom(frag) - frag.Location.Y;
                        if (fh > lo) lo = fh;
                    }
                    double hi = totalContentHeight;
                    for (int iter = 0; iter < 20; iter++)
                    {
                        double mid = (lo + hi) / 2;
                        int cols = CountColumnsNeededVisual(fragments, mid);
                        if (cols <= colCount) hi = mid;
                        else lo = mid + 0.5;
                    }
                    columnHeight = Math.Ceiling(hi);
                    if (columnHeight > maxAllowedHeight)
                        columnHeight = maxAllowedHeight;
                }
            }
        }

        // Distribute fragments across columns.
        int currentCol = 0;
        double currentY = containerTop;

        foreach (var frag in fragments)
        {
            double fragHeight = GetVisualBottom(frag) - frag.Location.Y;

            bool wouldOverflow = (currentY - containerTop) + fragHeight > columnHeight;
            // CSS Multi-column §3.3: column-count sets the column *width* but is
            // not a hard cap on the number of columns.  When content does not fit
            // in the determined number of columns (e.g. column-fill: auto with a
            // constrained block-size), additional "overflow columns" are created
            // in the inline direction rather than piling the remainder into the
            // last column.  Balance mode keeps content within colCount via the
            // height search above, so this only takes effect when genuinely
            // overflowing.
            if (wouldOverflow && currentY > containerTop + 0.5)
            {
                currentCol++;
                currentY = containerTop;
            }

            double targetX = columnLeft + currentCol * (columnWidth + columnGap)
                + (frag.Location.X - fragmentParent.Location.X);
            double targetY = currentY;

            double dx = targetX - frag.Location.X;
            double dy = targetY - frag.Location.Y;

            if (Math.Abs(dx) > 0.1 || Math.Abs(dy) > 0.1)
            {
                frag.OffsetLeft(dx);
                frag.OffsetTop(dy);
            }

            if (frag.Size.Width > columnWidth + 1)
            {
                frag.Size = new SizeF((float)columnWidth, frag.Size.Height);
            }

            currentY += fragHeight;
        }

        // Update container dimensions.
        double maxBottom = containerTop;
        foreach (var frag in fragments)
        {
            double vb = GetVisualBottom(frag);
            if (vb > maxBottom)
                maxBottom = vb;
        }

        double newBottom = maxBottom + ActualPaddingBottom + ActualBorderBottomWidth;
        if (newBottom < ActualBottom)
            ActualBottom = newBottom;

        if (fragmentParent != this)
        {
            double fpBottom = maxBottom + fragmentParent.ActualPaddingBottom + fragmentParent.ActualBorderBottomWidth;
            if (fpBottom < fragmentParent.ActualBottom)
                fragmentParent.ActualBottom = fpBottom;
        }

        // Overflow columns extend the inline (right) edge beyond the declared
        // column-count, so size the scrollable/overflow extent to the columns
        // actually used rather than the specified count.
        int usedCols = Math.Max(colCount, currentCol + 1);
        double rightEdge = columnLeft + usedCols * columnWidth + (usedCols - 1) * columnGap
            + ActualPaddingRight + ActualBorderRightWidth;
        if (rightEdge > ActualRight)
            ActualRight = rightEdge;
    }

    /// <summary>
    /// Walks down through single-child containers to find the nearest
    /// descendant with multiple in-flow block children for multi-column
    /// fragmentation.
    /// </summary>
    private CssBox FindMultiColumnFragmentParent()
    {
        CssBox current = this;
        for (int depth = 0; depth < 10; depth++)
        {
            int inFlowCount = 0;
            CssBox onlyChild = null;
            foreach (var child in current.Boxes)
            {
                if (child.Position == CssConstants.Absolute || child.Position == CssConstants.Fixed)
                    continue;
                if (child.Display == CssConstants.None)
                    continue;
                inFlowCount++;
                onlyChild = child;
            }

            if (inFlowCount > 1)
                return current;

            if (inFlowCount == 1 && onlyChild != null && onlyChild.Boxes.Count > 0)
            {
                current = onlyChild;
                continue;
            }

            break;
        }

        return current.Boxes.Count > 1 ? current : null;
    }


    /// <summary>
    /// Counts columns needed using visual (overflow-aware) heights.
    /// </summary>
    private static int CountColumnsNeededVisual(List<CssBox> fragments, double columnHeight)
    {
        int cols = 1;
        double currentH = 0;
        foreach (var frag in fragments)
        {
            double fh = GetVisualBottom(frag) - frag.Location.Y;
            if (currentH + fh > columnHeight && currentH > 0.5)
            {
                cols++;
                currentH = fh;
            }
            else
            {
                currentH += fh;
            }
        }
        return cols;
    }

    /// <summary>
    /// Returns the visual bottom of a box, accounting for children that
    /// overflow a constrained height (e.g. height: 0 with visible overflow).
    /// </summary>
    private static double GetVisualBottom(CssBox box)
    {
        double bottom = box.ActualBottom;
        foreach (var child in box.Boxes)
        {
            if (child.Position == CssConstants.Absolute || child.Position == CssConstants.Fixed)
                continue;
            if (child.Display == CssConstants.None)
                continue;
            double cb = GetVisualBottom(child);
            if (cb > bottom) bottom = cb;
        }
        return bottom;
    }


    private static void CollectFragmentableBlocksCore(CssBox parent, double columnHeight,
        List<CssBox> result, int depth)
    {
        if (depth > 15) return; // safety limit

        foreach (var child in parent.Boxes)
        {
            if (child.Position == CssConstants.Absolute || child.Position == CssConstants.Fixed)
                continue;
            if (child.Display == CssConstants.None)
                continue;

            double childHeight = GetVisualBottom(child) - child.Location.Y;

            // If child fits in a column, or has break-inside: avoid, or
            // has no block children to further fragment, keep it as-is.
            bool avoidBreak = child.BreakInside == "avoid" ||
                child.BreakInside == "avoid-column";
            bool hasBlockChildren = false;
            foreach (var gc in child.Boxes)
            {
                if (gc.Position != CssConstants.Absolute && gc.Position != CssConstants.Fixed
                    && gc.Display != CssConstants.None)
                {
                    hasBlockChildren = true;
                    break;
                }
            }

            if (childHeight <= columnHeight + 0.5 || avoidBreak || !hasBlockChildren)
            {
                result.Add(child);
            }
            else
            {
                // Recurse: this child is too tall and can be fragmented.
                CollectFragmentableBlocksCore(child, columnHeight, result, depth + 1);
            }
        }
    }

    /// <summary>
    /// Loads the CSS background image if one is specified and not yet loaded.
    /// Called from <see cref="MeasureWordsSize"/> and overridden versions in
    /// subclasses (e.g. <see cref="CssBoxImage"/>) that replace the base
    /// measurement logic.
    /// </summary>
    protected void LoadBackgroundImageIfNeeded()
    {
        if (BackgroundImage == CssConstants.None || _backgroundImagesInitialized)
            return;

        _backgroundImagesInitialized = true;
        var layers = SplitBackgroundImageLayers(BackgroundImage);
        if (layers.Count == 0)
            return;

        _backgroundImageLoadHandlers = new List<ILayoutImageLoader?>(layers.Count);
        foreach (var layer in layers)
        {
            var src = TryExtractBackgroundImageUrl(layer);
            if (string.IsNullOrEmpty(src))
            {
                _backgroundImageLoadHandlers.Add(null);
                continue;
            }

            var imageLoadHandler = LayoutEnvironment.CreateImageLoader(OnImageLoadComplete);
            _backgroundImageLoadHandlers.Add(imageLoadHandler);
            imageLoadHandler.LoadImage(src, HtmlTag?.Attributes, BaseUrl);
        }
    }

    private static List<string> SplitBackgroundImageLayers(string backgroundImage)
    {
        var layers = new List<string>();
        if (string.IsNullOrWhiteSpace(backgroundImage))
            return layers;

        int depth = 0;
        int start = 0;
        for (int i = 0; i < backgroundImage.Length; i++)
        {
            switch (backgroundImage[i])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    if (depth > 0)
                        depth--;
                    break;
                case ',' when depth == 0:
                    layers.Add(backgroundImage[start..i].Trim());
                    start = i + 1;
                    break;
            }
        }

        layers.Add(backgroundImage[start..].Trim());
        return layers;
    }

    private static string? TryExtractBackgroundImageUrl(string layer)
    {
        if (string.IsNullOrWhiteSpace(layer))
            return null;

        layer = layer.Trim();
        if (!layer.StartsWith("url(", StringComparison.OrdinalIgnoreCase))
            return layer.Contains('(') ? null : layer;

        if (!layer.EndsWith(")", StringComparison.Ordinal))
            return null;

        var src = layer.Substring(4, layer.Length - 5).Trim();
        if (src.Length >= 2 &&
            ((src[0] == '\'' && src[^1] == '\'') ||
             (src[0] == '"' && src[^1] == '"')))
        {
            src = src[1..^1];
        }

        return src;
    }

    internal virtual void MeasureWordsSize(ILayoutEnvironment g)
    {
        if (_wordsSizeMeasured)
            return;

        LoadBackgroundImageIfNeeded();
        MeasureWordSpacing(g);

        if (Words.Count > 0)
        {
            foreach (var boxWord in Words)
            {
                boxWord.Width = boxWord.Text != "\n" ? g.MeasureText(ActualFont, boxWord.Text).Width : 0;
                boxWord.Height = ActualFont.Height;
            }
        }

        _wordsSizeMeasured = true;
    }

    /// <summary>
    /// Recursively calls <see cref="MeasureWordsSize"/> on all descendant
    /// boxes so that <c>ActualWordSpacing</c> and word dimensions are
    /// computed before <see cref="GetMinMaxWidth"/> is invoked for
    /// shrink-to-fit width (CSS2.1 §10.3.7).
    /// Note: the current box (<c>this</c>) is already measured by the
    /// <see cref="MeasureWordsSize"/> call at the start of
    /// <see cref="PerformLayoutImp"/>; only descendants need measuring.
    /// </summary>
    private void EnsureDescendantWordsMeasured(ILayoutEnvironment g)
    {
        var stack = new Stack<CssBox>();
        foreach (var child in Boxes)
            stack.Push(child);

        while (stack.Count > 0)
        {
            var box = stack.Pop();
            box.MeasureWordsSize(g);
            foreach (var child in box.Boxes)
                stack.Push(child);
        }
    }

    protected override sealed CssBoxProperties GetParent() => _parentBox;

    internal void InvalidateFontDependentSubtree()
    {
        InvalidateFontDependentValues();
        foreach (var child in Boxes)
            child.InvalidateFontDependentSubtree();
    }

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

    internal double GetMinimumWidth()
    {
        double maxWidth = 0;
        CssRect maxWidthWord = null;
        CssBoxHelper.GetMinimumWidth_LongestWord(this, ref maxWidth, ref maxWidthWord);

        double padding = 0f;
        if (maxWidthWord != null)
        {
            var box = maxWidthWord.OwnerBox;
            while (box != null)
            {
                padding += box.ActualBorderRightWidth + box.ActualPaddingRight + box.ActualBorderLeftWidth + box.ActualPaddingLeft;
                box = box != this ? box.ParentBox : null;
            }
        }

        return maxWidth + padding;
    }

    /// <summary>
    /// Min/max-content width measured from this box's <em>content</em>, ignoring
    /// its own explicit width (a percentage width is resolved against the caller's
    /// context, so for grid track sizing it is treated as auto). Descendants'
    /// explicit widths are honoured. Used by the grid track-sizing algorithm.
    /// </summary>
    internal void GetContentMinMaxWidth(out double minWidth, out double maxWidth)
    {
        double min = 0f;
        double maxSum = 0f;
        double paddingSum = 0f;
        double marginSum = 0f;

        CssBoxHelper.GetMinMaxSumWords(this, ref min, ref maxSum, ref paddingSum, ref marginSum, suppressExplicitWidthFor: this);

        maxWidth = paddingSum + maxSum;
        minWidth = paddingSum + (min < 90999 ? min : 0);
        maxWidth -= CssBoxHelper.EdgeWhitespaceSpacing(this);
        if (maxWidth < minWidth)
            maxWidth = minWidth;
    }

    internal void GetMinMaxWidth(out double minWidth, out double maxWidth)
    {
        // A grid with a fixed track template contributes its physical-width track
        // sum (+ gaps + own border/padding) as both min- and max-content, rather
        // than the intrinsic width of its inline content — so a shrink-to-fit grid
        // (or a nested grid item) sizes to its tracks, not its (often empty) text.
        if (TryComputeGridIntrinsicContentWidth(useMax: false, out double gridMin)
            && TryComputeGridIntrinsicContentWidth(useMax: true, out double gridMax))
        {
            double pb = ActualBorderLeftWidth + ActualBorderRightWidth
                      + ActualPaddingLeft + ActualPaddingRight;
            minWidth = gridMin + pb;
            maxWidth = gridMax + pb;
            return;
        }

        double min = 0f;
        double maxSum = 0f;
        double paddingSum = 0f;
        double marginSum = 0f;

        CssBoxHelper.GetMinMaxSumWords(this, ref min, ref maxSum, ref paddingSum, ref marginSum);

        maxWidth = paddingSum + maxSum;
        minWidth = paddingSum + (min < 90999 ? min : 0);

        // CSS Text 3 §4.1.1 (phase II): a collapsible space sequence at the
        // start of the first line / end of the last line of a formatting
        // context is removed and contributes no width.  Broiler models a
        // collapsed space as word-spacing carried on the neighbouring word
        // (HasSpaceBefore / HasSpaceAfter); GetMinMaxSumWords counts that
        // spacing for every word, so the leading space-before of the box's
        // first content word and the trailing space-after of its last word
        // inflate the preferred width by one space each.  This is the box's
        // own formatting-context edge (GetMinMaxWidth is only queried for
        // shrink-to-fit roots — table cells, floats, inline-blocks,
        // abspos), and the paint path already drops those edge spaces, so
        // the width must match.  Subtracting them makes a whitespace-padded
        // table cell (e.g. <td> Cell </td>) shrink to the same width as the
        // tight cell, so adjacent cells abut as in a real <table>
        // (CSS2 tables/table-anonymous-objects-*).
        maxWidth -= CssBoxHelper.EdgeWhitespaceSpacing(this);
        if (maxWidth < minWidth)
            maxWidth = minWidth;
    }

    /// <summary>
    /// CSS2.1 §10.3.7: Computes the shrink-to-fit width for an auto-width
    /// absolutely positioned element by independently measuring each direct
    /// child's total width and returning the maximum.
    /// Each block or float child is its own "line"; the preferred width is
    /// the widest line.  This avoids the incorrect accumulation that occurs
    /// when <see cref="CssBoxHelper.GetMinMaxSumWords"/> sums float widths
    /// with preceding block widths.
    /// </summary>
    private double ComputeShrinkToFitWidth()
    {
        // A grid with a fixed track template shrink-to-fits to its physical-width
        // track sum (+ gaps), not the max-content of its inline content — an empty
        // or small-item grid would otherwise collapse (fit-content / float /
        // inline-grid grids). Content-box width; the caller adds border/padding.
        if (TryComputeGridIntrinsicContentWidth(useMax: true, out double gridMaxContent))
            return gridMaxContent;

        double maxLineWidth = 0;
        // Running width of a horizontal run of adjacent floated children. At
        // max-content the container is under no width constraint, so a run of
        // float:left/right children lays out side by side and their widths ADD;
        // a non-floated (block) child ends the run and starts its own line.
        double floatRunWidth = 0;

        foreach (var child in Boxes)
        {
            double childWidth;

            if (child.Width != CssConstants.Auto && !string.IsNullOrEmpty(child.Width)
                && !IsPercentageWidth(child.Width))
            {
                // Explicit (definite) width: use declared width + borders/padding
                double containingBlockWidth = Size.Width > 0 && !double.IsNaN(Size.Width) ? Size.Width : 0;
                childWidth = child.ParseLengthWithLineHeight(child.Width, containingBlockWidth)
                           + child.ActualBorderLeftWidth + child.ActualBorderRightWidth
                           + child.ActualPaddingLeft + child.ActualPaddingRight;
            }
            else
            {
                // Auto- or percentage-width child: compute its intrinsic
                // preferred width. CSS Sizing 3 §5.1: a child's percentage width
                // resolves against the size we are *computing*, so it is treated
                // as auto for the container's max-content — otherwise a
                // width:100% child resolves against the container's current
                // (available) width and balloons the shrink-to-fit result to the
                // full container (e.g. a float or auto-fill grid item sized 100%
                // pins the float to the viewport instead of its content).
                // Guard against NaN from unmeasured words in deeply nested
                // inline elements (e.g. Acid2 .eyes → #eyes-a → <object>).
                child.GetMinMaxWidth(out _, out double childMax);
                childWidth = double.IsNaN(childMax) ? 0 : childMax;
            }

            childWidth += child.ActualMarginLeft + child.ActualMarginRight;
            if (double.IsNaN(childWidth))
                continue;

            // CSS Sizing 3 §5: the max-content width of a container is the widest
            // of its lines with no wrapping. Inline-level content stays on the line
            // and accumulates — adjacent floats (WPT floats-143: a <ul> of two
            // float:left <li> would otherwise shrink to one child's width and wrap
            // the second below the first) and **atomic inline-level boxes**
            // (inline-block / inline-table / inline-flex / inline-grid), which sit
            // side by side, so two 40px inline-blocks contribute 80, not 40. Only a
            // block-level child ends the run and starts its own line.
            if (child.Float != CssConstants.None
                || CssBoxHelper.IsAtomicInlineLevel(child.Display))
            {
                floatRunWidth += childWidth;
                maxLineWidth = Math.Max(maxLineWidth, floatRunWidth);
            }
            else
            {
                floatRunWidth = 0;
                maxLineWidth = Math.Max(maxLineWidth, childWidth);
            }
        }

        return maxLineWidth;
    }

    // ─────────────────────── CSS Sizing 4: aspect-ratio ───────────────────────

    /// <summary>
    /// CSS Sizing 4 §4: resolve the used border-box block (height) size of a box
    /// whose height is <c>auto</c> from its already-resolved used inline (width)
    /// size and its preferred <c>aspect-ratio</c>. The caller applies this only to
    /// in-flow block-level boxes, whose used width fills the containing block and
    /// so does not itself depend on the aspect ratio, making the transfer
    /// unambiguous.
    /// <para>The reference browser drops the experimental <c>display: grid-lanes</c>
    /// keyword to the element's default display (block; issue #1218) but still
    /// honours <c>aspect-ratio</c>, so a dropped grid-lanes container with an auto
    /// height is sized to a square — the <c>css-grid/grid-lanes/track-sizing/
    /// auto-repeat</c> cluster expects exactly this. Broiler previously ignored
    /// <c>aspect-ratio</c> on ordinary boxes and rendered a viewport-wide,
    /// min-height-tall bar, matching those references by only ~8%.</para>
    /// <para>Returns the transferred border-box height; the caller then applies the
    /// CSS2.1 §10.7 min-/max-height clamp (so a <c>min-height</c> floors the
    /// square). Returns <c>false</c> when there is no preferred aspect ratio,
    /// leaving every aspect-ratio-less box (the overwhelming majority) untouched.</para>
    /// </summary>
    private bool TryResolveAspectRatioBlockHeight(out double borderBoxHeight)
    {
        borderBoxHeight = 0;
        if (!TryParseAspectRatio(AspectRatio, out double ratio) || !(ratio > 0))
            return false;

        double borderBoxWidth = Size.Width;
        if (!(borderBoxWidth > 0))
            return false;

        // aspect-ratio relates the two sizes of the box named by box-sizing
        // (CSS Sizing 4 §4): the border box under `box-sizing: border-box`,
        // otherwise the content box. Transfer width→height in that box (ratio is
        // width/height), then map back to a border-box height for ActualBottom.
        double specifiedHeight;
        if (UsesBorderBoxSizing)
        {
            specifiedHeight = borderBoxWidth / ratio;
        }
        else
        {
            double contentWidth = borderBoxWidth
                - ActualPaddingLeft - ActualPaddingRight
                - ActualBorderLeftWidth - ActualBorderRightWidth;
            if (!(contentWidth > 0))
                return false;
            specifiedHeight = contentWidth / ratio;
        }

        borderBoxHeight = ResolveSpecifiedHeightToBorderBox(specifiedHeight);
        return borderBoxHeight > 0
            && !double.IsNaN(borderBoxHeight) && !double.IsInfinity(borderBoxHeight);
    }

    /// <summary>Parses an <c>aspect-ratio</c> value (<c>&lt;number&gt; [ /
    /// &lt;number&gt; ]?</c>, ignoring a leading/trailing <c>auto</c> keyword)
    /// into a width/height ratio. Returns <c>false</c> for <c>auto</c>/<c>none</c>
    /// or a non-positive ratio.</summary>
    internal static bool TryParseAspectRatio(string value, out double ratio)
    {
        ratio = 0;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        double w = double.NaN, h = 1;
        foreach (var token in value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Equals("auto", StringComparison.OrdinalIgnoreCase)
                || token.Equals("none", StringComparison.OrdinalIgnoreCase))
                continue;
            int slash = token.IndexOf('/');
            if (slash >= 0)
            {
                if (!double.TryParse(token[..slash].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out w))
                    return false;
                string rest = token[(slash + 1)..].Trim();
                if (rest.Length > 0
                    && !double.TryParse(rest, NumberStyles.Float, CultureInfo.InvariantCulture, out h))
                    return false;
            }
            else if (double.IsNaN(w))
            {
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out w))
                    return false;
            }
            else
            {
                // A second bare number is the denominator (e.g. `1 / 1` split on space).
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out h))
                    return false;
            }
        }
        if (double.IsNaN(w) || !(w > 0) || !(h > 0))
            return false;
        ratio = w / h;
        return true;
    }

    /// <summary>
    /// CSS Sizing 3 §5.1: <c>true</c> when <paramref name="width"/> is one of
    /// the intrinsic sizing keywords (<c>min-content</c>, <c>max-content</c>,
    /// <c>fit-content</c>).
    /// </summary>
    private static bool IsIntrinsicWidthKeyword(string width) =>
        string.Equals(width, "min-content", StringComparison.OrdinalIgnoreCase)
        || string.Equals(width, "max-content", StringComparison.OrdinalIgnoreCase)
        || string.Equals(width, "fit-content", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// CSS Sizing 3 §5.1: <c>true</c> when <paramref name="width"/> is a plain
    /// percentage (e.g. <c>100%</c>). Such a width resolves against the size being
    /// computed during a container's intrinsic (shrink-to-fit / max-content) pass,
    /// so it must be treated as <c>auto</c> there rather than resolved against the
    /// container's tentative width.
    /// </summary>
    private static bool IsPercentageWidth(string width) =>
        !string.IsNullOrEmpty(width)
        && width.EndsWith("%", StringComparison.Ordinal)
        && !width.Contains('(');

    /// <summary>
    /// CSS Sizing 3 §5: Resolves an intrinsic-keyword width to a used
    /// border-box width.  <c>min-content</c> uses the largest child
    /// min-content contribution, <c>max-content</c> the largest max-content
    /// contribution, and <c>fit-content</c> clamps the max-content size into
    /// the available space (but never below min-content).
    /// </summary>
    private double ResolveIntrinsicWidth(ILayoutEnvironment g, string keyword, double availableContentWidth)
    {
        EnsureDescendantWordsMeasured(g);

        double available = availableContentWidth - ActualMarginLeft - ActualMarginRight;
        double content;
        if (string.Equals(keyword, "min-content", StringComparison.OrdinalIgnoreCase))
        {
            content = ComputeIntrinsicInlineSize(useMin: true);
        }
        else if (string.Equals(keyword, "max-content", StringComparison.OrdinalIgnoreCase))
        {
            content = ComputeIntrinsicInlineSize(useMin: false);
        }
        else // fit-content
        {
            double max = ComputeIntrinsicInlineSize(useMin: false);
            double min = ComputeIntrinsicInlineSize(useMin: true);
            content = Math.Min(Math.Max(min, available), max);
        }

        if (double.IsNaN(content) || content < 0)
            content = 0;

        return ResolveSpecifiedWidthToBorderBox(content);
    }

    /// <summary>
    /// Computes the intrinsic inline size (content width) as the widest direct
    /// child contribution.  Each block/float child forms its own line, so the
    /// container's intrinsic size is the maximum child width rather than the
    /// sum.  When <paramref name="useMin"/> is set, auto-width children
    /// contribute their min-content width; otherwise their max-content width.
    /// </summary>
    private double ComputeIntrinsicInlineSize(bool useMin)
    {
        double maxLineWidth = 0;

        foreach (var child in Boxes)
        {
            double childWidth;

            if (child.Width != CssConstants.Auto && !string.IsNullOrEmpty(child.Width)
                && !IsIntrinsicWidthKeyword(child.Width))
            {
                double containingBlockWidth = Size.Width > 0 && !double.IsNaN(Size.Width) ? Size.Width : 0;
                childWidth = child.ParseLengthWithLineHeight(child.Width, containingBlockWidth)
                           + child.ActualBorderLeftWidth + child.ActualBorderRightWidth
                           + child.ActualPaddingLeft + child.ActualPaddingRight;
            }
            else
            {
                child.GetMinMaxWidth(out double childMin, out double childMax);
                double intrinsic = useMin ? childMin : childMax;
                childWidth = double.IsNaN(intrinsic) ? 0 : intrinsic;
            }

            childWidth += child.ActualMarginLeft + child.ActualMarginRight;
            if (!double.IsNaN(childWidth))
                maxLineWidth = Math.Max(maxLineWidth, childWidth);
        }

        return maxLineWidth;
    }

    /// <summary>
    /// CSS Box Model 4 §6.2: Applies <c>margin-trim</c> to this box by zeroing
    /// the block-start margin of its first in-flow block-level child and/or the
    /// block-end margin of its last in-flow block-level child, as requested by
    /// the property value (<c>block</c>, <c>block-start</c>, <c>block-end</c>).
    /// Inline-axis trimming is not yet supported.
    /// </summary>
    private void ApplyMarginTrim()
    {
        if (string.IsNullOrEmpty(MarginTrim) || MarginTrim == CssConstants.None)
            return;

        bool trimBlockStart = false;
        bool trimBlockEnd = false;
        foreach (var token in MarginTrim.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
        {
            switch (token.ToLowerInvariant())
            {
                case "block":
                    trimBlockStart = true;
                    trimBlockEnd = true;
                    break;
                case "block-start":
                    trimBlockStart = true;
                    break;
                case "block-end":
                    trimBlockEnd = true;
                    break;
            }
        }

        if (!trimBlockStart && !trimBlockEnd)
            return;

        CssBox first = null;
        CssBox last = null;
        foreach (var child in Boxes)
        {
            if (child.Display == CssConstants.None
                || child.Position == CssConstants.Absolute
                || child.Position == CssConstants.Fixed
                || child.Float != CssConstants.None
                || child.IsInline)
                continue;

            first ??= child;
            last = child;
        }

        if (trimBlockStart && first != null)
            first.MarginTop = "0";
        if (trimBlockEnd && last != null)
            last.MarginBottom = "0";
    }

    internal new void InheritStyle(CssBox box = null, bool everything = false) => base.InheritStyle(box ?? ParentBox, everything);

    protected double MarginTopCollapse(CssBoxProperties prevSibling)
    {
        double value;

        if (prevSibling != null)
        {
            // CSS2.1 §8.3.1: When the previous sibling is an "empty" box
            // (zero content height, no borders/padding, height auto/0), its
            // own top and bottom margins — and its children's margins —
            // collapse through.  The resulting collapsed margin participates
            // in collapsing with this element's top margin.
            if (prevSibling is CssBox prevBox && CssBoxHelper.IsEmptyCollapsible(prevBox))
            {
                double maxPos = Math.Max(ActualMarginTop, 0);
                double maxNeg = Math.Min(ActualMarginTop, 0);
                CssBoxHelper.CollectEmptyBoxMargins(prevBox, ref maxPos, ref maxNeg);
                double collapsed = maxPos + maxNeg; // maxNeg <= 0
                // Subtract the portion of the collapsed margin already
                // consumed when positioning the empty box itself (its
                // CollapsedMarginTop was recorded during its own layout).
                value = collapsed - prevBox.CollapsedMarginTop;
            }
            else
            {
                // CSS2.1 §8.3.1: Adjoining vertical margins collapse.
                // When both are positive → max(m1, m2).
                // When one is negative  → max(positives,0) + min(negatives,0).
                // When both are negative → 0 + min(m1,m2) = most-negative.
                // The general formula covers all three cases.
                // Use GetPropagatedMarginBottom so that a last-child's
                // bottom margin propagates through its parent when the
                // parent has no bottom border/padding and auto height
                // (CSS 2.1 §8.3.1 parent-child bottom-margin collapse).
                double prevMb = (prevSibling is CssBox prevSibBox)
                    ? CssBoxHelper.GetPropagatedMarginBottom(prevSibBox)
                    : prevSibling.ActualMarginBottom;
                double maxPos = Math.Max(
                    Math.Max(prevMb, 0),
                    Math.Max(ActualMarginTop, 0));
                double minNeg = Math.Min(
                    Math.Min(prevMb, 0),
                    Math.Min(ActualMarginTop, 0));
                value = maxPos + minNeg;
            }
            CollapsedMarginTop = value;
        }
        else if (_parentBox != null && _parentBox.ActualPaddingTop < 0.1 && _parentBox.ActualPaddingBottom < 0.1 && _parentBox.ActualBorderTopWidth < 0.1 && _parentBox.ActualBorderBottomWidth < 0.1
            // CSS Box Alignment §5.4: align-content != normal establishes
            // a BFC, which prevents parent–child margin collapsing.
            && (_parentBox.AlignContent == null || _parentBox.AlignContent == "normal"))
        {
            double parentEffective = Math.Max(_parentBox.ActualMarginTop, _parentBox.CollapsedMarginTop);

            // CSS2.1 §8.3.1: First in-flow child's top margin collapses
            // with the parent's top margin when the parent has no top
            // border and no top padding.  When the child's margin
            // exceeds the parent's, propagate the excess upward by
            // shifting the parent's position down.  Only do this for
            // non-root containers (not html/body) to avoid disturbing
            // the root element's established position.
            if (ActualMarginTop > parentEffective + 0.1
                && _parentBox.ParentBox != null
                && _parentBox.ParentBox.ParentBox != null)
            {
                double propagation = ActualMarginTop - parentEffective;
                _parentBox.Location = new PointF(
                    _parentBox.Location.X,
                    _parentBox.Location.Y + (float)propagation);
                _parentBox.CollapsedMarginTop = ActualMarginTop;
                value = 0;
            }
            else
            {
                value = Math.Max(0, ActualMarginTop - parentEffective);
            }
        }
        else
        {
            value = ActualMarginTop;

            // When the parent establishes a BFC (e.g. via align-content),
            // the first child's margin is fully consumed for positioning.
            // Record it so that an empty-collapsible sibling can subtract
            // the already-consumed portion during its own collapse.
            if (_parentBox != null
                && _parentBox.AlignContent != null
                && _parentBox.AlignContent != "normal")
            {
                CollapsedMarginTop = value;
            }
        }

        // fix for hr tag
        if (value < 0.1 && HtmlTag != null && HtmlTag.Name == "hr")
            value = GetEmHeight() * 1.1f;

        return value;
    }

    public bool BreakPage()
    {
        var container = LayoutEnvironment;

        if (Size.Height >= container.PageSize.Height)
            return false;

        var remTop = (Location.Y - container.MarginTop) % container.PageSize.Height;
        var remBottom = (ActualBottom - container.MarginTop) % container.PageSize.Height;

        if (remTop > remBottom)
        {
            var diff = container.PageSize.Height - remTop;
            Location = new PointF(Location.X, (float)(Location.Y + diff + 1));
            
            return true;
        }

        return false;
    }

    private double CalculateActualRight()
    {
        if (ActualRight <= 90999)
            return ActualRight;

        var maxRight = 0d;

        foreach (var box in Boxes)
            maxRight = Math.Max(maxRight, box.ActualRight + box.ActualMarginRight);

        return maxRight + ActualPaddingRight + ActualMarginRight + ActualBorderRightWidth;
    }

    private double MarginBottomCollapse()
    {
        double margin = 0;

        // NOTE: When the last in-flow child's bottom margin collapses through
        // this box (computed below, once the last child is known) the collapsed
        // margin is NOT included in this box's height — it is external spacing
        // propagated to the parent via GetPropagatedMarginBottom().  The
        // `margin` variable stays 0.

        // CSS2.1 §10.6.3 / §10.6.7: Floated children contribute to the
        // height of their parent only when the parent establishes a new
        // block formatting context (BFC).  Non-BFC blocks (e.g. a plain
        // <ul> inside a floated <dd>) must not include descendant floats
        // in their height calculation.
        bool isBfc = Float != CssConstants.None
            || Display == CssConstants.InlineBlock
            || Display == CssConstants.TableCell
            || (Overflow != null && Overflow != CssConstants.Visible)
            || Position == CssConstants.Absolute
            || Position == CssConstants.Fixed
            || (AlignContent != null && AlignContent != "normal");

        // Use the maximum ActualBottom across all children to handle
        // floated children that may not be the last in source order.
        // Initialize to the content-area top so that padding is preserved
        // even when all children are floated (CSS2.1 §10.6.3: content
        // height is zero but padding is additive).
        double maxChildBottom = Location.Y + ActualBorderTopWidth + ActualPaddingTop;
        CssBox lastInFlowChild = null;
        
        foreach (var child in Boxes)
        {
            // CSS2.1 §10.6.3: Only children in the normal flow are taken
            // into account.  Absolutely positioned and fixed-position boxes
            // are out of flow and must not influence the parent's auto height.
            if (child.Position == CssConstants.Absolute || child.Position == CssConstants.Fixed)
                continue;

            if (!isBfc && child.Float != CssConstants.None)
                continue;

            // CSS2.1 §9.4.3: Relative positioning is visual-only and
            // does not affect the flow position used for auto-height
            // calculation.  Undo the relative offset so the parent
            // measures the child's normal-flow bottom.
            double childBottom = child.ActualBottom;
            if (child.Position == CssConstants.Relative)
                childBottom -= CssBoxHelper.GetRelativeOffsetY(child);
            maxChildBottom = Math.Max(maxChildBottom, childBottom);
            lastInFlowChild = child;
        }

        // CSS2.1 §10.6.7: When a BFC root auto-sizes its height it must
        // extend to contain all descendant floats — not only direct-child
        // floats.  Walk the subtree (stopping at nested BFC boundaries)
        // to find the maximum float bottom.
        if (isBfc)
        {
            double maxFloatDesc = maxChildBottom;
            FindMaxDescendantFloatBottom(this, ref maxFloatDesc);
            maxChildBottom = Math.Max(maxChildBottom, maxFloatDesc);
        }

        // CSS2.1 §8.3.1 / §10.6.3: The auto height extends to the bottom
        // margin-edge of the last in-flow child unless that child's bottom
        // margin collapses through this box.  Collapse-through happens when
        // this box has no bottom border or padding, an auto (or
        // auto-resolved) height, and a block-level last in-flow child.  This
        // must match the condition used by GetPropagatedMarginBottom() (which
        // propagates the same margin to the parent): otherwise the child's
        // margin is double-counted — once inside this box's height and once as
        // external spacing.  Note this does NOT depend on whether this box is
        // its own parent's last child, nor on this box's own bottom margin.
        bool autoHeight = Height == CssConstants.Auto || string.IsNullOrEmpty(Height)
            || (Height.Contains('%')
                && (ContainingBlock == null || ContainingBlock.Height == CssConstants.Auto
                    || string.IsNullOrEmpty(ContainingBlock.Height)));
        bool collapseThrough = lastInFlowChild != null
            && ActualPaddingBottom < 0.1 && ActualBorderBottomWidth < 0.1
            && autoHeight
            && lastInFlowChild.Float == CssConstants.None
            && lastInFlowChild.Display != CssConstants.Inline
            && lastInFlowChild.Display != CssConstants.InlineBlock;
        if (!collapseThrough && lastInFlowChild != null)
            maxChildBottom += lastInFlowChild.ActualMarginBottom;
        return Math.Max(ActualBottom, maxChildBottom + margin + ActualPaddingBottom + ActualBorderBottomWidth);
    }

    /// <summary>
    /// CSS Box Alignment Level 3 §"Aligning Abspos"
    /// (https://drafts.csswg.org/css-align-3/#align-abspos): resolves
    /// justify-self / align-self for an absolutely-positioned box along ONE
    /// axis. The box is aligned within its inset-modified containing block
    /// (IMCB), then — in the DEFAULT overflow mode (no safe/unsafe keyword) —
    /// its final position is clamped to the range that <b>encloses both the
    /// IMCB and the actual containing block (CB)</b>, with the start edge
    /// winning when the two conflict:
    /// <code>
    ///   p0 = imcbStart + alignmentOffset                 // unsafe align in IMCB
    ///   lo = min(imcbStart, cbStart)
    ///   hi = max(imcbStart + imcbSize, cbStart + cbSize) - boxSize
    ///   pos = startIsLow ? max(lo, min(p0, hi))          // start = low  edge
    ///                    : min(hi, max(p0, lo))          // start = high edge
    /// </code>
    /// Explicit <c>unsafe</c> → align per keyword, no clamping. Explicit
    /// <c>safe</c> → on overflow, fall back to start alignment within the IMCB
    /// (the legacy behaviour, preserved unchanged).
    ///
    /// <para>
    /// DIAGNOSTIC NOTE (WPT issue #1100, css-align/abspos cluster): if abspos
    /// elements carrying align-self/justify-self render in the wrong place, or
    /// the <c>css-align/abspos/{align,justify}-self-default-overflow-*</c> WPT
    /// family regresses, THIS clamp and the IMCB construction at the four call
    /// sites (the "Post-layout self-alignment for absolutely positioned
    /// elements" block above) are the first suspects. The historic bug was
    /// clamping to the IMCB alone instead of the IMCB∪CB union, which placed the
    /// box correctly only when it fit inside the IMCB. The eight pinned offsets
    /// of <c>justify-self-default-overflow-htb-ltr-htb.html</c> live in
    /// <c>Broiler.Layout.Tests.AbsposSelfAlignmentTests</c> — run those first.
    /// </para>
    /// </summary>
    /// <param name="alignment">The justify-self/align-self value (may carry a
    /// leading <c>safe</c>/<c>unsafe</c> keyword).</param>
    /// <param name="imcbStart">Start coordinate of the IMCB along this axis
    /// (absolute layout coords).</param>
    /// <param name="imcbSize">Size of the IMCB along this axis.</param>
    /// <param name="cbStart">Start coordinate of the actual containing-block
    /// padding box along this axis.</param>
    /// <param name="cbSize">Size of the containing-block padding box along this
    /// axis.</param>
    /// <param name="boxSize">Border-box size of the aligned box along this
    /// axis.</param>
    /// <param name="isRtl">Whether the box's inline direction is RTL — flips
    /// start/end keyword resolution. (Does not affect <c>center</c>.)</param>
    /// <param name="startIsLow">Whether the containing block's start edge is the
    /// LOW coordinate edge along this axis (true for LTR inline / top-down
    /// block; false e.g. for an RTL inline axis or a vertical-rl block axis).
    /// Drives the overflow tie-break.</param>
    /// <returns>Offset to add to <paramref name="imcbStart"/>; the caller
    /// computes <c>pos = imcbStart + returnValue + leading margin</c>.</returns>
    /// <summary>
    /// Drops a leading <c>safe</c>/<c>unsafe</c> overflow keyword from an
    /// alignment value so the static-position path can force its own
    /// (always-unsafe) overflow handling by re-prefixing <c>unsafe</c>.
    /// </summary>
    private static string StripSafeUnsafe(string value)
    {
        if (value.StartsWith("safe ", StringComparison.OrdinalIgnoreCase))
            return value.Substring(5).Trim();
        if (value.StartsWith("unsafe ", StringComparison.OrdinalIgnoreCase))
            return value.Substring(7).Trim();
        return value;
    }

    internal static double ResolveAbsposSelfAlignment(
        string alignment,
        double imcbStart, double imcbSize,
        double cbStart, double cbSize,
        double boxSize, bool isRtl, bool startIsLow,
        bool? selfStartIsHigh = null)
    {
        double freeSpace = imcbSize - boxSize;

        // `start`/`end` resolve against the alignment CONTAINER's start edge
        // (whether that is the high/far edge of the axis is `isRtl`).
        // `self-start`/`self-end` resolve against the BOX's OWN start edge, which
        // differs when the box's writing-mode/direction differ from the container's
        // (WPT css-align/abspos/align-self-* mixed-writing-mode tests). When the
        // caller does not supply the box-relative flip we fall back to the
        // container's (correct whenever the two share a writing mode).
        bool selfHigh = selfStartIsHigh ?? isRtl;

        // Strip an explicit safe/unsafe prefix. Track whether one was present:
        // the abspos DEFAULT mode is neither plain "safe" nor "unsafe" — it
        // aligns unsafely in the IMCB and then clamps to the IMCB∪CB union.
        string a = alignment;
        bool hasKeyword = false;
        bool isSafe = true;
        if (a.StartsWith("safe ", StringComparison.OrdinalIgnoreCase))
        {
            a = a.Substring(5).Trim();
            hasKeyword = true;
            isSafe = true;
        }
        else if (a.StartsWith("unsafe ", StringComparison.OrdinalIgnoreCase))
        {
            a = a.Substring(7).Trim();
            hasKeyword = true;
            isSafe = false;
        }

        double dx;
        switch (a)
        {
            case "center":
                dx = freeSpace / 2;
                break;
            case "end":
            case "flex-end":
            // No baseline-sharing group for a lone abspos box → last-baseline
            // aligns to the end edge (CSS Align §9, baseline self-alignment).
            case "last baseline":
            case "last-baseline":
                dx = isRtl ? 0 : freeSpace;
                break;
            case "self-end":
                dx = selfHigh ? 0 : freeSpace;
                break;
            case "right":
                dx = freeSpace;
                break;
            case "start":
            case "flex-start":
            // first-baseline → start edge (no baseline group).
            case "baseline":
            case "first baseline":
            case "first-baseline":
                dx = isRtl ? freeSpace : 0;
                break;
            case "self-start":
                dx = selfHigh ? freeSpace : 0;
                break;
            case "left":
                dx = 0;
                break;
            default:
                dx = 0; // fallback: start-aligned
                break;
        }

        // Explicit safe/unsafe: preserve the legacy single-box behaviour.
        if (hasKeyword)
        {
            // Safe overflow: clamp so the element does not overflow the
            // strong (start) edge of the IMCB in the writing direction.
            if (isSafe)
            {
                if (isRtl)
                    dx = Math.Min(dx, Math.Max(freeSpace, 0)); // strong edge = right
                else
                    dx = Math.Max(dx, 0);                       // strong edge = left
            }
            // Unsafe: honour the alignment with no clamping.
            return dx;
        }

        // DEFAULT overflow mode: clamp the resolved POSITION to the union of the
        // IMCB and the actual containing block, with the start edge winning when
        // the box is larger than the union (lo > hi). See the summary/diagnostic
        // note above; validated against the htb-ltr-htb WPT offsets.
        double pos = imcbStart + dx;
        double lo = Math.Min(imcbStart, cbStart);
        double hi = Math.Max(imcbStart + imcbSize, cbStart + cbSize) - boxSize;
        pos = startIsLow
            ? Math.Max(lo, Math.Min(pos, hi))
            : Math.Min(hi, Math.Max(pos, lo));
        return pos - imcbStart;
    }

    /// <summary>
    /// Computes the shrink-to-fit content width of this box: the maximum
    /// right edge of all child boxes (relative to this box) plus padding
    /// and border.  Used for abspos self-alignment where the box size is
    /// content-driven rather than stretched.
    /// </summary>
    private double GetShrinkToFitWidth()
    {
        // If there's an explicit CSS width, use it (plus border/padding).
        if (Width != CssConstants.Auto && !string.IsNullOrEmpty(Width))
            return Size.Width;

        double maxRight = 0;
        foreach (var child in Boxes)
        {
            if (child.Display == CssConstants.None) continue;
            double childRight = (child.Location.X - Location.X)
                                + child.Size.Width
                                + child.ActualMarginRight;
            maxRight = Math.Max(maxRight, childRight);
        }

        if (maxRight <= 0) return Size.Width;

        return maxRight + ActualPaddingRight + ActualBorderRightWidth;
    }

    /// <summary>
    /// Computes the shrink-to-fit content height of this box: the maximum
    /// bottom edge of all child boxes (relative to this box) plus padding
    /// and border.  Used for abspos self-alignment where the box size is
    /// content-driven rather than stretched.
    /// </summary>
    private double GetShrinkToFitHeight()
    {
        // If there's an explicit CSS height, use it (plus border/padding).
        if (Height != CssConstants.Auto && !string.IsNullOrEmpty(Height))
        {
            double h = ActualBottom - Location.Y;
            return h > 0 ? h : Size.Height;
        }

        double maxBottom = 0;
        foreach (var child in Boxes)
        {
            if (child.Display == CssConstants.None) continue;
            double childBottom = (child.Location.Y - Location.Y)
                                 + (child.ActualBottom - child.Location.Y)
                                 + child.ActualMarginBottom;
            maxBottom = Math.Max(maxBottom, childBottom);
        }

        if (maxBottom <= 0)
        {
            double h = ActualBottom - Location.Y;
            return h > 0 ? h : Size.Height;
        }

        return maxBottom + ActualPaddingBottom + ActualBorderBottomWidth;
    }

    /// <summary>
    /// Recursively finds the maximum bottom edge of any float in the
    /// subtree, stopping at nested BFC boundaries.  Used by the BFC
    /// root height calculation so that grandchild (and deeper) floats
    /// are properly contained.
    /// </summary>
    private static void FindMaxDescendantFloatBottom(CssBox box, ref double maxBottom)
    {
        foreach (var child in box.Boxes)
        {
            if (child.Float != CssConstants.None && child.Display != CssConstants.None)
            {
                maxBottom = Math.Max(maxBottom, child.ActualBottom + child.ActualMarginBottom);
            }

            // Don't recurse into nested BFC roots — their floats are
            // contained by them, not by the outer BFC.
            bool childIsBfc = child.Float != CssConstants.None
                || child.Display == CssConstants.InlineBlock
                || child.Display == CssConstants.TableCell
                || child.Display is "flex" or "inline-flex" or "grid" or "inline-grid"
                || child.Position == CssConstants.Absolute
                || child.Position == CssConstants.Fixed
                || (child.Overflow != null && child.Overflow != CssConstants.Visible)
                || (child.AlignContent != null && child.AlignContent != "normal");
            if (!childIsBfc)
                FindMaxDescendantFloatBottom(child, ref maxBottom);
        }
    }

    /// <summary>
    /// CSS Grid Level 1 §8.5: When all grid items share the same
    /// grid-row and grid-column (e.g. grid-row: 1; grid-column: 1),
    /// they overlap in the same grid cell.  Reposition them to the
    /// container's content-area top-left so they stack visually with
    /// later items painted on top.
    /// </summary>
    /// <returns><c>true</c> if stacking was applied; <c>false</c>
    /// if items are not all in the same cell.</returns>
    private bool ApplyGridStacking()
    {
        bool allSameCell = true;
        string firstRow = null, firstCol = null;
        foreach (var child in Boxes)
        {
            if (child.Position == CssConstants.Absolute || child.Position == CssConstants.Fixed)
                continue;
            if (child.Display == CssConstants.None)
                continue;
            var cr = child.GridRow;
            var cc = child.GridColumn;
            // Items without explicit grid placement use auto.
            if (string.IsNullOrEmpty(cr) || cr == "auto"
                || string.IsNullOrEmpty(cc) || cc == "auto")
            { allSameCell = false; break; }
            if (firstRow == null)
            { firstRow = cr; firstCol = cc; }
            else if (cr != firstRow || cc != firstCol)
            { allSameCell = false; break; }
        }

        if (!allSameCell || firstRow == null)
            return false;

        double cellLeft = Location.X + ActualPaddingLeft + ActualBorderLeftWidth;
        double cellTop = Location.Y + ActualPaddingTop + ActualBorderTopWidth;
        double columnWidth = Size.Width - ActualPaddingLeft - ActualPaddingRight
            - ActualBorderLeftWidth - ActualBorderRightWidth;
        double maxBottom = cellTop;
        foreach (var child in Boxes)
        {
            if (child.Position == CssConstants.Absolute || child.Position == CssConstants.Fixed)
                continue;
            if (child.Display == CssConstants.None)
                continue;
            // CSS Grid Level 1 §6.1: an auto-width grid item with the default
            // (stretch) justify-self fills its column — the same rule the
            // multi-row auto-placement path applies. Same-cell items are still
            // stretched to the container's content width so they paint as the
            // full-width blue bars the check-layout grid reftests expect.
            StretchGridItemToColumnWidth(child, columnWidth);
            double dx = cellLeft + child.ActualMarginLeft - child.Location.X;
            double dy = cellTop + child.ActualMarginTop - child.Location.Y;
            if (Math.Abs(dx) > 0.1)
                child.OffsetLeft(dx);
            if (Math.Abs(dy) > 0.1)
                child.OffsetTop(dy);
            double childBottom = child.ActualBottom + child.ActualMarginBottom;
            if (childBottom > maxBottom)
                maxBottom = childBottom;
        }

        // If the grid declares explicit rows, size those tracks and place the
        // items in them (heights, block-axis alignment, container height). This
        // runs at the container's final width — unlike the definite-track pass,
        // which the shrink-to-fit measurement invokes at content width — so a
        // stretched-column grid sizes its percentage/fixed rows correctly here.
        // Declines (keeping the plain content-height stacking below) for a
        // none/auto or out-of-scope rows template.
        if (TryApplyStackingRowTracks(cellLeft, cellTop, columnWidth))
            return true;

        ActualBottom = maxBottom + ActualPaddingBottom + ActualBorderBottomWidth;
        return true;
    }

    /// <summary>
    /// Sizes an explicit <c>grid-template-rows</c> for the same-cell stacking
    /// path and re-places every in-flow item within its resolved row track (via
    /// <see cref="PlaceItemInArea"/>, so heights, block-axis alignment and the
    /// stale-inline-rect reset all match the definite-track pass). All items share
    /// one grid line here (the stacking precondition), so a single row band
    /// serves them. Sets the container's block size from the sized tracks and
    /// returns <c>true</c>; returns <c>false</c> to leave plain content-height
    /// stacking in place when the rows template is none/auto or out of scope.
    /// </summary>
    private bool TryApplyStackingRowTracks(double cellLeft, double cellTop, double columnWidth)
    {
        double em = GetEmHeight();
        List<GridTrackSpec> rowSpecs = ParseTrackList(GridTemplateRows, em);
        if (rowSpecs == null || rowSpecs.Count == 0 || rowSpecs.Count > MaxGridLine)
            return false;

        var (rowStartN, rowSpan) = ParseGridLine(GridRowOfFirstInFlowItem(), rowSpecs.Count);
        int rowStart = rowStartN ?? 0;
        if (rowStart < 0 || rowSpan < 1 || rowStart + rowSpan > MaxGridLine)
            return false;

        // Tallest same-cell item's margin-box height feeds the occupied track's
        // content contribution.
        double contentH = 0;
        foreach (var child in Boxes)
        {
            if (child.Position is CssConstants.Absolute or CssConstants.Fixed
                || child.Display == CssConstants.None)
                continue;
            double h = (child.ActualBottom - child.Location.Y)
                + child.ActualMarginTop + child.ActualMarginBottom;
            if (h > contentH) contentH = h;
        }

        int rowCount = Math.Max(rowSpecs.Count, rowStart + rowSpan);
        GridTrackSpec implicitRow = ParseSingleImplicitSpec(GridAutoRows, em);
        bool rowDefinite = TryGetDefiniteContentHeight(em, out double definiteHeight);
        double rowBasis = rowDefinite ? definiteHeight : 0;
        double rowGap = ResolveGridGap(RowGap, rowBasis, em);

        var rowItems = new List<AxisItem> { new AxisItem(rowStart, rowSpan, contentH, contentH) };
        double[] rowSizes = ResolveTrackSizes(rowSpecs, implicitRow, rowCount,
            rowBasis, rowDefinite, rowGap, rowBasis, em, rowItems);
        if (rowSizes == null)
            return false;

        // CSS Grid §7.2.1: percentage rows against an indefinite block size are
        // sized as 'auto' for the intrinsic height above, then resolved against
        // that intrinsic height for layout while the container keeps it.
        double intrinsic = SumTrackSizes(rowSizes, rowGap);
        if (!rowDefinite)
            ResolvePercentRowTracksAgainstIntrinsic(rowSpecs, implicitRow, rowSizes, intrinsic);

        double[] rowStartEdge = BuildTrackEdges(rowSizes, rowGap, out double[] rowEndEdge);
        double areaTop = cellTop + rowStartEdge[rowStart];
        double areaBottom = cellTop + rowEndEdge[rowStart + rowSpan - 1];
        double areaHeight = areaBottom - areaTop;

        foreach (var child in Boxes)
        {
            if (child.Position is CssConstants.Absolute or CssConstants.Fixed
                || child.Display == CssConstants.None)
                continue;
            PlaceItemInArea(child, cellLeft, areaTop, columnWidth, areaHeight);
        }

        // A definite-height grid keeps its specified content height (rows may
        // overflow it or leave trailing space); an indefinite one sizes to the
        // intrinsic (pass-1) row height.
        double gridContentHeight = rowDefinite ? definiteHeight : intrinsic;
        ActualBottom = Location.Y + ActualBorderTopWidth + ActualPaddingTop
            + gridContentHeight + ActualPaddingBottom + ActualBorderBottomWidth;
        return true;
    }

    /// <summary>The <c>grid-row</c> of the first in-flow grid item (all in-flow
    /// items share it on the stacking path, so any one is representative).</summary>
    private string GridRowOfFirstInFlowItem()
    {
        foreach (var child in Boxes)
        {
            if (child.Position is CssConstants.Absolute or CssConstants.Fixed
                || child.Display == CssConstants.None)
                continue;
            return child.GridRow;
        }
        return null;
    }

    /// <summary>
    /// CSS Grid Level 1 §6.1 / CSS Box Alignment §6.2: stretch a grid item to
    /// its column's content width when it has an <c>auto</c> used width under the
    /// default (<c>normal</c>/<c>stretch</c>) justify-self. Shared by the
    /// same-cell stacking path and the multi-row auto-placement path so both
    /// fill the column the same way. Returns whether the item's justify-self
    /// resolved to stretch (so the caller can skip a redundant justify offset).
    /// </summary>
    private bool StretchGridItemToColumnWidth(CssBox child, double columnWidth)
        => StretchGridItemToColumnWidth(child, columnWidth, out _);

    private bool StretchGridItemToColumnWidth(CssBox child, double columnWidth, out string resolvedJustify)
    {
        bool isAutoWidth = child.Width == CssConstants.Auto
            || string.IsNullOrEmpty(child.Width);
        string js = child.JustifySelf?.Trim().ToLowerInvariant();
        // CSS Box Alignment §6.2: 'justify-self: auto' resolves to the grid
        // container's 'justify-items' (the intermediate display: contents
        // ancestor, having no box, does not contribute).
        if (string.IsNullOrEmpty(js) || js == "auto")
        {
            string ji = JustifyItems?.Trim().ToLowerInvariant();
            js = string.IsNullOrEmpty(ji) || ji == "auto" || ji == "legacy"
                ? "normal"
                : ji;
        }
        bool isStretch = js == "normal" || js == "stretch";

        if (isStretch && isAutoWidth)
        {
            double targetWidth = columnWidth
                - child.ActualMarginLeft - child.ActualMarginRight;
            if (targetWidth > 0 && Math.Abs(child.Size.Width - targetWidth) > 0.5)
            {
                child.Size = new SizeF((float)targetWidth, child.Size.Height);
                child.ActualRight = child.Location.X + targetWidth;
                // The inline layout path (CreateLineBoxes) recorded per-line-box
                // rectangles the paint walker uses for the item's own
                // background/border; leaving them would paint the item at its
                // pre-stretch inline width. Grid items are blockified, so clear
                // the stale inline rects and let paint fall back to Location+Size.
                child.RectanglesReset();
            }
        }
        resolvedJustify = js;
        return isStretch;
    }

    /// <summary>
    /// Called from <see cref="CssLayoutEngine.FlowInlineBlock"/> after
    /// CreateLineBoxes to apply grid stacking or auto-placement for grid
    /// containers that are laid out via the inline-block path.
    /// </summary>
    internal void ApplyGridLayoutAfterInline()
    {
        // Prefer the real definite-track pass; it declines (returns false) unless
        // the container declares fixed explicit templates, in which case the
        // single-column approximation below runs unchanged.
        if (TryApplyGridTrackLayout())
            return;
        if (!ApplyGridStacking())
            ApplyGridAutoPlacement();
    }

    /// <summary>
    /// CSS Grid Level 1: Auto-placement for grid items that are not all
    /// in the same cell.  The inline layout path (CreateLineBoxes) places
    /// grid items as inline-blocks on a single line.  This method
    /// repositions them into proper grid rows (one item per row for a
    /// single-column grid) and applies justify-self within the column.
    /// </summary>
    private void ApplyGridAutoPlacement()
    {
        double cellLeft = Location.X + ActualPaddingLeft + ActualBorderLeftWidth;
        double cellTop = Location.Y + ActualPaddingTop + ActualBorderTopWidth;
        double columnWidth = Size.Width - ActualPaddingLeft - ActualPaddingRight
            - ActualBorderLeftWidth - ActualBorderRightWidth;
        if (columnWidth <= 0) return;

        double currentY = cellTop;
        foreach (var child in Boxes)
        {
            if (child.Position == CssConstants.Absolute || child.Position == CssConstants.Fixed)
                continue;
            if (child.Display == CssConstants.None)
                continue;

            // CSS Grid Level 1 §6.1: Grid items with auto/normal/stretch
            // justify-self and auto width should stretch to fill the column.
            bool isStretch = StretchGridItemToColumnWidth(child, columnWidth, out string js);

            // Move child to the start of the current row.
            double dx = cellLeft + child.ActualMarginLeft - child.Location.X;
            double dy = currentY + child.ActualMarginTop - child.Location.Y;
            if (Math.Abs(dx) > 0.1)
                child.OffsetLeft(dx);
            if (Math.Abs(dy) > 0.1)
                child.OffsetTop(dy);

            // CSS Grid Level 1 §6.1: Apply justify-self to position the
            // item within its grid cell (column width).
            double boxWidth = child.ActualRight - child.Location.X;
            double freeSpace = columnWidth - boxWidth;
            if (freeSpace > 0.5 && !isStretch)
            {
                bool isElementRtl = child.Direction == "rtl";
                bool isContainerRtl = Direction == "rtl";

                double justifyDx = 0;
                switch (js)
                {
                    case "center":
                        justifyDx = freeSpace / 2;
                        break;
                    case "end":
                    case "flex-end":
                        justifyDx = isContainerRtl ? 0 : freeSpace;
                        break;
                    case "self-end":
                        justifyDx = isElementRtl ? 0 : freeSpace;
                        break;
                    case "right":
                        justifyDx = freeSpace;
                        break;
                    case "start":
                    case "flex-start":
                        justifyDx = isContainerRtl ? freeSpace : 0;
                        break;
                    case "self-start":
                        justifyDx = isElementRtl ? freeSpace : 0;
                        break;
                    case "left":
                        justifyDx = 0;
                        break;
                }

                if (Math.Abs(justifyDx) > 0.5)
                    child.OffsetLeft(justifyDx);
            }

            currentY = child.ActualBottom + child.ActualMarginBottom;
        }
        ActualBottom = currentY + ActualPaddingBottom + ActualBorderBottomWidth;
    }

    /// <summary>
    /// CSS Box Alignment Level 3 §6.2 / CSS Flexbox §8.3: position flex/grid
    /// items along the block (cross) axis according to <c>align-items</c>
    /// (overridable per item by <c>align-self</c>).  Broiler approximates
    /// flex/grid with an inline formatting context (FlowInlineBlock), which
    /// leaves every item at the content-box block-start; this pass shifts
    /// items to center/end when the container has a definite block size with
    /// free space.  Only the common horizontal-writing-mode case is handled
    /// (grid and row/row-reverse flex); column flex (cross axis = inline)
    /// and overflowing content fall back to start alignment.
    /// </summary>
    private void ApplyFlexGridCrossAxisAlignment()
    {
        if (Display is not ("flex" or "inline-flex" or "grid" or "inline-grid"))
            return;

        // The real definite-track grid pass already placed items in their areas
        // (including block-axis alignment); skip the approximate re-alignment.
        if (_gridTrackLayoutApplied)
            return;

        // The cross axis must be the block (vertical) axis: true for grid and
        // for row-direction flex.  Column flex aligns items on the inline
        // axis, which this approximation does not handle.
        if (Display is "flex" or "inline-flex"
            && FlexDirection is "column" or "column-reverse")
            return;

        // A definite block size is required to have free space to distribute.
        // (Size.Height is content-derived at this point — the specified height
        // is only pre-resolved into Size for percentage values — so resolve
        // the definite block size directly from the 'height' declaration.)
        if (string.IsNullOrEmpty(Height) || Height == CssConstants.Auto
            || HeightPercentageResolvesToAuto())
            return;

        double cbHeight;
        if (Position == CssConstants.Fixed && LayoutEnvironment != null)
            cbHeight = LayoutEnvironment.ViewportSize.Height;
        else if (ContainingBlock?.ParentBox == null && LayoutEnvironment != null)
            cbHeight = LayoutEnvironment.ViewportSize.Height;
        else
            cbHeight = ContainingBlock?.Size.Height ?? 0;

        double contentTop = ClientTop;
        double contentHeight = ResolveSpecifiedHeightToBorderBox(
                CssValueParser.ParseLength(Height, cbHeight, GetEmHeight()))
            - ActualPaddingTop - ActualPaddingBottom
            - ActualBorderTopWidth - ActualBorderBottomWidth;
        if (contentHeight <= 0)
            return;

        string containerAlign = NormalizeBoxAlignment(AlignItems);

        foreach (CssBox item in Boxes)
        {
            if (item.Display == CssConstants.None)
                continue;
            // CSS2.1 §9.6.1 / §9.5: out-of-flow items are positioned separately.
            if (item.Position is CssConstants.Absolute or CssConstants.Fixed)
                continue;
            if (item.Float != CssConstants.None)
                continue;

            // 'align-self: auto' (and 'normal') resolve to the container's
            // 'align-items' (CSS Box Alignment §6.2).
            string self = NormalizeBoxAlignment(item.AlignSelf);
            string align = self is "" or "auto" or "normal" ? containerAlign : self;

            bool toCenter = align == "center";
            bool toEnd = align is "end" or "flex-end" or "self-end";
            if (!toCenter && !toEnd)
                continue; // start/flex-start/baseline/stretch → block-start

            double marginBoxHeight = (item.ActualBottom - item.Location.Y)
                + item.ActualMarginTop + item.ActualMarginBottom;
            double free = contentHeight - marginBoxHeight;
            if (free <= 0.5)
                continue; // safe alignment: no room → keep at start

            double marginBoxOffset = toCenter ? free / 2 : free;
            double targetTop = contentTop + marginBoxOffset + item.ActualMarginTop;
            double dy = targetTop - item.Location.Y;
            if (Math.Abs(dy) > 0.5)
            {
                item.OffsetTop(dy);
                item.ActualBottom += dy;
            }
        }
    }

    /// <summary>
    /// Normalises a CSS Box Alignment keyword: trims, strips a leading
    /// <c>safe</c>/<c>unsafe</c> overflow-alignment qualifier, and lower-cases.
    /// </summary>
    private static string NormalizeBoxAlignment(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        string v = value.Trim();
        if (v.StartsWith("safe ", StringComparison.OrdinalIgnoreCase))
            v = v.Substring(5).Trim();
        else if (v.StartsWith("unsafe ", StringComparison.OrdinalIgnoreCase))
            v = v.Substring(7).Trim();
        return v.ToLowerInvariant();
    }

    internal void OffsetTop(double amount)
    {
        List<CssLineBox> lines = [.. Rectangles.Keys];

        foreach (CssLineBox line in lines)
        {
            RectangleF r = Rectangles[line];
            Rectangles[line] = new RectangleF(r.X, (float)(r.Y + amount), r.Width, r.Height);
        }

        foreach (CssRect word in Words)
            word.Top += amount;

        foreach (CssBox b in Boxes)
        {
            // CSS2.1 §9.6.1: position:fixed elements are positioned relative
            // to the viewport and must not be shifted by ancestor offsets
            // (e.g. a parent's position:relative visual offset).
            if (b.Position != CssConstants.Fixed)
                b.OffsetTop(amount);
        }

        _listItemBox?.OffsetTop(amount);

        Location = new PointF(Location.X, (float)(Location.Y + amount));
    }

    internal void OffsetLeft(double amount)
    {
        List<CssLineBox> lines = [.. Rectangles.Keys];

        foreach (CssLineBox line in lines)
        {
            RectangleF r = Rectangles[line];
            Rectangles[line] = new RectangleF((float)(r.X + amount), r.Y, r.Width, r.Height);
        }

        foreach (CssRect word in Words)
            word.Left += amount;

        foreach (CssBox b in Boxes)
        {
            // CSS2.1 §9.6.1: position:fixed elements are positioned relative
            // to the viewport and must not be shifted by ancestor offsets.
            if (b.Position != CssConstants.Fixed)
                b.OffsetLeft(amount);
        }

        _listItemBox?.OffsetLeft(amount);

        Location = new PointF((float)(Location.X + amount), Location.Y);
    }

    internal void OffsetRectangle(CssLineBox lineBox, double gap)
    {
        if (Rectangles.TryGetValue(lineBox, out RectangleF r))
            Rectangles[lineBox] = new RectangleF(r.X, (float)(r.Y + gap), r.Width, r.Height);
    }

    internal void RectanglesReset() => Rectangles.Clear();

    private void OnImageLoadComplete(object? image, RectangleF rectangle, bool async)
    {
        if (image != null && async)
            LayoutEnvironment.RequestRefresh(false);
    }

    protected override ILayoutFont GetCachedFont(string fontFamily, double fsize, LayoutFontStyle st, string fontFeatures) => LayoutEnvironment.GetFont(fontFamily, fsize, st, fontFeatures);

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
