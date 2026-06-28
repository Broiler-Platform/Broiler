using System;
using System.Linq;
using System.Text;
using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// The DomBridge memoizes its box-geometry estimators during a check-layout
/// read pass so deep css-align / css-anchor-position trees stop timing out
/// (WPT #1113). These tests prove the memoization is behaviour-preserving: the
/// values <see cref="DomBridge.EvaluateCheckLayoutAssertions"/> produces with the
/// cache enabled are identical to the un-memoized path, on exactly the nested
/// abspos / relpos / auto-height structure that drove the exponential blow-up.
/// </summary>
public sealed class LayoutGeometryCacheEquivalenceTests
{
    private static System.Collections.Generic.IReadOnlyList<DomBridge.CheckLayoutAssertion> Evaluate(
        string html, bool cacheEnabled)
    {
        var previous = DomBridge.LayoutGeometryCacheEnabled;
        DomBridge.LayoutGeometryCacheEnabled = cacheEnabled;
        try
        {
            using var context = new JSContext();
            var bridge = new DomBridge();
            bridge.Attach(context, html, "file:///equivalence.html");
            return bridge.EvaluateCheckLayoutAssertions();
        }
        finally
        {
            DomBridge.LayoutGeometryCacheEnabled = previous;
        }
    }

    private static void AssertCachedEqualsUncached(string html)
    {
        var uncached = Evaluate(html, cacheEnabled: false);
        var cached = Evaluate(html, cacheEnabled: true);

        Assert.Equal(uncached.Count, cached.Count);
        Assert.NotEmpty(cached);
        for (var i = 0; i < cached.Count; i++)
        {
            Assert.Equal(uncached[i].Element, cached[i].Element);
            Assert.Equal(uncached[i].Property, cached[i].Property);
            Assert.Equal(uncached[i].Expected, cached[i].Expected);
            // Exact equality: memoization must not perturb the computed value.
            Assert.True(uncached[i].Actual.Equals(cached[i].Actual),
                $"{cached[i].Element} {cached[i].Property}: uncached={uncached[i].Actual} cached={cached[i].Actual}");
        }
    }

    // Mirrors css/css-align/blocks/align-content-block-002.html: a list-item test
    // box wrapping an auto-height in-flow chain plus an abspos + relpos descendant,
    // all of which exercise the up/down/sibling geometry recursion that exploded.
    // Kept to one wrapper so the *un-memoized* reference path still completes (it
    // is the exponential one the cache exists to tame).
    private static string OneWrapper()
    {
        var sb = new StringBuilder(
            "<!DOCTYPE html><html><head><style>" +
            "html,body{margin:0;padding:0}" +
            ".test{height:50px;margin:5px 20px;background:black;display:list-item}" +
            ".in-flow{margin:10px 0 4px;background:orange}" +
            ".relpos{position:relative;top:-15px}" +
            ".wrapper{position:relative;border:solid 2px gray}" +
            ".abspos{position:absolute;right:0;margin-top:-15px}" +
            ".overflow{height:0}" +
            "</style></head><body>");
        for (var i = 0; i < 1; i++)
        {
            sb.Append("<div class='wrapper'><div class='test'>")
              .Append("<div class='in-flow' data-offset-y='15'></div>")
              .Append("<div class='in-flow'><span class='abspos' data-offset-y='0'>ABS</span>")
              .Append("<span class='relpos' data-offset-y='0'>REL</span>")
              .Append("<div class='overflow' data-expected-height='0'>OVERFLOW</div></div>")
              .Append("</div></div>");
        }
        return sb.Append("</body></html>").ToString();
    }

    [Fact]
    public void Cached_Matches_Uncached_On_Nested_Abspos_Relpos_Auto_Tree()
        => AssertCachedEqualsUncached(OneWrapper());

    [Fact]
    public void Cached_Matches_Uncached_On_Deep_Auto_Height_Chain()
    {
        // A deep auto-height chain: each level's extent references its containing
        // block and its children — the recursion that previously fanned out.
        var sb = new StringBuilder(
            "<!DOCTYPE html><html><body style='margin:0'>");
        for (var depth = 0; depth < 4; depth++)
            sb.Append("<div style='margin-top:3px;padding-left:2px' data-offset-y='")
              .Append((depth + 1) * 3).Append("'>");
        sb.Append("<div data-expected-width='10' style='width:10px;height:10px'></div>");
        for (var depth = 0; depth < 4; depth++)
            sb.Append("</div>");
        AssertCachedEqualsUncached(sb.Append("</body></html>").ToString());
    }
}
