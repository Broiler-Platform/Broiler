namespace Broiler.Playback;

/// <summary>
/// Mirrors the HTML <c>HTMLMediaElement.readyState</c> values, so the eventual DOM
/// binding can surface this directly.
/// </summary>
public enum MediaReadyState
{
    HaveNothing = 0,
    HaveMetadata = 1,
    HaveCurrentData = 2,
    HaveFutureData = 3,
    HaveEnoughData = 4,
}
