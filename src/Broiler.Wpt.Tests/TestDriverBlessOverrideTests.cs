using System.IO;

namespace Broiler.Wpt.Tests;

/// <summary>
/// The runner's <c>test_driver</c> implementation must survive the real
/// <c>/resources/testdriver.js</c>, which a test loads <em>after</em> the injected stubs and which
/// replaces every method with one that funnels through <c>test_driver_internal</c> — unimplemented
/// here, because WPT's in-tree <c>testdriver-vendor.js</c> is an empty file.
/// <para>
/// <c>bless(intent, action)</c> is where that mattered. Upstream it appends a
/// <c>This test requires user interaction. Please click here to allow &lt;intent&gt;.</c> button and
/// awaits a WebDriver click on it, so with the real file in charge the action never ran: the test
/// rendered its un-activated state <em>and</em> the button appeared in the screenshot. The stub's
/// <c>typeof … === 'undefined'</c> guard could not catch this — it ran while the name was still
/// free, and testdriver.js overwrote it afterwards. Every <c>fullscreen/rendering</c> reftest failed
/// that way, including <c>backdrop-iframe</c>, the third of issue #1714's biggest problems.
/// </para>
/// </summary>
public class TestDriverBlessOverrideTests : IDisposable
{
    private readonly string _tempDir;

    public TestDriverBlessOverrideTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "broiler-wpt-testdriver-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (!Directory.Exists(_tempDir))
            return;

        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Leaving a directory under %TEMP% behind is not a test failure.
        }
    }

    private string WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_tempDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    /// <summary>
    /// Upstream testdriver.js in miniature: it claims <c>window.test_driver</c> for itself and its
    /// <c>bless</c> both mutates the document and returns a promise that never settles — the two
    /// symptoms the real file produced. Written under the WPT root the runner resolves
    /// <c>/resources/…</c> against, so the runner loads it exactly as it loads the real one.
    /// </summary>
    private void WriteUpstreamStyleTestDriver() =>
        WriteFile(
            "resources/testdriver.js",
            """
            (function() {
              window.test_driver = {
                bless: function(intent, action) {
                  var button = document.createElement('button');
                  button.setAttribute(
                    'style',
                    'display:block;width:320px;height:240px;background:red;border:0;margin:0');
                  document.body.appendChild(button);
                  return new Promise(function() {});
                }
              };
            })();
            """);

    private static string BlessPage(string href) =>
        $$"""
        <!DOCTYPE html>
        <html><head>
        <link rel="match" href="{{href}}">
        <style>body { margin: 0 } #box { width: 100px; height: 100px; background: red }</style>
        <script src="/resources/testdriver.js"></script>
        </head>
        <body>
        <div id="box"></div>
        <script>
        test_driver.bless('fullscreen', () => {
          document.getElementById('box').style.background = 'green';
        });
        </script>
        </body></html>
        """;

    /// <summary>
    /// The blessed action runs even though testdriver.js has already installed its own
    /// <c>bless</c>, so the document reaches the state the reference states.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void BlessedAction_Runs_Even_When_Upstream_TestDriver_Has_Claimed_test_driver()
    {
        WriteUpstreamStyleTestDriver();
        var test = WriteFile("bless.html", BlessPage("bless-ref.html"));
        WriteFile(
            "bless-ref.html",
            "<!DOCTYPE html><html><head><style>body { margin: 0 }</style></head>"
            + "<body><div style=\"width:100px;height:100px;background:green\"></div></body></html>");

        var result = new WptTestRunner(320, 240, referenceTestsOnly: true).RunReferenceTest(test, _tempDir);

        Assert.True(result.Passed, result.Message);
    }

    /// <summary>
    /// The vendor hook is re-topped too, so it does not matter which half of the pair a test
    /// includes last. Upstream's <c>testdriver-vendor.js</c> is empty and wptrunner substitutes its
    /// own implementations there, so a file that arrives after testdriver.js and defines
    /// <c>bless</c> is the shape this has to survive — and it is why
    /// <c>IsTestDriverScript</c> matches the stem rather than <c>testdriver.js</c> exactly.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Override_Also_Survives_A_Vendor_Hook_Loaded_After_TestDriver()
    {
        WriteUpstreamStyleTestDriver();
        WriteFile(
            "resources/testdriver-vendor.js",
            """
            (function() {
              window.test_driver.bless = function(intent, action) {
                return new Promise(function() {});
              };
            })();
            """);

        var test = WriteFile(
            "bless-vendor.html",
            BlessPage("bless-vendor-ref.html").Replace(
                "<script src=\"/resources/testdriver.js\"></script>",
                "<script src=\"/resources/testdriver.js\"></script>"
                + "<script src=\"/resources/testdriver-vendor.js\"></script>",
                StringComparison.Ordinal));
        WriteFile(
            "bless-vendor-ref.html",
            "<!DOCTYPE html><html><head><style>body { margin: 0 }</style></head>"
            + "<body><div style=\"width:100px;height:100px;background:green\"></div></body></html>");

        var result = new WptTestRunner(320, 240, referenceTestsOnly: true).RunReferenceTest(test, _tempDir);

        Assert.True(result.Passed, result.Message);
    }

    /// <summary>
    /// The interaction button upstream <c>bless</c> appends never reaches the document, so it
    /// cannot land in the screenshot. Its fixture's <c>bless</c> action is a no-op, so the only
    /// thing separating a pass from a viewport-filling red rectangle is which <c>bless</c> ran —
    /// which is what the <c>fullscreen/rendering</c> renders showed before the fix.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void BlessedAction_Does_Not_Append_The_User_Interaction_Button()
    {
        WriteUpstreamStyleTestDriver();
        var test = WriteFile(
            "bless-button.html",
            """
            <!DOCTYPE html>
            <html><head>
            <link rel="match" href="bless-button-ref.html">
            <style>body { margin: 0 }</style>
            <script src="/resources/testdriver.js"></script>
            </head>
            <body>
            <script>
            test_driver.bless('fullscreen', () => {});
            </script>
            </body></html>
            """);
        WriteFile(
            "bless-button-ref.html",
            "<!DOCTYPE html><html><head><style>body { margin: 0 }</style></head><body></body></html>");

        var result = new WptTestRunner(320, 240, referenceTestsOnly: true).RunReferenceTest(test, _tempDir);

        Assert.True(result.Passed, result.Message);
    }
}
