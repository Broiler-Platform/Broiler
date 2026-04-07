using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace Broiler.HtmlBridge;

/// <summary>Supported image format types.</summary>
public enum ImageFormat { Png, Jpeg, Gif, Bmp, Svg, WebP, Unknown }

/// <summary>Represents a decoded image with its raw pixel data.</summary>
/// <remarks>Initializes a new <see cref="DecodedImage"/>.</remarks>
public class DecodedImage(int width, int height, ImageFormat format, byte[] pixelData, string source)
{
    /// <summary>Width of the image in pixels.</summary>
    public int Width { get; } = width;
    /// <summary>Height of the image in pixels.</summary>
    public int Height { get; } = height;
    /// <summary>Detected or specified image format.</summary>
    public ImageFormat Format { get; } = format;
    /// <summary>RGBA pixel data (4 bytes per pixel).</summary>
    public byte[] PixelData { get; } = pixelData;
    /// <summary>Original source URL or data URI.</summary>
    public string Source { get; } = source;
}

/// <summary>Static utility class for decoding images from various sources.</summary>
public static class ImageDecoder
{
    /// <summary>Detects the image format from a file extension or data URI MIME type.</summary>
    public static ImageFormat DetectFormat(string source)
    {
        if (string.IsNullOrEmpty(source))
            return ImageFormat.Unknown;

        // Handle data URIs (e.g. "data:image/png;base64,...")
        if (source.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            if (source.Contains("image/png", StringComparison.OrdinalIgnoreCase)) return ImageFormat.Png;
            if (source.Contains("image/jpeg", StringComparison.OrdinalIgnoreCase)) return ImageFormat.Jpeg;
            if (source.Contains("image/gif", StringComparison.OrdinalIgnoreCase)) return ImageFormat.Gif;
            if (source.Contains("image/bmp", StringComparison.OrdinalIgnoreCase)) return ImageFormat.Bmp;
            if (source.Contains("image/svg+xml", StringComparison.OrdinalIgnoreCase)) return ImageFormat.Svg;
            if (source.Contains("image/webp", StringComparison.OrdinalIgnoreCase)) return ImageFormat.WebP;
            return ImageFormat.Unknown;
        }

        // Strip query string and fragment before checking extension
        var path = source.Split('?', '#')[0];
        var ext = path.LastIndexOf('.') >= 0
            ? path.Substring(path.LastIndexOf('.')).ToLowerInvariant()
            : string.Empty;

        return ext switch
        {
            ".png" => ImageFormat.Png,
            ".jpg" or ".jpeg" => ImageFormat.Jpeg,
            ".gif" => ImageFormat.Gif,
            ".bmp" => ImageFormat.Bmp,
            ".svg" => ImageFormat.Svg,
            ".webp" => ImageFormat.WebP,
            _ => ImageFormat.Unknown,
        };
    }

    /// <summary>Detects the image format from magic bytes in the file header.</summary>
    public static ImageFormat DetectFormatFromBytes(byte[] data)
    {
        if (data == null || data.Length < 4)
            return ImageFormat.Unknown;

        // PNG: 89 50 4E 47
        if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
            return ImageFormat.Png;

        // JPEG: FF D8 FF
        if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return ImageFormat.Jpeg;

        // GIF: 47 49 46
        if (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46)
            return ImageFormat.Gif;

        // BMP: 42 4D
        if (data[0] == 0x42 && data[1] == 0x4D)
            return ImageFormat.Bmp;

        // SVG: XML-based — check for XML declaration or <svg root element
        if (IsSvgData(data))
            return ImageFormat.Svg;

        return ImageFormat.Unknown;
    }

    /// <summary>
    /// Determines whether the given byte array contains SVG image data by
    /// looking for an XML declaration (&lt;?xml) or an &lt;svg root element
    /// within the first 1 KB of content (after skipping leading whitespace
    /// and any UTF-8 BOM).
    /// </summary>
    internal static bool IsSvgData(byte[] data)
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

    /// <summary>Creates a transparent placeholder image of the given dimensions.</summary>
    public static DecodedImage CreatePlaceholder(int width, int height, ImageFormat format)
    {
        var pixelData = new byte[width * height * 4]; // all zeros = transparent black
        return new DecodedImage(width, height, format, pixelData, string.Empty);
    }

