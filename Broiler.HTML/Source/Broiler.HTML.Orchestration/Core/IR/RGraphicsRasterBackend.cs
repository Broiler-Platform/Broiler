using Broiler.HTML.Adapters.Adapters;
using Broiler.HTML.Core.Core.IR;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Broiler.HTML.Orchestration.Core.IR;

/// <summary>
/// <see cref="IRasterBackend"/> implementation that replays a <see cref="DisplayList"/>
/// onto an <see cref="RGraphics"/> surface. Bridges the new IR paint pipeline back to
/// the existing platform adapters.
/// </summary>
internal sealed class RGraphicsRasterBackend : IRasterBackend
{
    public static readonly RGraphicsRasterBackend Instance = new();

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
                case DrawTiledGradientItem tiledGrad:
                    RenderDrawTiledGradient(g, tiledGrad);
                    break;
                case DrawLineItem line:
                    RenderDrawLine(g, line);
                    break;
                case DrawSvgRectItem svgRect:
                    RenderSvgRect(g, svgRect);
                    break;
                case DrawSvgEllipseItem svgEllipse:
                    RenderSvgEllipse(g, svgEllipse);
                    break;
                case DrawSvgTextItem svgText:
                    RenderSvgText(g, svgText);
                    break;
                case DrawSvgLineItem svgLine:
                    RenderSvgLine(g, svgLine);
                    break;
                case ClipItem clip:
                    if (clip.CornerNw > 0 || clip.CornerNe > 0 || clip.CornerSe > 0 || clip.CornerSw > 0)
                        g.PushClipRounded(clip.ClipRect, clip.CornerNw, clip.CornerNe, clip.CornerSe, clip.CornerSw);
                    else
                        g.PushClip(clip.ClipRect);
                    break;
                case RestoreItem:
                    g.PopClip();
                    break;
                case OpacityItem opacityItem:
                    g.SaveOpacityLayer(opacityItem.Opacity);
                    break;
                case RestoreOpacityItem:
                    g.RestoreOpacityLayer();
                    break;
                case BlendModeItem blendItem:
                    g.SaveBlendLayer(blendItem.Mode);
                    break;
                case RestoreBlendModeItem:
                    g.RestoreBlendLayer();
                    break;
            }
        }
    }

    private static void RenderFillRect(RGraphics g, FillRectItem item)
    {
        using var brush = g.GetSolidBrush(item.Color);
        // CSS2.1 §14.2: backgrounds extend to the padding edge.
        // P3.2: Do NOT round absolute coordinates — the canvas
        // transform already contains a fractional scroll offset that
        // aligns integer *layout* positions to exact pixel boundaries.
        // Rounding absolute coords shifts the fill by ~0.09 px in
        // viewport space, causing partial-coverage AA artifacts at
        // element edges (e.g. (231,231,231) vs (255,255,255)).
        g.DrawRectangle(brush, item.Bounds.X, item.Bounds.Y, item.Bounds.Width, item.Bounds.Height);
    }

    private static void RenderDrawBorder(RGraphics g, DrawBorderItem item)
    {
        var bounds = item.Bounds;
        var widths = item.Widths;

        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        // P3.1/P3.2 audit: Use raw layout coordinates for border edges.
        // The canvas transform contains the fractional scroll offset
        // that maps integer layout positions to exact pixel boundaries.
        // Rounding absolute coords was tested (Round, Floor, Ceiling
        // on origin, inner edges, or all edges) and always regressed
        // because it shifted borders by ~0.09 px in viewport space,
        // creating new partial-coverage artifacts.  The existing
        // rendering with SKPaint.IsAntialias = true produces the
        // correct CSS 2.1 Appendix E paint order.

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
                g.DrawLine(pen, Math.Ceiling(bounds.Left), bounds.Top + widths.Top / 2, bounds.Right - 1, bounds.Top + widths.Top / 2);
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
                g.DrawLine(pen, bounds.Left + widths.Left / 2, Math.Ceiling(bounds.Top), bounds.Left + widths.Left / 2, Math.Floor(bounds.Bottom));
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
            var origin = new PointF((float)Math.Round(item.Origin.X), (float)Math.Round(item.Origin.Y));
            var size = new SizeF(item.Bounds.Width, item.Bounds.Height);

            // Draw text shadow first (behind the actual text)
            if (!item.TextShadowColor.IsEmpty &&
                (item.TextShadowOffsetX != 0 || item.TextShadowOffsetY != 0))
            {
                var shadowOrigin = new PointF(
                    origin.X + item.TextShadowOffsetX,
                    origin.Y + item.TextShadowOffsetY);
                g.DrawString(item.Text, font, item.TextShadowColor, shadowOrigin, size, item.IsRtl);
            }

            g.DrawString(item.Text, font, item.Color, origin, size, item.IsRtl);
        }
    }

    private static void RenderDrawImage(RGraphics g, DrawImageItem item)
    {
        if (item.ImageHandle is not RImage image)
            return;

        if (item.SourceRect != RectangleF.Empty)
            g.DrawImage(image, item.DestRect, item.SourceRect);
        else
            g.DrawImage(image, item.DestRect);
    }

    private static void RenderDrawTiledImage(RGraphics g, DrawTiledImageItem item)
    {
        if (item.ImageHandle is not RImage image)
            return;

        var srcRect = item.SourceRect == RectangleF.Empty
            ? new RectangleF(0, 0, (float)image.Width, (float)image.Height)
            : item.SourceRect;

        // CSS background-size: when TileWidth/TileHeight are specified,
        // the visual tile dimensions differ from the source image dimensions.
        float tileW = item.TileWidth > 0 ? item.TileWidth : srcRect.Width;
        float tileH = item.TileHeight > 0 ? item.TileHeight : srcRect.Height;

        var fill = item.FillRect;
        var origin = item.TileOrigin;

        // Clip to the element's padding box
        var clip = fill;
        clip.Intersect(g.GetClip());
        g.PushClip(clip);

        switch (item.Repeat)
        {
            case "no-repeat":
                g.DrawImage(image, new RectangleF(origin.X, origin.Y, tileW, tileH), srcRect);
                break;
            case "repeat-x":
                {
                    // Shift origin left to cover the fill area
                    float ox = origin.X;
                    while (ox > fill.X) ox -= tileW;
                    if (tileW == srcRect.Width && tileH == srcRect.Height)
                    {
                        using var brush = g.GetTextureBrush(image, srcRect, new PointF(ox, origin.Y));
                        g.DrawRectangle(brush, fill.X, origin.Y, fill.Width, tileH);
                    }
                    else
                    {
                        // Scaled tiles: draw individual tiles
                        for (float tx = ox; tx < fill.Right; tx += tileW)
                            g.DrawImage(image, new RectangleF(tx, origin.Y, tileW, tileH), srcRect);
                    }
                    break;
                }
            case "repeat-y":
                {
                    float oy = origin.Y;
                    while (oy > fill.Y) oy -= tileH;
                    if (tileW == srcRect.Width && tileH == srcRect.Height)
                    {
                        using var brush = g.GetTextureBrush(image, srcRect, new PointF(origin.X, oy));
                        g.DrawRectangle(brush, origin.X, fill.Y, tileW, fill.Height);
                    }
                    else
                    {
                        for (float ty = oy; ty < fill.Bottom; ty += tileH)
                            g.DrawImage(image, new RectangleF(origin.X, ty, tileW, tileH), srcRect);
                    }
                    break;
                }
            default: // "repeat"
                {
                    float ox = origin.X;
                    while (ox > fill.X) ox -= tileW;
                    float oy = origin.Y;
                    while (oy > fill.Y) oy -= tileH;
                    if (tileW == srcRect.Width && tileH == srcRect.Height)
                    {
                        using var brush = g.GetTextureBrush(image, srcRect, new PointF(ox, oy));
                        g.DrawRectangle(brush, fill.X, fill.Y, fill.Width, fill.Height);
                    }
                    else
                    {
                        for (float ty = oy; ty < fill.Bottom; ty += tileH)
                            for (float tx = ox; tx < fill.Right; tx += tileW)
                                g.DrawImage(image, new RectangleF(tx, ty, tileW, tileH), srcRect);
                    }
                    break;
                }
        }

        g.PopClip();
    }

    private static void RenderDrawTiledGradient(RGraphics g, DrawTiledGradientItem item)
    {
        int tileW = (int)Math.Max(1, item.TileWidth);
        int tileH = (int)Math.Max(1, item.TileHeight);
        var fill = item.FillRect;
        var origin = item.TileOrigin;

        // Build color/position arrays from the pre-parsed stops.
        Color[] colors;
        float[] positions;
        if (item.Stops != null && item.Stops.Count > 0)
        {
            colors = new Color[item.Stops.Count];
            positions = new float[item.Stops.Count];
            for (int i = 0; i < item.Stops.Count; i++)
            {
                colors[i] = item.Stops[i].Color;
                positions[i] = item.Stops[i].Position;
            }
        }
        else
        {
            return; // No stops to render.
        }

        using var tileImage = g.CreateLinearGradientTile(tileW, tileH, colors, positions, item.Angle);
        if (tileImage == null)
            return;

        var srcRect = new RectangleF(0, 0, tileW, tileH);

        // Clip to the element's fill area.
        var clip = fill;
        clip.Intersect(g.GetClip());
        g.PushClip(clip);

        switch (item.Repeat)
        {
            case "no-repeat":
                g.DrawImage(tileImage, new RectangleF(origin.X, origin.Y, tileW, tileH), srcRect);
                break;
            case "repeat-x":
            {
                float ox = origin.X;
                while (ox > fill.X) ox -= tileW;
                using var brush = g.GetTextureBrush(tileImage, srcRect, new PointF(ox, origin.Y));
                g.DrawRectangle(brush, fill.X, origin.Y, fill.Width, tileH);
                break;
            }
            case "repeat-y":
            {
                float oy = origin.Y;
                while (oy > fill.Y) oy -= tileH;
                using var brush = g.GetTextureBrush(tileImage, srcRect, new PointF(origin.X, oy));
                g.DrawRectangle(brush, origin.X, fill.Y, tileW, fill.Height);
                break;
            }
            default: // "repeat"
            {
                float ox = origin.X;
                while (ox > fill.X) ox -= tileW;
                float oy = origin.Y;
                while (oy > fill.Y) oy -= tileH;
                using var brush = g.GetTextureBrush(tileImage, srcRect, new PointF(ox, oy));
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
            "dotted" => DashStyle.Dot,
            "dashed" => DashStyle.Dash,
            _ => DashStyle.Solid,
        };
        g.DrawLine(pen, item.Start.X, item.Start.Y, item.End.X, item.End.Y);
    }

    private static void RenderSvgRect(RGraphics g, DrawSvgRectItem item)
    {
        double x = item.Bounds.X + item.X;
        double y = item.Bounds.Y + item.Y;
        if (!item.Fill.IsEmpty && item.Fill.A > 0)
            g.DrawRectangle(g.GetSolidBrush(item.Fill), x, y, item.Width, item.Height);
        if (!item.Stroke.IsEmpty && item.Stroke.A > 0 && item.StrokeWidth > 0)
        {
            var pen = g.GetPen(item.Stroke);
            pen.Width = item.StrokeWidth;
            g.DrawRectangle(pen, x, y, item.Width, item.Height);
        }
    }

    private static void RenderSvgEllipse(RGraphics g, DrawSvgEllipseItem item)
    {
        // RGraphics has no native ellipse; approximate with the bounding rectangle fill.
        double x = item.Bounds.X + item.Cx - item.Rx;
        double y = item.Bounds.Y + item.Cy - item.Ry;
        double w = item.Rx * 2;
        double h = item.Ry * 2;
        if (!item.Fill.IsEmpty && item.Fill.A > 0)
            g.DrawRectangle(g.GetSolidBrush(item.Fill), x, y, w, h);
    }

    private static void RenderSvgText(RGraphics g, DrawSvgTextItem item)
    {
        if (string.IsNullOrEmpty(item.Text))
            return;
        if (item.FontHandle is RFont font)
        {
            var origin = new PointF(item.Bounds.X + item.X, item.Bounds.Y + item.Y);
            var size = new SizeF(item.Bounds.Width, item.Bounds.Height);
            g.DrawString(item.Text, font, item.Fill, origin, size, false);
        }
    }

    private static void RenderSvgLine(RGraphics g, DrawSvgLineItem item)
    {
        if (!item.Stroke.IsEmpty && item.Stroke.A > 0 && item.StrokeWidth > 0)
        {
            var pen = g.GetPen(item.Stroke);
            pen.Width = item.StrokeWidth;
            g.DrawLine(pen,
                item.Bounds.X + item.X1, item.Bounds.Y + item.Y1,
                item.Bounds.X + item.X2, item.Bounds.Y + item.Y2);
        }
    }

    private static RPen CreateBorderPen(RGraphics g, string style, Color color, double width)
    {
        var pen = g.GetPen(color);
        pen.Width = width;
        pen.DashStyle = style switch
        {
            "dotted" => DashStyle.Dot,
            "dashed" => DashStyle.Dash,
            _ => DashStyle.Solid,
        };
        return pen;
    }

    private static bool IsBorderStyleVisible(string style) => !string.IsNullOrEmpty(style) && style != "none" && style != "hidden";
}
