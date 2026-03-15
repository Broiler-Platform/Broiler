using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;

namespace TheArtOfDev.HtmlRenderer.Avalonia.Utilities;

/// <summary>
/// Cross-platform clipboard helper for Avalonia.
/// Provides plain-text clipboard operations via <see cref="IClipboard"/>.
/// </summary>
internal static class ClipboardHelper
{
    /// <summary>
    /// Copies plain text to the system clipboard.
    /// </summary>
    public static void CopyToClipboard(string plainText)
    {
        var clipboard = GetClipboard();
        // The adapter API (RAdapter.SetToClipboardInt) is synchronous by
        // design.  ConfigureAwait(false) avoids deadlocks by not trying
        // to marshal back to the UI thread.
        clipboard?.SetTextAsync(plainText ?? string.Empty)
            .ConfigureAwait(false).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Copies HTML content and a plain-text fallback to the system clipboard.
    /// On cross-platform targets the plain-text representation is used;
    /// the CF_HTML format is Windows-specific and not supported here.
    /// </summary>
    public static void CopyToClipboard(string html, string plainText)
    {
        // Avalonia's IClipboard does not expose CF_HTML or rich-data
        // formats cross-platform.  Store the plain-text representation.
        CopyToClipboard(plainText ?? html ?? string.Empty);
    }

    /// <summary>
    /// Creates a data object containing the given content for drag-drop
    /// or clipboard operations.  Returns the plain-text value; Avalonia
    /// does not have a cross-platform DataObject equivalent with HTML
    /// fragment support.
    /// </summary>
    public static object CreateDataObject(string html, string plainText)
    {
        return plainText ?? html ?? string.Empty;
    }

    private static IClipboard? GetClipboard()
    {
        if (Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return TopLevel.GetTopLevel(desktop.MainWindow)?.Clipboard;
        }

        return null;
    }
}
