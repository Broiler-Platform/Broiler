using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Storage;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The document's <c>PerformanceNavigationTiming</c> entry and the three <c>performance</c>
/// accessors that hand it out — <c>getEntries</c>, <c>getEntriesByType</c> and
/// <c>getEntriesByName</c> (Navigation Timing Level 2 §4, Performance Timeline §3). Pure static:
/// the entry is built from the page URL the bridge already knows and the protocol its loader
/// negotiates, and nothing here touches bridge state afterwards.
/// </summary>
/// <remarks>
/// <para>
/// <c>performance.getEntriesByType</c> answered with an empty array and <c>getEntries</c> did not
/// exist at all, so the standard way to reach this entry —
/// <c>performance.getEntries()[0].nextHopProtocol</c>, or
/// <c>getEntriesByType('navigation')[0]</c> — was a TypeError on <c>undefined</c> rather than a
/// missing measurement. That aborts the function that asked, which for the analytics preambles
/// that read navigation timing is the same function that goes on to install the rest of the page's
/// instrumentation.
/// </para>
/// <para>
/// <b>What this entry deliberately does not carry.</b> A navigation entry in a browser also
/// carries some twenty timing marks — <c>requestStart</c>, <c>responseEnd</c>,
/// <c>domContentLoadedEventEnd</c> and the rest. Broiler's loader does not record them, and
/// publishing them as zeroes would be worse than leaving them out: a page differencing two of them
/// would compute a plausible-looking <c>0 ms</c> where the honest answer is "not measured", and no
/// feature test could tell the two apart. The members that are here are the ones the engine can
/// actually answer. <c>startTime</c> and <c>duration</c> are not exceptions to that: a navigation
/// entry's <c>startTime</c> is fixed at 0 by the specification, and its <c>duration</c> is 0 until
/// the load event ends.
/// </para>
/// </remarks>
internal static class NavigationTimingBinding
{
    /// <summary>
    /// Installs the entry-list accessors on <paramref name="performance"/>.
    /// </summary>
    /// <param name="performance">The <c>performance</c> object being built.</param>
    /// <param name="pageUrl">The document's URL, or the empty string when it has none.</param>
    /// <param name="pageProtocol">
    /// The document URL's scheme with its colon (<c>"https:"</c>), as <c>location.protocol</c>
    /// reports it. It decides whether there was a network hop to name.
    /// </param>
    public static void Install(JSObject performance, string pageUrl, string pageProtocol)
    {
        var navigation = BuildNavigationEntry(pageUrl, pageProtocol);

        performance.FastAddValue((KeyString)"getEntries",
            new DomFunction((in _) => new JSArray([navigation]), "getEntries", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        performance.FastAddValue((KeyString)"getEntriesByType",
            new DomFunction((in a) => EntriesByType(navigation, in a), "getEntriesByType", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        performance.FastAddValue((KeyString)"getEntriesByName",
            new DomFunction((in a) => EntriesByName(navigation, pageUrl, in a), "getEntriesByName", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
    }

    /// <summary>
    /// The ALPN token for the hop that fetched this document, or the empty string when there was no
    /// network hop to name — which is what the specification asks for on a <c>file:</c> or
    /// <c>about:</c> document, and on HTML the bridge was handed directly as a string.
    /// </summary>
    private static string NextHopProtocol(string pageProtocol) =>
        pageProtocol is "http:" or "https:" ? Layout.Net.BroilerHttpProtocol.NextHopProtocol : string.Empty;

    private static JSObject BuildNavigationEntry(string pageUrl, string pageProtocol)
    {
        var entry = new JSObject();

        // PerformanceEntry (Performance Timeline §3.1). A navigation entry is named by the
        // document's URL and its startTime is 0 by definition — the time origin *is* this
        // navigation's start.
        entry.FastAddValue((KeyString)"name", new JSString(pageUrl), JSPropertyAttributes.EnumerableConfigurableValue);
        entry.FastAddValue((KeyString)"entryType", new JSString("navigation"), JSPropertyAttributes.EnumerableConfigurableValue);
        entry.FastAddValue((KeyString)"startTime", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
        entry.FastAddValue((KeyString)"duration", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);

        // PerformanceResourceTiming (Resource Timing §4.1), of which a navigation entry is a
        // subtype. `initiatorType` is fixed at "navigation" for one, and the protocol is the engine's
        // own — see BroilerHttpProtocol, which pins the request version this names.
        entry.FastAddValue((KeyString)"initiatorType", new JSString("navigation"), JSPropertyAttributes.EnumerableConfigurableValue);
        entry.FastAddValue((KeyString)"nextHopProtocol", new JSString(NextHopProtocol(pageProtocol)), JSPropertyAttributes.EnumerableConfigurableValue);

        // PerformanceNavigationTiming (Navigation Timing §4). "navigate" is the plain case — the
        // alternatives ("reload", "back_forward", "prerender") describe entries into a session
        // history Broiler does not keep, so none of them can arise here.
        entry.FastAddValue((KeyString)"type", new JSString("navigate"), JSPropertyAttributes.EnumerableConfigurableValue);

        // Redirects are followed inside the HTTP client, which does not report how many it took, so
        // this is the count Broiler can vouch for rather than a measurement.
        entry.FastAddValue((KeyString)"redirectCount", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);

        entry.FastAddValue((KeyString)"toJSON",
            new DomFunction((in _) => entry, "toJSON", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        return entry;
    }

    private static JSValue EntriesByType(JSObject navigation, in Arguments a)
    {
        string type = a.Length > 0 ? a[0].ToString() : string.Empty;
        return string.Equals(type, "navigation", StringComparison.Ordinal)
            ? new JSArray([navigation])
            : new JSArray();
    }

    private static JSValue EntriesByName(JSObject navigation, string pageUrl, in Arguments a)
    {
        if (a.Length == 0)
            return new JSArray();

        // The second argument narrows by entry type; a caller that passes one that is not
        // "navigation" is asking for entries this timeline has none of.
        if (a.Length > 1 && a[1] is not JSUndefined && !string.Equals(a[1].ToString(), "navigation", StringComparison.Ordinal))
            return new JSArray();

        return string.Equals(a[0].ToString(), pageUrl, StringComparison.Ordinal)
            ? new JSArray([navigation])
            : new JSArray();
    }
}