    /// <summary>
    /// Decodes a base64 data URI (e.g. "data:image/png;base64,...") into raw bytes.
    /// Returns null if the URI is not a valid base64 data URI.
    /// </summary>
    public static byte[]? DecodeDataUri(string dataUri)
    {
        if (string.IsNullOrEmpty(dataUri) || !dataUri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return null;

        // Find the comma that separates the header from the data.
        int commaIdx = dataUri.IndexOf(',');
        if (commaIdx < 0) return null;

        var header = dataUri[..commaIdx];
        var payload = dataUri[(commaIdx + 1)..];

        if (!header.Contains("base64", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            return Convert.FromBase64String(payload);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Shared <see cref="HttpClient"/> for fetching external images.
    /// </summary>
    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>
    /// Resolves and downloads an external image from an HTTP/HTTPS/file URL.
    /// Relative URLs are resolved against the <paramref name="pageUrl"/>.
    /// Returns the raw image bytes, or <c>null</c> on failure.
    /// </summary>
    public static byte[]? FetchExternalImageBytes(string imageUrl, string? pageUrl)
    {
        try
        {
            // Resolve relative URLs against the page URL
            string resolvedUrl;
            if (Uri.TryCreate(imageUrl, UriKind.Absolute, out _))
            {
                resolvedUrl = imageUrl;
            }
            else if (!string.IsNullOrEmpty(pageUrl)
                  && Uri.TryCreate(pageUrl, UriKind.Absolute, out var baseUri)
                  && Uri.TryCreate(baseUri, imageUrl, out var resolved))
            {
                resolvedUrl = resolved.AbsoluteUri;
            }
            else
            {
                return null;
            }

            // Handle file:// URLs — read from local filesystem
            if (resolvedUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(resolvedUrl);
                var path = uri.LocalPath;
                return System.IO.File.Exists(path) ? System.IO.File.ReadAllBytes(path) : null;
            }

            // Synchronous HTTP fetch
            var bytes = SharedHttpClient.GetByteArrayAsync(resolvedUrl)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
            return bytes;
        }
        catch (Exception ex)
        {
            RenderLogger.LogError(LogCategory.HtmlRenderer, "ImageDecoder.FetchExternalImageBytes",
                $"Failed to fetch external image '{imageUrl}': {ex.Message}", ex);
            return null;
        }
    }
}

/// <summary>A minimal SVG element representation for inline SVG rendering.</summary>
public class SvgElement
{
    /// <summary>Element tag name (e.g. "rect", "circle").</summary>
    public string TagName { get; set; } = string.Empty;
    /// <summary>Element attributes.</summary>
    public Dictionary<string, string> Attributes { get; set; } = [];
    /// <summary>Child elements.</summary>
    public List<SvgElement> Children { get; set; } = [];
    /// <summary>Text content of the element.</summary>
    public string TextContent { get; set; } = string.Empty;
}

/// <summary>Basic SVG parser for inline SVG content.</summary>
public static class SvgParser
{
    private static readonly HashSet<string> KnownElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "svg", "rect", "circle", "ellipse", "line", "polyline", "polygon",
        "path", "text", "g", "defs", "use"
    };

    private static readonly Regex TagRegex = new(
        @"<(/?)(\w+)((?:\s+[\w\-:]+\s*=\s*""[^""]*"")*)\s*(/?)>",
        RegexOptions.Compiled);

    private static readonly Regex AttrRegex = new(
        @"([\w\-:]+)\s*=\s*""([^""]*)""",
        RegexOptions.Compiled);

    /// <summary>Parses an SVG XML string into a <see cref="SvgElement"/> tree.</summary>
    public static SvgElement Parse(string svgContent)
    {
        if (string.IsNullOrEmpty(svgContent))
            return new SvgElement { TagName = "svg" };

        var root = new SvgElement { TagName = "svg" };
        var stack = new Stack<SvgElement>();
        stack.Push(root);

        int pos = 0;
        var matches = TagRegex.Matches(svgContent);

        foreach (Match match in matches)
        {
            var isClosing = match.Groups[1].Value == "/";
            var tagName = match.Groups[2].Value;
            var attrString = match.Groups[3].Value;
            var isSelfClosing = match.Groups[4].Value == "/";

            if (!KnownElements.Contains(tagName))
                continue;

            // Capture text content between previous position and this tag
            if (pos < match.Index && stack.Count > 0)
            {
                var text = svgContent.Substring(pos, match.Index - pos).Trim();
                if (!string.IsNullOrEmpty(text))
                    stack.Peek().TextContent += text;
            }
            pos = match.Index + match.Length;

            if (isClosing)
            {
                if (stack.Count > 1)
                    stack.Pop();
                continue;
            }

            var element = new SvgElement { TagName = tagName.ToLowerInvariant() };

            foreach (Match attr in AttrRegex.Matches(attrString))
                element.Attributes[attr.Groups[1].Value] = attr.Groups[2].Value;

            if (stack.Count > 0)
            {
                // Skip adding root "svg" as child of the implicit root
                if (stack.Count == 1 && tagName.Equals("svg", StringComparison.OrdinalIgnoreCase))
                {
                    // Copy attributes to existing root
                    foreach (var kv in element.Attributes)
                        root.Attributes[kv.Key] = kv.Value;
                    if (!isSelfClosing)
                        continue; // stay on root
                }
                else
                {
                    stack.Peek().Children.Add(element);
                }
            }

            if (!isSelfClosing)
                stack.Push(element);
        }

        return root;
    }

    /// <summary>Extracts the viewBox attribute as a <see cref="Rect"/> from the root SVG element.</summary>
    public static Rect GetViewBox(SvgElement svg)
    {
        if (svg == null || !svg.Attributes.TryGetValue("viewBox", out var vb))
            return default;

        var parts = vb.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4)
            return default;

        if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
            float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) &&
            float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var w) &&
            float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var h))
        {
            return new Rect(x, y, w, h);
        }

        return default;
    }
}

