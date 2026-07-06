using Broiler.Graphics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using CssConstants = Broiler.CSS.CssConstants;
using CssValueParser = Broiler.CSS.CssLengthParser;

namespace Broiler.Layout.Engine;

/// <summary>
/// CSS Grid Level 1 — a bounded, real track-sizing grid layout pass.
///
/// The main renderer approximates <c>display:grid</c> with a single stacked
/// column (see <see cref="ApplyGridStacking"/> / <see cref="ApplyGridAutoPlacement"/>
/// in CssBox.cs). This partial adds a real track-based pass that runs the §8.5
/// grid-item placement algorithm (line-based placement, spanning,
/// <c>grid-auto-flow</c> row/column with sparse/dense packing, implicit tracks) and
/// a bounded §11 track-sizing algorithm covering <c>&lt;length&gt;</c>,
/// <c>&lt;percentage&gt;</c>, <c>&lt;flex&gt;</c> (<c>fr</c>), <c>auto</c>,
/// <c>min-content</c>, <c>max-content</c>, <c>minmax()</c>, <c>repeat(&lt;int&gt;, …)</c>,
/// and <c>repeat(auto-fill, &lt;fixed-track-list&gt;)</c> (§7.2.3.1 repeat-to-fill).
///
/// Intrinsic column sizing draws its content contributions from
/// <see cref="GetMinMaxWidth"/> (a layout-independent measure); intrinsic row
/// sizing uses each item's already-measured height and declines (falls back to the
/// approximation) when a narrowed column would have reflowed that height, so it
/// never sizes a row from a stale measurement. Negative grid lines
/// (e.g. <c>grid-column: -2</c>) resolve against the explicit track count.
/// Anything still out of scope (<c>subgrid</c>, <c>fit-content()</c>,
/// <c>repeat(auto-fit, …)</c> — whose empty-track collapsing is unmodelled — and
/// named lines carrying sizes) makes the pass decline, confining the new
/// behaviour to grids it can size correctly.
/// </summary>
internal partial class CssBox
{
    /// <summary>Resolved placement of one grid item, 0-indexed, end-exclusive.</summary>
    private struct GridPlacement
    {
        public CssBox Item;
        public int? RowStart;   // null = auto
        public int RowSpan;
        public int? ColStart;   // null = auto
        public int ColSpan;
        public int PlacedRow;
        public int PlacedCol;
    }

    // Upper bound on any resolved grid line index / span or implicit track count.
    // Grids past this are declined (fall back to the approximation) so a
    // pathological placement — e.g. grid-row: 100000 — cannot allocate huge track
    // arrays or spin the placement search. Real content stays well under this.
    private const int MaxGridLine = 1000;

    /// <summary>The kind of a single (min or max) track sizing function.</summary>
    private enum GridSizeKind { Fixed, Percent, Fr, Auto, MinContent, MaxContent }

    /// <summary>One side (min or max) of a track sizing function.</summary>
    private readonly struct GridSize
    {
        public readonly GridSizeKind Kind;
        public readonly double Value; // px for Fixed, flex factor for Fr; else 0
        public GridSize(GridSizeKind kind, double value = 0) { Kind = kind; Value = value; }
        public bool IsIntrinsic => Kind is GridSizeKind.Auto or GridSizeKind.MinContent or GridSizeKind.MaxContent;
    }

    /// <summary>A track's <c>minmax(min, max)</c> sizing function.</summary>
    private readonly struct GridTrackSpec
    {
        public readonly GridSize Min;
        public readonly GridSize Max;
        public GridTrackSpec(GridSize min, GridSize max) { Min = min; Max = max; }
    }

    /// <summary>One grid item's span and content contributions along one axis.</summary>
    private readonly struct AxisItem
    {
        public readonly int Start;
        public readonly int Span;
        public readonly double MinContent;
        public readonly double MaxContent;
        public AxisItem(int start, int span, double minContent, double maxContent)
        { Start = start; Span = span; MinContent = minContent; MaxContent = maxContent; }
    }

