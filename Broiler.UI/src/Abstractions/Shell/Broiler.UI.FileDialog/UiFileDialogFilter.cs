using System;

namespace Broiler.UI.FileDialog;

public sealed class UiFileDialogFilter
{
    public UiFileDialogFilter(string name, string pattern, string defaultExtension = "")
    {
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("A file dialog filter name is required.", nameof(name)) : name.Trim();
        Pattern = string.IsNullOrWhiteSpace(pattern) ? "*" : pattern.Trim();
        DefaultExtension = NormalizeExtension(defaultExtension);
    }

    public string Name { get; }

    public string Pattern { get; }

    public string DefaultExtension { get; }

    private static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return string.Empty;

        string trimmed = extension.Trim();
        if (StringComparer.Ordinal.Equals(trimmed, "*"))
            return string.Empty;

        return trimmed.StartsWith(".", StringComparison.Ordinal) ? trimmed : "." + trimmed;
    }
}
