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
/// <c>min-content</c>, <c>max-content</c>, <c>minmax()</c>, and <c>repeat(&lt;int&gt;, …)</c>.
///
/// Intrinsic column sizing draws its content contributions from
/// <see cref="GetMinMaxWidth"/> (a layout-independent measure); intrinsic row
/// sizing uses each item's already-measured height and declines (falls back to the
/// approximation) when a narrowed column would have reflowed that height, so it
/// never sizes a row from a stale measurement. Anything still out of scope
/// (<c>subgrid</c>, <c>fit-content()</c>, <c>repeat(auto-fill/auto-fit, …)</c>,
/// named lines carrying sizes) makes the pass decline, confining the new behaviour
/// to grids it can size correctly.
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
        if (contentWidth <= 0)
            return false;

        // Parse both axes' explicit track lists into sizing functions. A null list
        // (none/empty or an out-of-scope token) declines the whole pass.
        List<GridTrackSpec> colSpecs = ParseTrackList(GridTemplateColumns, em);
        List<GridTrackSpec> rowSpecs = ParseTrackList(GridTemplateRows, em);
        if (colSpecs == null || rowSpecs == null || colSpecs.Count == 0 || rowSpecs.Count == 0)
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

            var (rowStart, rowSpan) = ParseGridLine(child.GridRow);
            var (colStart, colSpan) = ParseGridLine(child.GridColumn);
            // Decline rather than lay out an implausibly large grid.
            if (rowSpan > MaxGridLine || colSpan > MaxGridLine
                || (rowStart ?? 0) > MaxGridLine || (colStart ?? 0) > MaxGridLine)
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

        bool flowRow = (GridAutoFlow ?? "row").IndexOf("column", StringComparison.OrdinalIgnoreCase) < 0;
        bool dense = (GridAutoFlow ?? "").IndexOf("dense", StringComparison.OrdinalIgnoreCase) >= 0;

        PlaceItems(placements, colSpecs.Count, rowSpecs.Count, flowRow, dense);

        // Track counts after placement, including any implicit tracks.
        int maxColEnd = 0, maxRowEnd = 0;
        foreach (var p in placements)
        {
            maxColEnd = Math.Max(maxColEnd, p.PlacedCol + p.ColSpan);
            maxRowEnd = Math.Max(maxRowEnd, p.PlacedRow + p.RowSpan);
        }
        if (maxColEnd > MaxGridLine || maxRowEnd > MaxGridLine)
            return false;
        int colCount = Math.Max(maxColEnd, colSpecs.Count);
        int rowCount = Math.Max(maxRowEnd, rowSpecs.Count);

        double colGap = ResolveGridGap(ColumnGap, contentWidth, em);

        // ── Column axis (inline) ── content contributions are layout-independent.
        var colItems = new List<AxisItem>(placements.Count);
        foreach (var p in placements)
        {
            GridItemInlineContribution(p.Item, out double minW, out double maxW);
            double marginX = p.Item.ActualMarginLeft + p.Item.ActualMarginRight;
            colItems.Add(new AxisItem(p.PlacedCol, p.ColSpan, minW + marginX, maxW + marginX));
        }
        double[] colSizes = ResolveTrackSizes(colSpecs, implicitCol, colCount,
            contentWidth, definite: true, colGap, contentWidth, em, colItems);
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
        bool rowDefinite = TryGetDefiniteContentHeight(em, out double definiteHeight);
        double rowBasis = rowDefinite ? definiteHeight : 0;
        double rowGap = ResolveGridGap(RowGap, rowBasis, em);

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
            double h = (p.Item.ActualBottom - p.Item.Location.Y) + marginY;
            if (h < 0) h = 0;
            rowItems.Add(new AxisItem(p.PlacedRow, p.RowSpan, h, h));
        }
        double[] rowSizes = ResolveTrackSizes(rowSpecs, implicitRow, rowCount,
            rowBasis, rowDefinite, rowGap, rowBasis, em, rowItems);
        if (rowSizes == null)
            return false;

        double[] colStartEdge = BuildTrackEdges(colSizes, colGap, out double[] colEndEdge);
        double[] rowStartEdge = BuildTrackEdges(rowSizes, rowGap, out double[] rowEndEdge);

        double contentLeft = Location.X + ActualBorderLeftWidth + ActualPaddingLeft;
        double contentTop = Location.Y + ActualBorderTopWidth + ActualPaddingTop;

        foreach (var p in placements)
        {
            double areaLeft = contentLeft + colStartEdge[p.PlacedCol];
            double areaRight = contentLeft + colEndEdge[p.PlacedCol + p.ColSpan - 1];
            double areaTop = contentTop + rowStartEdge[p.PlacedRow];
            double areaBottom = contentTop + rowEndEdge[p.PlacedRow + p.RowSpan - 1];
            PlaceItemInArea(p.Item, areaLeft, areaTop, areaRight - areaLeft, areaBottom - areaTop);
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
                colSizes.Length, rowSizes.Length, contentLeft, contentTop);

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
        if (heightFills)
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
        int colCount, int rowCount, double contentLeft, double contentTop)
    {
        double padLeft = Location.X + ActualBorderLeftWidth;
        double padRight = Location.X + Size.Width - ActualBorderRightWidth;
        double padTop = Location.Y + ActualBorderTopWidth;
        double padBottom = ActualBottom - ActualBorderBottomWidth;

        foreach (var item in items)
        {
            var (rowStart, rowSpan) = ParseGridLine(item.GridRow);
            var (colStart, colSpan) = ParseGridLine(item.GridColumn);
            var (left, right) = ResolveAbsposAxis(colStart, colSpan, colStartEdge, colEndEdge,
                colCount, contentLeft, padLeft, padRight);
            var (top, bottom) = ResolveAbsposAxis(rowStart, rowSpan, rowStartEdge, rowEndEdge,
                rowCount, contentTop, padTop, padBottom);
            PlaceAbsposItemInArea(item, left, top, right - left, bottom - top);
        }
    }

    /// <summary>
    /// Resolve the [start, end] extent (absolute px) of an abspos grid item's
    /// grid area along one axis. <paramref name="start"/> is a 0-indexed track
    /// (null for an <c>auto</c> line, which resolves to the container's padding
    /// edges per CSS Grid §9.2); lines are clamped into the grid's line range.
    /// </summary>
    private static (double start, double end) ResolveAbsposAxis(int? start, int span,
        double[] startEdge, double[] endEdge, int trackCount, double contentOrigin,
        double padStart, double padEnd)
    {
        if (start == null || trackCount == 0)
            return (padStart, padEnd);
        int a = Math.Clamp(start.Value, 0, trackCount);
        int b = Math.Clamp(start.Value + Math.Max(1, span), 0, trackCount);
        if (b <= a) b = Math.Min(a + 1, trackCount);
        double s = a < trackCount ? contentOrigin + startEdge[a] : contentOrigin + endEdge[trackCount - 1];
        double e = contentOrigin + endEdge[b - 1];
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
        List<AxisItem> items)
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
            GridTrackSpec spec = t < explicitSpecs.Count ? explicitSpecs[t] : implicitSpec;
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

    // ─────────────────────────── Grid line parsing ───────────────────────────

    /// <summary>
    /// Parses a <c>grid-row</c>/<c>grid-column</c> value into a 0-indexed start
    /// line (null when auto) and a span. Supports <c>auto</c>, an integer line,
    /// <c>span &lt;int&gt;</c>, and the <c>start / end</c> two-value forms. Named
    /// lines and negative (from-end) indices fall back to <c>auto</c>.
    /// </summary>
    private static (int? start, int span) ParseGridLine(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (null, 1);
        string v = value.Trim();
        if (v.Equals("auto", StringComparison.OrdinalIgnoreCase))
            return (null, 1);

        int slash = v.IndexOf('/');
        if (slash < 0)
            return ParseSingleGridLine(v);

        var (startLine, startSpan) = ParseSingleGridLine(v.Substring(0, slash).Trim());
        var (endLine, endSpan) = ParseSingleGridLine(v.Substring(slash + 1).Trim());

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

    private static (int? start, int span) ParseSingleGridLine(string token)
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
        if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int line) && line >= 1)
            return (line - 1, 1); // 1-based line -> 0-based track index
        return (null, 1);         // named line / negative -> auto
    }
}
