using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using TheArtOfDev.HtmlRenderer.Adapters;
using TheArtOfDev.HtmlRenderer.Core.IR;

namespace TheArtOfDev.HtmlRenderer.Core;

/// <summary>
/// <see cref="IRasterBackend"/> implementation that replays a <see cref="DisplayList"/>
/// onto an <see cref="RGraphics"/> surface. Bridges the new IR paint pipeline back to
/// the existing platform adapters.
/// </summary>
/// <remarks>
/// Phase 3: This backend allows the new <see cref="PaintWalker"/> display-list output
/// to be rendered via the same <see cref="RGraphics"/> adapters used by the old paint path,
/// enabling gradual migration without replacing platform-specific code.
/// </remarks>
internal sealed class RGraphicsRasterBackend : IRasterBackend
{
    public static readonly RGraphicsRasterBackend Instance = new();

    /// <inheritdoc />
    public void Render(DisplayList list, object surface)
    {
        if (surface is not RGraphics g)
            throw new ArgumentException("Surface must be an RGraphics instance.", nameof(surface));

        foreach (var item in list.Items)
        {
            switch (item)
            {
                case FillRectItem fill:
                    RenderFillRect(g, fill);
                    break;
                case DrawBorderItem border:
                    RenderDrawBorder(g, border);
                    break;
                case DrawTextItem text:
                    RenderDrawText(g, text);
                    break;
                case DrawImageItem image:
                    RenderDrawImage(g, image);
                    break;
                case DrawTiledImageItem tiled:
                    RenderDrawTiledImage(g, tiled);
                    break;
                case DrawLineItem line:
                    RenderDrawLine(g, line);
                    break;
                case ClipItem clip:
                    g.PushClip(clip.ClipRect);
                    break;
                case RestoreItem:
                    g.PopClip();
                    break;
                case OpacityItem:
                    // Opacity not directly supported by RGraphics; skip for now
                    break;
            }
        }
    }

    private static void RenderFillRect(RGraphics g, FillRectItem item)
    {
        using var brush = g.GetSolidBrush(item.Color);
        // CSS2.1 §14.2: backgrounds extend to the padding edge.  Use Round
        // (not Ceiling) so the fill covers the element's top-left pixel even
        // when the layout position has a fractional component.  Ceiling was
        // incorrect because it shifts the fill up to 1 px right/down, creating
        // a gap where parent backgrounds (e.g. 'background: red') leak through
        // (see Acid2 nose §8.5 / Phase 9.4).
        g.DrawRectangle(brush, Math.Round(item.Bounds.X), Math.Round(item.Bounds.Y),
            item.Bounds.Width, item.Bounds.Height);
    }

