using System;
using System.Threading;

namespace Broiler.Media.Image;

/// <summary>
/// The presentation time a still render is taken at — the point on every animated image's
/// own timeline that a single-shot render should show.
/// </summary>
/// <remarks>
/// <para>
/// A live browser repaints as an animation advances, so "which frame" is answered by the
/// compositor's clock. A still render has no clock: it decodes each image once and paints it,
/// which is why an animated image would otherwise always show frame 0. This clock supplies the
/// missing value — a renderer pins it for the duration of a render pass and the decode path
/// selects frames against it via <see cref="ImageSequence.FrameAt"/>.
/// </para>
/// <para>
/// It is deliberately process-wide rather than <c>[ThreadStatic]</c>: image loading is dispatched
/// to the thread pool unless the host asks for synchronous loading, so a thread-local value would
/// be invisible to the very code that reads it. That makes concurrent renders at *different*
/// presentation times unsupported, which is the honest state of a stack that renders one document
/// per process; <see cref="Pin"/> exists so a caller restores the previous value rather than
/// leaving its own behind.
/// </para>
/// <para>
/// The default is <see cref="TimeSpan.Zero"/> — the first frame, which is what a render with no
/// opinion about time has always produced.
/// </para>
/// </remarks>
public static class ImageAnimationClock
{
    private static long _presentationTimeTicks;

    /// <summary>
    /// Time elapsed since an animated image started animating, as of the render being produced.
    /// Never negative; assigning a negative value stores <see cref="TimeSpan.Zero"/>.
    /// </summary>
    public static TimeSpan PresentationTime
    {
        get => TimeSpan.FromTicks(Interlocked.Read(ref _presentationTimeTicks));
        set => Interlocked.Exchange(ref _presentationTimeTicks, Math.Max(0, value.Ticks));
    }

    /// <summary>
    /// Sets <see cref="PresentationTime"/> for the lifetime of the returned scope and restores
    /// the previous value when it is disposed.
    /// </summary>
    public static IDisposable Pin(TimeSpan presentationTime)
    {
        var scope = new Scope(PresentationTime);
        PresentationTime = presentationTime;
        return scope;
    }

    private sealed class Scope(TimeSpan previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            PresentationTime = previous;
        }
    }
}
