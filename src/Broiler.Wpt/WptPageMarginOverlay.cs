using System;
using System.Collections.Generic;
using System.Globalization;
using System.Drawing;
using System.Text;
using Broiler.CSS;

namespace Broiler.Wpt;

/// <summary>
/// Builds the document that paints one page's margin boxes — CSS Paged Media 3 §5 expressed as
/// markup the engine already lays out, rather than as a second layout algorithm written here.
/// </summary>
/// <remarks>
/// <para>
/// The page's margin is a ring of eight places: four corners, and four edge strips each holding up
/// to three boxes. This file computes where each box's slot is — the part that is peculiar to paged
/// media — and hands everything else to the engine: a margin box is an ordinary box with an
/// ordinary background, border, font and alignment, and it is emitted as one.
/// </para>
/// <para>
/// Each slot is an absolutely positioned rectangle and each box fills it by insets, so the author's
/// own <c>border</c>, <c>padding</c> and <c>margin</c> apply where they normally would and never
/// have to be parsed here. Auto margins are what CSS Paged Media 3 §5.3.2 uses to centre a box in
/// its cross axis, and they resolve by themselves on an absolutely positioned box between two
/// insets — <c>margin-boxes/auto-margins-001</c> centres all sixteen boxes that way, and its
/// reference states it as <c>margin: auto</c> too.
/// </para>
/// <para>
/// Sharing an edge takes a measurement first, so there is a pass before the page is drawn:
/// <see cref="Measure"/> renders every box on its own, shrink-wrapped, and reads back the size each
/// one wants. CSS Paged Media 3 §5.3.2 needs it twice over — the unused length is divided among the
/// auto-sized boxes <em>in proportion to their max-content sizes</em>, and a box that states a
/// width still occupies its border and padding besides. Both are what the box came out as, not what
/// it asked for.
/// </para>
/// </remarks>
internal static class WptPageMarginOverlay
{
    /// <summary>
    /// The document painting <paramref name="boxes"/> over a page of <paramref name="page"/>, or
    /// <c>null</c> when the page has no margin boxes to paint.
    /// </summary>
    /// <param name="page">The page box the margin ring is measured from.</param>
    /// <param name="boxes">The margin box rules, by slot.</param>
    /// <param name="pageDeclarations">
    /// The <c>@page</c> rule's own non-geometry declarations — its font, colour, <c>white-space</c>
    /// and so on. Margin boxes inherit from the page, so these go on the overlay's root.
    /// </param>
    /// <param name="measured">
    /// Each box's outer size as it came out on its own, from <see cref="MeasureDocument"/>.
    /// </param>
    /// <param name="pageNumber">This page's number, for <c>counter(page)</c>. One-based.</param>
    /// <param name="pageCount">The document's page count, for <c>counter(pages)</c>.</param>
    internal static string? Build(
        WptPageBox page,
        IReadOnlyDictionary<WptMarginBoxSlot, IReadOnlyList<CssDeclaration>> boxes,
        IReadOnlyList<CssDeclaration> pageDeclarations,
        IReadOnlyDictionary<WptMarginBoxSlot, SizeF> measured,
        int pageNumber,
        int pageCount)
    {
        var generated = new Dictionary<WptMarginBoxSlot, IReadOnlyList<CssDeclaration>>();
        foreach (var (slot, declarations) in boxes)
        {
            if (WptPageMarginBoxes.GeneratesBox(declarations))
                generated[slot] = declarations;
        }

        if (generated.Count == 0)
            return null;

        double width = page.BoxSize.Width, height = page.BoxSize.Height;
        double left = page.MarginLeft, top = page.MarginTop;
        double right = page.MarginRight, bottom = page.MarginBottom;
        double areaWidth = Math.Max(0, width - left - right);
        double areaHeight = Math.Max(0, height - top - bottom);

        var emitter = new Emitter(generated, measured);

        emitter.Place(WptMarginBoxSlot.TopLeftCorner, 0, 0, left, top, Edge.Top);
        emitter.Place(WptMarginBoxSlot.TopRightCorner, width - right, 0, right, top, Edge.Top);
        emitter.Place(WptMarginBoxSlot.BottomLeftCorner, 0, height - bottom, left, bottom, Edge.Bottom);
        emitter.Place(WptMarginBoxSlot.BottomRightCorner, width - right, height - bottom, right, bottom, Edge.Bottom);

        emitter.Strip(
            [WptMarginBoxSlot.TopLeft, WptMarginBoxSlot.TopCenter, WptMarginBoxSlot.TopRight],
            left, 0, areaWidth, top, Edge.Top);
        emitter.Strip(
            [WptMarginBoxSlot.BottomLeft, WptMarginBoxSlot.BottomCenter, WptMarginBoxSlot.BottomRight],
            left, height - bottom, areaWidth, bottom, Edge.Bottom);
        emitter.Strip(
            [WptMarginBoxSlot.LeftTop, WptMarginBoxSlot.LeftMiddle, WptMarginBoxSlot.LeftBottom],
            0, top, left, areaHeight, Edge.Left);
        emitter.Strip(
            [WptMarginBoxSlot.RightTop, WptMarginBoxSlot.RightMiddle, WptMarginBoxSlot.RightBottom],
            width - right, top, right, areaHeight, Edge.Right);

        emitter.Emit();

        var pageCss = new StringBuilder();
        foreach (var declaration in pageDeclarations)
            pageCss.Append(declaration.Name).Append(':').Append(declaration.Value.Text).Append(';');

        return "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><style>"
            + "html,body{margin:0;padding:0;border:0;}"
            + "body{counter-reset:page " + Int(pageNumber) + " pages " + Int(pageCount) + ";" + pageCss + "}"
            + ".c{position:absolute;}"
            + ".t{display:table;width:100%;height:100%;}"
            + ".v{display:table-cell;}"
            + emitter.Rules
            + "</style></head><body>" + emitter.Body + "</body></html>";
    }


