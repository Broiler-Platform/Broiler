using System.IO;
using Broiler.HTML.Core.Entities;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Unit tests for <see cref="WptResourceHandlers"/> — the run's sub-resource policy.
/// </summary>
/// <remarks>
/// These pin the decisions, not the plumbing: that an off-corpus host is answered rather than
/// fetched, on <em>both</em> resource kinds, and that everything the corpus can serve is left
/// alone. The plumbing they cannot reach — that the policy is attached to the geometry container
/// and to the bridge's loader as well as to the render — is what the 2026-08-16 timeouts were, and
/// it is covered by <see cref="WptResourceHandlers.Attach"/>'s single call site rather than here.
/// </remarks>
public sealed class WptResourceHandlersTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _previousRoot;

    public WptResourceHandlersTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "broiler-wpt-res-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _previousRoot = WptResourceHandlers.Root;
    }

    public void Dispose()
    {
        WptResourceHandlers.Root = _previousRoot;
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort */ }
    }

    private static HtmlStylesheetLoadEventArgs Stylesheet(string src) =>
        (HtmlStylesheetLoadEventArgs)Activator.CreateInstance(
            typeof(HtmlStylesheetLoadEventArgs),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            binder: null,
            args: [src, new Dictionary<string, string>()],
            culture: null)!;

    [Theory]
    // The three hrefs of css/CSS2/cascade-import/cascade-import-009.xht: a CGI on a personal server
    // that pauses 2, 5 and 8 seconds by design. Fetching them is 15 s of a 30 s budget, twice over
    // (the geometry pass lays the document out before the render does), which is the whole timeout.
    [InlineData("http://software.hixie.ch/utilities/cgi/test-tools/delayed-file?pause=2")]
    [InlineData("http://software.hixie.ch/utilities/cgi/test-tools/delayed-file?pause=5")]
    [InlineData("http://software.hixie.ch/utilities/cgi/test-tools/delayed-file?pause=8")]
    [InlineData("https://example.com/theme.css")]
    public void Stylesheet_AnswersOffCorpusHostsInsteadOfFetching(string href)
    {
        var args = Stylesheet(href);
        WptResourceHandlers.Stylesheet(_tempDir)(null, args);

        // Non-empty is the load-bearing half: StylesheetLoadHandler reads an empty SetStyleSheet as
        // "the host supplied nothing" and fetches Src itself, so an empty string would be no guard
        // at all. And it must not have been rewritten to a path either — there is no such file.
        Assert.Equal(WptResourceHandlers.OffCorpusStylesheet, args.SetStyleSheet);
        Assert.False(string.IsNullOrEmpty(args.SetStyleSheet));
        Assert.Null(args.SetSrc);
    }

    [Fact]
    public void Stylesheet_ServesARootRelativeHrefFromTheCheckout()
    {
        var onDisk = Path.Combine(_tempDir, "style.css");
        File.WriteAllText(onDisk, "p { color: green; }");

        var args = Stylesheet("/style.css");
        WptResourceHandlers.Stylesheet(_tempDir)(null, args);

        Assert.Equal(onDisk, args.SetSrc);
        Assert.Null(args.SetStyleSheet);
    }

    [Theory]
    // Nothing that names no host, or names one the corpus serves, may be suppressed: a relative
    // href is a file next to the test, and a WPT host resolves to the same checkout.
    [InlineData("support/base.css")]
    [InlineData("data:text/css,p{color:green}")]
    [InlineData("http://web-platform.test:8000/css/support/base.css")]
    public void Stylesheet_LeavesEverythingTheRunCanServeAlone(string href)
    {
        var args = Stylesheet(href);
        WptResourceHandlers.Stylesheet(_tempDir)(null, args);

        Assert.Null(args.SetStyleSheet);
        Assert.Null(args.SetSrc);
    }

    [Fact]
    public void Install_RegistersThePolicyWithBothLoadersNoContainerEventReaches()
    {
        // The module initializer has already run — Install is idempotent, and calling it again is
        // how this states that it is the thing that registers them.
        WptResourceHandlers.Install();

        // The engine's (images in every container, and stylesheets once Broiler.HTML carries the
        // line that consults it) and the script bridge's own (external sheets and the preload scan).
        var engine = WptResourceHandlers.RegisteredEnginePolicy;
        var bridge = Broiler.HtmlBridge.Dom.Runtime.ResourceLoader.NetworkFetchPolicy;
        Assert.NotNull(engine);
        Assert.NotNull(bridge);

        foreach (var policy in new[] { engine!, bridge! })
        {
            WptResourceHandlers.Root = _tempDir;
            Assert.False(policy("http://software.hixie.ch/utilities/cgi/test-tools/delayed-file?pause=8"));
            Assert.True(policy("http://web-platform.test:8000/css/support/base.css"));
            Assert.True(policy("file:///tmp/base.css"));
            Assert.True(policy("support/pattern.png"));

            // Outside a rooted run — a host embedding this assembly for something other than a
            // conformance sweep — the policy must permit everything, exactly as no policy would.
            WptResourceHandlers.Root = null;
            Assert.True(policy("http://software.hixie.ch/utilities/cgi/test-tools/delayed-file?pause=8"));
        }
    }

}
