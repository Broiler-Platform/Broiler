using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using TheArtOfDev.HtmlRenderer.Adapters;
using TheArtOfDev.HtmlRenderer.Avalonia.Utilities;
using Color = System.Drawing.Color;
using PointF = System.Drawing.PointF;
using SizeF = System.Drawing.SizeF;
using RectangleF = System.Drawing.RectangleF;

namespace TheArtOfDev.HtmlRenderer.Avalonia.Adapters;

internal sealed class GraphicsAdapter : RGraphics
{
    private readonly DrawingContext _g;
    private readonly bool _releaseGraphics;
    /// <summary>
    /// Stack of disposable state objects returned by Avalonia PushClip / PushGeometryClip.
    /// Each Pop restores the previous clip by disposing the state.
    /// </summary>
    private readonly Stack<DrawingContext.PushedState> _clipStateStack = new();

    public GraphicsAdapter(DrawingContext g, RectangleF initialClip, bool releaseGraphics = false) : base(AvaloniaAdapter.Instance, initialClip)
    {
        ArgumentNullException.ThrowIfNull(g);

        _g = g;
        _releaseGraphics = releaseGraphics;
    }

    public GraphicsAdapter() : base(AvaloniaAdapter.Instance, RectangleF.Empty)
    {
        _g = null;
        _releaseGraphics = false;
    }

    public override void PopClip()
    {
        if (_clipStateStack.Count > 0)
            _clipStateStack.Pop().Dispose();
        _clipStack.Pop();
    }

    public override void PushClip(RectangleF rect)
    {
        _clipStack.Push(rect);
        _clipStateStack.Push(_g!.PushClip(Utils.Convert(rect)));
    }

    public override void PushClipExclude(RectangleF rect)
    {
        var outerRect = _clipStack.Peek();
        var geometry = new CombinedGeometry
        {
            Geometry1 = new RectangleGeometry(Utils.Convert(outerRect)),
            Geometry2 = new RectangleGeometry(Utils.Convert(rect)),
            GeometryCombineMode = GeometryCombineMode.Exclude
        };

        _clipStack.Push(outerRect);
        _clipStateStack.Push(_g!.PushGeometryClip(geometry));
    }

    public override Object SetAntiAliasSmoothingMode() => null!;

    public override void ReturnPreviousSmoothingMode(Object prevMode)
    { }

    public override SizeF MeasureString(string str, RFont font)
    {
        var fontAdapter = (FontAdapter)font;
        var glyphTypeface = fontAdapter.GlyphTypeface;

        if (glyphTypeface != null)
        {
            double width = 0;
            double emPx = 96d / 72d * font.Size;

            for (int i = 0; i < str.Length; i++)
            {
                var codepoint = (uint)str[i];
                var glyphIndex = glyphTypeface.GetGlyph(codepoint);

                if (glyphIndex == 0 && str[i] != '\0')
                {
                    width = 0;
                    break;
                }

                width += glyphTypeface.GetGlyphAdvance(glyphIndex);
            }

            if (width > 0)
                return new SizeF((float)(width * emPx), (float)font.Height);
        }

        // Fallback: use FormattedText
        var formattedText = new FormattedText(str, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, fontAdapter.Typeface, 96d / 72d * font.Size, Brushes.Red);
        return new SizeF((float)formattedText.WidthIncludingTrailingWhitespace, (float)formattedText.Height);
    }

    public override void MeasureString(string str, RFont font, double maxWidth, out int charFit, out double charFitWidth)
    {
        charFit = 0;
        charFitWidth = 0;

        var fontAdapter = (FontAdapter)font;
        var glyphTypeface = fontAdapter.GlyphTypeface;
        bool handled = false;

        if (glyphTypeface != null)
        {
            handled = true;
            double width = 0;
            double emPx = 96d / 72d * font.Size;

            for (int i = 0; i < str.Length; i++)
            {
                var codepoint = (uint)str[i];
                var glyphIndex = glyphTypeface.GetGlyph(codepoint);

                if (glyphIndex == 0 && str[i] != '\0')
                {
                    handled = false;
                    break;
                }

                double advanceWidth = glyphTypeface.GetGlyphAdvance(glyphIndex) * emPx;

                if (!(width + advanceWidth < maxWidth))
                {
                    charFit = i;
                    charFitWidth = width;
                    break;
                }
                width += advanceWidth;
            }
        }

        if (!handled)
        {
            var formattedText = new FormattedText(str, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, fontAdapter.Typeface, 96d / 72d * font.Size, Brushes.Red);
            charFit = str.Length;
            charFitWidth = formattedText.WidthIncludingTrailingWhitespace;
        }
    }

