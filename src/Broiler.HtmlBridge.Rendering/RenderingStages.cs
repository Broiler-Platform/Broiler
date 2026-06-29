using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Broiler.HtmlBridge;

// RF-BRIDGE-1a: the bridge's parallel paint/composite pipeline below is unused at
// runtime (the renderer's CssLayoutEngine + paint walker produce the painted
// output) and is deprecated for removal at the next htmlbridge-public-surface
// major. Cross-references among the deprecated types are intentional, so silence
// CS0618 here.
#pragma warning disable CS0618

/// <summary>Types of paint commands produced during the paint stage.</summary>
public enum PaintCommandType { Background, Border, Text, Image, BoxShadow, Group, TextShadow }

/// <summary>Represents a single painting instruction in the rendering pipeline.</summary>
[Obsolete("Unused at runtime; the renderer's paint walker produces painted output. Deprecated for removal at the next htmlbridge-public-surface major (RF-BRIDGE-1a).")]
public class PaintCommand
{
    /// <summary>The type of paint operation.</summary>
    public PaintCommandType Type { get; set; }
    /// <summary>Bounding rectangle for this command.</summary>
    public Rect Bounds { get; set; }
    /// <summary>CSS background-color value.</summary>
    public string BackgroundColor { get; set; } = "";
    /// <summary>CSS border-color value.</summary>
    public string BorderColor { get; set; } = "";
    /// <summary>Border width in pixels.</summary>
    public float BorderWidth { get; set; }
    /// <summary>Border radius in pixels.</summary>
    public float BorderRadius { get; set; }
    /// <summary>CSS border-style (solid, dotted, dashed, etc.).</summary>
    public string BorderStyle { get; set; } = "solid";
    /// <summary>Text content for text paint commands.</summary>
    public string Text { get; set; } = "";
    /// <summary>Font size in pixels.</summary>
    public float FontSize { get; set; }
    /// <summary>Font family name.</summary>
    public string FontFamily { get; set; } = "";
    /// <summary>CSS color value for text.</summary>
    public string Color { get; set; } = "";
    /// <summary>Opacity from 0.0 (fully transparent) to 1.0 (fully opaque).</summary>
    public float Opacity { get; set; } = 1.0f;
    /// <summary>Image source URL for image commands.</summary>
    public string ImageSource { get; set; } = "";
    /// <summary>Z-index for stacking order.</summary>
    public int ZIndex { get; set; }
    /// <summary>Child commands for group paint commands.</summary>
    public List<PaintCommand> Children { get; set; } = [];
    /// <summary>CSS text-shadow color value.</summary>
    public string TextShadowColor { get; set; } = "";
    /// <summary>Text shadow horizontal offset in pixels.</summary>
    public float TextShadowOffsetX { get; set; }
    /// <summary>Text shadow vertical offset in pixels.</summary>
    public float TextShadowOffsetY { get; set; }
    /// <summary>Whether this element is visibility:hidden (occupies space but not painted).</summary>
    public bool IsHidden { get; set; }
}

/// <summary>Represents a compositing layer that groups paint commands at the same z-index.</summary>
[Obsolete("Unused at runtime; the renderer's paint walker produces painted output. Deprecated for removal at the next htmlbridge-public-surface major (RF-BRIDGE-1a).")]
public class PaintLayer
{
    /// <summary>Z-index of this layer in the stacking order.</summary>
    public int ZIndex { get; set; }
    /// <summary>Layer opacity from 0.0 to 1.0.</summary>
    public float Opacity { get; set; } = 1.0f;
    /// <summary>Bounding rectangle of this layer.</summary>
    public Rect Bounds { get; set; }
    /// <summary>Paint commands belonging to this layer.</summary>
    public List<PaintCommand> Commands { get; set; } = [];
    /// <summary>The DOM element that created this layer.</summary>
    public DomElement Element { get; set; } = null!;
}

/// <summary>
/// Converts a layout tree into a flat list of paint commands (the paint stage).
/// Walks the tree back-to-front and generates background, border, text, and
/// box-shadow commands for each <see cref="LayoutBox"/>.
/// </summary>
[Obsolete("Unused at runtime; the renderer's paint walker produces painted output. Deprecated for removal at the next htmlbridge-public-surface major (RF-BRIDGE-1a).")]
public class Painter
{
    /// <summary>Walks the layout tree and generates paint commands in paint order (back-to-front).</summary>
    public List<PaintCommand> Paint(LayoutBox root)
    {
        var commands = new List<PaintCommand>();
        PaintBoxRecursive(root, commands);
        return commands;
    }

