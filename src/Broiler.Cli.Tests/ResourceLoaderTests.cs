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