    public override void DrawString(string str, RFont font, Color color, PointF point, SizeF size, bool rtl)
    {
        var colorConv = ((BrushAdapter)_adapter.GetSolidBrush(color)).Brush;
        var fontAdapter = (FontAdapter)font;
        var glyphTypeface = fontAdapter.GlyphTypeface;

        bool glyphRendered = false;

        if (glyphTypeface != null)
        {
            double emPx = 96d / 72d * font.Size;
            ushort[] glyphs = new ushort[str.Length];
            double totalWidth = 0;

            int i = 0;
            for (; i < str.Length; i++)
            {
                var codepoint = (uint)str[i];
                var glyphIndex = glyphTypeface.GetGlyph(codepoint);

                if (glyphIndex == 0 && str[i] != '\0')
                    break;

                glyphs[i] = glyphIndex;
                totalWidth += glyphTypeface.GetGlyphAdvance(glyphIndex);
            }

            if (i >= str.Length)
            {
                double baseline = glyphTypeface.Metrics.Ascent * emPx;
                point.Y += (float)baseline;
                point.X += (float)(rtl ? emPx * totalWidth : 0);

                glyphRendered = true;

                var origin = Utils.ConvertRound(point);
                var glyphRun = new GlyphRun(glyphTypeface, emPx,
                    str.ToCharArray(),
                    glyphs);

                using (_g!.PushTransform(Matrix.CreateTranslation(origin.X, origin.Y)))
                {
                    _g.DrawGlyphRun(colorConv, glyphRun);
                }
            }
        }

        if (!glyphRendered)
        {
            var formattedText = new FormattedText(str, CultureInfo.CurrentCulture, rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight, fontAdapter.Typeface, 96d / 72d * font.Size, colorConv);
            point.X += (float)(rtl ? formattedText.Width : 0);
            _g!.DrawText(formattedText, Utils.ConvertRound(point));
        }
    }

    public override RBrush GetTextureBrush(RImage image, RectangleF dstRect, PointF translateTransformLocation)
    {
        var avaloniaImage = ((ImageAdapter)image).Image;
        var brush = new ImageBrush(avaloniaImage)
        {
            Stretch = Stretch.None,
            TileMode = TileMode.Tile,
            DestinationRect = new RelativeRect(dstRect.X, dstRect.Y, dstRect.Width, dstRect.Height, RelativeUnit.Absolute),
            Transform = new TranslateTransform(translateTransformLocation.X, translateTransformLocation.Y)
        };
        return new BrushAdapter(brush.ToImmutable());
    }

    public override RGraphicsPath GetGraphicsPath() => new GraphicsPathAdapter();

    public override void Dispose()
    {
        if (_releaseGraphics)
            _g?.Dispose();
    }

    public override void DrawLine(RPen pen, double x1, double y1, double x2, double y2)
    {
        x1 = (int)x1;
        x2 = (int)x2;
        y1 = (int)y1;
        y2 = (int)y2;

        var adj = pen.Width;
        if (Math.Abs(x1 - x2) < .1 && Math.Abs(adj % 2 - 1) < .1)
        {
            x1 += .5;
            x2 += .5;
        }
        if (Math.Abs(y1 - y2) < .1 && Math.Abs(adj % 2 - 1) < .1)
        {
            y1 += .5;
            y2 += .5;
        }

        _g!.DrawLine(((PenAdapter)pen).CreatePen(), new Point(x1, y1), new Point(x2, y2));
    }

    public override void DrawRectangle(RPen pen, double x, double y, double width, double height)
    {
        var adj = pen.Width;
        if (Math.Abs(adj % 2 - 1) < .1)
        {
            x += .5;
            y += .5;
        }

        _g!.DrawRectangle(null, ((PenAdapter)pen).CreatePen(), new Rect(x, y, width, height));
    }

    public override void DrawRectangle(RBrush brush, double x, double y, double width, double height) => _g!.DrawRectangle(((BrushAdapter)brush).Brush, null, new Rect(x, y, width, height));

    public override void DrawImage(RImage image, RectangleF destRect, RectangleF srcRect)
    {
        var avaloniaImage = ((ImageAdapter)image).Image;
        var src = new Rect(srcRect.X, srcRect.Y, srcRect.Width, srcRect.Height);
        var dest = Utils.ConvertRound(destRect);
        _g!.DrawImage(avaloniaImage, src, dest);
    }

    public override void DrawImage(RImage image, RectangleF destRect) => _g!.DrawImage(((ImageAdapter)image).Image, Utils.ConvertRound(destRect));

    public override void DrawPath(RPen pen, RGraphicsPath path) => _g!.DrawGeometry(null, ((PenAdapter)pen).CreatePen(), ((GraphicsPathAdapter)path).GetClosedGeometry());

    public override void DrawPath(RBrush brush, RGraphicsPath path) => _g!.DrawGeometry(((BrushAdapter)brush).Brush, null, ((GraphicsPathAdapter)path).GetClosedGeometry());

    public override void DrawPolygon(RBrush brush, PointF[] points)
    {
        if (points != null && points.Length > 0)
        {
            var g = new StreamGeometry();
            using (var context = g.Open())
            {
                context.BeginFigure(Utils.Convert(points[0]), true);
                for (int i = 1; i < points.Length; i++)
                    context.LineTo(Utils.Convert(points[i]));
                context.EndFigure(true);
            }

            _g!.DrawGeometry(((BrushAdapter)brush).Brush, null, g);
        }
    }
}
