namespace Broiler.Layout.Engine;

/// <summary>
/// The directory a <em>root-relative</em> sub-document URL — <c>&lt;frame src="/a/b.html"&gt;</c> —
/// resolves against, when the host knows what the document root of a local render is.
/// </summary>
/// <remarks>
/// <para>
/// HTML §"resolve a URL" resolves a leading <c>/</c> against the document's origin, not against the
/// directory the containing page sits in. Served over HTTP that is the server's document root; in a
/// <c>file://</c> render there is no origin to ask, and
/// <c>FragmentTreeBuilder.TryLoadEmbeddedDocument</c> joined the URL onto the containing directory
/// like any other relative reference. <c>Path.Combine</c> discards the left operand when the right
/// one is rooted, so <c>/resource-timing/resources/green.html</c> came out as an absolute path at
/// the filesystem root, failed <c>File.Exists</c>, and the frame painted empty — WPT
/// <c>resource-timing/initiator-type/frameset</c>, whose whole visible content is one such frame.
/// </para>
/// <para>
/// Null by default, so a host that sets nothing renders exactly as before: an unresolvable
/// root-relative frame stays the empty box it has always been. The WPT runner sets it to the
/// checkout it was pointed at, which is the same root its stylesheet, image and script loaders
/// already resolve <c>/</c>-paths against (<c>WptTestRunner.TryResolveWptRootRelativePath</c>) —
/// this closes the one sub-resource kind that had no such hook.
/// </para>
/// <para>
/// Thread-static and scope-restoring, like the engine's other render levers
/// (<see cref="CanvasBackdrop.Current"/>, <see cref="NativeZoom.Enabled"/>): the load happens
/// synchronously on the thread building the fragment tree, so a thread-local value is visible to the
/// code that reads it, and concurrent renders under different roots do not collide.
/// </para>
/// </remarks>
internal static class DocumentRoot
{
    /// <summary>
    /// The document root directory for this thread's render, or <see langword="null"/> to leave
    /// root-relative sub-document URLs unresolved.
    /// </summary>
    [System.ThreadStatic]
    public static string? Current;

    /// <summary>
    /// Sets <see cref="Current"/> for the lifetime of the returned scope and restores the previous
    /// value when it is disposed, so nesting is safe.
    /// </summary>
    public static System.IDisposable Pin(string? root)
    {
        var scope = new Scope(Current);
        Current = root;
        return scope;
    }

    private sealed class Scope(string? previous) : System.IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Current = previous;
        }
    }
}