    /// <summary>The slots a measure render lays out, in the order it stacks them.</summary>
    internal static IReadOnlyList<WptMarginBoxSlot> MeasuredSlots(
        IReadOnlyDictionary<WptMarginBoxSlot, IReadOnlyList<CssDeclaration>> boxes)
    {
        var slots = new List<WptMarginBoxSlot>();
        foreach (WptMarginBoxSlot slot in Enum.GetValues<WptMarginBoxSlot>())
        {
            if (boxes.TryGetValue(slot, out var declarations)
                && WptPageMarginBoxes.GeneratesBox(declarations))
            {
                slots.Add(slot);
            }
        }

        return slots;
    }

    /// <summary>
    /// The document that measures each margin box: every one of them shrink-wrapped in a container
    /// the size of the strip it will live on, stacked down the page, each painted a colour of its
    /// own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each box floats, so it comes out at the size it wants — its max-content size, clamped by the
    /// strip, which is the size CSS Paged Media 3 §5.3.2 shares an edge by. The container is the
    /// strip so that a percentage width resolves against the same thing it will resolve against on
    /// the page.
    /// </para>
    /// <para>
    /// Read back by colour rather than out of the fragment tree: the colour is on the box's
    /// background <em>and</em> its border, so the painted extent is the border box — outer size,
    /// borders and padding included, which is the other half of what the sharing needs. Reading a
    /// tree would need the boxes identified in it, and this needs nothing but the pixels the same
    /// renderer produces anyway.
    /// </para>
    /// </remarks>
    internal static string MeasureDocument(
        WptPageBox page,
        IReadOnlyDictionary<WptMarginBoxSlot, IReadOnlyList<CssDeclaration>> boxes,
        IReadOnlyList<CssDeclaration> pageDeclarations,
        IReadOnlyList<WptMarginBoxSlot> slots,
        out Size surface)
    {
        double left = page.MarginLeft, top = page.MarginTop;
        double right = page.MarginRight, bottom = page.MarginBottom;
        double areaWidth = Math.Max(1, page.BoxSize.Width - left - right);
        double areaHeight = Math.Max(1, page.BoxSize.Height - top - bottom);

        var body = new StringBuilder();
        var rules = new StringBuilder();
        double widest = 1, total = 0;

        for (int i = 0; i < slots.Count; i++)
        {
            var (w, h) = SlotStrip(slots[i], left, top, right, bottom, areaWidth, areaHeight);
            widest = Math.Max(widest, w);
            total += h + MeasureGap;

            var declarations = WptPageMarginBoxes.DeclarationsCss(boxes[slots[i]]);
            body.Append("<div style=\"width:").Append(WptPageMarginBoxes.Px(w))
                .Append(";height:").Append(WptPageMarginBoxes.Px(h))
                // A floor of one pixel, so a box with nothing in it still paints and can be read
                // back. An empty `content: ""` is a real margin box and often a sized one, and a
                // box that measures as nothing would be given no room on its edge at all.
                .Append("\"><div style=\"float:left;min-width:1px;min-height:1px;").Append(declarations)
                .Append("background:").Append(MeasureColour(i))
                .Append(";border-color:").Append(MeasureColour(i))
                .Append(";\"><span id=\"m").Append(Int(i)).Append("\"></span></div></div>");

            rules.Append("#m").Append(Int(i)).Append("::before{content:")
                .Append(WptPageMarginBoxes.ContentValue(boxes[slots[i]])).Append(";}");
        }

        var pageCss = new StringBuilder();
        foreach (var declaration in pageDeclarations)
            pageCss.Append(declaration.Name).Append(':').Append(declaration.Value.Text).Append(';');

        surface = new Size(
            (int)Math.Ceiling(widest) + MeasureGap,
            (int)Math.Ceiling(total) + MeasureGap);

        return "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><style>"
            + "html,body{margin:0;padding:0;border:0;}"
            + "body{" + pageCss + "}"
            + rules
            + "</style></head><body>" + body + "</body></html>";
    }

