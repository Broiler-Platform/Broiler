using System.Runtime.InteropServices.JavaScript;

namespace Broiler.UI.WebAssembly.Demo;

/// <summary>
/// Managed-to-page bridge for the gallery. The page module registers these under the
/// <c>gallery.*</c> namespace of <c>main.js</c>; the direct-Canvas backend owns actual pixel
/// presentation, so this surface only carries scheduling, cursor, caret, status, and environment.
/// </summary>
internal static partial class BrowserInterop
{
    /// <summary>Requests a coalesced animation-frame render (managed → page).</summary>
    [JSImport("gallery.scheduleFrame", "main.js")]
    internal static partial void ScheduleFrame();

    /// <summary>Sets the CSS cursor for the canvas (managed → page).</summary>
    [JSImport("gallery.setCursor", "main.js")]
    internal static partial void SetCursor(string cursor);

    /// <summary>Signals that the gallery is initialized so the page can attach input and observers.</summary>
    [JSImport("gallery.ready", "main.js")]
    internal static partial void Ready(double logicalWidth, double logicalHeight);

    /// <summary>Reports an unrecoverable managed failure to the page.</summary>
    [JSImport("gallery.failed", "main.js")]
    internal static partial void Failed(string exceptionType, string details);

    /// <summary>
    /// Publishes per-frame page state: the managed text caret rectangle (for the hidden native
    /// editor and IME placement), the current focus/status text, and the active color scheme.
    /// </summary>
    [JSImport("gallery.publishFrame", "main.js")]
    internal static partial void PublishFrame(
        double frameIndex,
        bool caretActive,
        double caretX,
        double caretY,
        double caretWidth,
        double caretHeight,
        int caretIndex,
        int selectionStart,
        int selectionLength,
        bool focusedIsText,
        string statusText,
        bool darkTheme);

    /// <summary>Reads the page's <c>prefers-color-scheme</c> at startup (page → managed, sync).</summary>
    [JSImport("gallery.prefersDarkScheme", "main.js")]
    internal static partial bool PrefersDarkScheme();

    /// <summary>Reads the page's <c>prefers-reduced-motion</c> at startup (page → managed, sync).</summary>
    [JSImport("gallery.prefersReducedMotion", "main.js")]
    internal static partial bool PrefersReducedMotion();
}
