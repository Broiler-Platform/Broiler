using System;
using System.Drawing;
using SkiaSharp;
using SixLabors.Fonts;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImageSharpColor = SixLabors.ImageSharp.Color;
using ImageSharpPointF = SixLabors.ImageSharp.PointF;
using SixLaborsFont = SixLabors.Fonts.Font;

namespace Broiler.HTML.Image.Adapters;

internal sealed class SkiaTextShaper : ITextShaper
{
    private SkiaTextShaper() { }

    public static SkiaTextShaper Instance { get; } = new();

    public SizeF MeasureString(FontAdapter font, string text)
    {
        var width = font.Font.MeasureText(text);
        return new SizeF(width, (float)font.Height);
    }

    public void MeasureString(FontAdapter font, string text, double maxWidth, out int charFit, out double charFitWidth)
    {
        charFit = 0;
        charFitWidth = 0;

        for (int i = 1; i <= text.Length; i++)
        {
            var substring = text.Substring(0, i);
            var width = font.Font.MeasureText(substring);
            if (width > maxWidth)
                break;

            charFit = i;
            charFitWidth = width;
        }
    }

    public bool TryDrawString(BCanvas canvas, FontAdapter font, string text, Color color, PointF point)
    {
        if (!font.TryGetBroilerRenderFont(out var broilerFont))
            return false;

        using var textBitmap = RenderTextBitmap(
            broilerFont,
            text,
            static (context, options, textValue, colorValue) => context.DrawText(options, textValue, ToImageSharpColor(colorValue)),
            text,
            color);
        DrawBitmap(canvas, textBitmap, point);
        return true;
    }

    public bool TryDrawGradientString(BCanvas canvas, FontAdapter font, string text, RectangleF rect, PointF point, SizeF size, Color[] colors, float[] positions, float angle)
    {
        if (!font.TryGetBroilerRenderFont(out var broilerFont))
            return false;

        using var textBitmap = RenderTextBitmap(
            broilerFont,
            text,
            (context, options, textValue, gradientState) =>
            {
                if (gradientState.Colors.Length == 1)
                {
                    context.DrawText(options, textValue, ToImageSharpColor(gradientState.Colors[0]));
                    return;
                }

                float shaderWidth = Math.Max(gradientState.Rect.Width, TextMeasurer.MeasureSize(textValue, options).Width);
                float shaderHeight = Math.Max(gradientState.Rect.Height > 0 ? gradientState.Rect.Height : gradientState.Size.Height, broilerFont.Size);
                var shaderRect = new RectangleF(
                    gradientState.Rect.X - gradientState.Point.X,
                    gradientState.Rect.Y - gradientState.Point.Y,
                    shaderWidth,
                    shaderHeight);
                var (startPoint, endPoint) = GetGradientEndpoints(shaderRect, gradientState.Angle);
                var stops = new ColorStop[gradientState.Colors.Length];
                for (int i = 0; i < gradientState.Colors.Length; i++)
                {
                    float offset = gradientState.Positions != null && i < gradientState.Positions.Length
                        ? Math.Clamp(gradientState.Positions[i], 0f, 1f)
                        : (float)i / (gradientState.Colors.Length - 1);
                    stops[i] = new ColorStop(offset, ToImageSharpColor(gradientState.Colors[i]));
                }

                var brush = new LinearGradientBrush(
                    new ImageSharpPointF(startPoint.X, startPoint.Y),
                    new ImageSharpPointF(endPoint.X, endPoint.Y),
                    GradientRepetitionMode.None,
                    stops);
                context.DrawText(options, textValue, brush);
            },
            text,
            (Rect: rect, Point: point, Size: size, Colors: colors, Positions: positions, Angle: angle));
        DrawBitmap(canvas, textBitmap, point);
        return true;
    }

    public void DrawString(SKCanvas canvas, FontAdapter font, string text, Color color, PointF point)
    {
        var origin = GetDrawOrigin(font, point);
        using var paint = new SKPaint
        {
            Color = Utilities.Utils.Convert(color),
            IsAntialias = true,
        };

        canvas.DrawText(text, origin.X, origin.Y, font.RenderFont, paint);
    }