    /// <summary>
    /// Runs the real track-sizing grid pass. Returns <c>true</c> when it laid the
    /// container out (caller must not run the approximation), <c>false</c> to
    /// decline (unsupported track syntax, no in-flow items, degenerate size, or a
    /// row measurement that a narrowed column would have invalidated).
    /// </summary>
    private bool TryApplyGridTrackLayout()
    {
        double contentWidth = Size.Width - ActualPaddingLeft - ActualPaddingRight
            - ActualBorderLeftWidth - ActualBorderRightWidth;
        double em = GetEmHeight();
        // A subgrid grid can be handed a zero-width area — a nested subgrid whose
        // parent's auto columns collapsed to 0 because its empty content
        // contributes no width — yet it must still size its rows: the block size is
        // width-independent (grid-auto-rows) and the visible grey area is the box's
        // own background over its border box. Only decline a zero-width area for an
        // ordinary grid, whose column-driven layout would be degenerate there.
        bool subgridInvolved = StartsWithSubgrid(GridTemplateColumns)
            || StartsWithSubgrid(GridTemplateRows);
        if (contentWidth <= 0 && !subgridInvolved)
            return false;
        if (contentWidth < 0)
            contentWidth = 0;

        // CSS Grid Level 2 §7.3 (subgrid): an axis whose template is `subgrid`
        // takes its tracks from the parent grid over the item's spanned area. The
        // parent's track pass resolves and records those sizes (SubgridColumn/RowSizes)
        // and re-runs this layout; until then the axis is unresolved and the pass
        // declines, falling back to the approximation.
        bool colSubgrid = SubgridColumnSizes != null && StartsWithSubgrid(GridTemplateColumns);
        bool rowSubgrid = SubgridRowSizes != null && StartsWithSubgrid(GridTemplateRows);
        bool anySubgrid = colSubgrid || rowSubgrid;

        // CSS Grid Level 2 §7.3: an axis that declares `subgrid` but has no
        // inherited tracks yet (SubgridColumn/RowSizes still null) is unresolved —
        // either this box has no grid to subgrid onto (a true orphan) or its parent
        // grid has not recorded the inherited sizes yet. For THIS pass the axis
        // computes to `none` and the box lays out as a standalone grid: crucially
        // its *other*, non-subgridded axis (e.g. `grid-auto-rows` on a column
        // subgrid) still sizes correctly, so the grid gets its proper block size
        // instead of collapsing. A true orphan keeps this result; a real subgrid is
        // re-run by its parent's LayoutSubgridItem once the inherited sizes exist,
        // which replaces this standalone layout with the true subgridded one.
        //
        // Unflagged browsers reach this for the subgridded children of a
        // `display: grid-lanes` container: the (unsupported) grid-lanes display is
        // dropped to block, so the child grids have no grid parent and their
        // `grid-template-columns: subgrid …` falls back this way — including the
        // nested `subgrid > subgrid` case — matching the Chromium reference for the
        // WPT css-grid/grid-lanes grid-subgridded-to-grid-lanes suite.
        bool colOrphanSubgrid = SubgridColumnSizes == null && StartsWithSubgrid(GridTemplateColumns);
        bool rowOrphanSubgrid = SubgridRowSizes == null && StartsWithSubgrid(GridTemplateRows);
        bool anyOrphanSubgrid = colOrphanSubgrid || rowOrphanSubgrid;

        // Available content-box sizes and gaps for resolving repeat(auto-fill, …)
        // track counts (CSS Grid §7.2.3.1). The inline size is the container's
        // already-clamped used content width; the block size is a definite height
        // (or a definite min-height when the height is indefinite), else 0 → one
        // repetition. Gaps enter the repeat-to-fill count, so resolve them here.
        double autoRepeatColGap = ResolveGridGap(ColumnGap, contentWidth, em);
        double autoRepeatBlock = ComputeAutoRepeatBlockSize(em, out bool autoRepeatBlockFromMin);
        double autoRepeatRowGap = ResolveGridGap(RowGap, autoRepeatBlock, em);

        // Parse both axes' explicit track lists into sizing functions. A null list
        // (none/empty or an out-of-scope token) declines the whole pass; an orphan
        // subgrid axis contributes no explicit tracks (its `subgrid` computed to none).
        // A repeat(auto-fill, …) template is expanded to a concrete track list here.
        List<GridTrackSpec> colSpecs = colSubgrid ? FixedTrackSpecs(SubgridColumnSizes)
            : colOrphanSubgrid ? new List<GridTrackSpec>()
            : ParseTrackListMaybeAutoRepeat(GridTemplateColumns, em, contentWidth, autoRepeatColGap);
        List<GridTrackSpec> rowSpecs = rowSubgrid ? FixedTrackSpecs(SubgridRowSizes)
            : rowOrphanSubgrid ? new List<GridTrackSpec>()
            : ParseTrackListMaybeAutoRepeat(GridTemplateRows, em, autoRepeatBlock, autoRepeatRowGap, autoRepeatBlockFromMin);

        // When one axis is a resolved subgrid (or an orphan subgrid resolved to
        // none), the other axis may legitimately be implicit-only (e.g. a column
        // subgrid with `grid-auto-rows`): treat a none/empty track list there as
        // zero explicit tracks so the placed items generate implicit tracks, rather
        // than declining.
        if (anySubgrid || anyOrphanSubgrid)
        {
            if (colSpecs == null && IsNoneOrEmptyTemplate(GridTemplateColumns)) colSpecs = new List<GridTrackSpec>();
            if (rowSpecs == null && IsNoneOrEmptyTemplate(GridTemplateRows)) rowSpecs = new List<GridTrackSpec>();
            // A bare `auto` template is one explicit auto-sized track, but
            // ParseTrackList lumps it in with `none` (returns null so the general
            // path declines to the approximation). For a subgrid/standalone grid,
            // keep it as a real auto track so a nested item taller than the implicit
            // grid-auto-* size grows the track instead of the pass declining — the
            // `grid-lanes > .subgrid { grid-template-rows: auto }` outer grids that
            // wrap a taller inner subgrid (WPT grid-subgridded-to-grid-lanes) need this.
            if (colSpecs == null && IsBareAutoTemplate(GridTemplateColumns))
                colSpecs = new List<GridTrackSpec> { AutoTrackSpec };
            if (rowSpecs == null && IsBareAutoTemplate(GridTemplateRows))
                rowSpecs = new List<GridTrackSpec> { AutoTrackSpec };
        }

        // An axis with no explicit template (`none`/empty) but a `grid-auto-*`
        // sizing function is implicit-only: its tracks come entirely from
        // auto-placement (§7.5). Treat the empty template as zero explicit tracks
        // so the placed items generate implicit tracks, rather than declining the
        // whole pass — this is the common `display:grid` + `grid-auto-columns/rows`
        // shape (the css-grid alignment suite, whose grids omit templates), which
        // the approximation renders collapsed. An *unsupported* token (subgrid,
        // fit-content, …) still parses to null and declines below.
        bool colImplicitOnly = colSpecs == null && IsNoneOrEmptyTemplate(GridTemplateColumns);
        bool rowImplicitOnly = rowSpecs == null && IsNoneOrEmptyTemplate(GridTemplateRows);
        if (colImplicitOnly) colSpecs = new List<GridTrackSpec>();
        if (rowImplicitOnly) rowSpecs = new List<GridTrackSpec>();

        if (colSpecs == null || rowSpecs == null)
            return false;
        // The implicit-only (`none` template + grid-auto-*) path is a newly enabled
        // takeover from the baseline-aware cross-axis approximation, so it declines
        // for the two shapes this bounded pass sizes worse than that approximation:
        //   • baseline self-alignment (`align-self: baseline`, `justify-items: last
        //     baseline`, …) synthesizes a shared baseline across a column/row
        //     (CSS Align §9) this pass does not compute — it would fall to start;
        //   • an item whose used block size the pass cannot trust from a single
        //     measurement — a nested flex/grid/table container, a replaced or
        //     form-control item, or a scroll container — where sizing an auto row
        //     from the measured height collapses or mis-stretches the item.
        // Grids with an explicit template on the affected axis keep going through
        // this pass exactly as before; only the implicit-only takeover is gated.
        if ((colImplicitOnly || rowImplicitOnly)
            && (GridUsesBaselineSelfAlignment() || !GridImplicitPathItemsAreSimple()))
            return false;
        // Implicit tracks are allowed to carry the whole axis for a subgrid, an
        // orphan subgrid, or an implicit-only (`none` template + grid-auto-*) axis;
        // the placed items below generate them. Any other empty axis is degenerate
        // and declines to the approximation.
        bool implicitTracksAllowed =
            anySubgrid || anyOrphanSubgrid || colImplicitOnly || rowImplicitOnly;
        if (colSpecs.Count == 0 && rowSpecs.Count == 0
            && !anyOrphanSubgrid && !(colImplicitOnly && rowImplicitOnly))
            return false;
        if (!implicitTracksAllowed && (colSpecs.Count == 0 || rowSpecs.Count == 0))
            return false;
        if (colSpecs.Count > MaxGridLine || rowSpecs.Count > MaxGridLine)
            return false;

        // Implicit tracks (grid-auto-columns/rows); default 'auto'.
        GridTrackSpec implicitCol = ParseSingleImplicitSpec(GridAutoColumns, em);
        GridTrackSpec implicitRow = ParseSingleImplicitSpec(GridAutoRows, em);

        // Collect in-flow grid items in document order. Absolutely-positioned
        // children are gathered separately: they take no part in track sizing or
        // auto-placement, but when the grid container is their containing block
        // (i.e. it is positioned) their grid area forms their containing block
        // (CSS Grid §9), which the abspos pass below resolves once tracks exist.
        bool gridIsAbsposContainingBlock =
            Position is CssConstants.Relative or CssConstants.Absolute or CssConstants.Fixed;
        var placements = new List<GridPlacement>();
        List<CssBox> absposItems = null;
        var colNames = ParseLineNames(GridTemplateColumns);
        var rowNames = ParseLineNames(GridTemplateRows);
        foreach (var child in Boxes)
        {
            if (child.Display == CssConstants.None)
                continue;
            if (child.Position == CssConstants.Absolute || child.Position == CssConstants.Fixed)
            {
                if (gridIsAbsposContainingBlock)
                    (absposItems ??= new List<CssBox>()).Add(child);
                continue;
            }

            var (rowStart, rowSpan) = ParseGridLine(child.GridRow, rowSpecs.Count, rowNames);
            var (colStart, colSpan) = ParseGridLine(child.GridColumn, colSpecs.Count, colNames);
            // Decline rather than lay out an implausibly large grid. A negative
            // start references a leading implicit track (normalised below), so bound
            // its magnitude too.
            if (rowSpan > MaxGridLine || colSpan > MaxGridLine
                || Math.Abs(rowStart ?? 0) > MaxGridLine || Math.Abs(colStart ?? 0) > MaxGridLine)
                return false;
            placements.Add(new GridPlacement
            {
                Item = child,
                RowStart = rowStart,
                RowSpan = rowSpan,
                ColStart = colStart,
                ColSpan = colSpan,
            });
        }
        if (placements.Count == 0)
            return false;

        // CSS Grid §8.3: a definite line that resolves before the explicit grid
        // (a negative index here) references a *leading* implicit track. Normalise
        // by shifting every placement right/down so the leftmost/topmost referenced
        // line is index 0; the explicit grid then begins at explicitColStart /
        // explicitRowStart. leading == 0 for every grid without a before-grid line,
        // so those are byte-identical to before. Auto-placement into leading tracks
        // is out of scope, so decline when an auto-placed item coexists with them.
        int explicitColStart = 0, explicitRowStart = 0;
        {
            int minCol = 0, minRow = 0;
            foreach (var p in placements)
            {
                if (p.ColStart.HasValue) minCol = Math.Min(minCol, p.ColStart.Value);
                if (p.RowStart.HasValue) minRow = Math.Min(minRow, p.RowStart.Value);
            }
            explicitColStart = -minCol;
            explicitRowStart = -minRow;
            if (explicitColStart > 0 || explicitRowStart > 0)
            {
                foreach (var p in placements)
                    if (!p.ColStart.HasValue || !p.RowStart.HasValue)
                        return false;
                for (int i = 0; i < placements.Count; i++)
                {
                    var p = placements[i];
                    p.ColStart += explicitColStart;
                    p.RowStart += explicitRowStart;
                    placements[i] = p;
                }
            }
        }

        bool flowRow = (GridAutoFlow ?? "row").IndexOf("column", StringComparison.OrdinalIgnoreCase) < 0;
        bool dense = (GridAutoFlow ?? "").IndexOf("dense", StringComparison.OrdinalIgnoreCase) >= 0;

        PlaceItems(placements, colSpecs.Count + explicitColStart, rowSpecs.Count + explicitRowStart, flowRow, dense);

        // Track counts after placement, including any implicit tracks.
        int maxColEnd = 0, maxRowEnd = 0;
        foreach (var p in placements)
        {
            maxColEnd = Math.Max(maxColEnd, p.PlacedCol + p.ColSpan);
            maxRowEnd = Math.Max(maxRowEnd, p.PlacedRow + p.RowSpan);
        }
        if (maxColEnd > MaxGridLine || maxRowEnd > MaxGridLine)
            return false;
        // The explicit tracks occupy [explicitColStart, explicitColStart+count); any
        // leading implicit tracks precede them, so the axis is at least that wide.
        int colCount = Math.Max(maxColEnd, explicitColStart + colSpecs.Count);
        int rowCount = Math.Max(maxRowEnd, explicitRowStart + rowSpecs.Count);

        // A subgridded axis adopts the parent grid's gutter (CSS Grid L2 §7.3).
        double colGap = colSubgrid && SubgridColumnGap.HasValue
            ? SubgridColumnGap.Value
            : ResolveGridGap(ColumnGap, contentWidth, em);

        // ── Column axis (inline) ── content contributions are layout-independent.
        var colItems = new List<AxisItem>(placements.Count);
        foreach (var p in placements)
        {
            GridItemInlineContribution(p.Item, out double minW, out double maxW);
            double marginX = p.Item.ActualMarginLeft + p.Item.ActualMarginRight;
            colItems.Add(new AxisItem(p.PlacedCol, p.ColSpan, minW + marginX, maxW + marginX));
        }
        // CSS Grid §11.7: the default (normal) / explicit stretch content
        // distribution grows auto tracks to fill the container; a packing/spacing
        // justify-content leaves them at content size and positions them instead.
        bool stretchCols = IsStretchContentDistribution(JustifyContent);
        double[] colSizes = ResolveTrackSizes(colSpecs, implicitCol, colCount,
            contentWidth, definite: true, colGap, contentWidth, em, colItems, stretchCols,
            explicitStart: explicitColStart);
        if (colSizes == null)
            return false;

        // ── Row axis (block) ── heights are width-dependent, so only trust a
        // measured height when the item's final column width did not shrink it
        // below its max-content (which would have reflowed the content taller).
        // The row axis is definite only when 'height' is an explicit length (a
        // percentage needs a definite containing block, out of scope here). Derive
        // the definite content-box height straight from the declaration — Size.Height
        // at this point may be a measurement, not the specified height — so it can
        // drive fr/percentage rows. An indefinite height must not.
        bool rowDefinite = TryGetDefiniteContentHeight(em, out double definiteHeight) || rowSubgrid;
        double rowBasis = rowDefinite ? definiteHeight : 0;
        double rowGap = rowSubgrid && SubgridRowGap.HasValue
            ? SubgridRowGap.Value
            : ResolveGridGap(RowGap, rowBasis, em);

        var rowItems = new List<AxisItem>(placements.Count);
        foreach (var p in placements)
        {
            double colWidth = TrackSpan(colSizes, p.PlacedCol, p.ColSpan, colGap);
            GridItemInlineContribution(p.Item, out double _, out double maxW);
            double marginX = p.Item.ActualMarginLeft + p.Item.ActualMarginRight;
            // The item was measured at the container width; if its resolved column
            // area is narrower than that max-content width, its measured height is
            // stale — decline any content-based row sizing rather than guess.
            if (RowNeedsMeasuredHeight(rowSpecs, implicitRow, p.PlacedRow, p.RowSpan, rowDefinite)
                && colWidth + 0.5 < maxW + marginX)
                return false;

            double marginY = p.Item.ActualMarginTop + p.Item.ActualMarginBottom;
            // A replaced item with a definite block-size records its height on its
            // image word (measured through the container line box), leaving the box
            // ActualBottom at 0; trust the definite block-size for the row instead
            // of that stale zero (WPT css-grid/nested-grid-item-block-size-001).
            double replacedH = GridReplacedItemDefiniteBorderBoxHeight(p.Item);
            double h = (replacedH > 0 ? replacedH : p.Item.ActualBottom - p.Item.Location.Y) + marginY;
            if (h < 0) h = 0;
            rowItems.Add(new AxisItem(p.PlacedRow, p.RowSpan, h, h));
        }
        bool stretchRows = IsStretchContentDistribution(AlignContent);
        double[] rowSizes = ResolveTrackSizes(rowSpecs, implicitRow, rowCount,
            rowBasis, rowDefinite, rowGap, rowBasis, em, rowItems, stretchRows,
            explicitStart: explicitRowStart);
        if (rowSizes == null)
            return false;

        // CSS Box Alignment §5 (content distribution): when the tracks do not
        // fill the container along an axis, justify-content (inline) / align-content
        // (block) positions or spaces them within the leftover room. The block axis
        // only has leftover room when the container's block size is definite.
        double rowContainerSize = rowDefinite ? definiteHeight : SumTrackSizes(rowSizes, rowGap);
        double[] colStartEdge = BuildTrackEdgesAligned(colSizes, colGap, contentWidth,
            JustifyContent, out double[] colEndEdge);
        double[] rowStartEdge = BuildTrackEdgesAligned(rowSizes, rowGap, rowContainerSize,
            AlignContent, out double[] rowEndEdge);

        double contentLeft = Location.X + ActualBorderLeftWidth + ActualPaddingLeft;
        double contentTop = Location.Y + ActualBorderTopWidth + ActualPaddingTop;

        // CSS Writing Modes §3: an `rtl` inline (column) axis runs right→left, so a
        // cell's physical x mirrors within the content box. Mirroring the resolved
        // (LTR) column edges also flips justify-content start↔end correctly.
        bool rtl = string.Equals(Direction, "rtl", StringComparison.OrdinalIgnoreCase);

        foreach (var p in placements)
        {
            double ltrLeft = colStartEdge[p.PlacedCol];
            double ltrRight = colEndEdge[p.PlacedCol + p.ColSpan - 1];
            double areaLeft = rtl ? contentLeft + contentWidth - ltrRight : contentLeft + ltrLeft;
            double areaRight = rtl ? contentLeft + contentWidth - ltrLeft : contentLeft + ltrRight;
            double areaTop = contentTop + rowStartEdge[p.PlacedRow];
            double areaBottom = contentTop + rowEndEdge[p.PlacedRow + p.RowSpan - 1];
            PlaceItemInArea(p.Item, areaLeft, areaTop, areaRight - areaLeft, areaBottom - areaTop);

            // CSS Grid L2 §7.3: a subgrid item, now sized to its grid area, takes
            // its tracks in the subgridded axis from this grid's tracks over the
            // spanned range and lays its own items into them.
            LayoutSubgridItem(p, colSizes, rowSizes, colGap, rowGap);
        }

        // The grid's block size is the block-end edge of its last row track. Every
        // grid-template-rows track contributes even when unoccupied, so a 4-track
        // template with items in only the first 3 rows still sizes to all four.
        double gridHeight = rowSizes.Length > 0 ? rowEndEdge[rowSizes.Length - 1] : 0;
        double borderBoxHeight = ActualPaddingTop + ActualPaddingBottom
            + ActualBorderTopWidth + ActualBorderBottomWidth + gridHeight;
        ActualBottom = Location.Y + borderBoxHeight;
        ActualRight = CalculateActualRight();
        Size = new SizeF(Size.Width, (float)borderBoxHeight);

        // Now that the grid has its final size and track edges, position any
        // absolutely-positioned grid items within their resolved grid areas.
        if (absposItems != null)
            PlaceAbsposGridItems(absposItems, colStartEdge, colEndEdge, rowStartEdge, rowEndEdge,
                colSizes.Length, rowSizes.Length, contentLeft, contentTop,
                colSpecs.Count, rowSpecs.Count, explicitColStart, explicitRowStart,
                colNames, rowNames, rtl);

        _gridTrackLayoutApplied = true;
        return true;
    }