    /// <summary>Generates paint commands for a single layout box.</summary>
    public List<PaintCommand> PaintBox(LayoutBox box)
    {
        var commands = new List<PaintCommand>();
        var bounds = new Rect(
            box.Dimensions.X, box.Dimensions.Y,
            box.Dimensions.Width, box.Dimensions.Height);
        var el = box.Element;

        // Elements with visibility:hidden still occupy space but are not painted.
        if (box.Visibility == CssVisibility.Hidden)
            return commands;

        var bgColor = GetBackgroundColor(el);
        if (!string.IsNullOrEmpty(bgColor))
        {
            commands.Add(new PaintCommand
            {
                Type = PaintCommandType.Background,
                Bounds = bounds,
                BackgroundColor = bgColor,
                Opacity = GetOpacity(el),
                ZIndex = GetZIndex(el)
            });
        }

        // Background image (including data: URIs).
        var bgImage = GetStyleValue(el, "background-image", "");
        if (!string.IsNullOrEmpty(bgImage) && bgImage != "none")
        {
            // Extract URL from url('...') or url("...") or url(...)
            var src = bgImage;
            if (src.StartsWith("url(", StringComparison.OrdinalIgnoreCase))
            {
                src = src[4..];
                if (src.EndsWith(")")) src = src[..^1];
                src = src.Trim('\'', '"', ' ');
            }
            commands.Add(new PaintCommand
            {
                Type = PaintCommandType.Image,
                Bounds = bounds,
                ImageSource = src,
                Opacity = GetOpacity(el),
                ZIndex = GetZIndex(el)
            });
        }

        var borderColor = GetBorderColor(el);
        float borderWidth = box.Dimensions.Border.Top;
        if (!string.IsNullOrEmpty(borderColor) && borderWidth > 0)
        {
            commands.Add(new PaintCommand
            {
                Type = PaintCommandType.Border,
                Bounds = box.Dimensions.BorderBox(),
                BorderColor = borderColor,
                BorderWidth = borderWidth,
                BorderRadius = GetBorderRadius(el),
                BorderStyle = GetBorderStyle(el),
                Opacity = GetOpacity(el),
                ZIndex = GetZIndex(el)
            });
        }

        var shadow = GetBoxShadow(el);
        if (shadow != null)
        {
            commands.Add(new PaintCommand
            {
                Type = PaintCommandType.BoxShadow,
                Bounds = bounds,
                Color = shadow,
                Opacity = GetOpacity(el),
                ZIndex = GetZIndex(el)
            });
        }

        if (el.IsTextNode && !string.IsNullOrEmpty(el.TextContent))
        {
            var textCmd = new PaintCommand
            {
                Type = PaintCommandType.Text,
                Bounds = bounds,
                Text = el.TextContent,
                FontSize = ParseFloat(el, "font-size", 16f),
                FontFamily = GetStyleValue(el, "font-family", "sans-serif"),
                Color = GetColor(el),
                Opacity = GetOpacity(el),
                ZIndex = GetZIndex(el)
            };

            // Parse text-shadow if present.
            var textShadow = GetStyleValue(el, "text-shadow", "");
            if (!string.IsNullOrEmpty(textShadow) && textShadow != "none")
            {
                ParseTextShadow(textShadow, textCmd);
            }

            commands.Add(textCmd);
        }

        return commands;
    }

    private void PaintBoxRecursive(LayoutBox box, List<PaintCommand> commands)
    {
        commands.AddRange(PaintBox(box));
        foreach (var child in box.Children)
        {
            PaintBoxRecursive(child, commands);
        }
    }

    /// <summary>Reads the background-color style from an element.</summary>
    public string GetBackgroundColor(DomElement el)
        => GetStyleValue(el, "background-color", "");

    /// <summary>Reads the border-color style from an element.</summary>
    public string GetBorderColor(DomElement el)
        => GetStyleValue(el, "border-color", "");

