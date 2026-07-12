using System.Text;
using Broiler.CSS.Dom;
using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// DOM/CSS promotion §2.1 — differential parity between the bridge's sparse computed-style
/// projection (<c>DomBridge.GetComputedProps</c>) and its candidate canonical replacement
/// (<c>CssStyleEngine.GetSparseComputedStyle</c>, added in the Broiler.CSS submodule).
///
/// This does NOT swap any of the ~98 <c>GetComputedProps</c> call sites. It measures how
/// faithfully the canonical projection reproduces the bridge's output over a representative
/// corpus and pins the reconciliation surface, so the (higher-risk) swap can be scoped
/// against a known delta — and so that delta cannot silently grow.
///
/// The measured delta falls into four documented classes (all others must match exactly):
///  1. <b>UA display defaults</b> — the bridge injects a user-agent <c>display</c> for block
///     elements (<c>ApplyUserAgentDisplayDefaults</c>); the canonical projection leaves
///     <c>display</c> to the renderer, so it is present-in-bridge / absent-in-sparse.
///  2. <b>Inheritance model</b> — the canonical projection backfills inherited properties from
///     the parent's <em>full</em> computed style, so every inherited property is materialised
///     from the root's initials down. The bridge propagates inheritance from the parent's
///     <em>sparse</em> map, so an inherited property never declared anywhere stays absent.
///     The whole inherited-property set is therefore present-in-sparse / absent-in-bridge on
///     elements that declare none of it.
///  3. <b>Value resolution</b> (bidirectional) — the two paths resolve <c>var()</c>, the
///     CSS-wide keywords (<c>initial/inherit/unset/revert</c>) and relative font-weight
///     keywords at different points, so one side may hold the resolved value while the other
///     holds the raw token. The canonical projection resolves <c>var()</c>/<c>initial</c>/
///     <c>unset</c> and <c>bold</c>→<c>700</c> that the bridge leaves raw; conversely its
///     <c>ComputeStyle</c> path has no inherit-fold, so it emits a raw <c>inherit</c> that the
///     bridge resolves. Same property, resolved-vs-raw value either way.
///  4. <b>Custom properties</b> — the canonical projection surfaces <c>--*</c> custom
///     properties; the bridge omits them.
///
/// A swap of the call sites must reconcile 1 and 2 (they change what undeclared reads back as)
/// and audit 3/4; this test is the checklist and the drift guard, not a green-light on its own.
/// </summary>
public sealed class SparseComputedStyleParityTests
{
    private static readonly string[] Corpus =
    [
        @"<!DOCTYPE html><html><head><style>
            #a { color: red; font: italic bold 20px Ahem; }
            .p { color: purple; }
            div { margin: 10px 20px; padding: 5px; }
        </style></head><body>
            <div id='a' class='p'>x<span>y</span></div>
            <p class='p'>inherited<em>child</em></p>
        </body></html>",

        @"<!DOCTYPE html><html><head><style>
            #rel { position: relative; }
            #abs { position: absolute; top: 10px; left: 20px; width: 5px; height: 5px; }
            #ov { overflow: hidden; display: inline-block; }
        </style></head><body>
            <div id='rel' style='border: 1px solid green; background: red no-repeat'>
                <a id='abs'></a></div>
            <section id='ov'>scroll</section>
        </body></html>",

        @"<!DOCTYPE html><html><head><style>
            input { color: blue; } button { font-weight: bold; }
        </style></head><body>
            <form>
                <input id='i' type='text' value='hi'>
                <button id='b'>ok</button>
                <select id='s' size='3'><option>1</option><option>2</option></select>
                <textarea id='ta' rows='4'>text</textarea>
            </form>
        </body></html>",

        @"<!DOCTYPE html><html><head><style>
            :root { --accent: teal; }
            #v { color: var(--accent); text-decoration: underline; }
            #k { color: inherit; display: initial; margin: unset; }
        </style></head><body>
            <div id='v'>var</div>
            <div class='p'><span id='k'>keywords</span></div>
        </body></html>",
    ];