    /// <summary>Set once the definite-track pass has positioned this grid's items,
    /// so the flex/grid cross-axis approximation does not re-align them.</summary>
    private bool _gridTrackLayoutApplied;

    /// <summary>
    /// An item's inline-axis content contribution (min-content, max-content) for
    /// track sizing. A percentage width resolves against the track being sized, so
    /// it contributes its content size (as if <c>auto</c>) rather than the resolved
    /// percentage; a fixed/auto width uses the standard measure.
    /// </summary>
    private static void GridItemInlineContribution(CssBox item, out double min, out double max)
    {
        string w = item.Width;
        if (!string.IsNullOrEmpty(w) && w.EndsWith("%", StringComparison.Ordinal))
            item.GetContentMinMaxWidth(out min, out max);
        else
            item.GetMinMaxWidth(out min, out max);
    }

    /// <summary>Total pixel span of tracks <c>[start, start+span)</c>, gaps included.</summary>
    private static double TrackSpan(double[] sizes, int start, int span, double gap)
    {
        double total = 0;
        for (int i = 0; i < span; i++)
        {
            int t = start + i;
            if (t >= 0 && t < sizes.Length)
                total += sizes[t];
        }
        if (span > 1)
            total += (span - 1) * gap;
        return total;
    }

    /// <summary>
    /// §8.5 grid item placement: resolves each item's final (row, column) origin.
    /// Works in generic (major, minor) coordinates so a single body serves both
    /// <c>grid-auto-flow: row</c> (major = row, minor = column) and
    /// <c>column</c> (major = column, minor = row).
    /// </summary>
    private static void PlaceItems(List<GridPlacement> items, int explicitCols, int explicitRows,
        bool flowRow, bool dense)
    {
        // Map (row, col) <-> (major, minor) for the active flow direction.
        int MajorStart(GridPlacement p) => (flowRow ? p.RowStart : p.ColStart) ?? -1;
        bool MajorAuto(GridPlacement p) => (flowRow ? p.RowStart : p.ColStart) == null;
        bool MinorAuto(GridPlacement p) => (flowRow ? p.ColStart : p.RowStart) == null;
        int MinorStart(GridPlacement p) => (flowRow ? p.ColStart : p.RowStart) ?? -1;
        int MajorSpan(GridPlacement p) => flowRow ? p.RowSpan : p.ColSpan;
        int MinorSpan(GridPlacement p) => flowRow ? p.ColSpan : p.RowSpan;

        var occupied = new HashSet<long>();
        long Key(int row, int col) => ((long)row << 20) ^ (uint)col;

        bool Fits(int major, int minor, int majorSpan, int minorSpan)
        {
            if (major < 0 || minor < 0)
                return false;
            for (int a = 0; a < majorSpan; a++)
                for (int i = 0; i < minorSpan; i++)
                {
                    int row = flowRow ? major + a : minor + i;
                    int col = flowRow ? minor + i : major + a;
                    if (occupied.Contains(Key(row, col)))
                        return false;
                }
            return true;
        }
        void Mark(int major, int minor, int majorSpan, int minorSpan)
        {
            for (int a = 0; a < majorSpan; a++)
                for (int i = 0; i < minorSpan; i++)
                {
                    int row = flowRow ? major + a : minor + i;
                    int col = flowRow ? minor + i : major + a;
                    occupied.Add(Key(row, col));
                }
        }
        void Commit(int index, int major, int minor)
        {
            var p = items[index];
            p.PlacedRow = flowRow ? major : minor;
            p.PlacedCol = flowRow ? minor : major;
            items[index] = p;
            Mark(major, minor, MajorSpan(p), MinorSpan(p));
        }

        // The number of tracks along the minor (fixed) axis. Extended by any
        // definite placement or auto span that reaches past the explicit grid.
        int minorCount = flowRow ? explicitCols : explicitRows;
        for (int i = 0; i < items.Count; i++)
        {
            var p = items[i];
            if (!MinorAuto(p))
                minorCount = Math.Max(minorCount, MinorStart(p) + MinorSpan(p));
            minorCount = Math.Max(minorCount, MinorSpan(p));
        }
        if (minorCount < 1) minorCount = 1;

        // Phase 1 — items definite in both axes.
        for (int i = 0; i < items.Count; i++)
        {
            var p = items[i];
            if (!MajorAuto(p) && !MinorAuto(p))
                Commit(i, MajorStart(p), MinorStart(p));
        }

        // Phase 2 — items locked to a major line (definite major, auto minor).
        int p2CursorMajor = -1, p2CursorMinor = 0;
        for (int i = 0; i < items.Count; i++)
        {
            var p = items[i];
            if (MajorAuto(p) || !MinorAuto(p))
                continue;
            int major = MajorStart(p);
            int minor;
            if (dense)
            {
                minor = 0;
            }
            else
            {
                if (p2CursorMajor != major) { p2CursorMajor = major; p2CursorMinor = 0; }
                minor = p2CursorMinor;
            }
            while (!Fits(major, minor, MajorSpan(p), MinorSpan(p)))
                minor++;
            Commit(i, major, minor);
            if (!dense) p2CursorMinor = minor + MinorSpan(p);
        }

        // Phase 4 — remaining items (auto major), in document order, sharing one
        // auto-placement cursor.
        int cursorMajor = 0, cursorMinor = 0;
        for (int i = 0; i < items.Count; i++)
        {
            var p = items[i];
            if (!MajorAuto(p))
                continue;

            if (dense) { cursorMajor = 0; cursorMinor = 0; }

            if (!MinorAuto(p))
            {
                // Definite minor, auto major: move the cursor to the item's minor
                // line — if that means moving backwards along the minor axis, the
                // cursor has wrapped, so advance the major line (§8.5 step 4). Then
                // walk the major axis forward until the item fits.
                int minor = MinorStart(p);
                if (!dense && minor < cursorMinor)
                    cursorMajor++;
                cursorMinor = minor;
                int major = cursorMajor;
                while (!Fits(major, minor, MajorSpan(p), MinorSpan(p)))
                    major++;
                Commit(i, major, minor);
                cursorMajor = major; // cursorMinor already equals the item's minor
            }
            else
            {
                // Auto in both axes: advance along the minor axis, wrapping to the
                // next major line when the item would overflow the minor track count.
                int major = cursorMajor, minor = cursorMinor;
                while (true)
                {
                    if (minor + MinorSpan(p) > minorCount)
                    {
                        minor = 0;
                        major++;
                        continue;
                    }
                    if (Fits(major, minor, MajorSpan(p), MinorSpan(p)))
                        break;
                    minor++;
                }
                Commit(i, major, minor);
                if (!dense) { cursorMajor = major; cursorMinor = minor + MinorSpan(p); }
            }
        }
    }

    /// <summary>
    /// Sizes and positions one grid item within its resolved grid area. A
    /// stretched or percentage-sized item fills the area (minus its margins); a
    /// fixed-size item keeps its size and is aligned to the area start. Empty
    /// items — the common check-layout case — carry no content to reflow, so the
    /// border box is set directly (mirroring <see cref="ApplyGridAutoPlacement"/>).
    /// </summary>
    private void PlaceItemInArea(CssBox item, double areaLeft, double areaTop, double areaWidth, double areaHeight)
    {
        double marginL = item.ActualMarginLeft, marginR = item.ActualMarginRight;
        double marginT = item.ActualMarginTop, marginB = item.ActualMarginBottom;

        bool widthFills = FillsArea(item.Width);
        bool heightFills = FillsArea(item.Height);

        double targetLeft = areaLeft + marginL;
        double targetTop = areaTop + marginT;

        double newWidth = item.Size.Width;
        if (widthFills)
        {
            double w = areaWidth - marginL - marginR;
            if (w > 0) newWidth = w;
        }
        double newHeight = item.Size.Height;
        // A replaced item with a definite block-size measured its height onto its
        // image word, so item.Size.Height is a stale 0 here; use the definite
        // block-size (column-independent) rather than filling or keeping the zero.
        double replacedDefiniteH = GridReplacedItemDefiniteBorderBoxHeight(item);
        if (replacedDefiniteH > 0)
            newHeight = replacedDefiniteH;
        else if (heightFills)
        {
            double h = areaHeight - marginT - marginB;
            if (h > 0) newHeight = h;
        }

        // Alignment of a non-filling item within its area (start by default).
        if (!widthFills)
        {
            double free = (areaWidth - marginL - marginR) - newWidth;
            if (free > 0.5)
                targetLeft += GridAxisAlignmentOffset(item.JustifySelf, JustifyItems, free, item.Direction == "rtl");
        }
        if (!heightFills)
        {
            double free = (areaHeight - marginT - marginB) - newHeight;
            if (free > 0.5)
                targetTop += GridAxisAlignmentOffset(item.AlignSelf, AlignItems, free, false);
        }

        double dx = targetLeft - item.Location.X;
        double dy = targetTop - item.Location.Y;
        if (Math.Abs(dx) > 0.01)
            item.OffsetLeft(dx);
        if (Math.Abs(dy) > 0.01)
            item.OffsetTop(dy);

        item.Size = new SizeF((float)newWidth, (float)newHeight);
        item.ActualRight = item.Location.X + newWidth;
        item.ActualBottom = item.Location.Y + newHeight;

        // When the grid container was laid out through the inline path
        // (ContainsInlinesOnly → CreateLineBoxes), each item div acquired a
        // per-line-box entry in its Rectangles map sized to the inline-block it
        // was measured as (typically the full container width/height). That map
        // is what the paint walker uses for the item's own background/border
        // (Fragment.InlineRects), so leaving it in place would paint the item at
        // its pre-grid inline size even though we just resized the border box.
        // Grid items are blockified (CSS Grid §4), so their background is always a
        // single border box — clear the stale inline rects and let paint fall
        // back to Location+Size.
        item.RectanglesReset();
    }

    /// <summary>
    /// CSS Grid §9 (Absolute Positioning): resolve each absolutely-positioned
    /// grid item's grid area from the sized tracks and position the item within
    /// it. The area — not the grid container's padding box — is the item's
    /// containing block, so an <c>auto</c> grid line resolves to the container's
    /// padding edge and specified lines resolve to grid line positions.
    /// </summary>
    private void PlaceAbsposGridItems(List<CssBox> items,
        double[] colStartEdge, double[] colEndEdge,
        double[] rowStartEdge, double[] rowEndEdge,
        int colCount, int rowCount, double contentLeft, double contentTop,
        int explicitCols, int explicitRows, int explicitColStart, int explicitRowStart,
        IReadOnlyDictionary<string, List<int>> colNames, IReadOnlyDictionary<string, List<int>> rowNames,
        bool rtl)
    {
        double padLeft = Location.X + ActualBorderLeftWidth;
        double padRight = Location.X + Size.Width - ActualBorderRightWidth;
        double padTop = Location.Y + ActualBorderTopWidth;
        double padBottom = ActualBottom - ActualBorderBottomWidth;

        foreach (var item in items)
        {
            var (colStartLine, colEndLine) = ParseAbsposGridLines(item.GridColumn, explicitCols, explicitColStart, colNames);
            var (rowStartLine, rowEndLine) = ParseAbsposGridLines(item.GridRow, explicitRows, explicitRowStart, rowNames);
            var (left, right) = ResolveAbsposAxis(colStartLine, colEndLine, colStartEdge, colEndEdge,
                colCount, contentLeft, padLeft, padRight);
            // rtl: the inline (column) axis runs right→left, so mirror the resolved
            // area around the containing block's padding box (CSS Grid §9.2).
            if (rtl)
            {
                double m = padLeft + padRight;
                (left, right) = (m - right, m - left);
            }
            var (top, bottom) = ResolveAbsposAxis(rowStartLine, rowEndLine, rowStartEdge, rowEndEdge,
                rowCount, contentTop, padTop, padBottom);
            PlaceAbsposItemInArea(item, left, top, right - left, bottom - top);
        }
    }