/// <summary>SVG draw command types.</summary>
public enum SvgDrawCommandType { Rectangle, Circle, Ellipse, Line, Polyline, Polygon, Path, Text, Group }

/// <summary>Represents a single SVG drawing instruction.</summary>
public class SvgDrawCommand
{
    /// <summary>Type of draw command.</summary>
    public SvgDrawCommandType Type { get; set; }
    /// <summary>X coordinate.</summary>
    public float X { get; set; }
    /// <summary>Y coordinate.</summary>
    public float Y { get; set; }
    /// <summary>Width (for rectangles and ellipses).</summary>
    public float Width { get; set; }
    /// <summary>Height (for rectangles and ellipses).</summary>
    public float Height { get; set; }
    /// <summary>Radius (for circles).</summary>
    public float Radius { get; set; }
    /// <summary>Point data for polyline/polygon elements.</summary>
    public List<float> Points { get; set; } = [];
    /// <summary>Fill color.</summary>
    public string Fill { get; set; } = string.Empty;
    /// <summary>Stroke color.</summary>
    public string Stroke { get; set; } = string.Empty;
    /// <summary>Stroke width.</summary>
    public float StrokeWidth { get; set; }
    /// <summary>Text content for text commands.</summary>
    public string Text { get; set; } = string.Empty;
}

/// <summary>Converts SVG elements to drawing instructions.</summary>
public static class SvgRenderer
{
    /// <summary>Renders an SVG element tree into a list of draw commands.</summary>
    public static IReadOnlyList<SvgDrawCommand> Render(SvgElement root)
    {
        var commands = new List<SvgDrawCommand>();
        if (root == null)
            return commands;

        RenderElement(root, commands);
        return commands;
    }