    /// <summary>Reads the border-radius style from an element.</summary>
    public float GetBorderRadius(DomElement el)
        => ParseFloat(el, "border-radius", 0f);

    /// <summary>Reads the box-shadow style from an element, or returns null if none.</summary>
    public string? GetBoxShadow(DomElement el)
    {
        var val = GetStyleValue(el, "box-shadow", "");
        return string.IsNullOrEmpty(val) || val == "none" ? null : val;
    }

    /// <summary>Reads the opacity style from an element (default 1.0).</summary>
    public float GetOpacity(DomElement el)
        => ParseFloat(el, "opacity", 1.0f);

    /// <summary>Reads the z-index style from an element (default 0). Treats 'auto' as 0.</summary>
    public int GetZIndex(DomElement el)
    {
        var val = GetStyleValue(el, "z-index", "");
        if (string.IsNullOrEmpty(val) || string.Equals(val, "auto", StringComparison.OrdinalIgnoreCase))
            return 0;
        return int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result : 0;
    }

    /// <summary>Reads the color style from an element.</summary>
    public string GetColor(DomElement el)
        => GetStyleValue(el, "color", "#000000");

    /// <summary>Reads the border-style from an element (default "solid").</summary>
    public string GetBorderStyle(DomElement el)
        => GetStyleValue(el, "border-style", "solid");

