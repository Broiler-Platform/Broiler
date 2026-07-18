using Broiler.CSS;
using Broiler.Graphics;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;


namespace Broiler.Layout.Engine;

internal abstract partial class CssBoxProperties
{
    internal const string InvalidCustomPropertySentinel = "\u0000";

    #region CSS Fields

    private string _borderTopWidth = "medium";
    private string _borderRightWidth = "medium";
    private string _borderBottomWidth = "medium";
    private string _borderLeftWidth = "medium";
    private string _borderTopColor = "black";
    private string _borderRightColor = "black";
    private string _borderBottomColor = "black";
    private string _borderLeftColor = "black";
    private string _outlineColor = string.Empty;
    private string _outlineWidth = "medium";
    private string _outlineStyle = "none";
    private string _outlineOffset = "0";
    private string _bottom = "auto";
    private string _color = "black";
    private string _cornerRadius = "0";
    private string _fontSize = "medium";
    private string _left = "auto";
    private string _lineHeight = "normal";
    private string _paddingLeft = "0";
    private string _paddingBottom = "0";
    private string _paddingRight = "0";
    private string _paddingTop = "0";
    private string _right = "auto";
    private string _width = "auto";
    private string _height = "auto";
    private string _maxWidth = "none";
    private string _minWidth = "0";
    private string _inlineSize = "auto";
    private string _blockSize = "auto";
    private string _writingMode = "horizontal-tb";
    private string _backgroundColor = "transparent";
    private string _backgroundImage = "none";
    private string _backgroundClip = "border-box";
    private string _clipPath = "none";
    private string _textIndent = "0";
    private string _textDecorationColor = "currentcolor";
    private string _top = "auto";
    private string _wordSpacing = "normal";

    #endregion


    #region Fields

    private PointF _location;
    private SizeF _size;

    private double _actualCornerNw = double.NaN;
    private double _actualCornerNe = double.NaN;
    private double _actualCornerSw = double.NaN;
    private double _actualCornerSe = double.NaN;
    private BColor _actualColor = BColor.Empty;
    private double _actualBackgroundGradientAngle = double.NaN;
    private double _actualHeight = double.NaN;
    private double _actualWidth = double.NaN;
    private double _actualPaddingTop = double.NaN;
    private double _actualPaddingBottom = double.NaN;
    private double _actualPaddingRight = double.NaN;
    private double _actualPaddingLeft = double.NaN;
    private double _actualMarginTop = double.NaN;
    private double _collapsedMarginTop = double.NaN;
    private double _actualMarginBottom = double.NaN;
    private double _actualMarginRight = double.NaN;
    private double _actualMarginLeft = double.NaN;

    // CSS2.1 §8.3: a used margin of `auto` resolves to 0, and the ActualMargin* getters below
    // rewrite the specified string `auto → "0"` (a caching side-effect). That loses the fact that
    // the margin was *specified* auto — which the §10.3.7/§10.6.4 abspos/fixed auto-margin centring
    // needs. Latch it here (set when a getter first sees `auto`, before the rewrite) so the
    // centring can ask <see cref="IsSpecifiedMarginLeftAuto"/> after the string has been zeroed.
    private bool _marginLeftWasAuto;
    private bool _marginRightWasAuto;
    private bool _marginTopWasAuto;
    private bool _marginBottomWasAuto;

    /// <summary>Whether <c>margin-left</c> was specified <c>auto</c> (survives the getter's used-value rewrite).</summary>
    internal bool IsSpecifiedMarginLeftAuto => _marginLeftWasAuto || MarginLeft == CssConstants.Auto;
    internal bool IsSpecifiedMarginRightAuto => _marginRightWasAuto || MarginRight == CssConstants.Auto;
    internal bool IsSpecifiedMarginTopAuto => _marginTopWasAuto || MarginTop == CssConstants.Auto;
    internal bool IsSpecifiedMarginBottomAuto => _marginBottomWasAuto || MarginBottom == CssConstants.Auto;

    /// <summary>Drops the cached used margins so a subsequent <c>ActualMargin*</c> read reflects a
    /// margin string just rewritten (e.g. auto-margin centring resolving an <c>auto</c> to a px value).</summary>
    internal void InvalidateActualMargins()
    {
        _actualMarginLeft = double.NaN;
        _actualMarginRight = double.NaN;
        _actualMarginTop = double.NaN;
        _actualMarginBottom = double.NaN;
    }
    private double _actualBorderTopWidth = double.NaN;
    private double _actualBorderLeftWidth = double.NaN;
    private double _actualBorderBottomWidth = double.NaN;
    private double _actualBorderRightWidth = double.NaN;
    private double _actualOutlineWidth = double.NaN;
    private double _actualOutlineOffset = double.NaN;
    private BColor _actualOutlineColor = BColor.Empty;
    private double _actualMaxWidth = double.NaN;
    private double _actualMinWidth = double.NaN;

    /// <summary>
    /// the width of whitespace between words
    /// </summary>
    private double _actualLineHeight = double.NaN;
    private double _actualTextIndent = double.NaN;
    private double _actualBorderSpacingHorizontal = double.NaN;
    private double _actualBorderSpacingVertical = double.NaN;
    private BColor _actualBackgroundGradient = BColor.Empty;
    private BColor _actualBorderTopColor = BColor.Empty;
    private BColor _actualBorderLeftColor = BColor.Empty;
    private BColor _actualBorderBottomColor = BColor.Empty;
    private BColor _actualBorderRightColor = BColor.Empty;
    private BColor _actualTextDecorationColor = BColor.Empty;
    private BColor _actualBackgroundColor = BColor.Empty;
    private ILayoutFont _actualFont;

    #endregion


    #region CSS Properties

    public string BorderBottomWidth
    {
        get { return _borderBottomWidth; }
        set
        {
            _borderBottomWidth = value;
            _actualBorderBottomWidth = float.NaN;
        }
    }

    public string BorderLeftWidth
    {
        get { return _borderLeftWidth; }
        set
        {
            _borderLeftWidth = value;
            _actualBorderLeftWidth = float.NaN;
        }
    }

    public string BorderRightWidth
    {
        get { return _borderRightWidth; }
        set
        {
            _borderRightWidth = value;
            _actualBorderRightWidth = float.NaN;
        }
    }

    public string BorderTopWidth
    {
        get { return _borderTopWidth; }
        set
        {
            _borderTopWidth = value;
            _actualBorderTopWidth = float.NaN;
        }
    }

    private string _borderBottomStyle = "none";
    private string _borderLeftStyle = "none";
    private string _borderRightStyle = "none";
    private string _borderTopStyle = "none";

    /// <summary>CSS2.1 §8.5.3: Changing border-style affects the used border-width
    /// (style "none"/"hidden" forces width to zero), so invalidate the cached
    /// actual width whenever the style changes.</summary>
    public string BorderBottomStyle
    {
        get => _borderBottomStyle;
        set { _borderBottomStyle = value; _actualBorderBottomWidth = double.NaN; }
    }

    public string BorderLeftStyle
    {
        get => _borderLeftStyle;
        set { _borderLeftStyle = value; _actualBorderLeftWidth = double.NaN; }
    }

    public string BorderRightStyle
    {
        get => _borderRightStyle;
        set { _borderRightStyle = value; _actualBorderRightWidth = double.NaN; }
    }

    public string BorderTopStyle
    {
        get => _borderTopStyle;
        set { _borderTopStyle = value; _actualBorderTopWidth = double.NaN; }
    }

    public string BorderBottomColor
    {
        get { return ResolveCssVariables(_borderBottomColor); }
        set
        {
            _borderBottomColor = value;
            _actualBorderBottomColor = BColor.Empty;
        }
    }

    public string BorderLeftColor
    {
        get { return ResolveCssVariables(_borderLeftColor); }
        set
        {
            _borderLeftColor = value;
            _actualBorderLeftColor = BColor.Empty;
        }
    }

    public string BorderRightColor
    {
        get { return ResolveCssVariables(_borderRightColor); }
        set
        {
            _borderRightColor = value;
            _actualBorderRightColor = BColor.Empty;
        }
    }

    public string BorderTopColor
    {
        get { return ResolveCssVariables(_borderTopColor); }
        set
        {
            _borderTopColor = value;
            _actualBorderTopColor = BColor.Empty;
        }
    }

    public string BorderSpacing { get; set; } = "0";
    public string BorderCollapse { get; set; } = "separate";

    // CSS UI §2: outline is painted just outside the border edge and does not
    // affect layout. Stored uniformly (outline cannot be set per-side).
    // Backing fields invalidate the lazily-resolved used values on set, matching
    // the border-width/-colour caching pattern so repeated paint-time reads of
    // ActualOutline* don't re-parse the length/colour strings.
    public string OutlineWidth
    {
        get => _outlineWidth;
        set { _outlineWidth = value; _actualOutlineWidth = double.NaN; }
    }
    public string OutlineStyle
    {
        // CSS UI §2: outline-style none/hidden forces the used width to zero, so
        // invalidate the cached used width when the style changes.
        get => _outlineStyle;
        set { _outlineStyle = value; _actualOutlineWidth = double.NaN; }
    }
    public string OutlineColor
    {
        get => ResolveCssVariables(_outlineColor);
        set { _outlineColor = value ?? string.Empty; _actualOutlineColor = BColor.Empty; }
    }
    public string OutlineOffset
    {
        get => _outlineOffset;
        set { _outlineOffset = value; _actualOutlineOffset = double.NaN; }
    }

    /// <summary>Used outline width in px; 0 when the outline style is none/hidden.</summary>
    public double ActualOutlineWidth
    {
        get
        {
            if (double.IsNaN(_actualOutlineWidth))
            {
                if (string.IsNullOrEmpty(OutlineStyle) || OutlineStyle == CssConstants.None
                    || OutlineStyle.Equals("hidden", StringComparison.OrdinalIgnoreCase))
                    _actualOutlineWidth = 0;
                else
                    _actualOutlineWidth = CssLengthParser.GetActualBorderWidth(OutlineWidth, GetEmHeight());
            }

            return _actualOutlineWidth;
        }
    }

