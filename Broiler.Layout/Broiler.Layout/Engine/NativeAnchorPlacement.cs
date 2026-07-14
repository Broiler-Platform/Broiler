namespace Broiler.Layout.Engine;

/// <summary>
/// Feature flag gating the native CSS anchor-positioning placement post-pass
/// (HtmlBridge complexity-reduction roadmap Phase 5 item 3, P5.8c). Default off:
/// while off, the layout engine ignores anchor properties and the HtmlBridge
/// anchor resolver continues to pre-bake anchor positions as it does today.
/// </summary>
/// <remarks>
/// Thread-static so a test (or a future per-document caller) can enable it for one
/// layout without affecting concurrently-running layouts. The cutover that flips it
/// on for real (and makes the bridge stop pre-baking the matching boxes) is P5.8d.
/// </remarks>
internal static class NativeAnchorPlacement
{
    [System.ThreadStatic]
    public static bool Enabled;
}