    private static void RenderDrawBorder(RGraphics g, DrawBorderItem item)
    {
        var bounds = item.Bounds;
        var widths = item.Widths;

        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        // P2.1/P2.2 audit: The border trapezoid AA kernel uses
        // SKPaint.IsAntialias = true (set in SkiaImageAdapter.CreateSolidBrush)
        // with direct SKPath polygon construction.  Rows y=146–165 of the Acid2
        // nose diamond show 62–86% per-scanline match because the ::before
        // pseudo-element diamond shape differs at the layout level (element
        // position/dimensions), not the rasterisation level.  Pixel-snapping
        // outer bounds was tested and regressed adjacent regions; width-only
        // snapping had no measurable effect (widths are already at integer px).
        // The existing rendering is aligned with CSS 2.1 Appendix E paint order.

        // Fill corner rectangles to prevent anti-aliased seams along
        // the diagonal edges where two same-color border trapezoids meet.
        FillBorderCorners(g, item);

        // Top border
        if (widths.Top > 0 && item.TopColor.A > 0 && IsBorderStyleVisible(item.TopStyle))
        {
            if (item.TopStyle == "solid")
            {
                // Trapezoid rendering for correct corner joins with asymmetric widths
                var pts = new PointF[4];
                pts[0] = new PointF(bounds.Left, bounds.Top);
                pts[1] = new PointF(bounds.Right, bounds.Top);
                pts[2] = new PointF((float)(bounds.Right - widths.Right), (float)(bounds.Top + widths.Top));
                pts[3] = new PointF((float)(bounds.Left + widths.Left), (float)(bounds.Top + widths.Top));
                g.DrawPolygon(g.GetSolidBrush(item.TopColor), pts);
            }
            else
            {
                var pen = CreateBorderPen(g, item.TopStyle, item.TopColor, widths.Top);
                g.DrawLine(pen, Math.Ceiling(bounds.Left), bounds.Top + widths.Top / 2,
                    bounds.Right - 1, bounds.Top + widths.Top / 2);
            }
        }

        // Left border
        if (widths.Left > 0 && item.LeftColor.A > 0 && IsBorderStyleVisible(item.LeftStyle))
        {
            if (item.LeftStyle == "solid")
            {
                var pts = new PointF[4];
                pts[0] = new PointF(bounds.Left, bounds.Top);
                pts[1] = new PointF((float)(bounds.Left + widths.Left), (float)(bounds.Top + widths.Top));
                pts[2] = new PointF((float)(bounds.Left + widths.Left), (float)(bounds.Bottom - widths.Bottom));
                pts[3] = new PointF(bounds.Left, bounds.Bottom);
                g.DrawPolygon(g.GetSolidBrush(item.LeftColor), pts);
            }
            else
            {
                var pen = CreateBorderPen(g, item.LeftStyle, item.LeftColor, widths.Left);
                g.DrawLine(pen, bounds.Left + widths.Left / 2, Math.Ceiling(bounds.Top),
                    bounds.Left + widths.Left / 2, Math.Floor(bounds.Bottom));
            }
        }

        // Bottom border
        if (widths.Bottom > 0 && item.BottomColor.A > 0 && IsBorderStyleVisible(item.BottomStyle))
        {
            if (item.BottomStyle == "solid")
            {
                var pts = new PointF[4];
                pts[0] = new PointF((float)(bounds.Left + widths.Left), (float)(bounds.Bottom - widths.Bottom));
                pts[1] = new PointF((float)(bounds.Right - widths.Right), (float)(bounds.Bottom - widths.Bottom));
                pts[2] = new PointF(bounds.Right, bounds.Bottom);
                pts[3] = new PointF(bounds.Left, bounds.Bottom);
                g.DrawPolygon(g.GetSolidBrush(item.BottomColor), pts);
            }
            else
            {
                var pen = CreateBorderPen(g, item.BottomStyle, item.BottomColor, widths.Bottom);
                g.DrawLine(pen, Math.Ceiling(bounds.Left), bounds.Bottom - widths.Bottom / 2,
                    bounds.Right - 1, bounds.Bottom - widths.Bottom / 2);
            }
        }

        // Right border
        if (widths.Right > 0 && item.RightColor.A > 0 && IsBorderStyleVisible(item.RightStyle))
        {
            if (item.RightStyle == "solid")
            {
                var pts = new PointF[4];
                pts[0] = new PointF((float)(bounds.Right - widths.Right), (float)(bounds.Top + widths.Top));
                pts[1] = new PointF(bounds.Right, bounds.Top);
                pts[2] = new PointF(bounds.Right, bounds.Bottom);
                pts[3] = new PointF((float)(bounds.Right - widths.Right), (float)(bounds.Bottom - widths.Bottom));
                g.DrawPolygon(g.GetSolidBrush(item.RightColor), pts);
            }
            else
            {
                var pen = CreateBorderPen(g, item.RightStyle, item.RightColor, widths.Right);
                g.DrawLine(pen, bounds.Right - widths.Right / 2, Math.Ceiling(bounds.Top),
                    bounds.Right - widths.Right / 2, Math.Floor(bounds.Bottom));
            }
        }
    }

    /// <summary>
    /// Fills corner rectangles where two adjacent solid borders share the same color.
    /// This prevents visible anti-aliased seams along the diagonal edge where the
    /// two border trapezoids meet, which would otherwise let the background bleed through.
    /// </summary>
    private static void FillBorderCorners(RGraphics g, DrawBorderItem item)
    {
        var bounds = item.Bounds;
        var widths = item.Widths;

        bool hasTop = widths.Top > 0 && item.TopColor.A > 0 && item.TopStyle == "solid";
        bool hasRight = widths.Right > 0 && item.RightColor.A > 0 && item.RightStyle == "solid";
        bool hasBottom = widths.Bottom > 0 && item.BottomColor.A > 0 && item.BottomStyle == "solid";
        bool hasLeft = widths.Left > 0 && item.LeftColor.A > 0 && item.LeftStyle == "solid";

        // Top-left corner
        if (hasTop && hasLeft && item.TopColor == item.LeftColor)
            g.DrawRectangle(g.GetSolidBrush(item.TopColor),
                bounds.Left, bounds.Top, widths.Left, widths.Top);

        // Top-right corner
        if (hasTop && hasRight && item.TopColor == item.RightColor)
            g.DrawRectangle(g.GetSolidBrush(item.TopColor),
                bounds.Right - widths.Right, bounds.Top, widths.Right, widths.Top);

        // Bottom-left corner
        if (hasBottom && hasLeft && item.BottomColor == item.LeftColor)
            g.DrawRectangle(g.GetSolidBrush(item.BottomColor),
                bounds.Left, bounds.Bottom - widths.Bottom, widths.Left, widths.Bottom);

        // Bottom-right corner
        if (hasBottom && hasRight && item.BottomColor == item.RightColor)
            g.DrawRectangle(g.GetSolidBrush(item.BottomColor),
                bounds.Right - widths.Right, bounds.Bottom - widths.Bottom, widths.Right, widths.Bottom);
    }

