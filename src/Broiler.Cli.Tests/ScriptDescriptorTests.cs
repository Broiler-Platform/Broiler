using System.Linq;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Scripting;

namespace Broiler.Cli.Tests;

/// <summary>
/// Phase 7 (item 3): metadata-rich script descriptors. <see cref="ScriptExtractionService.ExtractAll"/>
/// now exposes a <see cref="ScriptExtractionResult.Descriptors"/> list capturing each discovered
/// <c>&lt;script&gt;</c>'s document order, source kind, URL, nonce, and async/defer/module flags — the
/// metadata that used to be computed and discarded. The classic execution buckets are unchanged (see
/// <see cref="ContentSecurityPolicyTests"/> / the GoogleSearchPolyfill and ScriptEngine extraction tests).
/// </summary>
public sealed class ScriptDescriptorTests
{
    [Fact]
    public void Captures_Kind_Order_And_Content_For_Inline_And_DataUri_Scripts()
    {
        // data:text/javascript,var%20b%3D2 decodes to "var b=2".
        const string html =
            "<script>var a=1;</script>" +
            "<script src=\"data:text/javascript,var%20b%3D2\"></script>";
        var d = ScriptExtractionService.ExtractAll(html).Descriptors;

        Assert.Equal(2, d.Count);

        Assert.Equal(0, d[0].DocumentOrder);
        Assert.Equal(ScriptSourceKind.Inline, d[0].Kind);
        Assert.Null(d[0].Url);
        Assert.Equal("var a=1;", d[0].Content);

        Assert.Equal(1, d[1].DocumentOrder);
        Assert.Equal(ScriptSourceKind.DataUri, d[1].Kind);
        Assert.Equal("data:text/javascript,var%20b%3D2", d[1].Url);
        Assert.Equal("var b=2", d[1].Content);
    }

    [Fact]
    public void Captures_Async_Defer_And_Nonce_Flags()
    {
        const string html =
            "<script defer nonce=\"abc\">var a=1;</script>" +
            "<script async>var b=2;</script>";
        var d = ScriptExtractionService.ExtractAll(html).Descriptors;

        Assert.True(d[0].IsDefer);
        Assert.False(d[0].IsAsync);
        Assert.Equal("abc", d[0].Nonce);

        Assert.True(d[1].IsAsync);
        Assert.False(d[1].IsDefer);
    }

    [Fact]
    public void Records_Module_Scripts_In_Descriptors_But_Not_In_Execution_Buckets()
    {
        const string html =
            "<script>var a=1;</script>" +
            "<script type=\"module\">import x from './m.js';</script>";
        var result = ScriptExtractionService.ExtractAll(html);

        // The module is present in the metadata list (with IsModule set) ...
        var module = result.Descriptors.Single(x => x.IsModule);
        Assert.Equal(1, module.DocumentOrder);

        // ... but omitted from the classic execution buckets (unchanged behaviour).
        Assert.Single(result.Scripts);
        Assert.Contains("var a=1;", result.Scripts);
        Assert.DoesNotContain(result.Scripts, s => s.Contains("import"));
    }

    [Fact]
    public void Captures_External_Src_Kind_And_Url()
    {
        const string html = "<script src=\"app.js\"></script>";
        var d = ScriptExtractionService.ExtractAll(html).Descriptors;

        Assert.Single(d);
        Assert.Equal(ScriptSourceKind.External, d[0].Kind);
        Assert.Equal("app.js", d[0].Url);
    }
}
