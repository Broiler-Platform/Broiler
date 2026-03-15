using System;
using System.Collections.Generic;
using System.Linq;
using Broiler.App.Rendering;
using Broiler.HTML.Dom.Core.Dom;

namespace Broiler.DevConsole;

/// <summary>
/// Provides the core logic for the developer console: log filtering,
/// box-tree snapshots, computed-style extraction, and box-model info.
/// </summary>
public sealed class ConsoleService : IDisposable
{
    private readonly LogSubscription _logSubscription;
    private readonly List<RenderLogEntry> _entries = [];
    private readonly object _lock = new();

    /// <summary>Raised when a new log entry is received.</summary>
    public event Action<RenderLogEntry>? EntryReceived;

    public ConsoleService()
    {
        _logSubscription = new LogSubscription(OnEntryLogged);
    }

    private void OnEntryLogged(RenderLogEntry entry)
    {
        lock (_lock)
            _entries.Add(entry);

        EntryReceived?.Invoke(entry);
    }

    /// <summary>
    /// Returns all log entries, optionally filtered by level, category,
    /// and a search term applied to the message text.
    /// </summary>
    public IReadOnlyList<RenderLogEntry> GetFilteredEntries(
        LogLevel? minimumLevel = null,
        LogCategory? category = null,
        string? searchText = null)
    {
        lock (_lock)
        {
            IEnumerable<RenderLogEntry> result = _entries;

            if (minimumLevel.HasValue)
                result = result.Where(e => e.Level >= minimumLevel.Value);

            if (category.HasValue)
                result = result.Where(e => e.Category == category.Value);

            if (!string.IsNullOrWhiteSpace(searchText))
                result = result.Where(e =>
                    e.Message.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    e.Context.Contains(searchText, StringComparison.OrdinalIgnoreCase));

            return result.ToList();
        }
    }

    /// <summary>Clears all captured log entries.</summary>
    public void ClearEntries()
    {
        lock (_lock)
            _entries.Clear();
    }

    /// <summary>
    /// Builds a tree of <see cref="BoxTreeNode"/> from the given
    /// <paramref name="root"/> box, suitable for display in a tree view.
    /// </summary>
    internal static BoxTreeNode BuildBoxTree(CssBox root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return BuildNode(root, 0);
    }

    private static BoxTreeNode BuildNode(CssBox box, int depth)
    {
        var node = new BoxTreeNode
        {
            Tag = box.HtmlTag?.Name ?? "anon",
            Id = box.HtmlTag?.TryGetAttribute("id"),
            CssClass = box.HtmlTag?.TryGetAttribute("class"),
            Display = box.Display,
            Depth = depth,
            Box = box,
        };

        foreach (var child in box.Boxes)
            node.Children.Add(BuildNode(child, depth + 1));

        return node;
    }