    private static void RenderElement(SvgElement element, List<SvgDrawCommand> commands)
    {
        var fill = GetAttr(element, "fill");
        var stroke = GetAttr(element, "stroke");
        var strokeWidth = ParseFloat(GetAttr(element, "stroke-width"));

        switch (element.TagName)
        {
            case "rect":
                commands.Add(new SvgDrawCommand
                {
                    Type = SvgDrawCommandType.Rectangle,
                    X = ParseFloat(GetAttr(element, "x")),
                    Y = ParseFloat(GetAttr(element, "y")),
                    Width = ParseFloat(GetAttr(element, "width")),
                    Height = ParseFloat(GetAttr(element, "height")),
                    Fill = fill, Stroke = stroke, StrokeWidth = strokeWidth,
                });
                break;

            case "circle":
                commands.Add(new SvgDrawCommand
                {
                    Type = SvgDrawCommandType.Circle,
                    X = ParseFloat(GetAttr(element, "cx")),
                    Y = ParseFloat(GetAttr(element, "cy")),
                    Radius = ParseFloat(GetAttr(element, "r")),
                    Fill = fill, Stroke = stroke, StrokeWidth = strokeWidth,
                });
                break;

            case "ellipse":
                commands.Add(new SvgDrawCommand
                {
                    Type = SvgDrawCommandType.Ellipse,
                    X = ParseFloat(GetAttr(element, "cx")),
                    Y = ParseFloat(GetAttr(element, "cy")),
                    Width = ParseFloat(GetAttr(element, "rx")) * 2,
                    Height = ParseFloat(GetAttr(element, "ry")) * 2,
                    Fill = fill, Stroke = stroke, StrokeWidth = strokeWidth,
                });
                break;

            case "line":
                commands.Add(new SvgDrawCommand
                {
                    Type = SvgDrawCommandType.Line,
                    X = ParseFloat(GetAttr(element, "x1")),
                    Y = ParseFloat(GetAttr(element, "y1")),
                    Width = ParseFloat(GetAttr(element, "x2")),
                    Height = ParseFloat(GetAttr(element, "y2")),
                    Stroke = stroke, StrokeWidth = strokeWidth,
                });
                break;

            case "polyline":
                commands.Add(new SvgDrawCommand
                {
                    Type = SvgDrawCommandType.Polyline,
                    Points = ParsePoints(GetAttr(element, "points")),
                    Fill = fill, Stroke = stroke, StrokeWidth = strokeWidth,
                });
                break;

            case "polygon":
                commands.Add(new SvgDrawCommand
                {
                    Type = SvgDrawCommandType.Polygon,
                    Points = ParsePoints(GetAttr(element, "points")),
                    Fill = fill, Stroke = stroke, StrokeWidth = strokeWidth,
                });
                break;

            case "path":
                commands.Add(new SvgDrawCommand
                {
                    Type = SvgDrawCommandType.Path,
                    Text = GetAttr(element, "d"),
                    Fill = fill, Stroke = stroke, StrokeWidth = strokeWidth,
                });
                break;

            case "text":
                commands.Add(new SvgDrawCommand
                {
                    Type = SvgDrawCommandType.Text,
                    X = ParseFloat(GetAttr(element, "x")),
                    Y = ParseFloat(GetAttr(element, "y")),
                    Text = element.TextContent,
                    Fill = fill, Stroke = stroke, StrokeWidth = strokeWidth,
                });
                break;

            case "g":
                commands.Add(new SvgDrawCommand
                {
                    Type = SvgDrawCommandType.Group,
                    Fill = fill, Stroke = stroke, StrokeWidth = strokeWidth,
                });
                break;
        }

        foreach (var child in element.Children)
            RenderElement(child, commands);
    }

    private static string GetAttr(SvgElement el, string name) => el.Attributes.TryGetValue(name, out var v) ? v : string.Empty;

    private static float ParseFloat(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0f;
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f;
    }

    private static List<float> ParsePoints(string value)
    {
        var result = new List<float>();
        if (string.IsNullOrEmpty(value))
            return result;

        foreach (var part in value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries))
        {
            if (float.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                result.Add(f);
        }
        return result;
    }
}

/// <summary>Canvas 2D draw command types.</summary>
public enum CanvasDrawCommandType
{
    /// <summary>Fill a rectangle.</summary>
    FillRect,
    /// <summary>Stroke a rectangle outline.</summary>
    StrokeRect,
    /// <summary>Clear a rectangular area.</summary>
    ClearRect,
    /// <summary>Begin a new path.</summary>
    BeginPath,
    /// <summary>Move the pen to a point.</summary>
    MoveTo,
    /// <summary>Draw a line to a point.</summary>
    LineTo,
    /// <summary>Draw an arc.</summary>
    Arc,
    /// <summary>Close the current path.</summary>
    ClosePath,
    /// <summary>Fill the current path.</summary>
    Fill,
    /// <summary>Stroke the current path.</summary>
    Stroke,
    /// <summary>Fill text at a position.</summary>
    FillText,
    /// <summary>Stroke text at a position.</summary>
    StrokeText,
    /// <summary>Save the current state.</summary>
    Save,
    /// <summary>Restore the previously saved state.</summary>
    Restore,
}

