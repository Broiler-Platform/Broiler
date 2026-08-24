using Broiler.Graphics;

namespace Broiler.UI;

/// <summary>
/// Neutral parameters for <see cref="IUiWindowHost.CreateHostWindow"/> when a logical window
/// breaks out into its own native top-level window.
/// </summary>
/// <param name="Title">Initial window title.</param>
/// <param name="Placement">
/// Requested initial placement in device-independent pixels. May be empty, in which case the
/// host chooses a default size and position.
/// </param>
/// <param name="IsModal">
/// True when the broken-out window is application-modal to its origin window. The origin session
/// blocks its own input regardless; the host may additionally apply native ownership/modality.
/// </param>
public readonly record struct UiHostWindowRequest(string Title, BRect Placement, bool IsModal);