    /// <summary>
    /// CSS Grid §9.2: resolve an abspos grid item's <c>grid-column</c>/<c>grid-row</c>
    /// to its two edge lines, each as a 0-based track *boundary* index in the
    /// (leading-shifted) grid coordinate, or <c>null</c> for an <c>auto</c> line —
    /// which resolves to the grid container's padding edge, NOT a track line.
    /// Unlike the in-flow <see cref="ParseGridLine"/>, an <c>auto</c> here stays
    /// auto rather than collapsing into a span. Negative lines count back from the
    /// last explicit line; every resolved line is shifted by
    /// <paramref name="explicitStart"/> (the leading implicit track count) so it
    /// indexes the same coordinate as the sized tracks.
    /// </summary>
    private static (int? startLine, int? endLine) ParseAbsposGridLines(
        string value, int explicitTracks, int explicitStart,
        IReadOnlyDictionary<string, List<int>> names = null)
    {
        string v = (value ?? "").Trim();
        int slash = v.IndexOf('/');
        string startTok = slash < 0 ? v : v.Substring(0, slash).Trim();
        string endTok = slash < 0 ? "" : v.Substring(slash + 1).Trim();

        int? Line(string tok)
        {
            if (string.IsNullOrEmpty(tok) || tok.Equals("auto", StringComparison.OrdinalIgnoreCase)
                || tok.StartsWith("span", StringComparison.OrdinalIgnoreCase))
                return null;                    // auto / span -> padding edge (§9.2)
            if (int.TryParse(tok, NumberStyles.Integer, CultureInfo.InvariantCulture, out int line))
            {
                int b = line >= 1
                    ? line - 1                   // 1-based line -> 0-based boundary
                    : explicitTracks + 1 + line; // negative -> back from the last line
                return b + explicitStart;        // into the leading-shifted coordinate
            }
            // Named line: resolve against the template's line-name map, else auto.
            if (names != null)
            {
                int space = tok.IndexOf(' ');
                int nth = 1;
                string name = tok;
                if (space > 0 && int.TryParse(tok.Substring(0, space).Trim(),
                        NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedNth) && parsedNth >= 1)
                {
                    nth = parsedNth;
                    name = tok.Substring(space + 1).Trim();
                }
                if (names.TryGetValue(name, out var lines) && lines.Count > 0)
                    return lines[Math.Min(nth, lines.Count) - 1] + explicitStart;
            }
            return null;                         // unresolved named line -> auto
        }

        return (Line(startTok), Line(endTok));
    }

    /// <summary>
    /// Resolve the [start, end] extent (absolute px) of an abspos grid item's grid
    /// area along one axis from its two boundary lines. A <c>null</c> line resolves
    /// to the container's padding edge (CSS Grid §9.2); a line index is clamped into
    /// the grid's boundary range and mapped through the track edges.
    /// </summary>
    private static (double start, double end) ResolveAbsposAxis(int? startLine, int? endLine,
        double[] startEdge, double[] endEdge, int trackCount, double contentOrigin,
        double padStart, double padEnd)
    {
        double Boundary(int line)
        {
            int l = Math.Clamp(line, 0, trackCount);
            return contentOrigin + (l < trackCount ? startEdge[l] : endEdge[trackCount - 1]);
        }
        double s = startLine == null || trackCount == 0 ? padStart : Boundary(startLine.Value);
        double e = endLine == null || trackCount == 0 ? padEnd : Boundary(endLine.Value);
        return e < s ? (s, s) : (s, e);
    }

    /// <summary>
    /// CSS Grid §9: position an absolutely-positioned grid item within its
    /// resolved grid area, which forms the item's containing block. Insets
    /// (top/left/right/bottom) offset from the area edges; a percentage or fixed
    /// width/height resolves against the area, while an <c>auto</c> size keeps
    /// the item's shrink-to-fit measurement (grid abspos items align to
    /// <c>start</c>, not <c>stretch</c>, by default) unless both insets pin it.
    /// The area is recorded on the item so any later abspos re-resolution
    /// (<see cref="GetAbsoluteContainingBlockPaddingBox"/>) uses it too.
    /// </summary>
    private static void PlaceAbsposItemInArea(CssBox item,
        double areaLeft, double areaTop, double areaWidth, double areaHeight)
    {
        item.GridAreaContainingBlock =
            new RectangleF((float)areaLeft, (float)areaTop, (float)areaWidth, (float)areaHeight);
        double em = item.GetEmHeight();

        double marginL = item.ActualMarginLeft, marginR = item.ActualMarginRight;
        double? left = ParseAbsposInset(item.Left, areaWidth, em);
        double? right = ParseAbsposInset(item.Right, areaWidth, em);
        double width = ResolveAbsposSize(item.Width, areaWidth, em, item.Size.Width,
            left, right, marginL, marginR, out bool widthAuto);
        if (!widthAuto) width = item.ResolveSpecifiedWidthToBorderBox(width);
        double x;
        if (left.HasValue) x = areaLeft + left.Value + marginL;
        else if (right.HasValue) x = areaLeft + areaWidth - right.Value - marginR - width;
        else x = areaLeft + marginL;

        double marginT = item.ActualMarginTop, marginB = item.ActualMarginBottom;
        double? top = ParseAbsposInset(item.Top, areaHeight, em);
        double? bottom = ParseAbsposInset(item.Bottom, areaHeight, em);
        double height = ResolveAbsposSize(item.Height, areaHeight, em, item.Size.Height,
            top, bottom, marginT, marginB, out bool heightAuto);
        if (!heightAuto) height = item.ResolveSpecifiedHeightToBorderBox(height);
        double y;
        if (top.HasValue) y = areaTop + top.Value + marginT;
        else if (bottom.HasValue) y = areaTop + areaHeight - bottom.Value - marginB - height;
        else y = areaTop + marginT;

        double dx = x - item.Location.X;
        double dy = y - item.Location.Y;
        if (Math.Abs(dx) > 0.01) item.OffsetLeft(dx);
        if (Math.Abs(dy) > 0.01) item.OffsetTop(dy);
        item.Size = new SizeF((float)Math.Max(0, width), (float)Math.Max(0, height));
        item.ActualRight = item.Location.X + item.Size.Width;
        item.ActualBottom = item.Location.Y + item.Size.Height;
        item.AbsposLocationFinalized = true;
        item.RectanglesReset();
    }

    /// <summary>An inset (top/left/right/bottom): null for <c>auto</c>, else px
    /// resolved against the grid area extent <paramref name="basis"/>.</summary>
    private static double? ParseAbsposInset(string value, double basis, double em)
    {
        if (string.IsNullOrEmpty(value) || value == CssConstants.Auto)
            return null;
        return CssValueParser.ParseLength(value, basis, em);
    }

    /// <summary>
    /// Used inline/block size of an abspos grid item along one axis. A fixed or
    /// percentage size resolves against the area; <c>auto</c> (or an intrinsic
    /// keyword) keeps the shrink-to-fit measurement, except when both insets are
    /// specified (CSS2.1 §10.3.7/§10.6.4), where the size fills the remaining gap.
    /// </summary>
    private static double ResolveAbsposSize(string size, double areaSize, double em,
        double measured, double? startInset, double? endInset,
        double marginStart, double marginEnd, out bool wasAuto)
    {
        if (!string.IsNullOrEmpty(size) && size != CssConstants.Auto
            && !IsIntrinsicWidthKeyword(size))
        {
            wasAuto = false;
            return CssValueParser.ParseLength(size, areaSize, em);
        }
        wasAuto = true;
        if (startInset.HasValue && endInset.HasValue)
        {
            double gap = areaSize - startInset.Value - endInset.Value - marginStart - marginEnd;
            return gap > 0 ? gap : 0;
        }
        return measured;
    }

    // ─────────────────────────────── Subgrid ────────────────────────────────

    /// <summary>
    /// CSS Grid Level 2 §7.3: resolve a subgrid item's inherited tracks from this
    /// grid's sized tracks over the item's spanned area, then re-run the item's
    /// own grid layout so its children flow into those tracks. A no-op unless the
    /// item is itself a grid container declaring <c>subgrid</c> on an axis.
    /// </summary>
    private void LayoutSubgridItem(GridPlacement p, double[] colSizes, double[] rowSizes,
        double colGap, double rowGap)
    {
        var item = p.Item;
        if (item.Display is not ("grid" or "inline-grid"))
            return;
        bool colSub = StartsWithSubgrid(item.GridTemplateColumns);
        bool rowSub = StartsWithSubgrid(item.GridTemplateRows);
        if (!colSub && !rowSub)
            return;

        if (colSub)
        {
            item.SubgridColumnSizes = SliceTrackSizes(colSizes, p.PlacedCol, p.ColSpan);
            item.SubgridColumnGap = colGap;
        }
        if (rowSub)
        {
            item.SubgridRowSizes = SliceTrackSizes(rowSizes, p.PlacedRow, p.RowSpan);
            item.SubgridRowGap = rowGap;
        }
        item.TryApplyGridTrackLayout();
    }

    /// <summary>The <paramref name="span"/> track sizes starting at <paramref name="start"/>
    /// (clamped to the available tracks).</summary>
    private static double[] SliceTrackSizes(double[] sizes, int start, int span)
    {
        int n = Math.Max(0, Math.Min(span, sizes.Length - start));
        var slice = new double[n];
        for (int i = 0; i < n; i++)
            slice[i] = sizes[start + i];
        return slice;
    }

    /// <summary>True when a track template's first token is the <c>subgrid</c> keyword.</summary>
    private static bool StartsWithSubgrid(string template)
    {
        if (string.IsNullOrWhiteSpace(template))
            return false;
        string t = template.TrimStart();
        return t.StartsWith("subgrid", StringComparison.OrdinalIgnoreCase)
            && (t.Length == 7 || (!char.IsLetterOrDigit(t[7]) && t[7] != '-'));
    }

    /// <summary>True for a <c>none</c>/empty track template (no explicit tracks).</summary>
    private static bool IsNoneOrEmptyTemplate(string template)
        => string.IsNullOrWhiteSpace(template)
            || template.Trim().Equals("none", StringComparison.OrdinalIgnoreCase);

    /// <summary>True for a bare <c>auto</c> track template (one auto-sized track).</summary>
    private static bool IsBareAutoTemplate(string template)
        => template != null && template.Trim().Equals("auto", StringComparison.OrdinalIgnoreCase);

    /// <summary>A single <c>auto</c> min/max track sizing function.</summary>
    private static GridTrackSpec AutoTrackSpec
        => new(new GridSize(GridSizeKind.Auto), new GridSize(GridSizeKind.Auto));

    /// <summary>Build fixed (px) track-sizing functions from inherited subgrid sizes.</summary>
    private static List<GridTrackSpec> FixedTrackSpecs(double[] sizes)
    {
        if (sizes == null)
            return null;
        var specs = new List<GridTrackSpec>(sizes.Length);
        foreach (var s in sizes)
        {
            var g = new GridSize(GridSizeKind.Fixed, s);
            specs.Add(new GridTrackSpec(g, g));
        }
        return specs;
    }

    /// <summary>
    /// True when a grid item should stretch to fill its area along an axis: an
    /// <c>auto</c> or percentage used size under the default stretch alignment.
    /// (The check-layout tests use <c>width:100%;height:100%</c>.)
    /// </summary>
    private static bool FillsArea(string size)
    {
        if (string.IsNullOrEmpty(size) || size == CssConstants.Auto)
            return true;
        return size.EndsWith("%", StringComparison.Ordinal);
    }

