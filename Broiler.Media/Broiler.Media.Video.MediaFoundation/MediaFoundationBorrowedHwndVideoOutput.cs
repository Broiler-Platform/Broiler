using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Broiler.Media.Video;

namespace Broiler.Media.Video.MediaFoundation;

[SupportedOSPlatform("windows")]
public sealed class MediaFoundationBorrowedHwndVideoOutput : IVideoOutput
{
    private readonly object _gate = new();

    public MediaFoundationBorrowedHwndVideoOutput(
        nint hwnd,
        string displayName,
        int width,
        int height,
        bool isVisible = true,
        bool validateNativeWindow = true)
    {
        if (hwnd == 0)
            throw new ArgumentException("A borrowed HWND must be non-zero.", nameof(hwnd));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("A video target needs a display name.", nameof(displayName));
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));
        if (validateNativeWindow && !MediaFoundationNative.IsWindow(hwnd))
            throw new ArgumentException("The borrowed HWND is not a live native window.", nameof(hwnd));

        Hwnd = hwnd;
        DisplayName = displayName.Trim();
        Width = width;
        Height = height;
        IsVisible = isVisible;
    }

    public event EventHandler<MediaFoundationVideoTargetChangedEventArgs>? TargetChanged;

    public nint Hwnd { get; }

    public string DisplayName { get; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public bool IsVisible { get; private set; }

    public bool IsDestroyed { get; private set; }

    public bool Completed { get; private set; }

    public MediaError? Failure { get; private set; }

    public void Resize(int width, int height)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));

        lock (_gate)
        {
            ThrowIfDestroyed();
            Width = width;
            Height = height;
        }

        RaiseChanged(MediaFoundationVideoTargetChangeKind.Resized);
    }

    public void SetVisible(bool isVisible)
    {
        lock (_gate)
        {
            ThrowIfDestroyed();
            IsVisible = isVisible;
        }

        RaiseChanged(MediaFoundationVideoTargetChangeKind.VisibilityChanged);
    }

    public void NotifyDestroyed()
    {
        bool changed;
        lock (_gate)
        {
            changed = !IsDestroyed;
            IsDestroyed = true;
            IsVisible = false;
        }

        if (changed)
            RaiseChanged(MediaFoundationVideoTargetChangeKind.Destroyed);
    }

    public ValueTask CompleteAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Completed = true;
        return ValueTask.CompletedTask;
    }

    public ValueTask FailAsync(MediaError error, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Failure = error ?? throw new ArgumentNullException(nameof(error));
        return ValueTask.CompletedTask;
    }

    internal void ThrowIfUsableTargetRequired()
    {
        lock (_gate)
            ThrowIfDestroyed();
    }

    private void RaiseChanged(MediaFoundationVideoTargetChangeKind kind) =>
        TargetChanged?.Invoke(this, new MediaFoundationVideoTargetChangedEventArgs(kind, Width, Height, IsVisible));

    private void ThrowIfDestroyed()
    {
        if (IsDestroyed)
            throw new ObjectDisposedException(nameof(MediaFoundationBorrowedHwndVideoOutput), "The borrowed HWND has been destroyed by its owner.");
    }
}
