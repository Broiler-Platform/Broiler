using System.Linq;
using System.Reflection;
using Broiler.Dom;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom.Runtime;
using Broiler.JavaScript.Runtime;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for the P3.16 browsing-context state authority (<see cref="BrowsingContextManager"/>): direct
/// unit tests of the surface, the deliberately-asymmetric sub-window map lifecycle, the session reset,
/// plus an ownership guard that the scattered bridge fields it replaced are gone.
/// </summary>
public sealed class BrowsingContextManagerTests
{
    private static DomElement Frame()
    {
        var doc = new DomDocument();
        return doc.CreateElement("iframe");
    }

    [Fact]
    public void SubDocument_Identity_RoundTrips()
    {
        var m = new BrowsingContextManager();
        var frame = Frame();
        var subDoc = new JSObject();

        Assert.False(m.TryGetSubDocument(frame, out _));
        m.SetSubDocument(frame, subDoc);
        Assert.True(m.TryGetSubDocument(frame, out var got));
        Assert.Same(subDoc, got);
    }

    [Fact]
    public void SetSubWindow_LinksBothDirections()
    {
        var m = new BrowsingContextManager();
        var frame = Frame();
        var subWindow = new JSObject();

        m.SetSubWindow(frame, subWindow);

        Assert.True(m.TryGetSubWindow(frame, out var byContainer));
        Assert.Same(subWindow, byContainer);
        Assert.True(m.IsSubWindow(subWindow));
        Assert.True(m.TryGetSubWindowContainer(subWindow, out var container));
        Assert.Same(frame, container);
        Assert.Contains(subWindow, m.SubWindows);
    }

    [Fact]
    public void RemoveContainerCaches_DropsForwardSubWindow_ButKeepsReverse()
    {
        // Preserves the pre-consolidation asymmetry: InvalidateCachedSubDocument removed the
        // container→sub-window entry but not the reverse sub-window→container map (which is only
        // bulk-cleared on session reset).
        var m = new BrowsingContextManager();
        var frame = Frame();
        var subWindow = new JSObject();
        m.SetSubWindow(frame, subWindow);
        m.SetLocation(frame, "about:srcdoc");
        m.SetBaseUrl(frame, "file:///a");

        m.RemoveContainerCaches(frame);

        Assert.False(m.TryGetSubWindow(frame, out _));           // forward dropped
        Assert.False(m.TryGetLocation(frame, out _));
        Assert.False(m.TryGetBaseUrl(frame, out _));
        Assert.True(m.IsSubWindow(subWindow));                    // reverse kept
        Assert.True(m.TryGetSubWindowContainer(subWindow, out _));
    }

    [Fact]
    public void ResetSession_ClearsReverseMap_AndCurrentWindow_ButNotForward()
    {
        // Matches the pre-consolidation ClearRuntimeSessionState: it bulk-cleared _subWindowContainers
        // and _currentWindowOverride only; the per-container caches keep their own lifecycle.
        var m = new BrowsingContextManager();
        var frame = Frame();
        var subWindow = new JSObject();
        m.SetSubWindow(frame, subWindow);
        m.SetSubDocument(frame, new JSObject());
        m.CurrentWindowOverride = subWindow;

        m.ResetSession();

        Assert.False(m.IsSubWindow(subWindow));                   // reverse cleared
        Assert.Null(m.CurrentWindowOverride);                     // override cleared
        Assert.True(m.TryGetSubDocument(frame, out _));           // forward per-container caches kept
        Assert.True(m.TryGetSubWindow(frame, out _));
    }

    [Fact]
    public void LoadMarks_Track_ObjectFailure_And_OnloadFired()
    {
        var m = new BrowsingContextManager();
        var el = Frame();

        Assert.False(m.HasObjectLoadFailed(el));
        m.MarkObjectLoadFailed(el);
        Assert.True(m.HasObjectLoadFailed(el));

        Assert.False(m.HasOnloadFired(el));
        m.MarkOnloadFired(el);
        Assert.True(m.HasOnloadFired(el));
        m.ClearOnloadFired(el);
        Assert.False(m.HasOnloadFired(el));
    }

    [Fact]
    public void ContentDocument_Links_And_Unlinks_BothDirections()
    {
        var m = new BrowsingContextManager();
        var frame = Frame();
        var doc = new DomDocument();

        m.LinkContentDocument(frame, doc);
        Assert.Same(doc, m.GetContentDocument(frame));
        Assert.Same(frame, m.GetContainerForDocument(doc));

        Assert.Same(doc, m.UnlinkContentDocument(frame));
        Assert.Null(m.GetContentDocument(frame));
        Assert.Null(m.GetContainerForDocument(doc));
        Assert.Null(m.UnlinkContentDocument(frame)); // idempotent
    }

    [Fact]
    public void OwnershipGuard_BridgeOwnsManager_AndScatteredFieldsAreGone()
    {
        var bridgeFields = typeof(DomBridge)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .Select(f => f.Name)
            .ToHashSet();

        Assert.Contains("_browsingContexts", bridgeFields);
        foreach (var gone in new[]
                 {
                     "_subDocumentCache", "_subWindowCache", "_subDocumentLocationCache",
                     "_subDocumentBaseUrlCache", "_objectLoadFailures", "_onloadFired",
                     "_subWindowContainers", "_currentWindowOverride",
                     "_contentDocuments", "_documentContainers",
                 })
            Assert.DoesNotContain(gone, bridgeFields);
    }
}
