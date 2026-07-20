using System.Security.Cryptography;
using System.Text;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Scripting;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

public class ContentSecurityPolicyTests
{
    [Fact]
    public void ExecuteScriptsWithDom_Blocks_Inline_Script_Without_UnsafeInline()
    {
        const string html = """
            <!DOCTYPE html>
            <html>
            <head>
                <meta http-equiv="Content-Security-Policy" content="script-src 'self'">
            </head>
            <body>
                <script>document.body.setAttribute('data-inline', 'ran');</script>
            </body>
            </html>
            """;

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.DoesNotContain("data-inline=\"ran\"", result);
    }

    [Fact]
    public void ExecuteScriptsWithDom_DefaultSrc_Falls_Back_For_Inline_Scripts()
    {
        const string html = """
            <!DOCTYPE html>
            <html>
            <head>
                <meta http-equiv="Content-Security-Policy" content="default-src 'self'">
            </head>
            <body>
                <script>document.body.setAttribute('data-inline', 'ran');</script>
            </body>
            </html>
            """;

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.DoesNotContain("data-inline=\"ran\"", result);
    }

    [Fact]
    public void ExecuteScriptsWithDom_Allows_Inline_Script_With_Matching_Nonce()
    {
        const string html = """
            <!DOCTYPE html>
            <html>
            <head>
                <meta http-equiv="Content-Security-Policy" content="script-src 'nonce-good'">
            </head>
            <body>
                <script nonce="good">document.body.setAttribute('data-inline', 'ran');</script>
            </body>
            </html>
            """;

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("data-inline=\"ran\"", result);
    }

    [Fact]
    public void ExecuteScriptsWithDom_ScriptSrcElem_Takes_Precedence_Over_ScriptSrc_For_Inline_Scripts()
    {
        const string html = """
            <!DOCTYPE html>
            <html>
            <head>
                <meta http-equiv="Content-Security-Policy" content="script-src 'unsafe-inline'; script-src-elem 'self'">
            </head>
            <body>
                <script>document.body.setAttribute('data-inline', 'blocked');</script>
            </body>
            </html>
            """;

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.DoesNotContain("data-inline=\"blocked\"", result);
    }

    [Fact]
    public void ExecuteScriptsWithDom_Blocks_Inline_Event_Handler_When_ScriptSrcAttr_Disallows_It()
    {
        const string html = """
            <!DOCTYPE html>
            <html>
            <head>
                <meta http-equiv="Content-Security-Policy" content="script-src 'unsafe-inline'; script-src-attr 'self'">
            </head>
            <body>
                <button id="btn" onclick="document.body.setAttribute('data-attr', 'blocked');">go</button>
                <script>document.getElementById('btn').click();</script>
            </body>
            </html>
            """;

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.DoesNotContain("data-attr=\"blocked\"", result);
    }

    [Fact]
    public void ExecuteScriptsWithDom_Allows_Inline_Event_Handler_When_ScriptSrc_Falls_Back_To_UnsafeInline()
    {
        const string html = """
            <!DOCTYPE html>
            <html>
            <head>
                <meta http-equiv="Content-Security-Policy" content="script-src 'unsafe-inline'">
            </head>
            <body>
                <button id="btn" onclick="document.body.setAttribute('data-attr', 'ran');">go</button>
                <script>document.getElementById('btn').click();</script>
            </body>
            </html>
            """;

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("data-attr=\"ran\"", result);
    }