    /// <summary>
    /// Extracts the resolved CSS property values for the given box,
    /// grouped by category (Layout, Text, Visual, Box Model).
    /// </summary>
    internal static IReadOnlyList<ComputedStyleInfo> GetComputedStyles(CssBox box)
    {
        ArgumentNullException.ThrowIfNull(box);

        var styles = new List<ComputedStyleInfo>
        {
            // Layout
            new() { Name = "display", Value = box.Display, Category = "Layout" },
            new() { Name = "position", Value = box.Position, Category = "Layout" },
            new() { Name = "float", Value = box.Float, Category = "Layout" },
            new() { Name = "width", Value = box.Width, Category = "Layout" },
            new() { Name = "height", Value = box.Height, Category = "Layout" },
            new() { Name = "max-width", Value = box.MaxWidth, Category = "Layout" },
            new() { Name = "min-width", Value = box.MinWidth, Category = "Layout" },
            new() { Name = "max-height", Value = box.MaxHeight, Category = "Layout" },
            new() { Name = "min-height", Value = box.MinHeight, Category = "Layout" },
            new() { Name = "overflow", Value = box.Overflow, Category = "Layout" },
            new() { Name = "vertical-align", Value = box.VerticalAlign, Category = "Layout" },
            new() { Name = "direction", Value = box.Direction, Category = "Layout" },
            new() { Name = "top", Value = box.Top, Category = "Layout" },
            new() { Name = "right", Value = box.Right, Category = "Layout" },
            new() { Name = "bottom", Value = box.Bottom, Category = "Layout" },
            new() { Name = "left", Value = box.Left, Category = "Layout" },

            // Box Model
            new() { Name = "margin-top", Value = box.MarginTop, Category = "Box Model" },
            new() { Name = "margin-right", Value = box.MarginRight, Category = "Box Model" },
            new() { Name = "margin-bottom", Value = box.MarginBottom, Category = "Box Model" },
            new() { Name = "margin-left", Value = box.MarginLeft, Category = "Box Model" },
            new() { Name = "padding-top", Value = box.PaddingTop, Category = "Box Model" },
            new() { Name = "padding-right", Value = box.PaddingRight, Category = "Box Model" },
            new() { Name = "padding-bottom", Value = box.PaddingBottom, Category = "Box Model" },
            new() { Name = "padding-left", Value = box.PaddingLeft, Category = "Box Model" },
            new() { Name = "border-top-width", Value = box.BorderTopWidth, Category = "Box Model" },
            new() { Name = "border-right-width", Value = box.BorderRightWidth, Category = "Box Model" },
            new() { Name = "border-bottom-width", Value = box.BorderBottomWidth, Category = "Box Model" },
            new() { Name = "border-left-width", Value = box.BorderLeftWidth, Category = "Box Model" },
            new() { Name = "border-top-style", Value = box.BorderTopStyle, Category = "Box Model" },
            new() { Name = "border-right-style", Value = box.BorderRightStyle, Category = "Box Model" },
            new() { Name = "border-bottom-style", Value = box.BorderBottomStyle, Category = "Box Model" },
            new() { Name = "border-left-style", Value = box.BorderLeftStyle, Category = "Box Model" },
            new() { Name = "border-top-color", Value = box.BorderTopColor, Category = "Box Model" },
            new() { Name = "border-right-color", Value = box.BorderRightColor, Category = "Box Model" },
            new() { Name = "border-bottom-color", Value = box.BorderBottomColor, Category = "Box Model" },
            new() { Name = "border-left-color", Value = box.BorderLeftColor, Category = "Box Model" },

            // Text
            new() { Name = "font-family", Value = box.FontFamily ?? string.Empty, Category = "Text" },
            new() { Name = "font-size", Value = box.FontSize, Category = "Text" },
            new() { Name = "font-style", Value = box.FontStyle, Category = "Text" },
            new() { Name = "font-weight", Value = box.FontWeight, Category = "Text" },
            new() { Name = "color", Value = box.Color, Category = "Text" },
            new() { Name = "text-align", Value = box.TextAlign, Category = "Text" },
            new() { Name = "text-decoration", Value = box.TextDecoration, Category = "Text" },
            new() { Name = "text-indent", Value = box.TextIndent, Category = "Text" },
            new() { Name = "line-height", Value = box.LineHeight, Category = "Text" },
            new() { Name = "white-space", Value = box.WhiteSpace, Category = "Text" },
            new() { Name = "word-spacing", Value = box.WordSpacing, Category = "Text" },
            new() { Name = "word-break", Value = box.WordBreak, Category = "Text" },

            // Visual
            new() { Name = "background-color", Value = box.BackgroundColor, Category = "Visual" },
            new() { Name = "background-image", Value = box.BackgroundImage, Category = "Visual" },
            new() { Name = "background-repeat", Value = box.BackgroundRepeat, Category = "Visual" },
            new() { Name = "background-position", Value = box.BackgroundPosition, Category = "Visual" },
            new() { Name = "list-style", Value = box.ListStyle, Category = "Visual" },
            new() { Name = "list-style-type", Value = box.ListStyleType, Category = "Visual" },
            new() { Name = "list-style-position", Value = box.ListStylePosition, Category = "Visual" },
        };

        return styles;
    }

    /// <summary>
    /// Extracts box-model dimensions from the given box.
    /// Returns computed (actual) pixel values where available.
    /// </summary>
    internal static BoxModelInfo GetBoxModel(CssBox box)
    {
        ArgumentNullException.ThrowIfNull(box);

        return new BoxModelInfo
        {
            Margin = new BoxEdges
            {
                Top = TryGetActual(() => box.ActualMarginTop),
                Right = TryGetActual(() => box.ActualMarginRight),
                Bottom = TryGetActual(() => box.ActualMarginBottom),
                Left = TryGetActual(() => box.ActualMarginLeft),
            },
            Border = new BoxEdges
            {
                Top = TryGetActual(() => box.ActualBorderTopWidth),
                Right = TryGetActual(() => box.ActualBorderRightWidth),
                Bottom = TryGetActual(() => box.ActualBorderBottomWidth),
                Left = TryGetActual(() => box.ActualBorderLeftWidth),
            },
            Padding = new BoxEdges
            {
                Top = TryGetActual(() => box.ActualPaddingTop),
                Right = TryGetActual(() => box.ActualPaddingRight),
                Bottom = TryGetActual(() => box.ActualPaddingBottom),
                Left = TryGetActual(() => box.ActualPaddingLeft),
            },
            ContentWidth = SafeActual(box.Size.Width),
            ContentHeight = SafeActual(box.Size.Height),
        };
    }

    /// <summary>
    /// Safely retrieves a computed actual value.  Returns 0 when the value
    /// is NaN or the accessor throws (e.g. no HtmlContainer attached).
    /// </summary>
    private static double TryGetActual(Func<double> accessor)
    {
        try
        {
            var value = accessor();
            return double.IsNaN(value) ? 0 : value;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Returns 0 when the actual value is NaN (not yet computed).
    /// </summary>
    private static double SafeActual(double value) => double.IsNaN(value) ? 0 : value;

    /// <inheritdoc />
    public void Dispose()
    {
        _logSubscription.Dispose();
    }
}
