using System;

namespace Broiler.Media.Video.MediaFoundation;

public sealed class MediaFoundationVideoTargetChangedEventArgs : EventArgs
{
    public MediaFoundationVideoTargetChangedEventArgs(
        MediaFoundationVideoTargetChangeKind kind,
        int width,
        int height,
        bool isVisible)
    {
        if (width < 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height < 0)
            throw new ArgumentOutOfRangeException(nameof(height));

        Kind = kind;
        Width = width;
        Height = height;
        IsVisible = isVisible;
    }

    public MediaFoundationVideoTargetChangeKind Kind { get; }

    public int Width { get; }

    public int Height { get; }

    public bool IsVisible { get; }
}