    private static readonly string[] CssWideKeywords = ["initial", "inherit", "unset", "revert"];
    private static readonly HashSet<string> FontWeightKeywords =
        new(["normal", "bold", "bolder", "lighter"], StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void CanonicalSparseProjection_Reproduces_Bridge_GetComputedProps_Modulo_Documented_Delta()
    {
        // Divergences that are NOT explained by one of the four documented classes — these are
        // the regressions/drift the guard exists to catch.
        var unexpectedOnlyInBridge = new SortedDictionary<string, (string el, string val)>();
        var unexpectedMismatch = new SortedDictionary<string, (string el, string bridge, string sparse)>();
        var unexpectedOnlyInSparse = new SortedDictionary<string, (string el, string val)>();

        int elementsChecked = 0;

        foreach (var html in Corpus)
        {
            using var ctx = new JSContext();
            var bridge = new DomBridge();
            bridge.Attach(ctx, html, "file:///parity.html");

            foreach (var element in bridge.Elements)
            {
                var tag = element.TagName?.ToLowerInvariant() ?? "?";
                if (tag is "html" or "head" or "meta" or "title" or "style" or "script"
                    or "#document" or "#doctype" or "#comment" or "#text")
                    continue;

                elementsChecked++;
                var desc = $"{tag}#{element.Id}";
                var bridgeMap = bridge.GetComputedPropsForParity(element);
                var sparseMap = bridge.GetSparseComputedStyleForParity(element);

                foreach (var kv in bridgeMap)
                {
                    if (!sparseMap.TryGetValue(kv.Key, out var sparseVal))
                    {
                        // Class 1: UA display defaults are the only expected bridge-only key.
                        if (!string.Equals(kv.Key, "display", StringComparison.OrdinalIgnoreCase))
                            unexpectedOnlyInBridge.TryAdd(kv.Key, (desc, kv.Value));
                    }
                    else if (!string.Equals(sparseVal, kv.Value, StringComparison.Ordinal)
                             && !IsExpectedValueResolution(kv.Key, kv.Value, sparseVal))
                    {
                        unexpectedMismatch.TryAdd(kv.Key, (desc, kv.Value, sparseVal));
                    }
                }

                foreach (var kv in sparseMap)
                {
                    if (bridgeMap.ContainsKey(kv.Key))
                        continue;
                    // Class 2 (inherited-property materialisation) + class 4 (custom properties)
                    // are the expected sparse-only keys.
                    if (kv.Key.StartsWith("--", StringComparison.Ordinal)
                        || CssComputedDefaults.InheritedProperties.Contains(kv.Key))
                        continue;
                    unexpectedOnlyInSparse.TryAdd(kv.Key, (desc, kv.Value));
                }
            }
        }

        Assert.True(elementsChecked >= 15, $"corpus regressed: only {elementsChecked} elements checked");

        var report = new StringBuilder();
        report.AppendLine(
            $"Uncategorised divergences (should be 0): onlyInBridge={unexpectedOnlyInBridge.Count}, "
            + $"valueMismatch={unexpectedMismatch.Count}, onlyInSparse={unexpectedOnlyInSparse.Count}");
        AppendGroup(report, "UNEXPECTED PRESENT-IN-BRIDGE / ABSENT-IN-SPARSE", unexpectedOnlyInBridge);
        AppendMismatch(report, "UNEXPECTED VALUE MISMATCH", unexpectedMismatch);
        AppendGroup(report, "UNEXPECTED PRESENT-IN-SPARSE / ABSENT-IN-BRIDGE", unexpectedOnlyInSparse);

        Assert.True(
            unexpectedOnlyInBridge.Count == 0
            && unexpectedMismatch.Count == 0
            && unexpectedOnlyInSparse.Count == 0,
            report.ToString());
    }

    // A value mismatch is EXPECTED when it is the value-resolution class: one side holds a raw
    // var() / CSS-wide keyword / relative-font-weight token while the other holds the resolved
    // value. This is bidirectional — the engine resolves var()/initial/unset/bold that the
    // bridge leaves raw, and the bridge resolves the `inherit` the engine's ComputeStyle leaves raw.
    private static bool IsExpectedValueResolution(string key, string bridgeValue, string sparseValue) =>
        IsUnresolvedToken(key, bridgeValue) || IsUnresolvedToken(key, sparseValue);

    private static bool IsUnresolvedToken(string key, string value)
    {
        var v = value.Trim();
        if (v.Contains("var(", StringComparison.OrdinalIgnoreCase))
            return true;
        if (CssWideKeywords.Contains(v.ToLowerInvariant()))
            return true;
        if (string.Equals(key, "font-weight", StringComparison.OrdinalIgnoreCase)
            && FontWeightKeywords.Contains(v))
            return true;
        return false;
    }

    private static void AppendGroup(StringBuilder sb, string title,
        SortedDictionary<string, (string el, string val)> group)
    {
        if (group.Count == 0)
            return;
        sb.AppendLine($"--- {title} ---");
        foreach (var (key, (el, val)) in group)
            sb.AppendLine($"  {key} = \"{val}\"  (e.g. {el})");
    }

    private static void AppendMismatch(StringBuilder sb, string title,
        SortedDictionary<string, (string el, string bridge, string sparse)> group)
    {
        if (group.Count == 0)
            return;
        sb.AppendLine($"--- {title} ---");
        foreach (var (key, (el, b, s)) in group)
            sb.AppendLine($"  {key}: bridge=\"{b}\" sparse=\"{s}\"  (e.g. {el})");
    }
}