    /// <summary>
    /// True when this grid or any of its in-flow items requests baseline
    /// self-alignment on either axis (<c>align-items</c>/<c>justify-items</c> on
    /// the container, or <c>align-self</c>/<c>justify-self</c> on an item). Baseline
    /// alignment needs a synthesized shared baseline the bounded track pass does not
    /// compute, so callers use this to decline to the baseline-aware approximation.
    /// </summary>
    private bool GridUsesBaselineSelfAlignment()
    {
        if (MentionsBaseline(AlignItems) || MentionsBaseline(JustifyItems))
            return true;
        foreach (var child in Boxes)
        {
            if (child.Display == CssConstants.None)
                continue;
            if (child.Position == CssConstants.Absolute || child.Position == CssConstants.Fixed)
                continue;
            if (MentionsBaseline(child.AlignSelf) || MentionsBaseline(child.JustifySelf))
                return true;
        }
        return false;
    }

    /// <summary>True when an alignment value is (or ends with) the
    /// <c>baseline</c> keyword — covering <c>baseline</c>, <c>first baseline</c>,
    /// and <c>last baseline</c>.</summary>
    private static bool MentionsBaseline(string value)
        => !string.IsNullOrEmpty(value)
            && value.IndexOf("baseline", StringComparison.OrdinalIgnoreCase) >= 0;

