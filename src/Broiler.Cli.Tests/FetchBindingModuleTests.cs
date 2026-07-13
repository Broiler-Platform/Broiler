using System.Reflection;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom.Features;
using Broiler.HtmlBridge.Dom.Runtime;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 eleventh slice (P3.11): the fetch /
/// XMLHttpRequest networking surface (fetch + Headers/Request/Response/FormData/Blob/AbortController
/// and the XHR polyfill) is now a co-located binding module (<see cref="FetchBinding"/>) whose host
/// I/O goes through the injected Phase 2 <see cref="ResourceLoader"/>; the only bridge coupling (the
/// page URL for redirect resolution) is the narrow <see cref="IFetchHost"/> contract. The
/// characterizations exercise the extracted feature end-to-end through the bridge with no network
/// dependency, and check that the two non-networking registrations moved out of the fetch method
/// (<c>MessageChannel</c>, <c>getComputedStyle</c>) still install.
/// </summary>
public sealed class FetchBindingModuleTests
{
    [Fact]
    public void Fetch_Feature_Module_Is_Co_Located_And_Internal()
    {
        var moduleType = typeof(FetchBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);
        Assert.False(typeof(IFetchHost).IsPublic);
    }

    [Fact]
    public void DomBridge_Consumes_Fetch_Through_The_Host_Contract()
    {
        Assert.True(typeof(IFetchHost).IsAssignableFrom(typeof(DomBridge)));
        Assert.Contains(
            typeof(DomBridge).GetFields(BindingFlags.NonPublic | BindingFlags.Instance),
            static field => field.FieldType == typeof(FetchBinding));
    }

    [Fact]
    public void FetchBinding_Loads_Through_The_Injected_ResourceLoader()
    {
        // Networking host I/O is the ResourceLoader seam, not an ad-hoc HttpClient in the module.
        Assert.Contains(
            typeof(FetchBinding).GetFields(BindingFlags.NonPublic | BindingFlags.Instance),
            static field => field.FieldType == typeof(ResourceLoader));
    }

    [Fact]
    public void Response_Constructor_And_Json_Factory_Through_The_Module()
    {
        const string html = "<!DOCTYPE html><html><body></body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");

        var result = context.Eval("""
            (() => {
                var r = new Response('hi', { status: 201, statusText: 'Created' });
                var j = Response.json({ a: 1 });
                return r.status + '|' + r.statusText + '|' + j.headers.get('Content-Type');
            })()
            """);

        Assert.Equal("201|Created|application/json", result.ToString());
    }

    [Fact]
    public void Headers_And_FormData_Objects_Through_The_Module()
    {
        const string html = "<!DOCTYPE html><html><body></body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");

        var result = context.Eval("""
            (() => {
                var h = new Headers({ 'X-One': 'a' });
                h.append('X-One', 'b');
                var fd = new FormData();
                fd.append('k', '1');
                fd.append('k', '2');
                return h.get('x-one') + '|' + fd.getAll('k').length + '|' + fd.get('k');
            })()
            """);

        Assert.Equal("a, b|2|1", result.ToString());
    }

    [Fact]
    public void XMLHttpRequest_Polyfill_Is_Installed_By_The_Module()
    {
        const string html = "<!DOCTYPE html><html><body></body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");

        var result = context.Eval("(typeof XMLHttpRequest) + '|' + (new XMLHttpRequest().readyState)");
        Assert.Equal("function|0", result.ToString());
    }

    [Fact]
    public void Relocated_MessageChannel_And_GetComputedStyle_Still_Install()
    {
        const string html = "<!DOCTYPE html><html><body></body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");

        // These two globals historically lived inside the fetch registration and were relocated to the
        // window-globals site when fetch became its own module; verify both still install and work.
        var result = context.Eval("""
            (() => {
                var ch = new MessageChannel();
                var hasPorts = (ch.port1 !== undefined) && (ch.port2 !== undefined);
                var cs = window.getComputedStyle(document.body);
                var hasComputed = (typeof cs === 'object') && (typeof cs.getPropertyValue === 'function');
                return hasPorts + '|' + hasComputed;
            })()
            """);

        Assert.Equal("true|true", result.ToString());
    }
}