    /// <summary>Used outline-offset in px (the gap between border edge and outline).</summary>
    public double ActualOutlineOffset
    {
        get
        {
            if (double.IsNaN(_actualOutlineOffset))
                _actualOutlineOffset = string.IsNullOrEmpty(OutlineOffset)
                    ? 0
                    : CssLengthParser.ParseLength(OutlineOffset, 0, GetEmHeight());

            return _actualOutlineOffset;
        }
    }

    /// <summary>
    /// Used outline colour. The initial value (<c>auto</c>/<c>invert</c>) and an
    /// unset colour resolve to <c>currentColor</c> (the element's text colour),
    /// matching modern browsers.
    /// </summary>
    public BColor ActualOutlineColor
    {
        get
        {
            string c = OutlineColor;
            if (string.IsNullOrEmpty(c)
                || c.Equals("auto", StringComparison.OrdinalIgnoreCase)
                || c.Equals("invert", StringComparison.OrdinalIgnoreCase)
                || c.Equals("currentcolor", StringComparison.OrdinalIgnoreCase))
                return ActualColor; // cached element colour

            if (_actualOutlineColor.IsEmpty)
                _actualOutlineColor = GetActualColor(c);

            return _actualOutlineColor;
        }
    }

    public string CornerRadius
    {
        get { return _cornerRadius; }
        set
        {
            string raw = value ?? string.Empty;

            int slashIndex = raw.IndexOf('/');
            if (slashIndex >= 0)
                raw = raw[..slashIndex];

            string[] r = raw.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

            switch (r.Length)
            {
                case 1:
                    CornerNeRadius = r[0];
                    CornerNwRadius = r[0];
                    CornerSeRadius = r[0];
                    CornerSwRadius = r[0];
                    break;

                case 2:
                    CornerNeRadius = r[0];
                    CornerNwRadius = r[0];
                    CornerSeRadius = r[1];
                    CornerSwRadius = r[1];
                    break;

                case 3:
                    CornerNeRadius = r[0];
                    CornerNwRadius = r[1];
                    CornerSeRadius = r[2];
                    break;

                case 4:
                    CornerNeRadius = r[0];
                    CornerNwRadius = r[1];
                    CornerSeRadius = r[2];
                    CornerSwRadius = r[3];
                    break;
            }

            _cornerRadius = value;
        }
    }

    public string CornerNwRadius { get; set; } = "0";
    public string CornerNeRadius { get; set; } = "0";
    public string CornerSeRadius { get; set; } = "0";
    public string CornerSwRadius { get; set; } = "0";
    public string MarginBottom { get; set; } = "0";
    public string MarginLeft { get; set; } = "0";
    public string MarginRight { get; set; } = "0";
    public string MarginTop { get; set; } = "0";

    /// <summary>
    /// CSS Box Model 4 §6.2 <c>margin-trim</c>: controls trimming of a box's
    /// own margins adjacent to its content edges (e.g. the block-start margin
    /// of the first child and the block-end margin of the last child).
    /// Not inherited.  Default <c>none</c>.
    /// </summary>
    public string MarginTrim { get; set; } = "none";

    public string PaddingBottom
    {
        get { return _paddingBottom; }
        set
        {
            _paddingBottom = value;
            _actualPaddingBottom = double.NaN;
        }
    }

    public string PaddingLeft
    {
        get { return _paddingLeft; }
        set
        {
            _paddingLeft = value;
            _actualPaddingLeft = double.NaN;
        }
    }

    public string PaddingRight
    {
        get { return _paddingRight; }
        set
        {
            _paddingRight = value;
            _actualPaddingRight = double.NaN;
        }
    }

    public string PaddingTop
    {
        get { return _paddingTop; }
        set
        {
            _paddingTop = value;
            _actualPaddingTop = double.NaN;
        }
    }

    public string PageBreakInside { get; set; } = CssConstants.Auto;

    public string Left
    {
        get { return _left; }
        set
        {
            _left = value;

            if (Position == CssConstants.Fixed)
                _location = GetActualLocation(Left, Top);
        }
    }

    public string Top
    {
        get { return _top; }
        set
        {
            _top = value;

            if (Position == CssConstants.Fixed)
                _location = GetActualLocation(Left, Top);
        }
    }

    public string Right
    {
        get { return _right; }
        set { _right = value; }
    }

    public string Bottom
    {
        get { return _bottom; }
        set { _bottom = value; }
    }

    public string Width
    {
        get => ResolvePhysicalSize(_width, isWidth: true);
        set
        {
            _width = value;
            _actualWidth = double.NaN;
        }
    }
    public string MaxWidth
    {
        get => _maxWidth;
        set { _maxWidth = value; _actualMaxWidth = double.NaN; }
    }
    public string MinWidth
    {
        get => _minWidth;
        set { _minWidth = value; _actualMinWidth = double.NaN; }
    }
    internal bool IsMinWidthSpecified { get; set; }

    /// <summary>
    /// Resolves <see cref="MaxWidth"/> against a percentage basis, caching the
    /// result when it is basis-independent (an absolute length with no
    /// percentage term) so the repeated max-width clamps in width resolution do
    /// not re-tokenize a fixed cap such as <c>"300px"</c>. Percentage caps still
    /// resolve against the supplied basis every call.
    /// </summary>
    protected double ResolveMaxWidthLength(double basis)
        => ResolveCachedConstraintLength(MaxWidth, basis, ref _actualMaxWidth);

    /// <summary>Cached <see cref="MinWidth"/> resolution; see <see cref="ResolveMaxWidthLength"/>.</summary>
    protected double ResolveMinWidthLength(double basis)
        => ResolveCachedConstraintLength(MinWidth, basis, ref _actualMinWidth);

    private double ResolveCachedConstraintLength(string value, double basis, ref double cache)
    {
        if (!double.IsNaN(cache))
            return cache;

        double resolved = ParseLengthWithLineHeight(value, basis);

        // Only an absolute length (no percentage term) is independent of the
        // basis; cache it. Percentage / expression-with-% values differ per
        // basis and are re-resolved on every call.
        if (value != null && value.IndexOf('%') < 0)
            cache = resolved;

        return resolved;
    }
    /// <summary>
    /// True when this absolutely/fixed-positioned box has already had its
    /// <see cref="CssBoxProperties.Location"/> advanced to its final CSS
    /// <c>left</c>/<c>top</c> offset by the positioning pass, so its inline
    /// content flows at that final origin. <c>AdjustAbsolutePosition</c> must then
    /// NOT re-add the offset (it would double it). Boxes whose Location stays at
    /// the static position (e.g. native form controls) keep this false and still
    /// rely on <c>AdjustAbsolutePosition</c>.
    /// </summary>
    internal bool AbsposLocationFinalized { get; set; }

    /// <summary>
    /// The static position (CSS2.1 §10.3.7 / §10.6.4) an out-of-flow box would
    /// occupy in its inline formatting context — the inline cursor where FlowBox
    /// encountered it. Recorded by the inline layout so that when the box's block
    /// layout resolves its used position, an axis with <c>auto</c> insets falls
    /// back to this inline static position instead of the block-flow static
    /// (top of the containing block), which does not model inline placement.
    /// Null when the box was not flowed through an inline formatting context.
    /// </summary>
    internal PointF? InlineStaticPosition { get; set; }

    /// <summary>
    /// When this box is an absolutely-positioned grid item, the grid container's
    /// track-sizing pass records the item's resolved grid area here in absolute
    /// coordinates (X/Y = area origin, Width/Height = area size). CSS Grid §9
    /// makes that area — not the grid container's padding box — the box's
    /// containing block, so <see cref="CssBox.GetAbsoluteContainingBlockPaddingBox"/>
    /// returns it when set. Null for every other box.
    /// </summary>
    internal RectangleF? GridAreaContainingBlock { get; set; }

    /// <summary>
    /// CSS Grid Level 2 §7.3 (subgrid): when this box is a grid item whose
    /// <c>grid-template-columns</c> is <c>subgrid</c>, the parent grid's
    /// track-sizing pass records here the sizes (px) of the parent tracks this
    /// item's grid area spans, so the subgrid lays its own items out into the
    /// inherited tracks instead of parsing a template. Null when not a
    /// column-subgrid or not yet resolved by the parent. <see cref="SubgridRowSizes"/>
    /// is the analogous row-axis inheritance; the gaps carry the parent's gutters,
    /// which a subgridded axis adopts (CSS Grid L2 §7.3).
    /// </summary>
    internal double[] SubgridColumnSizes { get; set; }
    internal double[] SubgridRowSizes { get; set; }
    internal double? SubgridColumnGap { get; set; }
    internal double? SubgridRowGap { get; set; }

    public string Height
    {
        get => ResolvePhysicalSize(_height, isWidth: false);
        set
        {
            _height = value;
            _actualHeight = double.NaN;
        }
    }

    public string MaxHeight { get; set; } = "none";
    public string MinHeight { get; set; } = "0";

    /// <summary>CSS Sizing 4 <c>aspect-ratio</c> (raw value, e.g. <c>1 / 1</c>).
    /// Honoured for in-flow block-level boxes with an auto block size, which
    /// derive their used height from their used width and this ratio
    /// (<see cref="CssBox.TryResolveAspectRatioBlockHeight"/>).</summary>
    public string AspectRatio { get; set; } = "auto";

    public string InlineSize
    {
        get => _inlineSize;
        set
        {
            _inlineSize = value;
            _actualWidth = double.NaN;
            _actualHeight = double.NaN;
        }
    }

    public string BlockSize
    {
        get => _blockSize;
        set
        {
            _blockSize = value;
            _actualWidth = double.NaN;
            _actualHeight = double.NaN;
        }
    }

    public string BackgroundColor
    {
        get => ResolveCssVariables(_backgroundColor);
        set
        {
            _backgroundColor = value;
            _actualBackgroundColor = BColor.Empty;
        }
    }
    public string BackgroundImage
    {
        get => ResolveCssVariables(_backgroundImage);
        set => _backgroundImage = value;
    }