    /// <summary>
    /// True when every in-flow item of this grid is a plain box whose used block
    /// size the bounded track pass can trust from a single measurement — used to
    /// gate the implicit-only takeover away from item shapes the pass sizes worse
    /// than the approximation it replaces (see the caller). An item is *not* simple
    /// when it establishes a nested flex/grid/table formatting context (whose
    /// intrinsic block size the single measurement can under-size — collapsing an
    /// auto row), is a replaced element or form control (whose stretch/aspect-ratio
    /// sizing differs from a plain box), or is a scroll container. A plain block
    /// item with <c>height:100%</c> is simple: that just stretches it to the row.
    /// </summary>
    private bool GridImplicitPathItemsAreSimple()
    {
        foreach (var child in Boxes)
        {
            if (child.Display == CssConstants.None)
                continue;
            if (child.Position == CssConstants.Absolute || child.Position == CssConstants.Fixed)
                continue;
            if (child.Display is "flex" or "inline-flex" or "grid" or "inline-grid"
                or CssConstants.Table or CssConstants.InlineTable)
                return false;
            bool isImg = child.IsImage
                || (child.HtmlTag != null && child.HtmlTag.Name.Equals("img", StringComparison.OrdinalIgnoreCase));
            if (isImg)
            {
                // A replaced image whose block-size (height) is a definite length
                // — `block-size:55vw`, `height:200px`, … — can size an auto row
                // safely: its used height has no reflow dependence on the resolved
                // column width, so the row's measured-height contribution is stable
                // (WPT css-grid/nested-grid-item-block-size-001, whose img nested in
                // `display:grid` otherwise collapses to height 0 under the fallback
                // approximation). An image with an indefinite/percentage/ratio-only
                // block size still declines — its height would change with the
                // column, which this bounded pass measures once and cannot re-solve.
                if (!GridReplacedItemHasDefiniteBlockSize(child))
                    return false;
                continue;
            }
            if (child.HtmlTag != null)
            {
                string tag = child.HtmlTag.Name;
                if (tag.Equals("button", StringComparison.OrdinalIgnoreCase)
                    || tag.Equals("input", StringComparison.OrdinalIgnoreCase)
                    || tag.Equals("select", StringComparison.OrdinalIgnoreCase)
                    || tag.Equals("textarea", StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            if (!string.IsNullOrEmpty(child.Overflow) && child.Overflow != CssConstants.Visible)
                return false;
        }
        return true;
    }

    /// <summary>
    /// True when a replaced (image) grid item carries a definite block-size — a
    /// non-auto, non-percentage length on its block axis (the <c>Height</c> getter
    /// maps <c>block-size</c>/writing-mode onto physical height), resolved through
    /// the length parser so viewport/font units (<c>55vw</c>, <c>10rem</c>) count.
    /// Such an item's used height is fixed independent of its resolved column
    /// width, so sizing an auto row from its single measurement is safe — unlike a
    /// ratio-only or percentage-height image whose height follows the column.
    /// </summary>
    private static bool GridReplacedItemHasDefiniteBlockSize(CssBox item) =>
        CssLayoutEngine.TryResolveDefiniteImageLength(item.Height, item.GetEmHeight(), out _);

    /// <summary>
    /// CSS Grid §5.1 / Sizing 3: the grid container's intrinsic (min-content when
    /// <paramref name="useMax"/> is false, else max-content) **content-box** width —
    /// the sum of its <c>grid-template-columns</c> track sizes plus gaps, for a
    /// horizontal writing mode (a vertical grid's physical-width axis is the rows,
    /// applied through the rotation — declined below). Bounded to templates whose
    /// tracks are all definite fixed lengths (a plain length or a <c>minmax()</c>
    /// with fixed sides — <c>repeat(&lt;int&gt;,…)</c> already expanded by
    /// <see cref="ParseTrackList"/>): declines for <c>fr</c>/<c>auto</c>/
    /// content/percentage/<c>auto-fill</c> tracks, whose size needs the real track
    /// pass (and the items). Lets a shrink-to-fit grid (float, <c>inline-grid</c>,
    /// <c>fit-content</c>/<c>min-content</c>/<c>max-content</c>) size to its tracks
    /// instead of collapsing to its (often empty) inline content
    /// (WPT css-grid grid-gutters-and-tracks-001, …-margin-border-padding-vertical-rl).
    /// </summary>
    private bool TryComputeGridIntrinsicContentWidth(bool useMax, out double contentWidth)
    {
        contentWidth = 0;
        if (Display is not ("grid" or "inline-grid"))
            return false;
        // Vertical writing modes lay the grid out in a logical frame and rotate it,
        // so the physical-width axis (rows) must be applied through the rotation —
        // out of scope here; a vertical grid keeps its existing sizing.
        if (IsVerticalWritingMode(WritingMode))
            return false;
        double em = GetEmHeight();
        var specs = ParseTrackList(GridTemplateColumns, em);
        if (specs == null || specs.Count == 0 || specs.Count > MaxGridLine)
            return false;

        double sum = 0;
        foreach (var spec in specs)
        {
            GridSize side = useMax ? spec.Max : spec.Min;
            if (side.Kind != GridSizeKind.Fixed)
                return false;                 // needs the real track pass
            sum += Math.Max(0, side.Value);
        }
        double gap = ResolveGridGap(ColumnGap, 0, em);
        sum += gap * (specs.Count - 1);
        contentWidth = sum;
        return true;
    }

    /// <summary>
    /// The definite border-box block-size (height) of a replaced (image) grid
    /// item, or <c>0</c> when its block size is not a definite length. A replaced
    /// element measured through its container's line box (the inline path used for
    /// a grid holding an <c>&lt;img&gt;</c>) records its height on its image word,
    /// not on the box's <c>ActualBottom</c> — which stays <c>0</c> — so the track
    /// pass must read the definite block-size straight from the declaration to size
    /// its auto row and place the item, rather than the stale zero measurement.
    /// The specified <c>block-size</c> is the content box (content-box sizing) or
    /// the border box (border-box sizing).
    /// </summary>
    private static double GridReplacedItemDefiniteBorderBoxHeight(CssBox item)
    {
        if (!item.IsImage
            && !(item.HtmlTag != null && item.HtmlTag.Name.Equals("img", StringComparison.OrdinalIgnoreCase)))
            return 0;
        if (!CssLayoutEngine.TryResolveDefiniteImageLength(item.Height, item.GetEmHeight(), out double h))
            return 0;
        if (!item.UsesBorderBoxSizing)
            h += item.ActualPaddingTop + item.ActualPaddingBottom
                + item.ActualBorderTopWidth + item.ActualBorderBottomWidth;
        return h > 0 ? h : 0;
    }

    private static double GridAxisAlignmentOffset(string self, string items, double free, bool rtl)
    {
        string v = self;
        if (string.IsNullOrEmpty(v) || v == "auto" || v == "normal")
            v = items;
        v = (v ?? "").Trim().ToLowerInvariant();
        if (v.StartsWith("safe ")) v = v.Substring(5).Trim();
        else if (v.StartsWith("unsafe ")) v = v.Substring(7).Trim();
        switch (v)
        {
            case "center": return free / 2;
            case "end":
            case "flex-end":
            case "self-end": return rtl ? 0 : free;
            case "right": return free;
            default: return rtl ? free : 0; // start / normal / stretch already handled
        }
    }

    // ─────────────────────────── Track-list parsing ───────────────────────────

    /// <summary>
    /// Parses a track list (<c>&lt;track-size&gt;</c> values and
    /// <c>repeat(&lt;int&gt;, …)</c>) into sizing functions. Returns <c>null</c> for
    /// <c>none</c>/empty or when any out-of-scope token appears (<c>subgrid</c>,
    /// <c>fit-content()</c>, <c>repeat(auto-fill/auto-fit, …)</c>) — the signal to
    /// decline the pass and use the existing approximation.
    /// </summary>
    private static List<GridTrackSpec> ParseTrackList(string value, double em)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        string v = value.Trim();
        if (v.Equals("none", StringComparison.OrdinalIgnoreCase)
            || v.Equals("auto", StringComparison.OrdinalIgnoreCase)
            || v.IndexOf("subgrid", StringComparison.OrdinalIgnoreCase) >= 0
            || v.IndexOf("masonry", StringComparison.OrdinalIgnoreCase) >= 0)
            return null;

        var specs = new List<GridTrackSpec>();
        return ParseTrackTokens(v, specs, depth: 0, em) ? specs : null;
    }

    /// <summary>
    /// Parses a track list, expanding a top-level <c>repeat(auto-fill, …)</c> to a
    /// concrete list of tracks from the axis's available size. Templates without an
    /// auto-repeat fall through to <see cref="ParseTrackList(string,double)"/>
    /// unchanged; an auto-repeat that cannot be resolved (non-fixed repeated tracks,
    /// <c>auto-fit</c>, or combined with subgrid) returns <c>null</c> to decline.
    /// </summary>
    private List<GridTrackSpec> ParseTrackListMaybeAutoRepeat(string value, double em,
        double availableSize, double gap, bool fillMinimum = false)
    {
        var expanded = ExpandAutoRepeatTrackList(value, em, availableSize, gap, fillMinimum, out bool hasAutoRepeat);
        if (hasAutoRepeat)
            return expanded;               // resolved list, or null to decline
        return ParseTrackList(value, em);  // no auto-repeat — ordinary parse
    }

    /// <summary>
    /// CSS Grid §7.2.3.1 (repeat-to-fill): when <paramref name="value"/> contains a
    /// top-level <c>repeat(auto-fill, &lt;fixed-track-list&gt;)</c>, computes the
    /// number of repetitions that fit in <paramref name="availableSize"/> (gaps
    /// included) and returns the fully expanded track list. Sets
    /// <paramref name="hasAutoRepeat"/> only when a genuine auto-repeat is present
    /// (a <c>repeat()</c> counted by auto-fill/auto-fit — not the keyword inside a
    /// line name); returns <c>null</c> (declining the pass) for an auto-repeat this
    /// bounded pass cannot resolve — <c>auto-fit</c> (empty-track collapsing is
    /// unmodelled), non-fixed repeated tracks, or a repeated block that would exceed
    /// <see cref="MaxGridLine"/> tracks.
    /// </summary>
    private List<GridTrackSpec> ExpandAutoRepeatTrackList(string value, double em,
        double availableSize, double gap, bool fillMinimum, out bool hasAutoRepeat)
    {
        hasAutoRepeat = false;
        if (string.IsNullOrWhiteSpace(value))
            return null;
        string v = value.Trim();
        // Fast path: auto-fill/auto-fit as a repeat count must spell the keyword, so
        // its absence proves there is no auto-repeat (this method is only reached off
        // the non-subgrid track-parsing path).
        if (v.IndexOf("auto-fill", StringComparison.OrdinalIgnoreCase) < 0
            && v.IndexOf("auto-fit", StringComparison.OrdinalIgnoreCase) < 0)
            return null;                    // no auto-repeat — caller uses ParseTrackList

        var before = new List<GridTrackSpec>();
        var auto = new List<GridTrackSpec>();
        var after = new List<GridTrackSpec>();
        bool seenAuto = false;

        int i = 0, n = v.Length;
        while (i < n)
        {
            while (i < n && char.IsWhiteSpace(v[i])) i++;
            if (i >= n) break;
            if (v[i] == '[')                // named-line brackets carry no size
            {
                int close = v.IndexOf(']', i);
                if (close < 0) return null;
                i = close + 1;
                continue;
            }
            int start = i, paren = 0;
            while (i < n && (paren > 0 || !char.IsWhiteSpace(v[i])))
            {
                if (v[i] == '(') paren++;
                else if (v[i] == ')') paren--;
                i++;
            }
            string token = v.Substring(start, i - start);
            if (token.Length == 0) continue;

            // Only a repeat() whose *count* argument is auto-fill/auto-fit is an
            // auto-repeat; the keyword appearing inside a line name (e.g.
            // [auto-fill-start]) is not, and must fall through to the ordinary parse.
            string autoKind = AutoRepeatCountKind(token);
            if (autoKind == "auto-fit")
            {
                hasAutoRepeat = true;       // empty-track collapsing unmodelled → decline
                return null;
            }
            if (autoKind == "auto-fill")
            {
                if (seenAuto)
                    return null;            // at most one auto-repeat per track list
                seenAuto = true;
                string inner = token.Substring(7, token.Length - 8);
                int comma = inner.IndexOf(',');
                if (comma < 0) return null;
                if (!ParseTrackTokens(inner.Substring(comma + 1), auto, depth: 1, em))
                    return null;
            }
            else
            {
                var bucket = seenAuto ? after : before;
                if (!ParseTrackTokens(token, bucket, depth: 1, em))
                    return null;            // bad fixed token / integer repeat
            }
        }

        if (!seenAuto || auto.Count == 0)
            return null;                    // keyword was only a line name → not auto-repeat
        hasAutoRepeat = true;

        // Every track in the repeated block must have a definite (fixed) size — the
        // repeat count is undefined otherwise (§7.2.3.1).
        double sumAuto = 0;
        foreach (var s in auto)
        {
            if (!TryFixedTrackSize(s, out double px)) return null;
            sumAuto += px;
        }
        double sumFixed = 0;
        foreach (var s in before) sumFixed += TryFixedTrackSize(s, out double px) ? px : 0;
        foreach (var s in after) sumFixed += TryFixedTrackSize(s, out double px) ? px : 0;

        int nFixed = before.Count + after.Count;
        int nAuto = auto.Count;

        // When available is a definite size, the largest k ≥ 1 that does not overflow
        // it (floor); when it is a definite *min* size (fillMinimum), the smallest
        // k ≥ 1 that reaches it (ceil) — §7.2.3.2. Both from
        // sumFixed + k·sumAuto + (nFixed + k·nAuto − 1)·gap  vs  available.
        int k = 1;
        double denom = sumAuto + nAuto * gap;
        if (availableSize > 0 && !double.IsInfinity(availableSize) && !double.IsNaN(availableSize)
            && denom > 0)
        {
            double numer = availableSize - sumFixed - (nFixed - 1) * gap;
            double kf = numer / denom;
            k = fillMinimum
                ? (kf > 1 ? (int)Math.Ceiling(kf - 1e-6) : 1)
                : (kf >= 1 ? (int)Math.Floor(kf + 1e-6) : 1);
        }
        if (k < 1) k = 1;
        // Bound the expansion so a tiny track in a large area cannot allocate a
        // pathological number of tracks.
        long total = (long)nFixed + (long)k * nAuto;
        if (total > MaxGridLine)
            return null;

        var result = new List<GridTrackSpec>(before.Count + k * nAuto + after.Count);
        result.AddRange(before);
        for (int r = 0; r < k; r++)
            result.AddRange(auto);
        result.AddRange(after);
        return result;
    }

    /// <summary>
    /// Classifies a single track-list token: returns <c>"auto-fill"</c> or
    /// <c>"auto-fit"</c> when it is a <c>repeat()</c> whose first (count) argument is
    /// that keyword, else <c>null</c>. Distinguishes a genuine auto-repeat from the
    /// keyword merely occurring inside a line-name token.
    /// </summary>
    private static string AutoRepeatCountKind(string token)
    {
        if (!token.StartsWith("repeat(", StringComparison.OrdinalIgnoreCase)
            || !token.EndsWith(")", StringComparison.Ordinal))
            return null;
        string inner = token.Substring(7, token.Length - 8);
        int comma = inner.IndexOf(',');
        if (comma < 0) return null;
        string count = inner.Substring(0, comma).Trim();
        if (count.Equals("auto-fill", StringComparison.OrdinalIgnoreCase)) return "auto-fill";
        if (count.Equals("auto-fit", StringComparison.OrdinalIgnoreCase)) return "auto-fit";
        return null;
    }

    /// <summary>
    /// The definite pixel size of a single-side fixed track (used for repeat-to-fill
    /// counting): a fixed min/max, or a <c>minmax()</c> whose max — else min — is a
    /// fixed length. Returns <c>false</c> for intrinsic/flex tracks, whose size is
    /// not definite until layout.
    /// </summary>
    private static bool TryFixedTrackSize(GridTrackSpec spec, out double px)
    {
        if (spec.Max.Kind == GridSizeKind.Fixed) { px = Math.Max(0, spec.Max.Value); return true; }
        if (spec.Min.Kind == GridSizeKind.Fixed) { px = Math.Max(0, spec.Min.Value); return true; }
        px = 0;
        return false;
    }

    /// <summary>
    /// Available content-box block size for resolving <c>repeat(auto-fill, …)</c>
    /// row counts: the definite content height when <c>height</c> is a length,
    /// raised to a definite <c>min-height</c> (each reduced to the content box under
    /// <c>box-sizing:border-box</c>). Returns 0 when the block size is indefinite,
    /// which resolves auto-fill to a single repetition.
    /// </summary>
    private double ComputeAutoRepeatBlockSize(double em, out bool fromMinConstraint)
    {
        // A definite (used) block size fills the repeat with the largest count that
        // does not overflow it (§7.2.3.2 — floor); an *indefinite* block size with a
        // definite min-height instead fills with the smallest count that reaches the
        // minimum (ceil). Report which case produced the returned size so the caller
        // rounds the repeat count the correct way.
        fromMinConstraint = false;
        bool hasDefinite = TryGetDefiniteContentHeight(em, out double dh);
        double h = hasDefinite ? dh : 0;
        if (!string.IsNullOrEmpty(MinHeight) && MinHeight != "0"
            && !MinHeight.Equals(CssConstants.Auto, StringComparison.OrdinalIgnoreCase)
            && !MinHeight.EndsWith("%", StringComparison.Ordinal))
        {
            double mh = CssValueParser.ParseLength(MinHeight, 0, em);
            if (!double.IsNaN(mh) && !double.IsInfinity(mh) && mh > 0)
            {
                if (UsesBorderBoxSizing)
                    mh -= ActualPaddingTop + ActualPaddingBottom
                        + ActualBorderTopWidth + ActualBorderBottomWidth;
                if (mh > h)
                {
                    h = mh;
                    // Only a min-height with no definite used height fills to the
                    // minimum (ceil); when a definite height exists the used size is
                    // definite even if the min raised it, so it still floors.
                    if (!hasDefinite) fromMinConstraint = true;
                }
            }
        }
        return h > 0 ? h : 0;
    }

    private static bool ParseTrackTokens(string v, List<GridTrackSpec> specs, int depth, double em)
    {
        if (depth > 4)
            return false; // guard against pathological nesting
        int i = 0, n = v.Length;
        while (i < n)
        {
            while (i < n && char.IsWhiteSpace(v[i])) i++;
            if (i >= n) break;

            // Named-line brackets ([name]) carry no size — skip them.
            if (v[i] == '[')
            {
                int close = v.IndexOf(']', i);
                if (close < 0) return false;
                i = close + 1;
                continue;
            }

            // Read one token, respecting parentheses (repeat(...)/minmax(...)).
            int start = i;
            int paren = 0;
            while (i < n && (paren > 0 || !char.IsWhiteSpace(v[i])))
            {
                if (v[i] == '(') paren++;
                else if (v[i] == ')') paren--;
                i++;
            }
            string token = v.Substring(start, i - start);
            if (token.Length == 0) continue;

            if (token.StartsWith("repeat(", StringComparison.OrdinalIgnoreCase) && token.EndsWith(")"))
            {
                string inner = token.Substring(7, token.Length - 8);
                int comma = inner.IndexOf(',');
                if (comma < 0) return false;
                string countStr = inner.Substring(0, comma).Trim();
                if (!int.TryParse(countStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count)
                    || count < 1 || count > 1000)
                    return false; // auto-fill / auto-fit / bad count — decline
                var repeated = new List<GridTrackSpec>();
                if (!ParseTrackTokens(inner.Substring(comma + 1), repeated, depth + 1, em))
                    return false;
                for (int r = 0; r < count; r++)
                    specs.AddRange(repeated);
                continue;
            }

            if (!TryParseTrackSpec(token, em, out GridTrackSpec spec))
                return false;
            specs.Add(spec);
        }
        return specs.Count > 0;
    }

    /// <summary>Parses one <c>&lt;track-size&gt;</c> token into a sizing function.</summary>
    private static bool TryParseTrackSpec(string token, double em, out GridTrackSpec spec)
    {
        spec = default;
        string t = token.Trim();
        if (t.Length == 0)
            return false;

        if (t.StartsWith("minmax(", StringComparison.OrdinalIgnoreCase) && t.EndsWith(")"))
        {
            string inner = t.Substring(7, t.Length - 8);
            int comma = SplitTopLevelComma(inner);
            if (comma < 0) return false;
            if (!TryParseGridSize(inner.Substring(0, comma).Trim(), allowFr: false, em, out GridSize min))
                return false;
            if (!TryParseGridSize(inner.Substring(comma + 1).Trim(), allowFr: true, em, out GridSize max))
                return false;
            spec = new GridTrackSpec(min, max);
            return true;
        }

        // fit-content() and any other functional notation are out of scope.
        if (t.IndexOf('(') >= 0)
            return false;

        if (!TryParseGridSize(t, allowFr: true, em, out GridSize size))
            return false;
        // A standalone <flex> implies an automatic minimum: minmax(auto, <flex>).
        if (size.Kind == GridSizeKind.Fr)
            spec = new GridTrackSpec(new GridSize(GridSizeKind.Auto), size);
        else
            spec = new GridTrackSpec(size, size);
        return true;
    }

    private static bool TryParseGridSize(string token, bool allowFr, double em, out GridSize size)
    {
        size = default;
        string t = token.Trim().ToLowerInvariant();
        if (t.Length == 0)
            return false;
        switch (t)
        {
            case "auto": size = new GridSize(GridSizeKind.Auto); return true;
            case "min-content": size = new GridSize(GridSizeKind.MinContent); return true;
            case "max-content": size = new GridSize(GridSizeKind.MaxContent); return true;
        }
        if (t.EndsWith("fr", StringComparison.Ordinal))
        {
            if (!allowFr) return false;
            if (double.TryParse(t.Substring(0, t.Length - 2), NumberStyles.Float, CultureInfo.InvariantCulture, out double fr)
                && fr >= 0)
            {
                size = new GridSize(GridSizeKind.Fr, fr);
                return true;
            }
            return false;
        }
        if (t.EndsWith("%", StringComparison.Ordinal))
        {
            // Percentage magnitude; resolved against the axis basis at sizing time.
            if (double.TryParse(t.Substring(0, t.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out double pct)
                && pct >= 0)
            {
                size = new GridSize(GridSizeKind.Percent, pct);
                return true;
            }
            return false;
        }
        if (t.IndexOf('(') >= 0)
            return false;
        // A <length>. Resolve em-relative and absolute units to pixels now (the
        // percent basis is irrelevant for a non-percentage length).
        double px = CssValueParser.ParseLength(t, 0, em);
        if (double.IsNaN(px) || double.IsInfinity(px) || px < 0)
            return false;
        size = new GridSize(GridSizeKind.Fixed, px);
        return true;
    }

    private static int SplitTopLevelComma(string s)
    {
        int paren = 0;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '(') paren++;
            else if (c == ')') paren--;
            else if (c == ',' && paren == 0) return i;
        }
        return -1;
    }

    /// <summary>Grid gap: <c>normal</c>/empty computes to 0 (unlike multicol).</summary>
    private double ResolveGridGap(string gap, double percentBasis, double em)
    {
        if (string.IsNullOrEmpty(gap) || gap == "normal")
            return 0;
        double v = CssValueParser.ParseLength(gap, percentBasis, em);
        return v > 0 ? v : 0;
    }

    private static GridTrackSpec ParseSingleImplicitSpec(string autoTracks, double em)
    {
        if (!string.IsNullOrWhiteSpace(autoTracks))
        {
            string first = FirstToken(autoTracks.Trim());
            if (TryParseTrackSpec(first, em, out GridTrackSpec spec))
                return spec;
        }
        // Default grid-auto-rows/columns is 'auto'.
        return new GridTrackSpec(new GridSize(GridSizeKind.Auto), new GridSize(GridSizeKind.Auto));
    }

    private static string FirstToken(string v)
    {
        int i = 0, paren = 0;
        while (i < v.Length && (paren > 0 || !char.IsWhiteSpace(v[i])))
        {
            if (v[i] == '(') paren++;
            else if (v[i] == ')') paren--;
            i++;
        }
        return v.Substring(0, i);
    }

    /// <summary>
    /// True when a row's sizing function is content-based (auto/min-content/
    /// max-content in its max), so the item's measured height feeds the track
    /// size and must not have been invalidated by a narrowed column.
    /// </summary>
    private static bool RowNeedsMeasuredHeight(List<GridTrackSpec> rowSpecs, GridTrackSpec implicitRow,
        int start, int span, bool rowDefinite)
    {
        for (int i = 0; i < span; i++)
        {
            int t = start + i;
            GridTrackSpec s = t < rowSpecs.Count ? rowSpecs[t] : implicitRow;
            if (s.Max.IsIntrinsic || s.Min.IsIntrinsic)
                return true;
            // A percentage row against an indefinite height resolves to 'auto',
            // so it too is content-sized from the measured height.
            if (!rowDefinite && (s.Max.Kind == GridSizeKind.Percent || s.Min.Kind == GridSizeKind.Percent))
                return true;
        }
        return false;
    }

    /// <summary>
    /// True with the definite content-box height when 'height' is an explicit
    /// length (not auto/empty and not a percentage — a percentage would need a
    /// definite containing block, out of scope for this bounded pass). For
    /// <c>box-sizing:border-box</c> the specified height includes padding+border,
    /// so those are subtracted to yield the content box.
    /// </summary>
    private bool TryGetDefiniteContentHeight(double em, out double contentHeight)
    {
        contentHeight = 0;
        if (string.IsNullOrEmpty(Height) || Height == CssConstants.Auto
            || Height.EndsWith("%", StringComparison.Ordinal))
            return false;
        double h = CssValueParser.ParseLength(Height, 0, em);
        if (double.IsNaN(h) || double.IsInfinity(h) || h <= 0)
            return false;
        if (UsesBorderBoxSizing)
            h -= ActualPaddingTop + ActualPaddingBottom + ActualBorderTopWidth + ActualBorderBottomWidth;
        if (h <= 0)
            return false;
        contentHeight = h;
        return true;
    }

    /// <summary>Total pixel extent of all <paramref name="sizes"/> plus the gaps
    /// between them — the grid's content-box size along that axis.</summary>
    private static double SumTrackSizes(double[] sizes, double gap)
    {
        if (sizes == null || sizes.Length == 0) return 0;
        double total = 0;
        foreach (double s in sizes) total += s;
        total += (sizes.Length - 1) * gap;
        return total > 0 ? total : 0;
    }

    /// <summary>
    /// CSS Grid §7.2.1: re-resolve pure-percentage row tracks against the grid's
    /// computed intrinsic block size after the indefinite-height pass sized them as
    /// 'auto'. Only a track sized purely by percentage (min and max the same
    /// percentage — i.e. <c>grid-template-rows: 60%</c>) becomes definite here;
    /// mixed intrinsic/percentage tracks keep their auto-derived size.
    /// </summary>
    private static void ResolvePercentRowTracksAgainstIntrinsic(List<GridTrackSpec> specs,
        GridTrackSpec implicitSpec, double[] sizes, double intrinsicHeight)
    {
        if (sizes == null || intrinsicHeight < 0) return;
        for (int t = 0; t < sizes.Length; t++)
        {
            GridTrackSpec spec = t < specs.Count ? specs[t] : implicitSpec;
            if (spec.Min.Kind == GridSizeKind.Percent && spec.Max.Kind == GridSizeKind.Percent
                && spec.Min.Value == spec.Max.Value)
            {
                double px = spec.Max.Value / 100.0 * intrinsicHeight;
                if (!double.IsNaN(px) && !double.IsInfinity(px) && px >= 0)
                    sizes[t] = px;
            }
        }
    }

    // ─────────────────────────── Track sizing (§11) ───────────────────────────

    /// <summary>
    /// Bounded CSS Grid §11 track sizing: resolves each of <paramref name="count"/>
    /// tracks to a pixel size. Fixed/percentage tracks resolve directly; intrinsic
    /// tracks (auto/min-content/max-content) grow to their items' content
    /// contributions; <c>fr</c> tracks split the leftover space. Returns
    /// <c>null</c> to decline (a percentage against an indefinite basis, or a
    /// negative/degenerate result).
    /// </summary>
    private double[] ResolveTrackSizes(List<GridTrackSpec> explicitSpecs, GridTrackSpec implicitSpec,
        int count, double containerSize, bool definite, double gap, double percentBasis, double em,
        List<AxisItem> items, bool stretchAutoTracks = false, int explicitStart = 0)
    {
        if (count < 1) return null;

        var baseSize = new double[count];
        var growth = new double[count];       // growth limit; double.PositiveInfinity when unbounded
        var isFr = new bool[count];
        var frFactor = new double[count];
        var minKind = new GridSizeKind[count]; // effective (percent already resolved)
        var maxKind = new GridSizeKind[count];

        for (int t = 0; t < count; t++)
        {
            // The explicit tracks occupy [explicitStart, explicitStart+Count);
            // leading (before) and trailing (after) tracks use the implicit spec.
            int e = t - explicitStart;
            GridTrackSpec spec = e >= 0 && e < explicitSpecs.Count ? explicitSpecs[e] : implicitSpec;
            // Resolve percentages against the axis basis: a percentage against a
            // definite basis is a fixed length; against an indefinite one it
            // behaves as 'auto' (CSS Grid §7.2.1 / §11.5).
            (GridSizeKind minK, double minPx) = ResolveEffective(spec.Min, percentBasis, definite);
            (GridSizeKind maxK, double maxPx) = ResolveEffective(spec.Max, percentBasis, definite);
            minKind[t] = minK;
            maxKind[t] = maxK;

            baseSize[t] = minK == GridSizeKind.Fixed ? Math.Max(0, minPx) : 0;

            if (maxK == GridSizeKind.Fr)
            {
                isFr[t] = true;
                frFactor[t] = maxPx; // ResolveEffective returns the flex factor here
                growth[t] = double.PositiveInfinity;
            }
            else if (maxK == GridSizeKind.Fixed)
            {
                growth[t] = Math.Max(baseSize[t], Math.Max(0, maxPx));
            }
            else
            {
                growth[t] = double.PositiveInfinity; // auto / min-content / max-content
            }
        }

        // Resolve intrinsic track sizes from single-span item contributions.
        double[] contentBase = new double[count]; // desired base (min-content)
        double[] contentGrow = new double[count]; // desired growth (max-content)
        foreach (var it in items)
        {
            if (it.Span != 1) continue;
            int t = it.Start;
            if (t < 0 || t >= count) continue;
            if (minKind[t] is GridSizeKind.Auto or GridSizeKind.MinContent or GridSizeKind.MaxContent)
            {
                double c = minKind[t] == GridSizeKind.MaxContent ? it.MaxContent : it.MinContent;
                contentBase[t] = Math.Max(contentBase[t], c);
            }
            if (maxKind[t] is GridSizeKind.Auto or GridSizeKind.MinContent or GridSizeKind.MaxContent)
            {
                double c = maxKind[t] == GridSizeKind.MinContent ? it.MinContent : it.MaxContent;
                contentGrow[t] = Math.Max(contentGrow[t], c);
            }
        }

        // Spanning items: distribute any content deficit across the intrinsic
        // tracks they cover (equal share — a bounded approximation of §11.5.1).
        foreach (var it in items)
        {
            if (it.Span <= 1) continue;
            DistributeSpanContribution(it, minKind, contentBase, count, gap, useMax: false, it.MinContent);
            DistributeSpanContribution(it, maxKind, contentGrow, count, gap, useMax: true, it.MaxContent);
        }

        for (int t = 0; t < count; t++)
        {
            if (minKind[t] is GridSizeKind.Auto or GridSizeKind.MinContent or GridSizeKind.MaxContent)
                baseSize[t] = Math.Max(baseSize[t], contentBase[t]);
            if (maxKind[t] is GridSizeKind.Auto or GridSizeKind.MinContent or GridSizeKind.MaxContent)
            {
                double g = Math.Max(baseSize[t], contentGrow[t]);
                growth[t] = double.IsPositiveInfinity(growth[t]) ? g : Math.Max(growth[t], g);
                // An intrinsic-max track grows to its max-content contribution.
                baseSize[t] = Math.Max(baseSize[t], g);
            }
            if (growth[t] < baseSize[t]) growth[t] = baseSize[t];
        }

        var sizes = new double[count];
        Array.Copy(baseSize, sizes, count);

        // Distribute leftover space to flexible (fr) tracks.
        double sumFr = 0;
        for (int t = 0; t < count; t++) if (isFr[t]) sumFr += frFactor[t];
        if (sumFr > 0 && definite && containerSize > 0)
        {
            double nonFr = 0;
            for (int t = 0; t < count; t++) if (!isFr[t]) nonFr += sizes[t];
            double totalGap = (count - 1) * gap;
            double free = containerSize - nonFr - totalGap;
            if (free < 0) free = 0;
            double frUnit = free / sumFr;
            for (int t = 0; t < count; t++)
                if (isFr[t]) sizes[t] = Math.Max(sizes[t], frFactor[t] * frUnit);
        }

        // CSS Grid §11.7 "Stretch auto Tracks": under the default (normal) content
        // distribution, leftover space in a definite container is shared equally
        // among the tracks with an 'auto' max sizing function. fr tracks already
        // absorbed free space, so skip when any is present.
        if (stretchAutoTracks && definite && containerSize > 0 && sumFr == 0)
        {
            double used = 0;
            for (int t = 0; t < count; t++) used += sizes[t];
            double free = containerSize - used - (count - 1) * gap;
            if (free > 0.5)
            {
                int autoCount = 0;
                for (int t = 0; t < count; t++) if (maxKind[t] == GridSizeKind.Auto) autoCount++;
                if (autoCount > 0)
                {
                    double share = free / autoCount;
                    for (int t = 0; t < count; t++)
                        if (maxKind[t] == GridSizeKind.Auto) sizes[t] += share;
                }
            }
        }

        for (int t = 0; t < count; t++)
        {
            if (double.IsNaN(sizes[t]) || double.IsInfinity(sizes[t]) || sizes[t] < 0)
                return null;
        }
        return sizes;
    }

    private static void DistributeSpanContribution(AxisItem it, GridSizeKind[] kinds, double[] target,
        int count, double gap, bool useMax, double contribution)
    {
        var intrinsic = new List<int>();
        double covered = 0;
        for (int i = 0; i < it.Span; i++)
        {
            int t = it.Start + i;
            if (t < 0 || t >= count) continue;
            covered += target[t];
            if (kinds[t] is GridSizeKind.Auto or GridSizeKind.MinContent or GridSizeKind.MaxContent)
                intrinsic.Add(t);
        }
        if (intrinsic.Count == 0) return;
        double gapsInSpan = (it.Span - 1) * gap;
        double need = contribution - covered - gapsInSpan;
        if (need <= 0) return;
        double share = need / intrinsic.Count;
        foreach (int t in intrinsic)
            target[t] += share;
    }

    /// <summary>
    /// Resolves a sizing function to its effective kind + pixel value for one axis.
    /// A percentage becomes a fixed length against a definite basis, or <c>auto</c>
    /// against an indefinite one. Fixed lengths pass through; <c>fr</c> returns its
    /// flex factor as the value; intrinsic kinds return 0.
    /// </summary>
    private static (GridSizeKind kind, double value) ResolveEffective(GridSize size, double basis, bool definite)
    {
        switch (size.Kind)
        {
            case GridSizeKind.Fixed:
                return (GridSizeKind.Fixed, size.Value);
            case GridSizeKind.Percent:
                return definite && basis > 0
                    ? (GridSizeKind.Fixed, basis * size.Value / 100.0)
                    : (GridSizeKind.Auto, 0);
            case GridSizeKind.Fr:
                return (GridSizeKind.Fr, size.Value);
            default:
                return (size.Kind, 0);
        }
    }

    /// <summary>
    /// Builds cumulative track-edge arrays for the given <paramref name="sizes"/>,
    /// inserting <paramref name="gap"/> between tracks.
    /// </summary>
    private static double[] BuildTrackEdges(double[] sizes, double gap, out double[] endEdge)
    {
        int count = Math.Max(sizes.Length, 1);
        var startEdge = new double[count];
        endEdge = new double[count];
        double cursor = 0;
        for (int i = 0; i < count; i++)
        {
            double size = i < sizes.Length ? sizes[i] : 0;
            startEdge[i] = cursor;
            endEdge[i] = cursor + size;
            cursor += size + gap;
        }
        return startEdge;
    }

    /// <summary>
    /// Builds cumulative track-edge arrays, positioning the whole track set within
    /// <paramref name="containerSize"/> according to the axis's content-distribution
    /// value (<c>justify-content</c> for columns, <c>align-content</c> for rows) when
    /// the tracks leave free space. Positional values (start/center/end) offset the
    /// set; distribution values (space-between/around/evenly) also widen the spacing
    /// between tracks. With no free space this matches <see cref="BuildTrackEdges"/>.
    /// </summary>
    private static double[] BuildTrackEdgesAligned(double[] sizes, double gap,
        double containerSize, string contentAlign, out double[] endEdge)
    {
        int count = Math.Max(sizes.Length, 1);
        var startEdge = new double[count];
        endEdge = new double[count];

        double sum = 0;
        for (int i = 0; i < sizes.Length; i++) sum += sizes[i];
        double free = containerSize - sum - (count - 1) * gap;

        double leading = 0, between = gap;
        if (free > 0.5)
            ResolveContentDistribution(contentAlign, free, count, gap, out leading, out between);

        double cursor = leading;
        for (int i = 0; i < count; i++)
        {
            double size = i < sizes.Length ? sizes[i] : 0;
            startEdge[i] = cursor;
            endEdge[i] = cursor + size;
            cursor += size + between;
        }
        return startEdge;
    }

    /// <summary>
    /// True when a <c>justify-content</c>/<c>align-content</c> value leaves auto
    /// tracks stretching to fill the container — the initial <c>normal</c> (and the
    /// explicit <c>stretch</c>). Any packing/spacing keyword (start/center/end/
    /// space-*) suppresses the stretch and instead positions the tracks.
    /// </summary>
    private static bool IsStretchContentDistribution(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        string v = value.Trim().ToLowerInvariant();
        return v == "normal" || v == "stretch";
    }

    /// <summary>
    /// CSS Box Alignment §5: resolves a content-distribution value into a leading
    /// offset for the first track and the spacing to insert between adjacent tracks
    /// (the base <paramref name="gap"/> plus any distributed free space). Only the
    /// physical (LTR / top-to-bottom) mapping is handled; <c>normal</c>/<c>stretch</c>
    /// and unrecognised values pack at the start (track growth for stretch is done
    /// during sizing, not here).
    /// </summary>
    private static void ResolveContentDistribution(string value, double free, int count,
        double gap, out double leading, out double between)
    {
        leading = 0;
        between = gap;
        string v = (value ?? "").Trim().ToLowerInvariant();
        if (v.StartsWith("safe ")) v = v.Substring(5).Trim();
        else if (v.StartsWith("unsafe ")) v = v.Substring(7).Trim();
        switch (v)
        {
            case "center":
                leading = free / 2;
                break;
            case "end":
            case "flex-end":
            case "right":
                leading = free;
                break;
            case "space-between":
                // ≥2 tracks share the free space between them; a single track packs
                // at the start.
                if (count > 1) between = gap + free / (count - 1);
                break;
            case "space-around":
                // Free space as equal gaps around every track (half at each end).
                {
                    double unit = free / count;
                    between = gap + unit;
                    leading = unit / 2;
                }
                break;
            case "space-evenly":
                // Free space as equal gaps between and outside every track.
                {
                    double unit = free / (count + 1);
                    between = gap + unit;
                    leading = unit;
                }
                break;
            default:
                // start / flex-start / left / normal / stretch / baseline → pack start.
                break;
        }
    }

    // ─────────────────────────── Grid line parsing ───────────────────────────

    /// <summary>
    /// Parses a <c>grid-row</c>/<c>grid-column</c> value into a 0-indexed start
    /// line (null when auto) and a span. Supports <c>auto</c>, an integer line,
    /// <c>span &lt;int&gt;</c>, and the <c>start / end</c> two-value forms. A
    /// negative line counts back from the end of the explicit grid
    /// (<paramref name="explicitTracks"/>): <c>-1</c> is the final line,
    /// <c>-2</c> the one before it, and so on; a negative line pointing before the
    /// grid's start, or a named line, falls back to <c>auto</c>.
    /// </summary>
    private static (int? start, int span) ParseGridLine(string value, int explicitTracks,
        IReadOnlyDictionary<string, List<int>> names = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (null, 1);
        string v = value.Trim();
        if (v.Equals("auto", StringComparison.OrdinalIgnoreCase))
            return (null, 1);

        int slash = v.IndexOf('/');
        if (slash < 0)
            return ParseSingleGridLine(v, explicitTracks, names);

        var (startLine, startSpan) = ParseSingleGridLine(v.Substring(0, slash).Trim(), explicitTracks, names);
        var (endLine, endSpan) = ParseSingleGridLine(v.Substring(slash + 1).Trim(), explicitTracks, names);

        if (startLine.HasValue && endLine.HasValue)
        {
            int a = startLine.Value, b = endLine.Value;
            int span = b - a;
            return span >= 1 ? (a, span) : (a, 1);
        }
        if (startLine.HasValue)
            return (startLine, Math.Max(1, endSpan)); // start / span n
        if (endLine.HasValue)
        {
            int span = Math.Max(1, startSpan);        // span n / end
            int start = endLine.Value - span;
            return start >= 0 ? (start, span) : (null, span);
        }
        return (null, 1);
    }

    private static (int? start, int span) ParseSingleGridLine(string token, int explicitTracks,
        IReadOnlyDictionary<string, List<int>> names = null)
    {
        if (string.IsNullOrEmpty(token) || token.Equals("auto", StringComparison.OrdinalIgnoreCase))
            return (null, 1);
        if (token.StartsWith("span", StringComparison.OrdinalIgnoreCase))
        {
            string rest = token.Substring(4).Trim();
            if (int.TryParse(rest, NumberStyles.Integer, CultureInfo.InvariantCulture, out int s) && s >= 1)
                return (null, s);
            return (null, 1);
        }
        if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int line))
        {
            if (line >= 1)
                return (line - 1, 1);         // 1-based line -> 0-based track boundary
            if (line <= -1 && explicitTracks >= 0)
            {
                // Negative line -N counts back from the last explicit line: the grid
                // has explicitTracks+1 lines, so line -1 is boundary `explicitTracks`.
                // A boundary that lands before the explicit grid (negative) references
                // a *leading* implicit track (CSS Grid §8.3); the caller normalises
                // these to non-negative indices by shifting the whole grid right/down.
                int boundary = explicitTracks + 1 + line;
                return (boundary, 1);
            }
            return (null, 1);                 // line 0 -> auto
        }

