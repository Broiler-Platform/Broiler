using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// Regression guard for the quirks-mode "the body element fills the html element"
/// / "the html element fills the viewport" quirks
/// (https://quirks.spec.whatwg.org/), driving WPT
/// css-grid/grid-lanes/grid-lanes-quirks-fill-viewport. In quirks mode an
/// auto-height &lt;body&gt; fills the html element's content box *minus its own
/// margins*; the quirks flag is derived from the doctype and published through
/// <c>Broiler.Layout.DocumentModeContext</c> (set by <c>DomBridge</c> on parse).
/// </summary>
public sealed class QuirksBodyFillTests
{
    // Quirks-mode detection from the doctype — the fiddly half of the feature.
    // The layout fill itself (an auto body/html growing to the viewport minus
    // margins) is exercised by the WPT reftest grid-lanes-quirks-fill-viewport;
    // Broiler's check-layout harness reports the body's client rect as the full
    // viewport regardless of mode, so it cannot measure the fill here.
    [Theory]
    [InlineData("<!DOCTYPE html>", false)]
    [InlineData("<!doctype html>", false)]
    [InlineData("<!DOCTYPE HTML>", false)]
    [InlineData("<!doctype quirks>", true)]
    [InlineData("<!doctype html PUBLIC \"-//W3C//DTD HTML 4.01//EN\">", false)]
    [InlineData("", true)]
    [InlineData("<html><body></body></html>", true)]
    public void IsQuirksHtml_MatchesDoctype(string html, bool expectedQuirks)
        => Assert.Equal(expectedQuirks, Broiler.Layout.DocumentModeContext.IsQuirksHtml(html));
}