    /// <summary>A blank row between two measured boxes, so neither runs into the next.</summary>
    private const int MeasureGap = 4;

    /// <summary>The strip a slot's box lives on: its length along the edge and its thickness.</summary>
    private static (double Width, double Height) SlotStrip(
        WptMarginBoxSlot slot,
        double left, double top, double right, double bottom,
        double areaWidth, double areaHeight) => slot switch
        {
            WptMarginBoxSlot.TopLeftCorner => (left, top),
            WptMarginBoxSlot.TopRightCorner => (right, top),
            WptMarginBoxSlot.BottomLeftCorner => (left, bottom),
            WptMarginBoxSlot.BottomRightCorner => (right, bottom),
            WptMarginBoxSlot.TopLeft or WptMarginBoxSlot.TopCenter or WptMarginBoxSlot.TopRight
                => (areaWidth, top),
            WptMarginBoxSlot.BottomLeft or WptMarginBoxSlot.BottomCenter or WptMarginBoxSlot.BottomRight
                => (areaWidth, bottom),
            WptMarginBoxSlot.LeftTop or WptMarginBoxSlot.LeftMiddle or WptMarginBoxSlot.LeftBottom
                => (left, areaHeight),
            _ => (right, areaHeight),
        };

    /// <summary>
    /// A colour per measured box, distinct from each other and from the white the page is cleared
    /// to, so each box's extent can be read straight off the render.
    /// </summary>
    internal static string MeasureColour(int index) =>
        "rgb(" + Int(1 + index * 15) + ",0," + Int(200 - index * 5) + ")";

    /// <summary>The RGB triple <see cref="MeasureColour"/> names, for reading it back.</summary>
    internal static (int R, int G, int B) MeasureRgb(int index) => (1 + index * 15, 0, 200 - index * 5);

    /// <summary>Which margin a strip or corner sits in, which is what says where its boxes align.</summary>
    private enum Edge { Top, Bottom, Left, Right }

