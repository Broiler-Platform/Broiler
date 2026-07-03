using Broiler.Graphics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using CssConstants = Broiler.CSS.CssConstants;
using CssValueParser = Broiler.CSS.CssLengthParser;

namespace Broiler.Layout.Engine;

/// <summary>
/// CSS Grid Level 1 — a bounded, definite-track grid layout pass.
///
/// The main renderer approximates <c>display:grid</c> with a single stacked
/// column (see <see cref="ApplyGridStacking"/> / <see cref="ApplyGridAutoPlacement"/>
/// in CssBox.cs). This partial adds a real track-based pass that engages only
/// when the container declares <em>fixed</em> explicit templates on both axes
/// (<c>grid-template-columns</c> and <c>grid-template-rows</c> made of
/// <c>&lt;length&gt;</c>/<c>&lt;percentage&gt;</c>/<c>repeat()</c> tracks). It runs the
/// §8.5 grid-item placement algorithm (line-based placement, spanning,
/// <c>grid-auto-flow</c> row/column, sparse/dense packing, implicit tracks sized
/// by <c>grid-auto-rows</c>/<c>grid-auto-columns</c>), sizes each item to its grid
/// area, and positions it. Anything it cannot model (<c>fr</c>, <c>auto</c>/content
/// tracks, <c>minmax()</c>, <c>subgrid</c>, named lines, <c>grid-template-areas</c>)
/// makes the pass decline so the existing approximation is used unchanged — this
/// confines the new behaviour to grids that genuinely need real track sizing.
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

    /// <summary>
    /// Runs the definite-track grid pass. Returns <c>true</c> when it laid the
    /// container out (caller must not run the approximation), <c>false</c> to
    /// decline (unsupported track syntax, no in-flow items, degenerate size).
    /// </summary>
    private bool TryApplyGridTrackLayout()
    {
        double contentWidth = Size.Width - ActualPaddingLeft - ActualPaddingRight
            - ActualBorderLeftWidth - ActualBorderRightWidth;
        double contentHeight = Size.Height - ActualPaddingTop - ActualPaddingBottom
            - ActualBorderTopWidth - ActualBorderBottomWidth;
        double em = GetEmHeight();

        // Engage only when BOTH axes carry a fixed explicit track list — the case
        // the approximation gets wrong and the definite-track math gets right.
        List<double> colTracks = ParseFixedTrackList(GridTemplateColumns, contentWidth, em);
        List<double> rowTracks = ParseFixedTrackList(GridTemplateRows, contentHeight, em);
        if (colTracks == null || rowTracks == null || colTracks.Count == 0 || rowTracks.Count == 0)
            return false;
        if (colTracks.Count > MaxGridLine || rowTracks.Count > MaxGridLine)
            return false;

        // Collect in-flow grid items in document order.
        var placements = new List<GridPlacement>();
        foreach (var child in Boxes)
        {
            if (child.Position == CssConstants.Absolute || child.Position == CssConstants.Fixed)
                continue;
            if (child.Display == CssConstants.None)
                continue;

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

        PlaceItems(placements, colTracks.Count, rowTracks.Count, flowRow, dense);

        // Build track edge positions, extending with implicit tracks as needed.
        int maxColEnd = 0, maxRowEnd = 0;
        foreach (var p in placements)
        {
            maxColEnd = Math.Max(maxColEnd, p.PlacedCol + p.ColSpan);
            maxRowEnd = Math.Max(maxRowEnd, p.PlacedRow + p.RowSpan);
        }
        if (maxColEnd > MaxGridLine || maxRowEnd > MaxGridLine)
            return false;

        double colGap = ResolveGridGap(ColumnGap, contentWidth, em);
        double rowGap = ResolveGridGap(RowGap, contentHeight, em);
        double implicitColSize = ResolveImplicitTrackSize(GridAutoColumns, contentWidth, em);
        double implicitRowSize = ResolveImplicitTrackSize(GridAutoRows, contentHeight, em);

        double[] colStartEdge = BuildTrackEdges(colTracks, maxColEnd, implicitColSize, colGap, out double[] colEndEdge);
        double[] rowStartEdge = BuildTrackEdges(rowTracks, maxRowEnd, implicitRowSize, rowGap, out double[] rowEndEdge);

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

        // The grid's block size is the block-end edge of its last row track.
        // That last track is the greater of the explicit template (every
        // grid-template-rows track contributes to the container's size even when
        // no item occupies it) and any implicit rows placement created past it —
        // so a 4-track template with items in only the first 3 rows still sizes
        // the container to all four tracks, matching Chromium.
        int rowLineCount = Math.Max(maxRowEnd, rowTracks.Count);
        double gridHeight = rowLineCount > 0 ? rowEndEdge[rowLineCount - 1] : 0;
        double borderBoxHeight = ActualPaddingTop + ActualPaddingBottom
            + ActualBorderTopWidth + ActualBorderBottomWidth + gridHeight;
        ActualBottom = Location.Y + borderBoxHeight;
        ActualRight = CalculateActualRight();
        Size = new SizeF(Size.Width, (float)borderBoxHeight);
        _gridTrackLayoutApplied = true;
        return true;
    }

    /// <summary>Set once the definite-track pass has positioned this grid's items,
    /// so the flex/grid cross-axis approximation does not re-align them.</summary>
    private bool _gridTrackLayoutApplied;

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

    /// <summary>
    /// Parses a fixed grid track list (<c>&lt;length&gt;</c>, <c>&lt;percentage&gt;</c>,
    /// and <c>repeat(&lt;int&gt;, …)</c> of those) into pixel track sizes. Returns
    /// <c>null</c> for <c>none</c>/empty or when any content-based, flexible, or
    /// otherwise unsupported token appears — the signal to decline the pass.
    /// </summary>
    private static List<double> ParseFixedTrackList(string value, double percentBasis, double em)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        string v = value.Trim();
        if (v.Equals("none", StringComparison.OrdinalIgnoreCase) || v.Equals("auto", StringComparison.OrdinalIgnoreCase))
            return null;

        var tracks = new List<double>();
        return ParseTrackTokens(v, percentBasis, em, tracks, depth: 0) ? tracks : null;
    }

    private static bool ParseTrackTokens(string v, double percentBasis, double em, List<double> tracks, int depth)
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

            // Read one token, respecting parentheses (repeat(...)).
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
                var repeated = new List<double>();
                if (!ParseTrackTokens(inner.Substring(comma + 1), percentBasis, em, repeated, depth + 1))
                    return false;
                for (int r = 0; r < count; r++)
                    tracks.AddRange(repeated);
                continue;
            }

            if (!TryParseFixedTrack(token, percentBasis, em, out double size))
                return false;
            tracks.Add(size);
        }
        return tracks.Count > 0;
    }

    private static bool TryParseFixedTrack(string token, double percentBasis, double em, out double size)
    {
        size = 0;
        string t = token.Trim();
        if (t.Length == 0)
            return false;
        // Flexible / content-based / functional track sizes are out of scope.
        if (t.EndsWith("fr", StringComparison.OrdinalIgnoreCase)
            || t.Equals("auto", StringComparison.OrdinalIgnoreCase)
            || t.Equals("min-content", StringComparison.OrdinalIgnoreCase)
            || t.Equals("max-content", StringComparison.OrdinalIgnoreCase)
            || t.IndexOf('(') >= 0)
            return false;
        if (t.EndsWith("%", StringComparison.Ordinal))
        {
            if (double.TryParse(t.Substring(0, t.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out double pct))
            {
                size = percentBasis > 0 ? percentBasis * pct / 100.0 : 0;
                return true;
            }
            return false;
        }
        double parsed = CssValueParser.ParseLength(t, percentBasis, em);
        if (parsed < 0 || double.IsNaN(parsed) || double.IsInfinity(parsed))
            return false;
        size = parsed;
        return true;
    }

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

    /// <summary>Grid gap: <c>normal</c>/empty computes to 0 (unlike multicol).</summary>
    private double ResolveGridGap(string gap, double percentBasis, double em)
    {
        if (string.IsNullOrEmpty(gap) || gap == "normal")
            return 0;
        double v = CssValueParser.ParseLength(gap, percentBasis, em);
        return v > 0 ? v : 0;
    }

    private static double ResolveImplicitTrackSize(string autoTracks, double percentBasis, double em)
    {
        if (string.IsNullOrWhiteSpace(autoTracks))
            return 0;
        // grid-auto-rows/columns may list several sizes; the first fixed one is a
        // good-enough approximation for the bounded engine (auto/content -> 0).
        string first = autoTracks.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0];
        return TryParseFixedTrack(first, percentBasis, em, out double size) ? size : 0;
    }

    /// <summary>
    /// Builds cumulative track-edge arrays for <paramref name="count"/> tracks,
    /// using the explicit <paramref name="tracks"/> then <paramref name="implicitSize"/>
    /// for any tracks beyond them, inserting <paramref name="gap"/> between tracks.
    /// </summary>
    private static double[] BuildTrackEdges(List<double> tracks, int count, double implicitSize, double gap,
        out double[] endEdge)
    {
        if (count < tracks.Count) count = tracks.Count;
        var startEdge = new double[Math.Max(count, 1)];
        endEdge = new double[Math.Max(count, 1)];
        double cursor = 0;
        for (int i = 0; i < count; i++)
        {
            double size = i < tracks.Count ? tracks[i] : implicitSize;
            startEdge[i] = cursor;
            endEdge[i] = cursor + size;
            cursor += size + gap;
        }
        return startEdge;
    }
}