    public string BackgroundPosition { get; set; } = "0% 0%";
    public string BackgroundRepeat { get; set; } = "repeat";
    public string BackgroundAttachment { get; set; } = "scroll";
    public string BackgroundOrigin { get; set; } = "padding-box";
    public string BackgroundSize { get; set; } = "auto";
    public string BackgroundGradient { get; set; } = "none";
    public string BackgroundGradientAngle { get; set; } = "90";

    // CSS Animations §3: Animation properties for static keyframe resolution.
    public string AnimationName { get; set; } = "none";
    public string AnimationDuration { get; set; } = "0s";
    public string AnimationTimingFunction { get; set; } = "ease";
    public string AnimationDelay { get; set; } = "0s";
    public string AnimationIterationCount { get; set; } = "1";
    public string AnimationDirection { get; set; } = "normal";
    public string AnimationFillMode { get; set; } = "none";
    public string AnimationPlayState { get; set; } = "running";

    public string Color
    {
        get { return ResolveCssVariables(_color); }
        set
        {
            _color = value;
            _actualColor = BColor.Empty;
        }
    }

    public string Content { get; set; } = "normal";
    public string Display { get; set; } = "inline";
    public string Direction { get; set; } = "ltr";
    public string EmptyCells { get; set; } = "show";
    public string CaptionSide { get; set; } = "top";
    public string Float { get; set; } = "none";
    public string Clear { get; set; } = "none";
    public string Position { get; set; } = "static";

    // CSS Viewport `zoom`: the cascaded per-element zoom factor, surfaced on the box for the native zoom
    // model (HtmlBridge complexity-reduction roadmap Phase 5, the CSS-`zoom`/visual-viewport endgame).
    // Populated by CssUtils.SetPropertyValue from the declared cascade; consumed only when
    // NativeZoom.Enabled (via EffectiveZoom), so it is inert by default and the HtmlBridge serialization
    // bake continues to carry zoom as it does today. Initial value `normal` (== factor 1).
    public string Zoom { get; set; } = "normal";

    /// <summary>
    /// This box's specified <c>zoom</c> as a positive factor: a number (<c>zoom: 2</c>), a percentage
    /// (<c>zoom: 150%</c> → 1.5), or 1.0 for the initial/<c>normal</c>/<c>inherit</c>/unparseable value.
    /// Not itself inherited — each element has its own zoom; the multiplicative compounding across
    /// ancestors is expressed by <see cref="EffectiveZoom"/>.
    /// </summary>
    internal double OwnZoom
    {
        get
        {
            var z = Zoom?.Trim();
            if (string.IsNullOrEmpty(z)
                || z.Equals("normal", System.StringComparison.OrdinalIgnoreCase)
                || z.Equals("inherit", System.StringComparison.OrdinalIgnoreCase))
                return 1.0;
            if (z.EndsWith('%')
                && double.TryParse(z[..^1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var pct)
                && pct > 0)
                return pct / 100.0;
            if (double.TryParse(z, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var num) && num > 0)
                return num;
            return 1.0;
        }
    }

    /// <summary>
    /// The compounded (effective) zoom applied to this box's used values: the product of this box's own
    /// <see cref="OwnZoom"/> and every ancestor's, per CSS <c>zoom</c> (the factor compounds down the
    /// tree). Always <c>1.0</c> unless <see cref="NativeZoom.Enabled"/>, so the engine is zoom-neutral by
    /// default and this foundation is inert until the used-value increments consume it.
    /// </summary>
    public double EffectiveZoom =>
        !NativeZoom.Enabled ? 1.0 : (GetParent()?.EffectiveZoom ?? 1.0) * OwnZoom;

    // CSS Anchor Positioning: the cascaded values are surfaced on the box so the
    // layout engine's anchor-placement post-pass can read them (HtmlBridge
    // complexity-reduction roadmap Phase 5 item 3, P5.8b). They are populated by
    // CssUtils.SetPropertyValue from the declared cascade like any other longhand;
    // the engine does not yet consume them (that is gated behind the P5.8c post-pass).
    public string AnchorName { get; set; } = "none";
    public string PositionAnchor { get; set; } = "auto";
    public string PositionArea { get; set; } = "none";
    public string PositionTry { get; set; } = "normal";
    public string PositionTryFallbacks { get; set; } = "none";
    // Default "normal" is a sentinel for UNSET (the cascade only emits position-visibility
    // when authored, since it is not in CssComputedDefaults). It lets the visibility pass
    // distinguish an unset target — which, when it has position-area + position-anchor, takes
    // an implicit "anchors-visible" (the position-visibility-initial reftest) — from an
    // explicit "always" (position-visibility-remove-anchors-visible), which must never hide.
    public string PositionVisibility { get; set; } = "normal";

    /// <summary>
    /// Runtime flag set by the native anchor post-pass' <c>position-visibility</c> resolution
    /// (P5.8d.2b) to suppress an anchor-positioned box whose anchor is not visible (scrolled out
    /// of an intervening clip container, <c>visibility:hidden</c>, or — for <c>anchors-valid</c> —
    /// missing). Unlike <c>display:none</c> it is applied <em>after</em> layout, so the box keeps
    /// its geometry but <see cref="Broiler.Layout.IR.FragmentTreeBuilder"/> excludes it (and its
    /// subtree) from the paint fragment tree. Not a CSS property — never cascaded or copied.
    /// </summary>
    public bool PositionHidden { get; set; }

    public string LineHeight
    {
        get { return _lineHeight; }
        set
        {
            // CSS2.1 §10.8: Preserve "normal" and "inherit" keywords as-is
            if (string.IsNullOrEmpty(value) || value == "normal" || value == "inherit")
            {
                _lineHeight = value ?? "normal";
                return;
            }

            // Unitless numbers (line-height: <number>) should be treated as a
            // multiplier of the element's font-size. Store as "Nem" so
            // ActualLineHeight resolves with the correct em factor at layout time,
            // avoiding precision loss from premature conversion at parse time
            // (CSS2.1 §10.8.1).
            if (!value.EndsWith("px", StringComparison.OrdinalIgnoreCase) &&
                !value.EndsWith("pt", StringComparison.OrdinalIgnoreCase) &&
                !value.EndsWith("em", StringComparison.OrdinalIgnoreCase) &&
                !value.EndsWith("ex", StringComparison.OrdinalIgnoreCase) &&
                !value.EndsWith("rem", StringComparison.OrdinalIgnoreCase) &&
                !value.EndsWith("cm", StringComparison.OrdinalIgnoreCase) &&
                !value.EndsWith("mm", StringComparison.OrdinalIgnoreCase) &&
                !value.EndsWith("in", StringComparison.OrdinalIgnoreCase) &&
                !value.EndsWith("pc", StringComparison.OrdinalIgnoreCase) &&
                !value.EndsWith('%') &&
                double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                _lineHeight = value + "em";
                return;
            }

            // CSS2.1 §10.8: For explicit length values (px, em, pt, etc.),
            // store the raw value and let ActualLineHeight resolve it at
            // layout time when the element's font-size is finalized.
            _lineHeight = value;
        }
    }

    public string VerticalAlign { get; set; } = "baseline";

    public string TextIndent
    {
        get { return _textIndent; }
        set { _textIndent = value; }
    }

    public string TextAlign { get; set; } = string.Empty;

    // CSS Text 4 §text-align-last: alignment of the block's last line (and lines
    // ending in a forced break).  Inherited; initial 'auto' (follows text-align,
    // except a justified block leaves its last line start-aligned).
    public string TextAlignLast { get; set; } = string.Empty;

    public string TextDecoration { get; set; } = string.Empty;
    public string TextDecorationStyle { get; set; } = "solid";
    public string TextDecorationColor
    {
        get => _textDecorationColor;
        set
        {
            _textDecorationColor = value;
            _actualTextDecorationColor = BColor.Empty;
        }
    }
    public string WhiteSpace { get; set; } = "normal";

    /// <summary>
    /// CSS Text 3 §2.1 <c>text-transform</c>. An inherited property applied to a
    /// box's text when its words are parsed (see <see cref="CssBox.ParseToWords"/>).
    /// The default <c>none</c> leaves text unchanged.
    /// </summary>
    public string TextTransform { get; set; } = "none";

    public string Visibility { get; set; } = "visible";

    public string WordSpacing
    {
        get { return _wordSpacing; }
        set { _wordSpacing = value; }
    }

    public string WordBreak { get; set; } = "normal";
    public string LineBreak { get; set; } = "auto";
    public string Opacity { get; set; } = "1";
    public string ZIndex { get; set; } = CssConstants.Auto;

    // CSS Position 4 §top-layer: non-null when this box is in the top layer (an open modal
    // <dialog>, an open popover, or a synthesized ::backdrop). Projected onto
    // Fragment.TopLayerOrder so the paint lifts it above ordinary stacking. Carries the order for
    // boxes the renderer *generates* (a native ::backdrop has no element to hold the
    // data-broiler-top-layer attribute FragmentTreeBuilder reads for stamped elements); null for
    // ordinary boxes, which stack normally.
    public int? TopLayerOrder { get; set; }

    public string BoxShadow { get; set; } = "none";
    public string TextShadow { get; set; } = "none";
    public string MixBlendMode { get; set; } = "normal";
    public string BackgroundBlendMode { get; set; } = "normal";
    public string Filter { get; set; } = "none";
    public string Isolation { get; set; } = "auto";
    public string BoxSizing { get; set; } = "content-box";

    public string BackgroundClip
    {
        get
        {
            if (_backgroundClip.Equals("inherit", StringComparison.OrdinalIgnoreCase) && GetParent() != null)
                return GetParent().BackgroundClip;

            return _backgroundClip;
        }
        set
        {
            _backgroundClip = value ?? "border-box";
        }
    }

    public string ClipPath
    {
        get => _clipPath;
        set => _clipPath = value ?? "none";
    }

    /// <summary>
    /// CSS Containment Module Level 2: the <c>contain</c> property.
    /// Values include <c>none</c>, <c>strict</c>, <c>content</c>,
    /// <c>size</c>, <c>layout</c>, <c>style</c>, <c>paint</c>,
    /// or a space-separated combination of the last four keywords.
    /// Used by background propagation (CSS Backgrounds §2.11.1):
    /// <c>contain: paint</c> on html or body suppresses canvas
    /// background propagation.
    /// </summary>
    public string Contain { get; set; } = "none";

    /// <summary>
    /// CSS Color Adjust Module Level 1: the <c>color-scheme</c> property.
    /// A space-separated list of the color schemes the element can render in
    /// (<c>normal</c>, <c>light</c>, <c>dark</c>, optionally prefixed by
    /// <c>only</c>). Used by canvas background painting (CSS Color Adjust
    /// §2.3): when the root's used color scheme is <c>dark</c>, the canvas is
    /// painted the UA dark backdrop colour rather than white.
    /// </summary>
    public string ColorScheme { get; set; } = "normal";

    /// <summary>
    /// CSS Containment Module Level 2: the <c>content-visibility</c> property.
    /// Values: <c>visible</c> (default), <c>hidden</c>, <c>auto</c>.
    /// <c>hidden</c> skips the element's contents (they are not painted and are
    /// subject to layout/size containment), while the element's own box —
    /// background, border, and box-model size — still renders.
    /// </summary>
    public string ContentVisibility { get; set; } = "visible";
    public string Transform { get; set; } = "none";

    /// <summary>
    /// CSS Will Change Module Level 1: the <c>will-change</c> property. A
    /// comma-separated hint list (<c>auto</c> by default). Consumed only by the
    /// native anchor-placement containing-block resolution: <c>will-change: transform</c>
    /// (and other values that would create one) establishes a containing block for
    /// absolutely-positioned descendants — see
    /// <see cref="CssBox.EstablishesNonPositionAbsPosContainingBlock"/>. No other layout
    /// or paint behaviour reads it today.
    /// </summary>
    public string WillChange { get; set; } = "auto";

    public string FlexDirection { get; set; } = "row";
    public string FlexGrow { get; set; } = "0";
    public string FlexShrink { get; set; } = "1";
    public string FlexBasis { get; set; } = "auto";
    public string FlexWrap { get; set; } = "nowrap";

    // CSS Box Alignment §8: the initial value of justify-content is 'normal',
    // not 'flex-start'. In a flex container 'normal' behaves as 'flex-start'
    // (packed at the main-start edge), so flex layout is unchanged; in a grid
    // container 'normal' triggers the default stretch of auto tracks, which
    // 'flex-start' (a packing distribution) suppresses.
    public string JustifyContent { get; set; } = "normal";
    public string JustifyItems { get; set; } = "normal";
    public string AlignItems { get; set; } = "stretch";
    public string AlignContent { get; set; } = "normal";
    public string JustifySelf { get; set; } = "auto";
    public string AlignSelf { get; set; } = "auto";
    public string UnicodeBidi { get; set; } = "normal";
    public string WritingMode
    {
        get => _writingMode;
        set
        {
            _writingMode = value;
            _actualWidth = double.NaN;
            _actualHeight = double.NaN;
        }
    }
    public string ColumnCount { get; set; } = "auto";
    public string ColumnWidth { get; set; } = "auto";
    public string ColumnFill { get; set; } = "balance";
    public string RowGap { get; set; } = "normal";
    public string ColumnGap { get; set; } = "normal";
    public string BreakInside { get; set; } = "auto";
    public string GridRow { get; set; } = "auto";
    public string GridColumn { get; set; } = "auto";
    // CSS Grid Level 1 §7/§8: explicit track lists and implicit-track/flow
    // controls consumed by the definite-track grid layout pass
    // (CssBoxGrid.TryApplyGridTrackLayout). "none"/empty means no explicit grid.
    public string GridTemplateColumns { get; set; } = "none";
    public string GridTemplateRows { get; set; } = "none";
    public string GridAutoFlow { get; set; } = "row";
    public string GridAutoRows { get; set; } = "auto";
    public string GridAutoColumns { get; set; } = "auto";
    public string FontFamily { get; set; }

    /// <summary>
    /// Raw CSS <c>font-feature-settings</c> value (e.g. <c>"ss05" on, "liga" off</c>),
    /// inherited.  Resolved to enabled OpenType feature tags by
    /// <see cref="GetEnabledFontFeatureTags"/>.
    /// </summary>
    public string FontFeatureSettings { get; set; }

    /// <summary>
    /// Raw CSS <c>font-variant-alternates</c> value (e.g.
    /// <c>styleset(crossed-doubleu)</c>), inherited.  Resolved against
    /// <c>@font-feature-values</c> into concrete feature tags after the cascade.
    /// </summary>
    public string FontVariantAlternates { get; set; }

    /// <summary>
    /// Parses <see cref="FontFeatureSettings"/> into a space-separated list of
    /// the OpenType feature tags that are switched on, or <c>null</c> when none.
    /// </summary>
    protected string GetEnabledFontFeatureTags()
    {
        string value = FontFeatureSettings;
        if (string.IsNullOrWhiteSpace(value) || value == "normal")
            return null;

        var tags = new System.Text.StringBuilder();
        foreach (var part in value.Split(','))
        {
            var item = part.Trim();
            if (item.Length == 0)
                continue;

            // "<tag>" [ <integer> | on | off ]; a 4-char quoted tag, optionally
            // followed by an on/off/value flag (default = on).
            bool firstQuote = item.Contains('"');
            bool altQuote = item.Contains('\'');
            char quote = firstQuote ? '"' : (altQuote ? '\'' : '\0');
            string tag;
            string flag;
            
            if (quote != '\0')
            {
                int start = item.IndexOf(quote);
                int endq = item.IndexOf(quote, start + 1);

                if (endq <= start)
                    continue;

                tag = item.Substring(start + 1, endq - start - 1).Trim();
                flag = item[(endq + 1)..].Trim();
            }
            else
            {
                var sp = item.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries);
                tag = sp[0];
                flag = sp.Length > 1 ? sp[1].Trim() : string.Empty;
            }

            if (tag.Length != 4)
                continue;

            bool enabled = flag.Length == 0
                || flag.Equals("on", StringComparison.OrdinalIgnoreCase)
                || flag == "1"
                || (int.TryParse(flag, out int v) && v != 0);

            if (enabled)
            {
                if (tags.Length > 0)
                    tags.Append(' ');

                tags.Append(tag);
            }
        }

        return tags.Length > 0 ? tags.ToString() : null;
    }

