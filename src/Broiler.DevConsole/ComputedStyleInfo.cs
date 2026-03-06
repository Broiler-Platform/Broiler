namespace Broiler.DevConsole;

/// <summary>
/// A single resolved CSS property name/value pair for display in the
/// Computed Styles pane.
/// </summary>
public sealed class ComputedStyleInfo
{
    /// <summary>CSS property name (e.g. "display", "margin-top").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Resolved value as a string (e.g. "block", "10px").</summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>
    /// Grouping category for UI display (e.g. "Layout", "Text", "Visual").
    /// </summary>
    public string Category { get; init; } = string.Empty;
}
