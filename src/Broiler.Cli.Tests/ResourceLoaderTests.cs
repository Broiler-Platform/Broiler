using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom.Runtime;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for the P2.6 host resource loader (<see cref="ResourceLoader"/>). The HTTP methods hit the
/// network, so they are exercised by the integration suites; here we cover the local-base-path
/// configuration and that the bridge routes <c>SetLocalBasePath</c> through the loader.
/// </summary>
[Xunit.Collection("SharedGeometryStatics")]
public sealed class ResourceLoaderTests
{
    [Fact]
    public void LocalBasePath_Defaults_Null_And_Round_Trips()
    {
        var loader = new ResourceLoader();
        Assert.Null(loader.LocalBasePath);

        loader.LocalBasePath = "/tmp/site";
        Assert.Equal("/tmp/site", loader.LocalBasePath);
    }

    [Fact]
    public void LoadText_Reads_A_File_Url_From_Disk()
    {
        // Phase 7 item 4: the file/http dispatch lives in the loader. Deterministic (no network).
        var path = Path.Combine(Path.GetTempPath(), $"broiler-loadtext-{Guid.NewGuid():N}.css");
        File.WriteAllText(path, "body { color: green; }");
        try
        {
            var loader = new ResourceLoader();
            Assert.Equal("body { color: green; }", loader.LoadText(new Uri(path).AbsoluteUri));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadText_Returns_Null_For_Missing_File_Relative_And_Unsupported_Scheme()
    {
        var loader = new ResourceLoader();
        Assert.Null(loader.LoadText(new Uri(Path.Combine(Path.GetTempPath(), "broiler-nope-xyz.css")).AbsoluteUri));
        Assert.Null(loader.LoadText("styles.css"));          // not absolute
        Assert.Null(loader.LoadText("ftp://example.com/x")); // unsupported scheme
    }

    [Fact]
    public void Bridge_SetLocalBasePath_Does_Not_Throw_And_Attach_Still_Works()
    {
        // Characterization: SetLocalBasePath now configures the loader; a subsequent attach + query
        // continues to work (the local base path only affects sub-resource resolution).
        using var ctx = new JSContext();
        using var bridge = new DomBridge();
        bridge.SetLocalBasePath("/tmp/site");
        bridge.Attach(ctx, "<!DOCTYPE html><html><body><p id='t'>hi</p></body></html>", "file:///r.html");

        Assert.Equal("true", ctx.Eval("document.getElementById('t') !== null").ToString());
    }
}