/// <summary>Represents a single canvas drawing command with all relevant parameters.</summary>
public class CanvasDrawCommand
{
    /// <summary>Type of draw command.</summary>
    public CanvasDrawCommandType Type { get; set; }
    /// <summary>X coordinate.</summary>
    public float X { get; set; }
    /// <summary>Y coordinate.</summary>
    public float Y { get; set; }
    /// <summary>Width.</summary>
    public float Width { get; set; }
    /// <summary>Height.</summary>
    public float Height { get; set; }
    /// <summary>Radius (for arc commands).</summary>
    public float Radius { get; set; }
    /// <summary>Start angle in radians (for arc commands).</summary>
    public float StartAngle { get; set; }
    /// <summary>End angle in radians (for arc commands).</summary>
    public float EndAngle { get; set; }
    /// <summary>Text content.</summary>
    public string Text { get; set; } = string.Empty;
    /// <summary>Fill style at the time of the command.</summary>
    public string FillStyle { get; set; } = string.Empty;
    /// <summary>Stroke style at the time of the command.</summary>
    public string StrokeStyle { get; set; } = string.Empty;
    /// <summary>Line width at the time of the command.</summary>
    public float LineWidth { get; set; }
    /// <summary>Global alpha at the time of the command.</summary>
    public float GlobalAlpha { get; set; }
}

/// <summary>Represents the HTML5 Canvas 2D rendering context with basic drawing operations.</summary>
/// <remarks>Initializes a new <see cref="CanvasRenderingContext2D"/> with the given dimensions.</remarks>
public class CanvasRenderingContext2D(int width, int height)
{
    /// <summary>Canvas width in pixels.</summary>
    public int Width { get; } = width;
    /// <summary>Canvas height in pixels.</summary>
    public int Height { get; } = height;
    /// <summary>Current fill color.</summary>
    public string FillStyle { get; set; } = "#000000";
    /// <summary>Current stroke color.</summary>
    public string StrokeStyle { get; set; } = "#000000";
    /// <summary>Current line width.</summary>
    public float LineWidth { get; set; } = 1.0f;
    /// <summary>Current font specification.</summary>
    public string Font { get; set; } = "10px sans-serif";
    /// <summary>Current text alignment.</summary>
    public string TextAlign { get; set; } = "start";
    /// <summary>Current global alpha (transparency).</summary>
    public float GlobalAlpha { get; set; } = 1.0f;

    /// <summary>Recorded drawing commands for later rendering.</summary>
    internal List<CanvasDrawCommand> Commands { get; } = [];

    private readonly Stack<CanvasState> _stateStack = new();

    /// <summary>Fills a rectangle at the specified position and size.</summary>
    public void FillRect(float x, float y, float width, float height) => Commands.Add(new CanvasDrawCommand
    {
        Type = CanvasDrawCommandType.FillRect,
        X = x,
        Y = y,
        Width = width,
        Height = height,
        FillStyle = FillStyle,
        LineWidth = LineWidth,
        GlobalAlpha = GlobalAlpha,
    });

    /// <summary>Strokes a rectangle outline at the specified position and size.</summary>
    public void StrokeRect(float x, float y, float width, float height) => Commands.Add(new CanvasDrawCommand
    {
        Type = CanvasDrawCommandType.StrokeRect,
        X = x,
        Y = y,
        Width = width,
        Height = height,
        StrokeStyle = StrokeStyle,
        LineWidth = LineWidth,
        GlobalAlpha = GlobalAlpha,
    });

    /// <summary>Clears a rectangular area, making it fully transparent.</summary>
    public void ClearRect(float x, float y, float width, float height) => Commands.Add(new CanvasDrawCommand
    {
        Type = CanvasDrawCommandType.ClearRect,
        X = x,
        Y = y,
        Width = width,
        Height = height,
        GlobalAlpha = GlobalAlpha,
    });

