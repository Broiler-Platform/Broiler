using Broiler.HtmlBridge;

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

        var extractor = new ScriptExtractor();
        var result = extractor.ExtractAll(html, "file:///test.html");

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
}