    /// <summary>
    /// Parses a CSS text-shadow value (e.g. "rgba(0,0,0,0.5) 2px 3px" or "2px 3px 0px red")
    /// and populates the command's TextShadow properties.
    /// </summary>
    internal static void ParseTextShadow(string value, PaintCommand cmd)
    {
        // text-shadow formats:
        //   <color> <offsetX> <offsetY>
        //   <offsetX> <offsetY> <blur>? <color>?
        //   rgba(r,g,b,a) Xpx Ypx
        var parts = new List<string>();
        int depth = 0;
        int start = 0;
        for (int i = 0; i <= value.Length; i++)
        {
            char c = i < value.Length ? value[i] : ' ';
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (c == ' ' && depth == 0 && i > start)
            {
                parts.Add(value[start..i]);
                start = i + 1;
            }
            else if (i == value.Length && start < i)
            {
                parts.Add(value[start..i]);
            }
        }

        var lengths = new List<float>();
        string color = "";
        foreach (var p in parts)
        {
            var trimmed = p.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // Try parsing as a CSS length
            if (trimmed.EndsWith("px", StringComparison.OrdinalIgnoreCase)
                || char.IsDigit(trimmed[0])
                || trimmed[0] == '-')
            {
                var num = trimmed.Replace("px", "").Trim();
                if (float.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                {
                    lengths.Add(v);
                    continue;
                }
            }
            // Otherwise treat as color
            color = trimmed;
        }

        if (lengths.Count >= 2)
        {
            cmd.TextShadowOffsetX = lengths[0];
            cmd.TextShadowOffsetY = lengths[1];
        }
        if (!string.IsNullOrEmpty(color))
            cmd.TextShadowColor = color;
        else
            cmd.TextShadowColor = "rgba(0,0,0,1)";
    }

    private static string GetStyleValue(DomElement el, string property, string defaultValue)
    {
        if (el.Style != null && el.Style.TryGetValue(property, out var value))
            return value;
        return defaultValue;
    }

    private static float ParseFloat(DomElement el, string property, float defaultValue)
    {
        var val = GetStyleValue(el, property, "");
        if (string.IsNullOrEmpty(val))
            return defaultValue;
        // Strip common CSS units
        val = val.Replace("px", "").Replace("em", "").Replace("rem", "").Trim();
        return float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : defaultValue;
    }
}

/// <summary>
/// Manages layers, z-index ordering, and opacity for the compositing stage.
/// Groups paint commands into <see cref="PaintLayer"/> instances and flattens
/// them back into a single ordered list.
/// </summary>
[Obsolete("Unused at runtime; the renderer's paint walker produces painted output. Deprecated for removal at the next htmlbridge-public-surface major (RF-BRIDGE-1a).")]
public class Compositor
{
    /// <summary>
    /// Groups paint commands into layers based on z-index. Commands with the
    /// same z-index are placed in the same layer. Layers are sorted ascending.
    /// </summary>
    public List<PaintLayer> BuildLayers(List<PaintCommand> commands)
    {
        var groups = new Dictionary<int, PaintLayer>();
        foreach (var cmd in commands)
        {
            if (!groups.TryGetValue(cmd.ZIndex, out var layer))
            {
                layer = new PaintLayer
                {
                    ZIndex = cmd.ZIndex,
                    Opacity = 1.0f,
                    Commands = []
                };
                groups[cmd.ZIndex] = layer;
            }
            layer.Commands.Add(cmd);
        }

        // Compute bounding box per layer
        foreach (var layer in groups.Values)
        {
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            foreach (var cmd in layer.Commands)
            {
                if (cmd.Bounds.X < minX) minX = cmd.Bounds.X;
                if (cmd.Bounds.Y < minY) minY = cmd.Bounds.Y;
                if (cmd.Bounds.X + cmd.Bounds.Width > maxX) maxX = cmd.Bounds.X + cmd.Bounds.Width;
                if (cmd.Bounds.Y + cmd.Bounds.Height > maxY) maxY = cmd.Bounds.Y + cmd.Bounds.Height;
            }
            layer.Bounds = new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        return groups.Values.OrderBy(l => l.ZIndex).ToList();
    }

    /// <summary>
    /// Flattens layers into a single ordered list of paint commands, applying
    /// z-index ordering and propagating layer opacity to each command.
    /// </summary>
    public List<PaintCommand> Composite(List<PaintLayer> layers)
    {
        var result = new List<PaintCommand>();
        foreach (var layer in layers.OrderBy(l => l.ZIndex))
        {
            foreach (var cmd in layer.Commands)
            {
                var composited = new PaintCommand
                {
                    Type = cmd.Type,
                    Bounds = cmd.Bounds,
                    BackgroundColor = cmd.BackgroundColor,
                    BorderColor = cmd.BorderColor,
                    BorderWidth = cmd.BorderWidth,
                    BorderRadius = cmd.BorderRadius,
                    BorderStyle = cmd.BorderStyle,
                    Text = cmd.Text,
                    FontSize = cmd.FontSize,
                    FontFamily = cmd.FontFamily,
                    Color = cmd.Color,
                    Opacity = cmd.Opacity * layer.Opacity,
                    ImageSource = cmd.ImageSource,
                    ZIndex = cmd.ZIndex,
                    Children = cmd.Children,
                    TextShadowColor = cmd.TextShadowColor,
                    TextShadowOffsetX = cmd.TextShadowOffsetX,
                    TextShadowOffsetY = cmd.TextShadowOffsetY,
                    IsHidden = cmd.IsHidden
                };
                result.Add(composited);
            }
        }
        return result;
    }

    /// <summary>
    /// Creates a new stacking context for elements with z-index != auto,
    /// opacity &lt; 1.0, or position fixed/sticky.
    /// </summary>
    public PaintLayer CreateStackingContext(LayoutBox box)
    {
        var painter = new Painter();
        return new PaintLayer
        {
            ZIndex = painter.GetZIndex(box.Element),
            Opacity = painter.GetOpacity(box.Element),
            Bounds = new Rect(
                box.Dimensions.X, box.Dimensions.Y,
                box.Dimensions.Width, box.Dimensions.Height),
            Commands = painter.PaintBox(box),
            Element = box.Element
        };
    }
}

/// <summary>The final output of the rendering pipeline after paint and composite stages.</summary>
/// <remarks>Initializes a new <see cref="RenderOutput"/>.</remarks>
[Obsolete("Unused at runtime; the renderer's paint walker produces painted output. Deprecated for removal at the next htmlbridge-public-surface major (RF-BRIDGE-1a).")]
public class RenderOutput(IReadOnlyList<PaintCommand> commands, IReadOnlyList<PaintLayer> layers, float width, float height)
{
    /// <summary>Ordered paint commands ready for rasterization.</summary>
    public IReadOnlyList<PaintCommand> Commands { get; } = commands;
    /// <summary>Composited layers used to produce the final output.</summary>
    public IReadOnlyList<PaintLayer> Layers { get; } = layers;
    /// <summary>Total content width in pixels.</summary>
    public float Width { get; } = width;
    /// <summary>Total content height in pixels.</summary>
    public float Height { get; } = height;
}