    private static void RenderDrawText(RGraphics g, DrawTextItem item)
    {
        if (string.IsNullOrEmpty(item.Text))
            return;

        if (item.FontHandle is RFont font)
        {
            // Phase 10.2: Round text origin to integer pixel coordinates.
            // Sub-pixel text positioning causes glyph rasterisation to differ
            // from Chromium's pixel-snapped baseline, producing per-glyph
            // anti-aliasing differences.
            var origin = new PointF(
                (float)Math.Round(item.Origin.X),
                (float)Math.Round(item.Origin.Y));
            g.DrawString(item.Text, font, item.Color, origin,
                new SizeF(item.Bounds.Width, item.Bounds.Height), item.IsRtl);
        }
    }

    private static void RenderDrawImage(RGraphics g, DrawImageItem item)
    {
        if (item.ImageHandle is RImage image)
        {
            if (item.SourceRect != RectangleF.Empty)
                g.DrawImage(image, item.DestRect, item.SourceRect);
            else
                g.DrawImage(image, item.DestRect);
        }
    }

    private static void RenderDrawTiledImage(RGraphics g, DrawTiledImageItem item)
    {
        if (item.ImageHandle is not RImage image)
            return;

        var srcRect = item.SourceRect == RectangleF.Empty
            ? new RectangleF(0, 0, (float)image.Width, (float)image.Height)
            : item.SourceRect;

        var fill = item.FillRect;
        var origin = item.TileOrigin;

        // Clip to the element's padding box
        var clip = fill;
        clip.Intersect(g.GetClip());
        g.PushClip(clip);

        switch (item.Repeat)
        {
            case "no-repeat":
                g.DrawImage(image,
                    new RectangleF(origin.X, origin.Y, srcRect.Width, srcRect.Height), srcRect);
                break;
            case "repeat-x":
            {
                // Shift origin left to cover the fill area
                float ox = origin.X;
                while (ox > fill.X) ox -= srcRect.Width;
                using var brush = g.GetTextureBrush(image, srcRect, new PointF(ox, origin.Y));
                g.DrawRectangle(brush, fill.X, origin.Y, fill.Width, srcRect.Height);
                break;
            }
            case "repeat-y":
            {
                float oy = origin.Y;
                while (oy > fill.Y) oy -= srcRect.Height;
                using var brush = g.GetTextureBrush(image, srcRect, new PointF(origin.X, oy));
                g.DrawRectangle(brush, origin.X, fill.Y, srcRect.Width, fill.Height);
                break;
            }
            default: // "repeat"
            {
                float ox = origin.X;
                while (ox > fill.X) ox -= srcRect.Width;
                float oy = origin.Y;
                while (oy > fill.Y) oy -= srcRect.Height;
                using var brush = g.GetTextureBrush(image, srcRect, new PointF(ox, oy));
                g.DrawRectangle(brush, fill.X, fill.Y, fill.Width, fill.Height);
                break;
            }
        }

        g.PopClip();
    }

    private static void RenderDrawLine(RGraphics g, DrawLineItem item)
    {
        var pen = g.GetPen(item.Color);
        pen.Width = item.Width;
        pen.DashStyle = item.DashStyle switch
        {
            "dotted" => System.Drawing.Drawing2D.DashStyle.Dot,
            "dashed" => System.Drawing.Drawing2D.DashStyle.Dash,
            _ => System.Drawing.Drawing2D.DashStyle.Solid,
        };
        g.DrawLine(pen, item.Start.X, item.Start.Y, item.End.X, item.End.Y);
    }

    private static RPen CreateBorderPen(RGraphics g, string style, Color color, double width)
    {
        var pen = g.GetPen(color);
        pen.Width = width;
        pen.DashStyle = style switch
        {
            "dotted" => System.Drawing.Drawing2D.DashStyle.Dot,
            "dashed" => System.Drawing.Drawing2D.DashStyle.Dash,
            _ => System.Drawing.Drawing2D.DashStyle.Solid,
        };
        return pen;
    }

    private static bool IsBorderStyleVisible(string style)
    {
        return !string.IsNullOrEmpty(style) && style != "none" && style != "hidden";
    }
}