    [Fact]
    public void ExecuteScriptsWithDom_Allows_Inline_Event_Handler_With_Matching_Hash_When_UnsafeHashes_Is_Present()
    {
        const string handler = "document.body.setAttribute('data-attr', 'hashed');";
        var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(handler)));
        var html = $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta http-equiv="Content-Security-Policy" content="script-src 'unsafe-inline'; script-src-attr 'unsafe-hashes' 'sha256-{{hash}}'">
            </head>
            <body>
                <button id="btn" onclick="{{handler}}">go</button>
                <script>document.getElementById('btn').click();</script>
            </body>
            </html>
            """;

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("data-attr=\"hashed\"", result);
    }

    [Fact]
    public void ExecuteScriptsWithDom_Blocks_Hashed_Inline_Event_Handler_Without_UnsafeHashes()
    {
        const string handler = "document.body.setAttribute('data-attr', 'hashed');";
        var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(handler)));
        var html = $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta http-equiv="Content-Security-Policy" content="script-src 'unsafe-inline'; script-src-attr 'sha256-{{hash}}'">
            </head>
            <body>
                <button id="btn" onclick="{{handler}}">go</button>
                <script>document.getElementById('btn').click();</script>
            </body>
            </html>
            """;

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.DoesNotContain("data-attr=\"hashed\"", result);
    }

    [Fact]
    public void ExecuteScriptsWithDom_Blocks_Data_Script_When_Policy_Is_Self_Only()
    {
        const string html = """
            <!DOCTYPE html>
            <html>
            <head>
                <meta http-equiv="Content-Security-Policy" content="script-src 'self'">
            </head>
            <body>
                <script src="data:text/javascript,document.body.setAttribute('data-external','ran')"></script>
            </body>
            </html>
            """;

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.DoesNotContain("data-external=\"ran\"", result);
    }

    [Fact]
    public void ExecuteScriptsWithDom_Allows_SameOrigin_File_Script_When_Policy_Is_Self()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "broiler-csp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var scriptPath = Path.Combine(tempDirectory, "ext.js");
            File.WriteAllText(scriptPath, "document.body.setAttribute('data-external','self-ok');");

            var pageUrl = new Uri(Path.Combine(tempDirectory, "page.html")).AbsoluteUri;
            var html = """
                <!DOCTYPE html>
                <html>
                <head>
                    <meta http-equiv="Content-Security-Policy" content="script-src 'self'">
                </head>
                <body>
                    <script src="ext.js"></script>
                </body>
                </html>
                """;

            var result = CaptureService.ExecuteScriptsWithDom(html, pageUrl);

            Assert.Contains("data-external=\"self-ok\"", result);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void ExecuteScriptsWithDom_Blocks_External_Script_When_StrictDynamic_Ignores_Static_Allowlist()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "broiler-csp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var scriptPath = Path.Combine(tempDirectory, "ext.js");
            File.WriteAllText(scriptPath, "document.body.setAttribute('data-external','ran');");

            var pageUrl = new Uri(Path.Combine(tempDirectory, "page.html")).AbsoluteUri;
            var html = """
                <!DOCTYPE html>
                <html>
                <head>
                    <meta http-equiv="Content-Security-Policy" content="script-src 'nonce-good' 'strict-dynamic' 'self'">
                </head>
                <body>
                    <script src="ext.js"></script>
                </body>
                </html>
                """;

            var result = CaptureService.ExecuteScriptsWithDom(html, pageUrl);

            Assert.DoesNotContain("data-external=\"ran\"", result);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void ExecuteScriptsWithDom_Allows_Nonce_Matching_External_Script_When_StrictDynamic_Is_Present()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "broiler-csp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var scriptPath = Path.Combine(tempDirectory, "ext.js");
            File.WriteAllText(scriptPath, "document.body.setAttribute('data-external','nonce-ok');");

            var pageUrl = new Uri(Path.Combine(tempDirectory, "page.html")).AbsoluteUri;
            var html = """
                <!DOCTYPE html>
                <html>
                <head>
                    <meta http-equiv="Content-Security-Policy" content="script-src 'nonce-good' 'strict-dynamic' 'self'">
                </head>
                <body>
                    <script nonce="good" src="ext.js"></script>
                </body>
                </html>
                """;

            var result = CaptureService.ExecuteScriptsWithDom(html, pageUrl);

            Assert.Contains("data-external=\"nonce-ok\"", result);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void ScriptExtractor_ExtractAll_Skips_Inline_Scripts_Blocked_By_MetaCsp()
    {
        const string html = """
            <!DOCTYPE html>
            <html>
            <head>
                <meta http-equiv="Content-Security-Policy" content="script-src 'nonce-good'">
            </head>
            <body>
                <script>var blocked = true;</script>
                <script nonce="good">var allowed = true;</script>
            </body>
            </html>
            """;

        var result = ScriptExtractionService.ExtractAll(html, "file:///test.html");

        Assert.Single(result.Scripts);
        Assert.Contains("var allowed = true;", result.Scripts);
    }

    [Fact]
    public void ScriptEngine_Execute_Blocks_Eval_When_MetaCsp_Disallows_UnsafeEval()
    {
        const string html = """
            <!DOCTYPE html>
            <html>
            <head>
                <meta http-equiv="Content-Security-Policy" content="script-src 'unsafe-inline'">
            </head>
            <body></body>
            </html>
            """;

        var script = """
            try {
                eval("document.body.textContent = 'after';");
            } catch (e) {
                document.body.setAttribute('data-csp', 'blocked');
            }
            """;

        var engine = new ScriptEngine();
        var result = engine.Execute(new[] { script }, html, "file:///test.html");

        Assert.NotNull(result);
        Assert.Contains("data-csp=\"blocked\"", result);
        Assert.DoesNotContain(">after<", result);
    }

    // --- style-src family (issue #1302 CSP style-src* WPT tests) ----------

    [Fact]
    public void StyleSrcAttr_None_Strips_Inline_Style_Attribute()
    {
        const string html = """
            <!DOCTYPE html>
            <html>
            <head><meta http-equiv="Content-Security-Policy"
                content="style-src-attr 'none'; style-src 'unsafe-inline';"></head>
            <body style="background: green"></body>
            </html>
            """;

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///t.html");

        Assert.DoesNotContain("background: green", result);
        Assert.DoesNotContain("style=", result);
    }

    [Fact]
    public void StyleSrcElem_None_Removes_Style_Element()
    {
        const string html = """
            <!DOCTYPE html>
            <html>
            <head><meta http-equiv="Content-Security-Policy"
                content="style-src-elem 'none'; style-src 'unsafe-inline';"></head>
            <body><style>body {background: green;}</style></body>
            </html>
            """;

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///t.html");

        Assert.DoesNotContain("background: green", result);
        Assert.DoesNotContain("<style", result);
    }

    // --- Phase 7 item 5: style CSP is a host decision enforced during Attach ----

    [Fact]
    public void Attach_Enforces_Style_Csp_On_The_Bridge_Itself()
    {
        // The bridge authorises styles as the final step of Attach when a policy is configured, so every
        // host path — including ScriptEngine.Execute, which never called ApplyStyleContentSecurityPolicy —
        // hands scripts/rendering a CSP-authorised DOM, not just the CLI/WPT hosts.
        const string html = """
            <!DOCTYPE html>
            <html>
            <head><meta http-equiv="Content-Security-Policy" content="style-src 'none'"></head>
            <body style="background: green"></body>
            </html>
            """;

        using var ctx = new JSContext();
        using var bridge = new DomBridge();
        bridge.Csp = ContentSecurityPolicy.FromHtml(html);
        bridge.Attach(ctx, html, "file:///t.html");

        Assert.DoesNotContain("background: green", bridge.SerializeToHtml());
    }

    [Fact]
    public void Attach_Leaves_Inline_Style_When_No_Csp_Configured()
    {
        const string html = """
            <!DOCTYPE html><html><head></head><body style="background: green"></body></html>
            """;

        using var ctx = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(ctx, html, "file:///t.html"); // no policy configured → nothing stripped

        Assert.Contains("background: green", bridge.SerializeToHtml());
    }

    [Fact]
    public void StyleSrcElem_Allowed_Attr_Blocked_Keeps_Element_Strips_Attribute()
    {
        const string html = """
            <!DOCTYPE html>
            <html>
            <head><meta http-equiv="Content-Security-Policy"
                content="style-src-elem 'unsafe-inline'; style-src-attr 'none';"></head>
            <body style="background: green"><style>body {background: blue;}</style></body>
            </html>
            """;

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///t.html");

        // The style attribute is blocked; the <style> element survives.
        Assert.DoesNotContain("background: green", result);
        Assert.Contains("background: blue", result);
        Assert.Contains("<style", result);
    }

    [Fact]
    public void StyleSrc_UnsafeInline_Keeps_Inline_Style_Attribute()
    {
        const string html = """
            <!DOCTYPE html>
            <html>
            <head><meta http-equiv="Content-Security-Policy"
                content="style-src 'unsafe-inline';"></head>
            <body style="background: green"></body>
            </html>
            """;

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///t.html");

        Assert.Contains("background: green", result);
    }

    [Fact]
    public void No_Csp_Keeps_Inline_Styles()
    {
        const string html = """
            <!DOCTYPE html>
            <html>
            <body style="background: green"><style>p {color: red;}</style></body>
            </html>
            """;

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///t.html");

        Assert.Contains("background: green", result);
        Assert.Contains("<style", result);
    }

    // Phase 7 item 5: external-stylesheet (<link rel=stylesheet>) CSP gating. The style analogue of
    // AllowsExternalScript, applied at the FetchExternalStylesheet call site so DOM/CSS never fetch or
    // apply a style-src-blocked external stylesheet.

    [Fact]
    public void AllowsExternalStyle_None_Blocks_Every_External_Stylesheet()
    {
        var csp = new ContentSecurityPolicy();
        csp.Parse("style-src 'none'");

        Assert.False(csp.AllowsExternalStyle("https://cdn.test/a.css", "https://a.test/page.html"));
        Assert.False(csp.AllowsExternalStyle("a.css", "https://a.test/page.html"));
    }

    [Fact]
    public void AllowsExternalStyle_Self_Allows_SameOrigin_And_Blocks_CrossOrigin()
    {
        var csp = new ContentSecurityPolicy();
        csp.Parse("style-src 'self'");

        Assert.True(csp.AllowsExternalStyle("https://a.test/theme.css", "https://a.test/page.html"));
        Assert.False(csp.AllowsExternalStyle("https://cdn.test/theme.css", "https://a.test/page.html"));
    }

    [Fact]
    public void AllowsExternalStyle_Honours_Host_Scheme_And_Nonce_Sources()
    {
        var host = new ContentSecurityPolicy();
        host.Parse("style-src https://cdn.test");
        Assert.True(host.AllowsExternalStyle("https://cdn.test/lib/a.css", "https://a.test/page.html"));
        Assert.False(host.AllowsExternalStyle("https://evil.test/a.css", "https://a.test/page.html"));

        var scheme = new ContentSecurityPolicy();
        scheme.Parse("style-src https:");
        Assert.True(scheme.AllowsExternalStyle("https://anywhere.test/a.css", "https://a.test/page.html"));
        Assert.False(scheme.AllowsExternalStyle("http://anywhere.test/a.css", "https://a.test/page.html"));

        var nonce = new ContentSecurityPolicy();
        nonce.Parse("style-src 'nonce-abc123'");
        Assert.True(nonce.AllowsExternalStyle("https://cdn.test/a.css", "https://a.test/page.html", "abc123"));
        Assert.False(nonce.AllowsExternalStyle("https://cdn.test/a.css", "https://a.test/page.html", "wrong"));
    }

    [Fact]
    public void AllowsExternalStyle_StyleSrcElem_Takes_Precedence_And_Falls_Back_To_DefaultSrc()
    {
        // style-src-elem wins over style-src for a <link>.
        var elem = new ContentSecurityPolicy();
        elem.Parse("style-src 'none'; style-src-elem 'self'");
        Assert.True(elem.AllowsExternalStyle("https://a.test/a.css", "https://a.test/page.html"));

        // With neither style directive, default-src applies.
        var fallback = new ContentSecurityPolicy();
        fallback.Parse("default-src 'self'");
        Assert.True(fallback.AllowsExternalStyle("https://a.test/a.css", "https://a.test/page.html"));
        Assert.False(fallback.AllowsExternalStyle("https://cdn.test/a.css", "https://a.test/page.html"));

        // No relevant directive at all → allowed (empty effective source set).
        var empty = new ContentSecurityPolicy();
        empty.Parse("img-src 'self'");
        Assert.True(empty.AllowsExternalStyle("https://cdn.test/a.css", "https://a.test/page.html"));
    }
}
