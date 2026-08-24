namespace Broiler.UI;

/// <summary>
/// Optional host capability that can create additional native top-level host windows.
/// A primary <see cref="IUiHost"/> that also implements this lets a logical subwindow
/// (an owned window or a dialog) "break out" into its own host window with its own
/// <see cref="UiSession"/>. Discovered via <c>Host is IUiWindowHost</c>, mirroring the
/// other optional host capabilities.
/// </summary>
public interface IUiWindowHost
{
    /// <summary>
    /// Creates and shows a new native top-level host window. The caller binds a fresh
    /// <see cref="UiSession"/> to the returned window and re-roots the broken-out logical
    /// window into it; the host owns the native window lifetime and drives its message loop.
    /// </summary>
    IUiHostWindow CreateHostWindow(UiHostWindowRequest request);
}