    public string FontSize
    {
        get { return _fontSize; }
        set
        {
            // CSS2.1 §6.2.1: 'inherit' resolves to the parent's computed value.
            if (value != null && value.Equals("inherit", StringComparison.OrdinalIgnoreCase) && GetParent() != null)
            {
                _fontSize = GetParent().FontSize;
                InvalidateFontDependentValues();
                return;
            }

            // CSS2.1 §15.7: a percentage font-size resolves against the PARENT's
            // computed font-size.  Resolve it to an absolute length immediately so
            // that descendants which inherit this computed value (InheritStyle copies
            // the string verbatim) do not re-apply the percentage and compound it —
            // e.g. body/div/span all set to 800% must each be 8× the root, not 8×8×8×.
            var trimmedValue = value?.Trim();
            if (trimmedValue != null
                && trimmedValue.EndsWith('%')
                && GetParent() != null
                && double.TryParse(trimmedValue[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                double resolvedPoints = CssLengthParser.ParseNumber(trimmedValue, GetParent().ActualFont.Size);
                _fontSize = resolvedPoints.ToString("0.0###", CultureInfo.InvariantCulture) + "pt";
                InvalidateFontDependentValues();
                
                if (this is CssBox percentBox)
                {
                    foreach (var child in percentBox.Boxes)
                        child.InvalidateFontDependentSubtree();
                }
                
                return;
            }

            string length = RegexParserUtils.Search(RegexParserUtils.CssLengthRegex(), value);

            if (length != null)
            {
                string computedValue;
                CssLength len = new(length);

                if (len.HasError)
                {
                    computedValue = "medium";
                }
                else if (len.Unit == CssUnit.Em && GetParent() != null)
                {
                    computedValue = len.ConvertEmToPoints(GetParent().ActualFont.Size).ToString();
                }
                else
                {
                    computedValue = len.ToString();
                }

                _fontSize = computedValue;
            }
            else
            {
                _fontSize = value;
            }

            InvalidateFontDependentValues();

            if (this is CssBox cssBox)
            {
                foreach (var child in cssBox.Boxes)
                    child.InvalidateFontDependentSubtree();
            }
        }
    }

    public string FontStyle { get; set; } = "normal";
    public string FontVariant { get; set; } = "normal";
    public string FontWeight { get; set; } = "normal";
    public string ListStyle { get; set; } = string.Empty;
    public string Overflow { get; set; } = "visible";
    public string ListStylePosition { get; set; } = "outside";
    public string ListStyleImage { get; set; } = string.Empty;
    public string ListStyleType { get; set; } = "disc";

    /// <summary>Semantic role of the element, set during style resolution from tag name.</summary>
    public BoxKind Kind { get; set; } = BoxKind.Anonymous;

    /// <summary>The <c>start</c> attribute of an <c>&lt;ol&gt;</c>, or null if not specified.</summary>
    public int? ListStart { get; set; }

    /// <summary>Whether an <c>&lt;ol&gt;</c> has the <c>reversed</c> attribute.</summary>
    public bool ListReversed { get; set; }

    /// <summary>The resolved <c>src</c> attribute for image elements, or null if not applicable.</summary>
    public string? ImageSource { get; set; }

    #endregion CSS Properties

    public PointF Location
    {
        get
        {
            if (_location.IsEmpty && Position == CssConstants.Fixed)
                _location = GetActualLocation(Left, Top);

            return _location;
        }
        set
        {
            _location = value;
        }
    }

    public SizeF Size
    {
        get { return _size; }
        set { _size = value; }
    }

    public RectangleF Bounds => new(Location, Size);

    public double AvailableWidth => Size.Width - ActualBorderLeftWidth - ActualPaddingLeft - ActualPaddingRight - ActualBorderRightWidth;

    public double ActualRight
    {
        get { return Location.X + Size.Width; }
        set { Size = new SizeF((float)(value - Location.X), Size.Height); }
    }

    public double ActualBottom
    {
        get { return Location.Y + Size.Height; }
        set { Size = new SizeF(Size.Width, (float)(value - Location.Y)); }
    }

    public double ClientLeft => Location.X + ActualBorderLeftWidth + ActualPaddingLeft;
    public double ClientTop => Location.Y + ActualBorderTopWidth + ActualPaddingTop;
    public double ClientRight => ActualRight - ActualPaddingRight - ActualBorderRightWidth;
    public double ClientBottom => ActualBottom - ActualPaddingBottom - ActualBorderBottomWidth;
    public RectangleF ClientRectangle => RectangleF.FromLTRB((float)ClientLeft, (float)ClientTop, (float)ClientRight, (float)ClientBottom);

    public double ActualHeight
    {
        get
        {
            if (double.IsNaN(_actualHeight))
            {
                _actualHeight = string.Equals(Height, "inherit", StringComparison.OrdinalIgnoreCase) && GetParent() != null
                    ? GetParent().ActualHeight
                    : ParseLengthWithLineHeight(Height, Size.Height);
            }

            return _actualHeight;
        }
    }

    public double ActualWidth
    {
        get
        {
            if (double.IsNaN(_actualWidth))
            {
                _actualWidth = string.Equals(Width, "inherit", StringComparison.OrdinalIgnoreCase) && GetParent() != null
                    ? GetParent().ActualWidth
                    : ParseLengthWithLineHeight(Width, Size.Width);
            }

            return _actualWidth;
        }
    }

    public double ActualPaddingTop
    {
        get
        {
            if (UsesLogicalFrameInsets())
                return FramePadding('T');

            if (double.IsNaN(_actualPaddingTop))
                _actualPaddingTop = ParseLengthWithLineHeight(PaddingTop, Size.Width);

            return _actualPaddingTop;
        }
    }

    public double ActualPaddingLeft
    {
        get
        {
            if (UsesLogicalFrameInsets())
                return FramePadding('L');

            if (double.IsNaN(_actualPaddingLeft))
                _actualPaddingLeft = ParseLengthWithLineHeight(PaddingLeft, Size.Width);

            return _actualPaddingLeft;
        }
    }

    public double ActualPaddingBottom
    {
        get
        {
            if (UsesLogicalFrameInsets())
                return FramePadding('B');

            if (double.IsNaN(_actualPaddingBottom))
                _actualPaddingBottom = ParseLengthWithLineHeight(PaddingBottom, Size.Width);

            return _actualPaddingBottom;
        }
    }

    public double ActualPaddingRight
    {
        get
        {
            if (UsesLogicalFrameInsets())
                return FramePadding('R');

            if (double.IsNaN(_actualPaddingRight))
                _actualPaddingRight = ParseLengthWithLineHeight(PaddingRight, Size.Width);

            return _actualPaddingRight;
        }
    }

    public double ActualMarginTop
    {
        get
        {
            if (double.IsNaN(_actualMarginTop))
            {
                if (MarginTop == CssConstants.Auto)
                {
                    _marginTopWasAuto = true;
                    MarginTop = "0";
                }

                var actualMarginTop = ParseLengthWithLineHeight(MarginTop, Size.Width);

                if (MarginTop.EndsWith('%'))
                    return actualMarginTop;

                _actualMarginTop = actualMarginTop;
            }

            return _actualMarginTop;
        }
    }

    public double CollapsedMarginTop
    {
        get { return double.IsNaN(_collapsedMarginTop) ? 0 : _collapsedMarginTop; }
        set { _collapsedMarginTop = value; }
    }

    public double ActualMarginLeft
    {
        get
        {
            if (double.IsNaN(_actualMarginLeft))
            {
                if (MarginLeft == CssConstants.Auto)
                {
                    _marginLeftWasAuto = true;
                    MarginLeft = "0";
                }

                var actualMarginLeft = ParseLengthWithLineHeight(MarginLeft, Size.Width);

                if (MarginLeft.EndsWith('%'))
                    return actualMarginLeft;

                _actualMarginLeft = actualMarginLeft;
            }
            return _actualMarginLeft;
        }
    }

    public double ActualMarginBottom
    {
        get
        {
            if (double.IsNaN(_actualMarginBottom))
            {
                if (MarginBottom == CssConstants.Auto)
                {
                    _marginBottomWasAuto = true;
                    MarginBottom = "0";
                }

                var actualMarginBottom = ParseLengthWithLineHeight(MarginBottom, Size.Width);

                if (MarginBottom.EndsWith('%'))
                    return actualMarginBottom;

                _actualMarginBottom = actualMarginBottom;
            }

            return _actualMarginBottom;
        }
    }

    public double ActualMarginRight
    {
        get
        {
            if (double.IsNaN(_actualMarginRight))
            {
                if (MarginRight == CssConstants.Auto)
                {
                    _marginRightWasAuto = true;
                    MarginRight = "0";
                }

                var actualMarginRight = ParseLengthWithLineHeight(MarginRight, Size.Width);

                if (MarginRight.EndsWith('%'))
                    return actualMarginRight;

                _actualMarginRight = actualMarginRight;
            }

            return _actualMarginRight;
        }
    }

    public double ActualBorderTopWidth
    {
        get
        {
            if (UsesLogicalFrameInsets())
                return FrameBorderWidth('T');

            if (double.IsNaN(_actualBorderTopWidth))
            {
                _actualBorderTopWidth = CssLengthParser.GetActualBorderWidth(BorderTopWidth, GetEmHeight());

                if (string.IsNullOrEmpty(BorderTopStyle) || BorderTopStyle == CssConstants.None)
                    _actualBorderTopWidth = 0f;
            }

            return _actualBorderTopWidth;
        }
    }

    public double ActualBorderLeftWidth
    {
        get
        {
            if (UsesLogicalFrameInsets())
                return FrameBorderWidth('L');

            if (double.IsNaN(_actualBorderLeftWidth))
            {
                _actualBorderLeftWidth = CssLengthParser.GetActualBorderWidth(BorderLeftWidth, GetEmHeight());

                if (string.IsNullOrEmpty(BorderLeftStyle) || BorderLeftStyle == CssConstants.None)
                    _actualBorderLeftWidth = 0f;
            }

            return _actualBorderLeftWidth;
        }
    }

    public double ActualBorderBottomWidth
    {
        get
        {
            if (UsesLogicalFrameInsets())
                return FrameBorderWidth('B');

            if (double.IsNaN(_actualBorderBottomWidth))
            {
                _actualBorderBottomWidth = CssLengthParser.GetActualBorderWidth(BorderBottomWidth, GetEmHeight());

                if (string.IsNullOrEmpty(BorderBottomStyle) || BorderBottomStyle == CssConstants.None)
                    _actualBorderBottomWidth = 0f;
            }

            return _actualBorderBottomWidth;
        }
    }

    public double ActualBorderRightWidth
    {
        get
        {
            if (UsesLogicalFrameInsets())
                return FrameBorderWidth('R');

            if (double.IsNaN(_actualBorderRightWidth))
            {
                _actualBorderRightWidth = CssLengthParser.GetActualBorderWidth(BorderRightWidth, GetEmHeight());

                if (string.IsNullOrEmpty(BorderRightStyle) || BorderRightStyle == CssConstants.None)
                    _actualBorderRightWidth = 0f;
            }

            return _actualBorderRightWidth;
        }
    }

    public BColor ActualBorderTopColor
    {
        get
        {
            if (_actualBorderTopColor.IsEmpty)
                _actualBorderTopColor = GetActualColor(BorderTopColor);

            return _actualBorderTopColor;
        }
    }

    protected abstract PointF GetActualLocation(string X, string Y);

    protected abstract BColor GetActualColor(string colorStr);

    protected virtual bool TryGetCustomPropertyValue(string propertyName, out string value)
    {
        value = string.Empty;
        return false;
    }

    private string ResolveCssVariables(string value)
    {
        if (string.IsNullOrEmpty(value) || value.IndexOf("var(", StringComparison.OrdinalIgnoreCase) < 0)
            return value;

        string resolved = value;
        for (int i = 0; i < 8 && resolved.Contains("var(", StringComparison.OrdinalIgnoreCase); i++)
        {
            resolved = CssRegex().Replace(resolved, match =>
                {
                    var propertyName = match.Groups[1].Value;
                    if (TryGetCustomPropertyValue(propertyName, out var propertyValue))
                    {
                        if (propertyValue == InvalidCustomPropertySentinel)
                            return match.Groups[2].Success ? match.Groups[2].Value.Trim() : string.Empty;

                        return propertyValue;
                    }

                    return match.Groups[2].Success ? match.Groups[2].Value.Trim() : string.Empty;
                });
        }

        return resolved;
    }

    public BColor ActualBorderLeftColor
    {
        get
        {
            if (_actualBorderLeftColor.IsEmpty)
                _actualBorderLeftColor = GetActualColor(BorderLeftColor);

            return _actualBorderLeftColor;
        }
    }

    public BColor ActualBorderBottomColor
    {
        get
        {
            if (_actualBorderBottomColor.IsEmpty)
                _actualBorderBottomColor = GetActualColor(BorderBottomColor);

            return _actualBorderBottomColor;
        }
    }

    public BColor ActualBorderRightColor
    {
        get
        {
            if (_actualBorderRightColor.IsEmpty)
                _actualBorderRightColor = GetActualColor(BorderRightColor);

            return _actualBorderRightColor;
        }
    }

    public BColor ActualTextDecorationColor
    {
        get
        {
            if (string.IsNullOrWhiteSpace(TextDecorationColor) ||
                TextDecorationColor.Equals("currentcolor", StringComparison.OrdinalIgnoreCase))
            {
                return ActualColor;
            }

            if (_actualTextDecorationColor.IsEmpty)
                _actualTextDecorationColor = GetActualColor(TextDecorationColor);

            return _actualTextDecorationColor;
        }
    }

    private string ResolvePhysicalSize(string explicitPhysicalValue, bool isWidth)
    {
        // PROTOTYPE (BROILER_VERTICAL_FLOW): a vertical-writing-mode box that
        // WILL be transposed by the post-layout rotation is laid out in a logical
        // (horizontal) frame whose frame-width is the box's INLINE size and
        // frame-height its BLOCK size; the rotation then swaps them into physical
        // space.  For a vertical writing mode physical 'width' is the block-size
        // and physical 'height' the inline-size, so the frame dimensions come
        // from the *swapped* physical properties (frame-width ← CSS height,
        // frame-height ← CSS width).  Without this an explicitly-sized box lays
        // out un-swapped and the rotation transposes it (a 20×80 inner → 80×20).
        // Gated on WillBeVerticalTransposed so a vertical box that is NOT actually
        // rotated (e.g. an abspos item nested in a vertical container, which the
        // runtime excludes from its container's rotation) is left untouched.
        // The cheap IsVerticalWritingMode check short-circuits the common
        // horizontal-tb case before the parent-chain walk.
        if (IsVerticalWritingMode(WritingMode)
            && VerticalFlowPrototype.Enabled
            && WillBeVerticalTransposed())
        {
            string logical = isWidth ? InlineSize : BlockSize;
            if (HasExplicitSize(logical))
                return logical;
            string swappedPhysical = isWidth ? _height : _width;
            return HasExplicitSize(swappedPhysical) ? swappedPhysical : explicitPhysicalValue;
        }

        if (HasExplicitSize(explicitPhysicalValue))
            return explicitPhysicalValue;

        // Legacy path (prototype disabled): vertical-writing-mode boxes swap
        // inline/block onto physical width/height directly (no post-layout
        // rotation).
        bool vertical = IsVerticalWritingMode(WritingMode) && !VerticalFlowPrototype.Enabled;
        var logicalValue = isWidth
            ? (vertical ? BlockSize : InlineSize)
            : (vertical ? InlineSize : BlockSize);

        return HasExplicitSize(logicalValue) ? logicalValue : explicitPhysicalValue;
    }

    /// <summary>
    /// PROTOTYPE (BROILER_VERTICAL_FLOW): whether this box will be transposed by
    /// the post-layout vertical-flow rotation — i.e. it lies inside a vertical
    /// rotation root (a vertical-writing-mode box whose parent is not vertical),
    /// reached without first crossing an out-of-flow box that establishes its own
    /// (non-transposed) rotation context.  Overridden in <see cref="CssBox"/>,
    /// which has the parent chain; the property base has no parent so it cannot
    /// be transposed.
    /// </summary>
    protected virtual bool WillBeVerticalTransposed() => false;

    private static bool HasExplicitSize(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !value.Equals("auto", StringComparison.OrdinalIgnoreCase);

    internal static bool IsVerticalWritingMode(string? writingMode)
    {
        var normalized = writingMode?.Trim().ToLowerInvariant();
        return normalized is "vertical-rl" or "vertical-lr" or "sideways-rl" or "sideways-lr";
    }

    /// <summary>
    /// PROTOTYPE (BROILER_VERTICAL_FLOW): a mirrored vertical writing mode
    /// (<c>vertical-rl</c> / <c>sideways-rl</c>) runs its block flow right→left,
    /// so the post-layout rotation (<see cref="CssBox.ApplyVerticalWritingModeFlow"/>)
    /// flips the block axis. Mirrors that transform's own <c>mirror</c> flag.
    /// </summary>
    private bool IsMirroredVerticalWritingMode()
    {
        var normalized = WritingMode?.Trim().ToLowerInvariant();
        return normalized is "vertical-rl" or "sideways-rl";
    }

    // PROTOTYPE (BROILER_VERTICAL_FLOW): depth of the logical (horizontal) frame
    // layout currently in progress for a vertical-writing-mode rotation root.
    // Non-zero only between a rotation root entering PerformLayout and its
    // post-layout ApplyVerticalWritingModeFlow, i.e. while its subtree is laid
    // out in the swapped frame — the window in which a transposed box's physical
    // border/padding insets must be read as LOGICAL (frame) insets so they land
    // on the correct axis after rotation. Cleared for the later paint pass, which
    // reads the box's authored PHYSICAL borders (writing-mode never rotates a
    // box's own borders). ThreadStatic so an unrelated document laid out on
    // another thread is unaffected; layout on a given thread is synchronous.
    [ThreadStatic] private static int _verticalFrameLayoutDepth;

    internal static bool InVerticalFrameLayout => _verticalFrameLayoutDepth > 0;
    internal static void PushVerticalFrameLayout() => _verticalFrameLayoutDepth++;
    internal static void PopVerticalFrameLayout() => _verticalFrameLayoutDepth--;

    /// <summary>
    /// PROTOTYPE (BROILER_VERTICAL_FLOW): whether this box's physical border and
    /// padding insets must be remapped onto the logical (frame) axes for the
    /// in-progress vertical-flow frame layout. True only for a transposed
    /// vertical-writing-mode box while its rotation root's subtree is being laid
    /// out. The physical→frame edge mapping (see <see cref="FrameBorderWidth"/> /
    /// <see cref="FramePadding"/>) makes a box's authored border-top/bottom
    /// contribute to its <em>inline</em> (post-rotation vertical) extent and its
    /// border-left/right to its <em>block</em> (post-rotation horizontal) extent,
    /// rather than the frame reading them un-rotated (which inflated the block
    /// extent by the inline-axis borders — a table-caption's blue box rendered
    /// ~2.3× too wide).
    /// </summary>
    private bool UsesLogicalFrameInsets() =>
        InVerticalFrameLayout
        && IsVerticalWritingMode(WritingMode)
        && WillBeVerticalTransposed();

    private double RawActualBorderWidth(string widthValue, string styleValue)
    {
        if (string.IsNullOrEmpty(styleValue) || styleValue == CssConstants.None)
            return 0f;
        return CssLengthParser.GetActualBorderWidth(widthValue, GetEmHeight());
    }

    // Frame edges are the logical (horizontal-tb LTR) frame's physical edges;
    // each maps to the authored physical side that must land there so the
    // post-layout rotation places it on the correct physical edge. Block-axis
    // edges (frame top/bottom) flip for a mirrored (rl) writing mode; inline-axis
    // edges (frame left/right, from the box's physical top/bottom) do not.
    private double FrameBorderWidth(char frameEdge)
    {
        bool mirror = IsMirroredVerticalWritingMode();
        return frameEdge switch
        {
            'T' => mirror ? RawActualBorderWidth(BorderRightWidth, BorderRightStyle)
                          : RawActualBorderWidth(BorderLeftWidth, BorderLeftStyle),
            'B' => mirror ? RawActualBorderWidth(BorderLeftWidth, BorderLeftStyle)
                          : RawActualBorderWidth(BorderRightWidth, BorderRightStyle),
            'L' => RawActualBorderWidth(BorderTopWidth, BorderTopStyle),
            'R' => RawActualBorderWidth(BorderBottomWidth, BorderBottomStyle),
            _ => 0f,
        };
    }

    private double FramePadding(char frameEdge)
    {
        bool mirror = IsMirroredVerticalWritingMode();
        return frameEdge switch
        {
            'T' => ParseLengthWithLineHeight(mirror ? PaddingRight : PaddingLeft, Size.Width),
            'B' => ParseLengthWithLineHeight(mirror ? PaddingLeft : PaddingRight, Size.Width),
            'L' => ParseLengthWithLineHeight(PaddingTop, Size.Width),
            'R' => ParseLengthWithLineHeight(PaddingBottom, Size.Width),
            _ => 0f,
        };
    }

    private double ParseCornerRadius(string radius)
    {
        double basis = radius != null && radius.Contains('%', StringComparison.Ordinal)
            ? Math.Max(0, Size.Width)
            : 0;

        return CssLengthParser.ParseLength(radius, basis, GetEmHeight());
    }

    public double ActualCornerNw
    {
        get
        {
            if (CornerNwRadius != null && CornerNwRadius.Contains('%', StringComparison.Ordinal))
                return ParseCornerRadius(CornerNwRadius);

            if (double.IsNaN(_actualCornerNw))
                _actualCornerNw = ParseCornerRadius(CornerNwRadius);

            return _actualCornerNw;
        }
    }

    public double ActualCornerNe
    {
        get
        {
            if (CornerNeRadius != null && CornerNeRadius.Contains('%', StringComparison.Ordinal))
                return ParseCornerRadius(CornerNeRadius);

            if (double.IsNaN(_actualCornerNe))
                _actualCornerNe = ParseCornerRadius(CornerNeRadius);

            return _actualCornerNe;
        }
    }

    public double ActualCornerSe
    {
        get
        {
            if (CornerSeRadius != null && CornerSeRadius.Contains('%', StringComparison.Ordinal))
                return ParseCornerRadius(CornerSeRadius);

            if (double.IsNaN(_actualCornerSe))
                _actualCornerSe = ParseCornerRadius(CornerSeRadius);

            return _actualCornerSe;
        }
    }

    public double ActualCornerSw
    {
        get
        {
            if (CornerSwRadius != null && CornerSwRadius.Contains('%', StringComparison.Ordinal))
                return ParseCornerRadius(CornerSwRadius);

            if (double.IsNaN(_actualCornerSw))
                _actualCornerSw = ParseCornerRadius(CornerSwRadius);

            return _actualCornerSw;
        }
    }

    public bool IsRounded => ActualCornerNe > 0f || ActualCornerNw > 0f || ActualCornerSe > 0f || ActualCornerSw > 0f;

    /// <summary>
    /// Whether geometry anti-aliasing should be avoided. Returns false by
    /// default; subclasses may override to provide container-specific behavior.
    /// </summary>
    public virtual bool AvoidGeometryAntialias => false;

    public double ActualWordSpacing { get; private set; } = double.NaN;

    public BColor ActualColor
    {
        get
        {
            if (_actualColor.IsEmpty)
                _actualColor = GetActualColor(Color);

            return _actualColor;
        }
    }

    public BColor ActualBackgroundColor
    {
        get
        {
            if (_actualBackgroundColor.IsEmpty)
                _actualBackgroundColor = GetActualColor(BackgroundColor);

            return _actualBackgroundColor;
        }
    }

    public BColor ActualBackgroundGradient
    {
        get
        {
            if (_actualBackgroundGradient.IsEmpty)
            {
                // "none" is the initial value and means no gradient; resolve to
                // fully-transparent so callers can simply check A > 0.  Without
                // this guard, GetActualColor("none") falls back to Color.Black
                // (opaque), which would cause EmitBackground to paint unintended
                // black fills.
                if (string.IsNullOrEmpty(BackgroundGradient) ||
                    string.Equals(BackgroundGradient, "none", StringComparison.OrdinalIgnoreCase))
                    _actualBackgroundGradient = BColor.FromArgb(0, 0, 0, 0);
                else
                    _actualBackgroundGradient = GetActualColor(BackgroundGradient);
            }

            return _actualBackgroundGradient;
        }
    }

    public double ActualBackgroundGradientAngle
    {
        get
        {
            if (double.IsNaN(_actualBackgroundGradientAngle))
                _actualBackgroundGradientAngle = CssLengthParser.ParseNumber(BackgroundGradientAngle, 360f);

            return _actualBackgroundGradientAngle;
        }
    }

    public ILayoutFont ActualFont
    {
        get
        {
            if (_actualFont != null)
                return _actualFont;

            if (string.IsNullOrEmpty(FontFamily))
                FontFamily = CssConstants.DefaultFont;

            if (string.IsNullOrEmpty(FontSize))
                FontSize = CssConstants.FontSize.ToString(CultureInfo.InvariantCulture) + "pt";

            LayoutFontStyle st = LayoutFontStyle.Regular;

            if (FontStyle == CssConstants.Italic || FontStyle == CssConstants.Oblique)
                st |= LayoutFontStyle.Italic;

            if (IsBoldWeight(FontWeight, GetParent()))
                st |= LayoutFontStyle.Bold;

            double parentSize = CssConstants.FontSize;

            if (GetParent() != null)
                parentSize = GetParent().ActualFont.Size;

            var fsize = FontSize switch
            {
                CssConstants.Medium => CssConstants.FontSize,
                CssConstants.XXSmall => CssConstants.FontSize - 4,
                CssConstants.XSmall => CssConstants.FontSize - 3,
                CssConstants.Small => CssConstants.FontSize - 2,
                CssConstants.Large => CssConstants.FontSize + 2,
                CssConstants.XLarge => CssConstants.FontSize + 3,
                CssConstants.XXLarge => CssConstants.FontSize + 4,
                CssConstants.Smaller => parentSize - 2,
                CssConstants.Larger => parentSize + 2,
                _ => CssLengthParser.ParseLength(FontSize, parentSize, parentSize, null, true, true),
            };

            // CSS 2.1 §15.4: font-size: 0 results in a zero-size em box.
            // Use a tiny positive value so the font object remains valid
            // while producing near-zero word dimensions in the layout engine.
            if (fsize <= 0)
                fsize = 0.001;

            _actualFont = GetCachedFont(FontFamily, fsize, st, GetEnabledFontFeatureTags());

            return _actualFont;
        }
    }

    protected abstract ILayoutFont GetCachedFont(string fontFamily, double fsize, LayoutFontStyle st, string fontFeatures);

    public double ActualLineHeight
    {
        get
        {
            if (double.IsNaN(_actualLineHeight))
            {
                // CSS2.1 §10.8: "normal" line-height uses a UA-chosen value.
                // Prefer the font's own line metrics so layout matches browser
                // line boxes more closely than a fixed 1.2× fallback.
                if (LineHeight == "normal" || string.IsNullOrEmpty(LineHeight))
                    _actualLineHeight = GetNormalLineHeight();
                else
                    _actualLineHeight = ParseLineHeightLength(LineHeight, Size.Height);
            }

            return _actualLineHeight;
        }
    }

    public double ActualTextIndent
    {
        get
        {
            if (double.IsNaN(_actualTextIndent))
                _actualTextIndent = ParseLengthWithLineHeight(TextIndent, Size.Width);

            return _actualTextIndent;
        }
    }

    public double ActualBorderSpacingHorizontal
    {
        get
        {
            if (double.IsNaN(_actualBorderSpacingHorizontal))
            {
                MatchCollection matches = RegexParserUtils.Match(RegexParserUtils.CssLengthRegex(), BorderSpacing);

                if (matches.Count == 0)
                {
                    _actualBorderSpacingHorizontal = 0;
                }
                else if (matches.Count > 0)
                {
                    _actualBorderSpacingHorizontal = ParseLengthWithLineHeight(matches[0].Value, 1);
                }
            }

            return _actualBorderSpacingHorizontal;
        }
    }

    public double ActualBorderSpacingVertical
    {
        get
        {
            if (double.IsNaN(_actualBorderSpacingVertical))
            {
                MatchCollection matches = RegexParserUtils.Match(RegexParserUtils.CssLengthRegex(), BorderSpacing);

                if (matches.Count == 0)
                {
                    _actualBorderSpacingVertical = 0;
                }
                else if (matches.Count == 1)
                {
                    _actualBorderSpacingVertical = ParseLengthWithLineHeight(matches[0].Value, 1);
                }
                else
                {
                    _actualBorderSpacingVertical = ParseLengthWithLineHeight(matches[1].Value, 1);
                }
            }

            return _actualBorderSpacingVertical;
        }
    }

    protected abstract CssBoxProperties GetParent();

    public double GetEmHeight() => ActualFont.Size * CssMetrics.PtToPx;

    /// <summary>
    /// The width of the "0" (ZERO, U+0030) glyph in this box's font — the CSS
    /// definition of the <c>ch</c> unit (CSS Values 3 §5.1.1). Returns
    /// <see cref="double.NaN"/> when no measuring environment is available yet,
    /// so callers fall back to the font-relative approximation. Overridden by
    /// <see cref="CssBox"/> to measure against the real font.
    /// </summary>
    protected virtual double GetChWidth() => double.NaN;

    protected double ParseLengthWithLineHeight(string length, double hundredPercent)
    {
        if (!string.IsNullOrWhiteSpace(length) &&
            length.EndsWith("rem", StringComparison.OrdinalIgnoreCase) &&
            double.TryParse(length[..^3], NumberStyles.Float, CultureInfo.InvariantCulture, out var rem))
        {
            return rem * GetRootEmHeight();
        }

        // CSS Values 3 §5.1.1: 1ch is the advance measure of the "0" glyph in the
        // element's font. Resolve it from the real font metrics when a measuring
        // environment is available (e.g. Ahem's "0" is a full 1em, not the 0.5em
        // approximation the generic length parser uses). Fall back to the parser
        // for calc()/unavailable-metrics cases.
        if (!string.IsNullOrWhiteSpace(length)
            && length.EndsWith("ch", StringComparison.OrdinalIgnoreCase)
            && !length.EndsWith("rch", StringComparison.OrdinalIgnoreCase)
            && double.TryParse(length[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var chCount))
        {
            double chWidth = GetChWidth();
            if (!double.IsNaN(chWidth) && chWidth > 0)
                return chCount * chWidth;
        }

        return CssLengthParser.ParseLength(
            length,
            hundredPercent,
            GetEmHeight(),
            null,
            false,
            false,
            ActualLineHeight,
            GetRootLineHeight());
    }

    private double ParseLineHeightLength(string length, double hundredPercent)
    {
        var parentLineHeight = GetParent()?.ActualLineHeight ?? GetNormalLineHeight();
        return CssLengthParser.ParseLength(
            length,
            hundredPercent,
            GetEmHeight(),
            null,
            false,
            false,
            parentLineHeight,
            GetRootLineHeight());
    }

    private double GetRootLineHeight()
    {
        var root = GetEffectiveRootBoxProperties();

        if (!ReferenceEquals(root, this))
            return root.ActualLineHeight;

        if (!double.IsNaN(_actualLineHeight))
            return _actualLineHeight;

        if (LineHeight == "normal" || string.IsNullOrEmpty(LineHeight))
            return GetNormalLineHeight();

        return CssLengthParser.ParseLength(
            LineHeight,
            Size.Height,
            GetEmHeight(),
            null,
            false,
            false,
            GetNormalLineHeight(),
            GetNormalLineHeight());
    }

    private double GetNormalLineHeight()
    {
        // ActualFont.Height is already expressed in CSS pixels (the font
        // compat factory bakes the pt→px ratio into the returned metric), so
        // it must NOT be scaled by 96/72 again here — doing so inflated the
        // 'normal' line height by ~1.33x (e.g. Arial 32px produced a 50px
        // line box instead of the ~37px browsers use).
        double fontHeight = ActualFont.Height;
        return fontHeight > 0 ? Math.Ceiling(fontHeight) : GetEmHeight() * CssMetrics.NormalLineHeightFactor;
    }

    private double GetRootEmHeight()
    {
        var root = GetEffectiveRootBoxProperties();

        const double baseRootEmHeight = CssMetrics.DefaultFontSizePx;
        if (!string.IsNullOrWhiteSpace(root.FontSize))
        {
            var resolved = CssLengthParser.ParseLength(
                root.FontSize,
                baseRootEmHeight,
                baseRootEmHeight,
                null,
                false,
                false,
                baseRootEmHeight * CssMetrics.NormalLineHeightFactor,
                baseRootEmHeight * CssMetrics.NormalLineHeightFactor);

            if (!double.IsNaN(resolved) && resolved > 0)
                return resolved;
        }

        return root.GetEmHeight();
    }

    private CssBoxProperties GetEffectiveRootBoxProperties()
    {
        CssBoxProperties root = this;
        while (root.GetParent() != null)
            root = root.GetParent();

        if (root is CssBox cssRoot)
        {
            while (cssRoot.HtmlTag == null && cssRoot.Boxes.Count == 1)
            {
                var child = cssRoot.Boxes[0];
                if (ReferenceEquals(child, cssRoot))
                    break;

                cssRoot = child;
            }

            root = cssRoot;
        }

        return root;
    }

    /// <summary>
    /// Resolves a CSS font-weight value to a numeric weight (100–900)
    /// per CSS 2.1 §15.6. Handles keywords <c>normal</c>, <c>bold</c>,
    /// <c>bolder</c>, <c>lighter</c>, and numeric strings.
    /// </summary>
    internal static int ResolveNumericFontWeight(string fontWeight, CssBoxProperties parent)
    {
        if (string.IsNullOrEmpty(fontWeight) || fontWeight == CssConstants.Normal || fontWeight == CssConstants.Inherit)
            return 400;

        if (fontWeight == CssConstants.Bold)
            return 700;

        if (int.TryParse(fontWeight, out int numeric))
            return Math.Clamp(numeric, 100, 900);

        if (fontWeight == CssConstants.Bolder || fontWeight == CssConstants.Lighter)
        {
            int parentWeight = 400;
            if (parent != null)
                parentWeight = ResolveNumericFontWeight(parent.FontWeight, parent.GetParent());

            return fontWeight == CssConstants.Bolder
                ? ResolveBolder(parentWeight)
                : ResolveLighter(parentWeight);
        }

        // Any other non-empty, non-normal value is treated as bold
        return 700;
    }

    /// <summary>
    /// CSS 2.1 §15.6: <c>bolder</c> selects the next weight above the inherited value.
    /// </summary>
    private static int ResolveBolder(int parentWeight)
    {
        if (parentWeight < 400) return 400;
        if (parentWeight < 600) return 700;

        return 900;
    }

    /// <summary>
    /// CSS 2.1 §15.6: <c>lighter</c> selects the next weight below the inherited value.
    /// </summary>
    private static int ResolveLighter(int parentWeight)
    {
        if (parentWeight > 700) return 400;
        if (parentWeight > 500) return 400;

        return 100;
    }

    /// <summary>
    /// Returns <c>true</c> when the resolved numeric font weight is 600 or above,
    /// meaning the font should use a bold face.
    /// </summary>
    private static bool IsBoldWeight(string fontWeight, CssBoxProperties parent)
    {
        if (string.IsNullOrEmpty(fontWeight) || fontWeight == CssConstants.Normal || fontWeight == CssConstants.Inherit)
            return false;

        return ResolveNumericFontWeight(fontWeight, parent) >= 600;
    }

    protected void SetAllBorders(string style = null, string width = null, string color = null)
    {
        if (style != null)
            BorderLeftStyle = BorderTopStyle = BorderRightStyle = BorderBottomStyle = style;

        if (width != null)
            BorderLeftWidth = BorderTopWidth = BorderRightWidth = BorderBottomWidth = width;

        if (color != null)
            BorderLeftColor = BorderTopColor = BorderRightColor = BorderBottomColor = color;
    }

    protected void MeasureWordSpacing(ILayoutEnvironment g)
    {
        if (!double.IsNaN(ActualWordSpacing))
            return;

        ActualWordSpacing = CssUtils.WhiteSpace(g, this);

        if (WordSpacing == CssConstants.Normal)
            return;

        string len = RegexParserUtils.Search(RegexParserUtils.CssLengthRegex(), WordSpacing);
        ActualWordSpacing += CssLengthParser.ParseLength(len, 1, GetEmHeight());
    }

    protected void InheritStyle(CssBox p, bool everything)
    {
        if (p == null)
            return;

        BorderSpacing = p.BorderSpacing;
        BorderCollapse = p.BorderCollapse;
        _color = p._color;
        EmptyCells = p.EmptyCells;
        CaptionSide = p.CaptionSide;
        WhiteSpace = p.WhiteSpace;
        TextTransform = p.TextTransform;
        Visibility = p.Visibility;
        _textIndent = p._textIndent;
        TextAlign = p.TextAlign;
        TextAlignLast = p.TextAlignLast;
        FontFamily = p.FontFamily;
        FontFeatureSettings = p.FontFeatureSettings;
        FontVariantAlternates = p.FontVariantAlternates;
        _fontSize = p._fontSize;
        FontStyle = p.FontStyle;
        FontVariant = p.FontVariant;
        FontWeight = p.FontWeight;
        ListStyleImage = p.ListStyleImage;
        ListStylePosition = p.ListStylePosition;
        ListStyleType = p.ListStyleType;
        ListStyle = p.ListStyle;
        _lineHeight = p._lineHeight;
        WordBreak = p.WordBreak;
        LineBreak = p.LineBreak;
        Direction = p.Direction;
        WritingMode = p.WritingMode;
        TextShadow = p.TextShadow;

        if (!everything)
            return;

        BackgroundColor = p.BackgroundColor;
        BackgroundGradient = p.BackgroundGradient;
        BackgroundGradientAngle = p.BackgroundGradientAngle;
        BackgroundImage = p.BackgroundImage;
        BackgroundPosition = p.BackgroundPosition;
        BackgroundRepeat = p.BackgroundRepeat;
        BackgroundAttachment = p.BackgroundAttachment;
        BackgroundOrigin = p.BackgroundOrigin;
        BackgroundSize = p.BackgroundSize;
        _borderTopWidth = p._borderTopWidth;
        _borderRightWidth = p._borderRightWidth;
        _borderBottomWidth = p._borderBottomWidth;
        _borderLeftWidth = p._borderLeftWidth;
        _borderTopColor = p._borderTopColor;
        _borderRightColor = p._borderRightColor;
        _borderBottomColor = p._borderBottomColor;
        _borderLeftColor = p._borderLeftColor;
        OutlineWidth = p.OutlineWidth;
        OutlineStyle = p.OutlineStyle;
        _outlineColor = p._outlineColor;
        OutlineOffset = p.OutlineOffset;
        BorderTopStyle = p.BorderTopStyle;
        BorderRightStyle = p.BorderRightStyle;
        BorderBottomStyle = p.BorderBottomStyle;
        BorderLeftStyle = p.BorderLeftStyle;
        _bottom = p._bottom;
        CornerNwRadius = p.CornerNwRadius;
        CornerNeRadius = p.CornerNeRadius;
        CornerSeRadius = p.CornerSeRadius;
        CornerSwRadius = p.CornerSwRadius;
        _cornerRadius = p._cornerRadius;
        Display = p.Display;
        Float = p.Float;
        BlockSize = p.BlockSize;
        Height = p.Height;
        InlineSize = p.InlineSize;
        MarginBottom = p.MarginBottom;
        MarginLeft = p.MarginLeft;
        MarginRight = p.MarginRight;
        MarginTop = p.MarginTop;
        MarginTrim = p.MarginTrim;
        _left = p._left;
        _lineHeight = p._lineHeight;
        Overflow = p.Overflow;
        _paddingLeft = p._paddingLeft;
        _paddingBottom = p._paddingBottom;
        _paddingRight = p._paddingRight;
        _paddingTop = p._paddingTop;
        _right = p._right;
        TextDecoration = p.TextDecoration;
        TextDecorationStyle = p.TextDecorationStyle;
        TextDecorationColor = p.TextDecorationColor;
        _top = p._top;
        Position = p.Position;
        VerticalAlign = p.VerticalAlign;
        Width = p.Width;
        MaxWidth = p.MaxWidth;
        MinWidth = p.MinWidth;
        IsMinWidthSpecified = p.IsMinWidthSpecified;
        MinHeight = p.MinHeight;
        MaxHeight = p.MaxHeight;
        _wordSpacing = p._wordSpacing;
        Opacity = p.Opacity;
        BoxShadow = p.BoxShadow;
        MixBlendMode = p.MixBlendMode;
        BackgroundBlendMode = p.BackgroundBlendMode;
        Filter = p.Filter;
        Isolation = p.Isolation;
        BoxSizing = p.BoxSizing;
        BackgroundClip = p.BackgroundClip;
        ClipPath = p.ClipPath;
        FlexDirection = p.FlexDirection;
        FlexGrow = p.FlexGrow;
        FlexShrink = p.FlexShrink;
        FlexBasis = p.FlexBasis;
        FlexWrap = p.FlexWrap;
        JustifyContent = p.JustifyContent;
        JustifyItems = p.JustifyItems;
        AlignItems = p.AlignItems;
        AlignContent = p.AlignContent;
        JustifySelf = p.JustifySelf;
        AlignSelf = p.AlignSelf;
        RowGap = p.RowGap;
        ColumnGap = p.ColumnGap;
    }

    protected void InvalidateFontDependentValues()
    {
        _actualFont = null;
        _actualHeight = double.NaN;
        _actualWidth = double.NaN;
        _actualPaddingTop = double.NaN;
        _actualPaddingBottom = double.NaN;
        _actualPaddingRight = double.NaN;
        _actualPaddingLeft = double.NaN;
        _actualMarginTop = double.NaN;
        _actualMarginBottom = double.NaN;
        _actualMarginRight = double.NaN;
        _actualMarginLeft = double.NaN;
        _actualLineHeight = double.NaN;
        _actualTextIndent = double.NaN;
        _actualBorderTopWidth = double.NaN;
        _actualBorderRightWidth = double.NaN;
        _actualBorderBottomWidth = double.NaN;
        _actualBorderLeftWidth = double.NaN;
        // Outline width/offset resolve against the em height, so they are
        // font-dependent and must be re-resolved when the font changes.
        _actualOutlineWidth = double.NaN;
        _actualOutlineOffset = double.NaN;
        // A cached em/ex-based min/max width is font-dependent too.
        _actualMaxWidth = double.NaN;
        _actualMinWidth = double.NaN;
        _actualCornerNw = double.NaN;
        _actualCornerNe = double.NaN;
        _actualCornerSw = double.NaN;
        _actualCornerSe = double.NaN;
        _actualBorderSpacingHorizontal = double.NaN;
        _actualBorderSpacingVertical = double.NaN;
    }

    [GeneratedRegex(@"var\(\s*(--[A-Za-z0-9_-]+)\s*(?:,\s*([^)]+))?\)", RegexOptions.IgnoreCase)]
    private static partial Regex CssRegex();
}