    private sealed class Emitter(
        IReadOnlyDictionary<WptMarginBoxSlot, IReadOnlyList<CssDeclaration>> generated,
        IReadOnlyDictionary<WptMarginBoxSlot, SizeF> measured)
    {
        internal readonly StringBuilder Body = new();
        internal readonly StringBuilder Rules = new();
        private int _next;

        private readonly List<(
            WptMarginBoxSlot Slot, double X, double Y, double W, double H,
            Edge Edge, Position Position, bool Horizontal)> _placed = [];

        /// <summary>A corner box, which has its whole corner to itself.</summary>
        internal void Place(WptMarginBoxSlot slot, double x, double y, double w, double h, Edge edge)
        {
            if (!generated.ContainsKey(slot) || w <= 0 || h <= 0)
                return;

            _placed.Add((slot, x, y, w, h, edge, Position.Start, edge is Edge.Top or Edge.Bottom));
        }

        /// <summary>
        /// Emits every placed box, in the order CSS Paged Media 3 §5.3.3 paints them: the top-left
        /// corner first and then clockwise round the page, so a box that overlaps its neighbour —
        /// which a negative margin lets it — covers the one before it.
        /// <c>margin-boxes/paint-order-001</c> is built on exactly that overlap.
        /// </summary>
        internal void Emit()
        {
            _placed.Sort((a, b) => ((int)a.Slot).CompareTo((int)b.Slot));
            foreach (var p in _placed)
                Box(p.Slot, generated[p.Slot], p.X, p.Y, p.W, p.H, p.Edge, p.Position, p.Horizontal);
        }

        /// <summary>
        /// One edge's up-to-three boxes, sharing the strip along its length: each keeps the length it
        /// declares, and what is left over is divided among those that declare none.
        /// </summary>
        internal void Strip(WptMarginBoxSlot[] slots, double x, double y, double w, double h, Edge edge)
        {
            bool horizontal = edge is Edge.Top or Edge.Bottom;
            double length = horizontal ? w : h;
            if (length <= 0 || (horizontal ? h : w) <= 0)
                return;

            var present = new List<int>();
            var lengths = new double[3];
            double stated = 0, flexible = 0;

            for (int i = 0; i < 3; i++)
            {
                if (!generated.ContainsKey(slots[i]))
                    continue;

                present.Add(i);
                lengths[i] = Wanted(slots[i], horizontal);
                if (StatesItsLength(slots[i], horizontal))
                    stated += lengths[i];
                else
                    flexible += lengths[i];
            }

            if (present.Count == 0)
                return;

            // CSS Paged Media 3 §5.3.2: what the edge has left over after the boxes that state a
            // length is divided among the ones that do not, in proportion to the size each of them
            // wants. margin-boxes/dimensions-004 spells that out in a comment and is written to
            // fail an equal division: two auto boxes wanting 1em and 3em on a 20em edge come out
            // 5em and 15em, not 9em and 11em.
            int flexibleCount = 0;
            foreach (int i in present)
            {
                if (!StatesItsLength(slots[i], horizontal))
                    flexibleCount++;
            }

            double free = length - stated - flexible;
            foreach (int i in present)
            {
                if (StatesItsLength(slots[i], horizontal))
                    continue;

                // In proportion to what each wants, unless none of them wants anything — a box
                // whose content is the empty string measures nothing at all, and three of those
                // still share the edge in thirds rather than vanishing
                // (margin-boxes/dimensions-010 is exactly that, in three colours).
                lengths[i] += flexible > 0 ? free * (lengths[i] / flexible) : free / flexibleCount;
            }

            // Along the edge the boxes sit apart: the first at the start, the last at the end, and
            // the middle one centred — what the references state as `justify-content: space-between`
            // (margin-boxes/auto-margins-001).
            var offsets = new double[3];
            offsets[0] = 0;
            offsets[2] = length - lengths[2];
            offsets[1] = (length - lengths[1]) / 2;

            foreach (int i in present)
            {
                var position = i switch { 0 => Position.Start, 1 => Position.Center, _ => Position.End };

                _placed.Add(horizontal
                    ? (slots[i], x + offsets[i], y, lengths[i], h, edge, position, true)
                    : (slots[i], x, y + offsets[i], w, lengths[i], edge, position, false));
            }
        }

        /// <summary>Where along its edge a box sits, which decides which end of its slot it is pinned to.</summary>
        private enum Position { Start, Center, End }

        /// <summary>The length a box came out as when it was measured on its own.</summary>
        private double Wanted(WptMarginBoxSlot slot, bool horizontal) =>
            measured.TryGetValue(slot, out var size) ? (horizontal ? size.Width : size.Height) : 0;

        /// <summary>Whether the box states its own length along its edge, rather than taking a share.</summary>
        private bool StatesItsLength(WptMarginBoxSlot slot, bool horizontal)
        {
            var declared = WptPageMarginBoxes.Declared(
                generated[slot], horizontal ? "width" : "height");
            return declared is not null
                && !declared.Equals(CssConstants.Auto, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Emits one margin box: its slot, the box filling that slot by insets, and a table cell
        /// inside it that aligns the content the way <c>vertical-align</c> and <c>text-align</c> say.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The content goes through <c>::before</c> so the engine's own generated-content machinery
        /// answers it — <c>open-quote</c> against the box's <c>quotes</c>, <c>counter(page)</c>
        /// against the counter the overlay's root resets, <c>counters()</c>, string concatenation
        /// and escapes all come for free, and nothing here could answer them differently from the
        /// way the rest of the engine does.
        /// </para>
        /// <para>
        /// The cross-axis insets are the alignment. A box that declares no thickness gets both, and
        /// fills the strip. One that declares a thickness gets only the inset on the side facing the
        /// page — a header sits on the bottom of the top margin — unless it declares an auto margin,
        /// which is CSS Paged Media 3 §5.3.2's way of centring it and needs both insets to resolve
        /// against.
        /// </para>
        /// </remarks>
        private void Box(
            WptMarginBoxSlot slot,
            IReadOnlyList<CssDeclaration> declarations,
            double x, double y, double w, double h,
            Edge edge, Position position, bool horizontal)
        {
            int id = _next++;
            var (defaultTextAlign, defaultVerticalAlign) = WptPageMarginBoxes.DefaultAlignment(slot);
            var verticalAlign = WptPageMarginBoxes.Declared(declarations, "vertical-align") ?? defaultVerticalAlign;

            var crossProperty = horizontal ? "height" : "width";
            var crossSize = WptPageMarginBoxes.Declared(declarations, crossProperty);
            bool crossStated = crossSize is not null
                && !crossSize.Equals(CssConstants.Auto, StringComparison.OrdinalIgnoreCase);
            bool crossCentred = DeclaresAutoMargin(declarations, horizontal ? "top" : "left")
                || DeclaresAutoMargin(declarations, horizontal ? "bottom" : "right");

            var insets = new StringBuilder();

            // Along the edge: the slot is already the length this box was given and already sits
            // where the box belongs, so one inset places it. A box that declared no length gets
            // both, because an absolutely positioned box with one inset and an auto size shrinks to
            // its content rather than filling — which for a box whose content is the empty string
            // is nothing at all.
            var mainSize = WptPageMarginBoxes.Declared(declarations, horizontal ? "width" : "height");
            bool mainStated = mainSize is not null
                && !mainSize.Equals(CssConstants.Auto, StringComparison.OrdinalIgnoreCase);

            if (horizontal)
            {
                insets.Append(position == Position.End && mainStated ? "right:0;" : "left:0;");
                if (!mainStated)
                    insets.Append("right:0;");
            }
            else
            {
                insets.Append(position == Position.End && mainStated ? "bottom:0;" : "top:0;");
                if (!mainStated)
                    insets.Append("bottom:0;");
            }

            // Across it: toward the page area, and stretched when nothing says otherwise.
            var near = edge switch
            {
                Edge.Top => "bottom",
                Edge.Bottom => "top",
                Edge.Left => "right",
                _ => "left",
            };
            var far = near switch { "bottom" => "top", "top" => "bottom", "right" => "left", _ => "right" };

            insets.Append(near).Append(":0;");
            if (!crossStated || crossCentred)
                insets.Append(far).Append(":0;");

            var style = new StringBuilder("position:absolute;overflow:hidden;");
            style.Append(insets);
            style.Append("text-align:").Append(defaultTextAlign).Append(';');
            style.Append(WptPageMarginBoxes.DeclarationsCss(declarations));

            Body.Append("<div class=\"c\" style=\"left:").Append(WptPageMarginBoxes.Px(x))
                .Append(";top:").Append(WptPageMarginBoxes.Px(y))
                .Append(";width:").Append(WptPageMarginBoxes.Px(w))
                .Append(";height:").Append(WptPageMarginBoxes.Px(h))
                .Append("\"><div style=\"").Append(style)
                .Append("\"><div class=\"t\"><div class=\"v\" style=\"vertical-align:")
                .Append(verticalAlign)
                .Append("\"><div id=\"mb").Append(Int(id))
                .Append("\"></div></div></div></div></div>");

            Rules.Append("#mb").Append(Int(id)).Append("::before{content:")
                .Append(WptPageMarginBoxes.ContentValue(declarations)).Append(";}");
        }

        private static bool DeclaresAutoMargin(IReadOnlyList<CssDeclaration> declarations, string side)
        {
            var longhand = WptPageMarginBoxes.Declared(declarations, "margin-" + side);
            if (longhand is not null)
                return longhand.Equals(CssConstants.Auto, StringComparison.OrdinalIgnoreCase);

            var shorthand = WptPageMarginBoxes.Declared(declarations, "margin");
            if (shorthand is null)
                return false;

            var parts = shorthand.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return false;

            int index = side switch
            {
                "top" => 0,
                "right" => 1,
                "bottom" => 2,
                _ => 3,
            };

            // The shorthand's usual folding: one value is every side, two are block then inline,
            // three leave the left to mirror the right.
            var resolved = parts.Length switch
            {
                1 => parts[0],
                2 => parts[index % 2 == 0 ? 0 : 1],
                3 => index == 3 ? parts[1] : parts[index],
                _ => parts[Math.Min(index, parts.Length - 1)],
            };

            return resolved.Equals(CssConstants.Auto, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);
}