    public void DrawGradientString(SKCanvas canvas, FontAdapter font, string text, RectangleF rect, PointF point, SizeF size, Color[] colors, float[] positions, float angle)
    {
        var renderFont = font.RenderFont;
        var origin = GetDrawOrigin(font, point);
        float shaderWidth = Math.Max(rect.Width, renderFont.MeasureText(text));
        float shaderHeight = Math.Max(rect.Height > 0 ? rect.Height : size.Height, (float)font.Size);
        var shaderRect = new RectangleF(rect.X, rect.Y, shaderWidth, shaderHeight);

        var radians = angle * Math.PI / 180.0;
        float cx = shaderRect.X + shaderRect.Width / 2f;
        float cy = shaderRect.Y + shaderRect.Height / 2f;
        float halfDiag = Math.Max(shaderRect.Width, shaderRect.Height) / 2f;
        float sin = (float)Math.Sin(radians);
        float cos = (float)Math.Cos(radians);
        var startPoint = new SKPoint(cx - sin * halfDiag, cy + cos * halfDiag);
        var endPoint = new SKPoint(cx + sin * halfDiag, cy - cos * halfDiag);

        var skColors = new SKColor[colors.Length];
        for (int i = 0; i < colors.Length; i++)
            skColors[i] = Utilities.Utils.Convert(colors[i]);

        canvas.SaveLayer();
        using (var maskPaint = new SKPaint { Color = SKColors.White, IsAntialias = false })
        {
            canvas.DrawText(text, origin.X, origin.Y, renderFont, maskPaint);
        }

        using var shader = SKShader.CreateLinearGradient(startPoint, endPoint, skColors, positions, SKShaderTileMode.Clamp);
        using var gradientPaint = new SKPaint
        {
            Shader = shader,
            BlendMode = SKBlendMode.SrcIn,
            IsAntialias = false,
        };

        if (string.Equals(font.Typeface.FamilyName, "Ahem", StringComparison.OrdinalIgnoreCase)
            && !text.Contains(' '))
        {
            gradientPaint.BlendMode = SKBlendMode.SrcOver;
            canvas.DrawRect(Utilities.Utils.Convert(shaderRect), gradientPaint);
            canvas.Restore();
            return;
        }

        canvas.DrawRect(Utilities.Utils.Convert(shaderRect), gradientPaint);
        canvas.Restore();
    }

    private static void DrawBitmap(BCanvas canvas, BBitmap textBitmap, PointF point)
    {
        canvas.DrawBitmap(
            textBitmap,
            new RectangleF(point.X, point.Y, textBitmap.Width, textBitmap.Height),
            new RectangleF(0, 0, textBitmap.Width, textBitmap.Height));
    }

    private static BBitmap RenderTextBitmap<TState>(
        SixLaborsFont font,
        string text,
        Action<IImageProcessingContext, RichTextOptions, string, TState> drawAction,
        string textValue,
        TState state)
    {
        var options = CreateTextOptions(font);
        var size = TextMeasurer.MeasureSize(text, options);
        var bounds = TextMeasurer.MeasureBounds(text, options);
        int width = Math.Max(1, (int)Math.Ceiling(Math.Max(size.Width, bounds.Right)));
        int height = Math.Max(1, (int)Math.Ceiling(Math.Max(size.Height, bounds.Bottom)));

        using var image = new SixLabors.ImageSharp.Image<Rgba32>(width, height, ImageSharpColor.Transparent);
        image.Mutate(context => drawAction(context, options, textValue, state));
        return BBitmap.CreateFromImageSharpImage(image);
    }

    private static ImageSharpColor ToImageSharpColor(Color color) =>
        ImageSharpColor.FromRgba(color.R, color.G, color.B, color.A);

    private static RichTextOptions CreateTextOptions(SixLaborsFont font) =>
        new(font)
        {
            Origin = new ImageSharpPointF(0, 0),
            Dpi = 96,
            KerningMode = KerningMode.None,
        };

    private static PointF GetDrawOrigin(FontAdapter font, PointF topLeft)
    {
        var metrics = font.RenderFont.Metrics;
        return new PointF(topLeft.X, topLeft.Y - metrics.Ascent);
    }

    private static (PointF StartPoint, PointF EndPoint) GetGradientEndpoints(RectangleF rect, float angle)
    {
        var radians = angle * Math.PI / 180.0;
        float cx = rect.X + rect.Width / 2f;
        float cy = rect.Y + rect.Height / 2f;
        float halfDiag = Math.Max(rect.Width, rect.Height) / 2f;
        float sin = (float)Math.Sin(radians);
        float cos = (float)Math.Cos(radians);
        return (
            new PointF(cx - sin * halfDiag, cy + cos * halfDiag),
            new PointF(cx + sin * halfDiag, cy - cos * halfDiag));
    }
}
