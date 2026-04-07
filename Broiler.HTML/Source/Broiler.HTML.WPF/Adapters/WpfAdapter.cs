using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using FontStyle = System.Drawing.FontStyle;
using Color = System.Drawing.Color;
using Microsoft.Win32;
using RectangleF = System.Drawing.RectangleF;
using Broiler.HTML.Adapters;
using Broiler.HTML.Adapters.Adapters;
using Broiler.HTML.WPF.Utilities;
using SkiaSharp;

namespace Broiler.HTML.WPF.Adapters;

internal sealed class WpfAdapter : RAdapter
{
    private static readonly List<string> ValidColorNamesLc;

    static WpfAdapter()
    {
        ValidColorNamesLc = [];
        var colorList = new List<PropertyInfo>(typeof(Colors).GetProperties());

        foreach (var colorProp in colorList)
            ValidColorNamesLc.Add(colorProp.Name.ToLower());
    }

    private WpfAdapter()
    {
        var systemFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var family in Fonts.SystemFontFamilies)
        {
            try
            {
                AddFontFamily(new FontFamilyAdapter(family));
                systemFonts.Add(family.Source);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HtmlRenderer] WpfAdapter failed to add font family: {ex.Message}");
            }
        }

        // CSS 2.1 §15.3 generic font family mappings.
        MapGenericFamily("sans-serif", systemFonts, "Arial", "Helvetica", "Liberation Sans", "DejaVu Sans");
        MapGenericFamily("serif", systemFonts, "Times New Roman", "Liberation Serif", "DejaVu Serif");
        MapGenericFamily("monospace", systemFonts, "Courier New", "Liberation Mono", "DejaVu Sans Mono");
        MapGenericFamily("cursive", systemFonts, "Comic Sans MS", "URW Chancery L");
        MapGenericFamily("fantasy", systemFonts, "Impact");

        // Common alias: web content often uses "Helvetica" expecting Arial-like metrics.
        if (!systemFonts.Contains("Helvetica"))
        {
            var arialLike = FirstAvailable(systemFonts, "Arial", "Liberation Sans", "DejaVu Sans");
            if (arialLike != null)
                AddFontFamilyMapping("Helvetica", arialLike);
        }
    }

    /// <summary>
    /// Maps a CSS generic font family name to the first available system font.
    /// </summary>
    private void MapGenericFamily(string genericName, HashSet<string> systemFonts, params string[] candidates)
    {
        var resolved = FirstAvailable(systemFonts, candidates);
        if (resolved != null)
            AddFontFamilyMapping(genericName, resolved);
    }

    private static string? FirstAvailable(HashSet<string> systemFonts, params string[] candidates) => Array.Find(candidates, systemFonts.Contains);

    public static WpfAdapter Instance { get; } = new();

    protected override Color GetColorInt(string colorName)
    {
        // check if color name is valid to avoid ColorConverter throwing an exception
        if (!ValidColorNamesLc.Contains(colorName.ToLower()))
            return Color.Empty;

        var convertFromString = ColorConverter.ConvertFromString(colorName) ?? Colors.Black;
        return Utilities.Utils.Convert((System.Windows.Media.Color)convertFromString);
    }

    protected override RPen CreatePen(Color color) => new PenAdapter(GetSolidColorBrush(color));

    protected override RBrush CreateSolidBrush(Color color) => new BrushAdapter(GetSolidColorBrush(color));

    protected override RBrush CreateLinearGradientBrush(RectangleF rect, Color color1, Color color2, double angle)
    {
        var startColor = angle <= 180 ? Utilities.Utils.Convert(color1) : Utilities.Utils.Convert(color2);
        var endColor = angle <= 180 ? Utilities.Utils.Convert(color2) : Utilities.Utils.Convert(color1);
        angle = angle <= 180 ? angle : angle - 180;

        double x = angle < 135 ? Math.Max((angle - 45) / 90, 0) : 1;
        double y = angle <= 45 ? Math.Max(0.5 - angle / 90, 0) : angle > 135 ? Math.Abs(1.5 - angle / 90) : 0;
        return new BrushAdapter(new LinearGradientBrush(startColor, endColor, new Point(x, y), new Point(1 - x, 1 - y)));
    }

    protected override RImage ConvertImageInt(object image) => image != null ? new ImageAdapter((BitmapImage)image) : null;

    protected override RImage ImageFromStreamInt(Stream memoryStream)
    {
        // Read the stream into a byte array so we can inspect the content
        // before attempting a bitmap decode.  WPF's BitmapImage does not
        // support SVG, so we detect SVG data and rasterize it via Svg.Skia
        // (matching the approach used by SkiaImageAdapter).
        byte[] data;
        if (memoryStream is MemoryStream ms)
        {
            data = ms.ToArray();
        }
        else if (memoryStream.CanSeek)
        {
            data = new byte[memoryStream.Length - memoryStream.Position];
            _ = memoryStream.Read(data, 0, data.Length);
        }
        else
        {
            using var copy = new MemoryStream();
            memoryStream.CopyTo(copy);
            data = copy.ToArray();
        }

        if (IsSvgData(data))
        {
            return RasterizeSvgToBitmapImage(data);
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.StreamSource = new MemoryStream(data);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();

        return new ImageAdapter(bitmap);
    }

    /// <summary>
    /// Rasterizes SVG data to a WPF <see cref="BitmapImage"/> by first
    /// rendering through Svg.Skia into an <see cref="SKBitmap"/>, encoding
    /// the result as PNG, and then loading the PNG bytes into a BitmapImage.
    /// </summary>
    private static RImage RasterizeSvgToBitmapImage(byte[] data)
    {
        var svgContent = System.Text.Encoding.UTF8.GetString(data);

        using var svg = new Svg.Skia.SKSvg();
        svg.FromSvg(svgContent);

        if (svg.Picture == null)
            return null;

        var bounds = svg.Picture.CullRect;
        int width = (int)Math.Ceiling(bounds.Width);
        int height = (int)Math.Ceiling(bounds.Height);

        // HTML spec default for replaced elements with no intrinsic size.
        if (width <= 0) width = 300;
        if (height <= 0) height = 150;

        using var skBitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(skBitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawPicture(svg.Picture);

        using var image = SKImage.FromBitmap(skBitmap);
        using var pngData = image.Encode(SKEncodedImageFormat.Png, 100);
        var pngStream = new MemoryStream(pngData.ToArray());

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.StreamSource = pngStream;
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();

        return new ImageAdapter(bitmap);
    }

    /// <summary>
    /// Determines whether the given byte array contains SVG image data by
    /// looking for an XML declaration (&lt;?xml) or an &lt;svg root element
    /// within the first 1 KB of content (after skipping leading whitespace
    /// and any UTF-8 BOM).
    /// </summary>
    private static bool IsSvgData(byte[] data)
    {
        if (data == null || data.Length < 4)
            return false;

        // Skip UTF-8 BOM if present
        int offset = 0;
        if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
            offset = 3;

        // Skip leading whitespace
        while (offset < data.Length && (data[offset] == ' ' || data[offset] == '\t' ||
               data[offset] == '\r' || data[offset] == '\n'))
            offset++;

        if (offset >= data.Length)
            return false;

        // Scan the first 1 KB for SVG markers
        int scanLength = Math.Min(data.Length, offset + 1024);
        var header = System.Text.Encoding.UTF8.GetString(data, offset, scanLength - offset);

        // Check for XML declaration followed by <svg, or a direct <svg element
        return (header.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) &&
               header.Contains("<svg", StringComparison.OrdinalIgnoreCase)) ||
               header.StartsWith("<svg", StringComparison.OrdinalIgnoreCase);
    }

    protected override RFont CreateFontInt(string family, double size, FontStyle style)
    {
        var fontFamily = (FontFamily)new FontFamilyConverter().ConvertFromString(family) ?? new FontFamily();
        return new FontAdapter(new Typeface(fontFamily, GetWpfFontStyle(style), GetFontWidth(style), FontStretches.Normal), size);
    }

    protected override RFont CreateFontInt(RFontFamily family, double size, FontStyle style) => new FontAdapter(new Typeface(((FontFamilyAdapter)family).FontFamily, GetWpfFontStyle(style), GetFontWidth(style), FontStretches.Normal), size);

    protected override object GetClipboardDataObjectInt(string html, string plainText) => ClipboardHelper.CreateDataObject(html, plainText);

    protected override void SetToClipboardInt(string text) => ClipboardHelper.CopyToClipboard(text);

    protected override void SetToClipboardInt(string html, string plainText) => ClipboardHelper.CopyToClipboard(html, plainText);

    protected override void SetToClipboardInt(RImage image) => Clipboard.SetImage(((ImageAdapter)image).Image);

    protected override RContextMenu CreateContextMenuInt() => new ContextMenuAdapter();

    protected override void SaveToFileInt(RImage image, string name, string extension, RControl control = null)
    {
        var saveDialog = new SaveFileDialog
        {
            Filter = "Images|*.png;*.bmp;*.jpg;*.tif;*.gif;*.wmp;",
            FileName = name,
            DefaultExt = extension
        };

        var dialogResult = saveDialog.ShowDialog();
        
        if (!dialogResult.GetValueOrDefault())
            return;

        var encoder = Utilities.Utils.GetBitmapEncoder(Path.GetExtension(saveDialog.FileName));
        encoder.Frames.Add(BitmapFrame.Create(((ImageAdapter)image).Image));
        using FileStream stream = new(saveDialog.FileName, FileMode.OpenOrCreate);
        encoder.Save(stream);
    }

    private static Brush GetSolidColorBrush(Color color)
    {
        Brush solidBrush;
        if (color == Color.White)
            solidBrush = Brushes.White;
        else if (color == Color.Black)
            solidBrush = Brushes.Black;
        else if (color.A < 1)
            solidBrush = Brushes.Transparent;
        else
            solidBrush = new SolidColorBrush(Utilities.Utils.Convert(color));
        return solidBrush;
    }

    private static System.Windows.FontStyle GetWpfFontStyle(FontStyle style)
    {
        if ((style & FontStyle.Italic) == FontStyle.Italic)
            return FontStyles.Italic;

        return FontStyles.Normal;
    }

    private static FontWeight GetFontWidth(FontStyle style)
    {
        if ((style & FontStyle.Bold) == FontStyle.Bold)
            return FontWeights.Bold;

        return FontWeights.Normal;
    }
}