        // Named line: `<name>` (the first line with that name) or `<int> <name>`
        // (the Nth). CSS Grid §8.3 — resolve against the template's line-name map.
        if (names != null)
        {
            int space = token.IndexOf(' ');
            int nth = 1;
            string name = token;
            if (space > 0 && int.TryParse(token.Substring(0, space).Trim(),
                    NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedNth) && parsedNth >= 1)
            {
                nth = parsedNth;
                name = token.Substring(space + 1).Trim();
            }
            if (names.TryGetValue(name, out var lines) && lines.Count > 0)
                return (lines[Math.Min(nth, lines.Count) - 1], 1);
        }
        return (null, 1);                     // unresolved named line -> auto
    }

    /// <summary>
    /// CSS Grid §7.1: the template's line-name map — each custom line name (from a
    /// <c>[name …]</c> bracket) to the 0-based line (track boundary) indices it
    /// labels. <c>repeat(&lt;int&gt;, …)</c> is expanded so names inside repeat
    /// exist at every repetition; <c>auto-fill</c>/<c>auto-fit</c> repeats stop the
    /// walk (their count is layout-dependent). Returns an empty map for an unnamed
    /// template.
    /// </summary>
    private static Dictionary<string, List<int>> ParseLineNames(string template)
    {
        var result = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(template))
        {
            int line = 0;
            CollectLineNames(template, result, ref line, depth: 0);
        }
        return result;
    }

    private static void CollectLineNames(string v, Dictionary<string, List<int>> result, ref int line, int depth)
    {
        if (depth > 4) return;
        int i = 0, n = v.Length;
        while (i < n)
        {
            while (i < n && char.IsWhiteSpace(v[i])) i++;
            if (i >= n) break;

            if (v[i] == '[')
            {
                int close = v.IndexOf(']', i);
                if (close < 0) return;
                foreach (var name in v.Substring(i + 1, close - i - 1)
                             .Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!result.TryGetValue(name, out var list))
                        result[name] = list = new List<int>();
                    if (!list.Contains(line)) list.Add(line);
                }
                i = close + 1;
                continue;
            }

            int start = i, paren = 0;
            while (i < n && (paren > 0 || !char.IsWhiteSpace(v[i])))
            {
                if (v[i] == '(') paren++;
                else if (v[i] == ')') paren--;
                i++;
            }
            string token = v.Substring(start, i - start);
            if (token.Length == 0) continue;

            if (token.StartsWith("repeat(", StringComparison.OrdinalIgnoreCase) && token.EndsWith(")"))
            {
                string inner = token.Substring(7, token.Length - 8);
                int comma = inner.IndexOf(',');
                if (comma < 0) return;
                if (int.TryParse(inner.Substring(0, comma).Trim(),
                        NumberStyles.Integer, CultureInfo.InvariantCulture, out int count)
                    && count >= 1 && count <= 1000)
                    for (int r = 0; r < count; r++)
                        CollectLineNames(inner.Substring(comma + 1), result, ref line, depth + 1);
                else
                    return;              // auto-fill/auto-fit — line count unknown
                continue;
            }

            line++;                      // one track (length / minmax / keyword)
        }
    }
}
