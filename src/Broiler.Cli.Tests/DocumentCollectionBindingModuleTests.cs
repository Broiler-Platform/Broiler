using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the
/// <c>document</c> live-collection accessors (<see cref="DocumentCollectionBinding"/>):
/// <c>document.forms</c>, <c>document.images</c>, <c>document.links</c>, <c>document.scripts</c>,
/// <c>document.styleSheets</c>.
/// The callbacks — previously the bridge's <c>JsRegistrationGetForms050Core</c> etc. in the shared
/// JsFunctionCallbacks/Registration.cs grab-bag — are now co-located; the document root, element
/// list, wrapper factory, tree-order link collector and stylesheet-object builder are reached through
/// the <see cref="IDocumentCollectionHost"/> contract. The characterization exercises the collections
/// end-to-end through the bridge (including <c>forms</c> named access and the <c>links</c>
/// href-only rule).
/// </summary>
public sealed class DocumentCollectionBindingModuleTests
{
    [Fact]
    public void DocumentCollection_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(DocumentCollectionBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);

        Assert.False(typeof(IDocumentCollectionHost).IsPublic);
        Assert.True(typeof(IDocumentCollectionHost).IsAssignableFrom(typeof(Broiler.HtmlBridge.DomBridge)));
    }

    [Fact]
    public void Collections_Enumerate_Elements_Through_The_Bridge()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<form name=""loginForm""><input></form>
<form></form>
<img src=""a.png""><img src=""b.png""><img src=""c.png"">
<a href=""x"">link1</a>
<a href=""y"">link2</a>
<a>no-href</a>
<style>.x{color:red}</style>
<style>.y{color:blue}</style>
<script>
var forms = document.forms;
var images = document.images;
var links = document.links;
var sheets = document.styleSheets;
var out = document.createElement('div');
out.id = 'result';
out.textContent =
  'forms=' + forms.length +
  '|named=' + (forms.loginForm ? 'yes' : 'no') +
  '|images=' + images.length +
  '|links=' + links.length +
  '|sheets=' + sheets.length;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("forms=2|named=yes|images=3|links=2|sheets=2", result);
    }

    // document.scripts, in tree order and counting <script src> as well as inline ones.
    [Fact]
    public void Scripts_Enumerates_Every_Script_Element_In_Tree_Order()
    {
        var html = @"<!DOCTYPE html>
<html><head><script src=""head.js""></script></head><body>
<script src=""body.js""></script>
<script>
var s = document.scripts;
var out = document.createElement('div');
out.id = 'result';
out.textContent =
  'scripts=' + s.length +
  '|first=' + (s[0].getAttribute('src') || 'inline') +
  '|second=' + (s[1].getAttribute('src') || 'inline') +
  '|third=' + (s[2].getAttribute('src') || 'inline');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("scripts=3|first=head.js|second=body.js|third=inline", result);
    }

    // The reported failure, reduced to the line that produced it. Cloudflare's
    // email-decode.min.js ends with
    //
    //     var e = document.currentScript || document.scripts[document.scripts.length - 1];
    //     e.parentNode.removeChild(e);
    //
    // — how a classic script finds its own element in order to remove it, since during synchronous
    // execution it is the last script in the document. `document.scripts` was absent, so reading
    // `.length` off it threw `Cannot get property length of undefined` and took the whole script
    // down. `document.currentScript` is also unbound, so the `||` did not short-circuit past it.
    // Cloudflare injects that script into every page it serves with email obfuscation on, which is
    // how it reached a report about html5test.com.
    [Fact]
    public void AScriptCanFindAndRemoveItselfThroughTheScriptsCollection()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var outcome;
try {
    var e = document.currentScript || document.scripts[document.scripts.length - 1];
    e.parentNode.removeChild(e);
    outcome = 'removed';
} catch (err) {
    outcome = 'threw: ' + err.message;
}
document.getElementById('result').textContent = outcome;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("removed", result);
        Assert.DoesNotContain("threw:", result);
    }
}
