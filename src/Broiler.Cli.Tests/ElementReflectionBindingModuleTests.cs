using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the reflected
/// content-attribute IDL accessors (<see cref="ElementReflectionBinding"/>) — the plain string reflectors
/// (<c>label.htmlFor</c>, <c>meta.httpEquiv</c>, <c>.type</c>, and the generic named string / dimension
/// setters) and the URL-typed getters (<c>&lt;object&gt;.data</c>, <c>&lt;a&gt;/&lt;area&gt;/&lt;base&gt;/
/// &lt;link&gt;.href</c>) that resolve their relative content attribute against the live page URL. The
/// content-attribute reads/writes use the bridge's neutral <c>internal static</c> helpers; only the page
/// URL comes through the one-member <see cref="IElementReflectionHost"/> contract. Was the bridge's
/// <c>JsElementInterfacesSetHtmlFor047Core</c>..<c>Callback063Core</c> (URL/reflection subset).
/// </summary>
public sealed class ElementReflectionBindingModuleTests
{
    [Fact(Timeout = 600000)]
    public void ElementReflection_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(ElementReflectionBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);

        Assert.False(typeof(IElementReflectionHost).IsPublic);
        Assert.True(typeof(IElementReflectionHost).IsAssignableFrom(typeof(Broiler.HtmlBridge.DomBridge)));
    }

    [Fact(Timeout = 600000)]
    public void Href_Reflection_Resolves_And_Round_Trips_Through_The_Bridge()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<script>
var a = document.createElement('a');
a.setAttribute('href', 'page.html');
document.body.appendChild(a);
var resolved = a.href;              // resolved against file:///dir/test.html
a.href = 'other/deep.html';        // set href content attribute
var afterSet = a.href;             // resolved again
var attr = a.getAttribute('href'); // raw content attribute

var out = document.createElement('div');
out.id = 'result';
out.textContent = 'resolved=' + resolved + '|afterSet=' + afterSet + '|attr=' + attr;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///dir/test.html");

        Assert.Contains(">resolved=file:///dir/page.html|afterSet=file:///dir/other/deep.html|attr=other/deep.html<", result);
    }

    // WPT issue #1497 problem 24 (dom/nodes/moveBefore/preserve-render-blocking-style): <link> had no
    // IDL reflectors at all, so the ordinary way to inject a stylesheet — createElement, set .rel and
    // .href, append — produced a bare <link> with no attributes. Nothing reached the cascade and the
    // page rendered unstyled. The serialized attributes are the substance: they are what the render
    // pass reads.
    [Fact(Timeout = 600000)]
    public void Link_Rel_And_Href_Set_Through_The_Idl_Reach_The_Content_Attributes()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<script>
var link = document.createElement('link');
link.rel = 'stylesheet';
link.href = 'sheet.css';
document.head.appendChild(link);
</script>
</head><body></body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///dir/test.html");

        Assert.Contains(@"<link rel=""stylesheet"" href=""sheet.css"">", result);
    }

    [Fact(Timeout = 600000)]
    public void Link_Href_Getter_Resolves_Against_The_Page_Url()
    {
        var html = @"<!DOCTYPE html>
<html><head></head><body>
<script>
var link = document.createElement('link');
link.rel = 'stylesheet';
link.href = 'deep/sheet.css';
var out = document.createElement('div');
out.textContent = 'idl=' + link.href + '|attr=' + link.getAttribute('href') + '|rel=' + link.rel;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///dir/test.html");

        // The IDL getter is a resolved URL; the content attribute keeps what was written.
        Assert.Contains(">idl=file:///dir/deep/sheet.css|attr=deep/sheet.css|rel=stylesheet<", result);
    }

    [Fact(Timeout = 600000)]
    public void Link_Reflects_The_Remaining_Plain_String_Idl_Attributes()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<script>
var link = document.createElement('link');
link.rel = 'preload';
link.as = 'style';
link.media = 'print';
link.hreflang = 'en';
link.integrity = 'sha384-abc';
link.referrerPolicy = 'no-referrer';
document.head.appendChild(link);
</script>
</head><body></body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///dir/test.html");

        foreach (var attribute in new[]
                 {
                     @"rel=""preload""", @"as=""style""", @"media=""print""", @"hreflang=""en""",
                     @"integrity=""sha384-abc""", @"referrerpolicy=""no-referrer""",
                 })
        {
            Assert.Contains(attribute, result);
        }
    }

    [Fact(Timeout = 600000)]
    public void Base_Href_Set_Through_The_Idl_Reaches_The_Content_Attribute()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<script>
var b = document.createElement('base');
b.href = 'sub/';
document.head.appendChild(b);
</script>
</head><body></body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///dir/test.html");

        Assert.Contains(@"<base href=""sub/"">", result);
    }
}
