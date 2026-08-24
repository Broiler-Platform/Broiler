using System;

namespace Broiler.UI;

/// <summary>
/// A native top-level host window spawned by an <see cref="IUiWindowHost"/>. It is itself an
/// <see cref="IUiHost"/> that a bound <see cref="UiSession"/> renders into and receives input
/// from, plus the neutral lifecycle a broken-out window needs. No native handle is exposed,
/// keeping Broiler.UI platform-neutral (see ADR 0002).
/// </summary>
public interface IUiHostWindow : IUiHost, IDisposable
{
    /// <summary>
    /// Binds the session that renders into, and receives input from, this window. The host's
    /// message loop calls <see cref="UiSession.RenderFrame"/> when the window needs painting and
    /// <see cref="UiSession.DispatchInput"/> for native input.
    /// </summary>
    void Bind(UiSession session);

    /// <summary>Sets the native window title.</summary>
    void SetTitle(string title);

    /// <summary>Brings the window to the front and gives it focus.</summary>
    void Activate();

    /// <summary>
    /// Raised when the user asks the OS to close this window (e.g. the native close button).
    /// The framework responds by closing the broken-out logical window, which then disposes
    /// this host window.
    /// </summary>
    event EventHandler? CloseRequested;
}