    /// <summary>Begins a new drawing path.</summary>
    public void BeginPath() => Commands.Add(new CanvasDrawCommand { Type = CanvasDrawCommandType.BeginPath });

    /// <summary>Moves the pen to the specified point without drawing.</summary>
    public void MoveTo(float x, float y) => Commands.Add(new CanvasDrawCommand
    {
        Type = CanvasDrawCommandType.MoveTo,
        X = x,
        Y = y,
    });

    /// <summary>Draws a straight line from the current point to the specified point.</summary>
    public void LineTo(float x, float y) => Commands.Add(new CanvasDrawCommand
    {
        Type = CanvasDrawCommandType.LineTo,
        X = x,
        Y = y,
    });

    /// <summary>Draws an arc centered at (x, y) with the given radius and angles.</summary>
    public void Arc(float x, float y, float radius, float startAngle, float endAngle) => Commands.Add(new CanvasDrawCommand
    {
        Type = CanvasDrawCommandType.Arc,
        X = x,
        Y = y,
        Radius = radius,
        StartAngle = startAngle,
        EndAngle = endAngle,
    });

    /// <summary>Closes the current path by connecting the last point to the first.</summary>
    public void ClosePath() => Commands.Add(new CanvasDrawCommand { Type = CanvasDrawCommandType.ClosePath });

    /// <summary>Fills the current path with the current fill style.</summary>
    public void Fill() => Commands.Add(new CanvasDrawCommand
    {
        Type = CanvasDrawCommandType.Fill,
        FillStyle = FillStyle,
        GlobalAlpha = GlobalAlpha,
    });

    /// <summary>Strokes the current path with the current stroke style.</summary>
    public void Stroke() => Commands.Add(new CanvasDrawCommand
    {
        Type = CanvasDrawCommandType.Stroke,
        StrokeStyle = StrokeStyle,
        LineWidth = LineWidth,
        GlobalAlpha = GlobalAlpha,
    });

    /// <summary>Fills text at the specified position.</summary>
    public void FillText(string text, float x, float y) => Commands.Add(new CanvasDrawCommand
    {
        Type = CanvasDrawCommandType.FillText,
        Text = text,
        X = x,
        Y = y,
        FillStyle = FillStyle,
        GlobalAlpha = GlobalAlpha,
    });

    /// <summary>Strokes text at the specified position.</summary>
    public void StrokeText(string text, float x, float y) => Commands.Add(new CanvasDrawCommand
    {
        Type = CanvasDrawCommandType.StrokeText,
        Text = text,
        X = x,
        Y = y,
        StrokeStyle = StrokeStyle,
        LineWidth = LineWidth,
        GlobalAlpha = GlobalAlpha,
    });

    /// <summary>Saves the current drawing state onto a stack.</summary>
    public void Save()
    {
        _stateStack.Push(new CanvasState
        {
            FillStyle = FillStyle,
            StrokeStyle = StrokeStyle,
            LineWidth = LineWidth,
            Font = Font,
            TextAlign = TextAlign,
            GlobalAlpha = GlobalAlpha,
        });
        Commands.Add(new CanvasDrawCommand { Type = CanvasDrawCommandType.Save });
    }

    /// <summary>Restores the most recently saved drawing state from the stack.</summary>
    public void Restore()
    {
        if (_stateStack.Count > 0)
        {
            var state = _stateStack.Pop();
            FillStyle = state.FillStyle;
            StrokeStyle = state.StrokeStyle;
            LineWidth = state.LineWidth;
            Font = state.Font;
            TextAlign = state.TextAlign;
            GlobalAlpha = state.GlobalAlpha;
        }
        Commands.Add(new CanvasDrawCommand { Type = CanvasDrawCommandType.Restore });
    }

    private class CanvasState
    {
        public string FillStyle { get; set; } = string.Empty;
        public string StrokeStyle { get; set; } = string.Empty;
        public float LineWidth { get; set; }
        public string Font { get; set; } = string.Empty;
        public string TextAlign { get; set; } = string.Empty;
        public float GlobalAlpha { get; set; }
    }
}
