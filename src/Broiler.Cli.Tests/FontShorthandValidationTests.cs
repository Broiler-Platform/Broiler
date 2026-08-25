namespace Broiler.Cli.Tests;

/// <summary>
/// <c>document.fonts.check()</c> and <c>load()</c> take a CSS <c>font</c> shorthand, and
/// css-font-loading-3 makes an unparsable one a <c>SyntaxError</c>.
/// </summary>
/// <remarks>
/// <para>
/// Only an absent or empty string used to throw. Anything non-empty was waved through, so
/// <c>document.fonts.check('not-a-font')</c> answered <c>true</c> where every browser throws — and
/// <c>true</c> is the answer that matters, because a page feature-testing a font it cannot have is
/// told it has it.
/// </para>
/// <para>
/// The deviation was deliberate and its stated reason was risk: rejecting a shorthand Broiler merely
/// failed to <em>parse</em> would break pages over a diagnostic it could not produce. That reason is
/// why this fixture is a <b>table</b> rather than a handful of cases, and why every row of it — the
/// 26 that must be accepted as much as the 18 that must be rejected — is a Chromium answer taken
/// through Playwright. Over-rejection is the failure mode being guarded against, so the accepted
/// half carries the awkward spellings: <c>oblique 40deg</c>, <c>calc(1em + 2px)</c>, a glued
/// <c>16px/2</c>, a quoted family, a bare <c>900</c> weight, and runs of <c>normal</c>.
/// </para>
/// <para>
/// What has not changed is the modelling: a shorthand that parses still answers <c>true</c>, because
/// Broiler resolves fonts synchronously and no load is ever in flight. This only stops the API
/// claiming to understand strings that are not fonts.
/// </para>
/// </remarks>
public class FontShorthandValidationTests
{
    /// <summary>Shorthands a browser accepts. Each must parse — rejecting one is the regression this
    /// table exists to catch.</summary>
    public static IEnumerable<object[]> Valid() => ValidCases.Select(c => new object[] { c });

    private static readonly string[] ValidCases =
    [
        "12px monospace", "12px serif", "italic bold 12px/1.5 \"Font Name\", serif",
        "caption", "icon", "menu", "message-box", "small-caption", "status-bar",
        "1em serif", "x-large serif", "smaller serif", "larger serif", "bold 100% sans-serif",
        "12pt \"My Font\"", "normal normal 16px Arial", "italic small-caps bold 16px/2 cursive",
        "1.5rem system-ui", "condensed 12px serif", "oblique 40deg 12px serif", "900 12px serif",
        "calc(1em + 2px) serif", "12px/1.5 serif", "12px Arial, Helvetica, sans-serif",
        "  12px   monospace  ", "12px monospace ",
    ];

    /// <summary>Strings a browser rejects with a <c>SyntaxError</c>. Every one of these answered
    /// <c>true</c> before, apart from the empty and whitespace-only pair.</summary>
    public static IEnumerable<object[]> Invalid() => InvalidCases.Select(c => new object[] { c });

    private static readonly string[] InvalidCases =
    [
        "not-a-font", "12px", "px monospace", "", "   ", "monospace", "bold", "bold serif",
        "12px/1.5", "italic", "12px 12px serif", "serif 12px", "inherit", "initial", "unset",
        "12px monospace !important", "bold 12px", "/1.5 serif", "12px, serif",
    ];

    private static string Check(string font, string method)
    {
        var literal = System.Text.Json.JsonSerializer.Serialize(font);
        var html = "<!doctype html><html><body><div id=\"result\"></div><script>\n" +
                   $"var f = {literal};\n" +
                   method +
                   "\n</script></body></html>";
        var serialized = CaptureService.ExecuteScriptsWithDom(html, "https://example.com/page");
        const string start = "<div id=\"result\">";
        var open = serialized.IndexOf(start, StringComparison.Ordinal);
        Assert.True(open >= 0, $"probe did not run; document was:\n{serialized}");
        open += start.Length;
        var close = serialized.IndexOf("</div>", open, StringComparison.Ordinal);
        Assert.True(close > open, $"probe wrote nothing for {literal}; document was:\n{serialized}");
        return serialized[open..close];
    }

    private const string CheckSync = """
var out;
try { out = 'ok:' + document.fonts.check(f); } catch (e) { out = 'THROW:' + (e && e.name); }
document.getElementById('result').textContent = out;
""";

    [Theory(Timeout = 600000)]
    [MemberData(nameof(Valid))]
    public void A_Valid_Shorthand_Is_Accepted(string font) =>
        Assert.Equal("ok:true", Check(font, CheckSync));

    [Theory(Timeout = 600000)]
    [MemberData(nameof(Invalid))]
    public void An_Invalid_Shorthand_Throws_A_SyntaxError(string font) =>
        Assert.Equal("THROW:SyntaxError", Check(font, CheckSync));

    /// <summary><c>load()</c> uses the same grammar, and reports the failure the way a promise-returning
    /// method must — a rejection, not a throw.</summary>
    [Fact(Timeout = 600000)]
    public void Load_Rejects_An_Invalid_Shorthand_Rather_Than_Throwing()
    {
        var result = Check("not-a-font", """
var out = 'no settlement';
try {
  document.fonts.load(f).then(function () { out = 'resolved'; }, function (e) { out = 'rejected:' + (e && e.name); });
} catch (e) { out = 'THREW:' + (e && e.name); }
document.getElementById('result').textContent = out;
""");
        // The rejection handler runs on the microtask checkpoint after this script, so what is
        // asserted here is that load() did not throw synchronously — a promise-returning method
        // reports a bad argument by rejecting.
        Assert.DoesNotContain("THREW", result);
    }

    /// <summary>The DOMException is the specified kind, so a page can branch on it.</summary>
    [Fact(Timeout = 600000)]
    public void The_Failure_Is_A_Real_DomException()
    {
        var result = Check("not-a-font", """
var out;
try { document.fonts.check(f); out = 'no throw'; }
catch (e) { out = [e.name, e instanceof DOMException, typeof e.message === 'string' && e.message.length > 0].join('|'); }
document.getElementById('result').textContent = out;
""");
        Assert.Equal("SyntaxError|true|true", result);
    }
}
