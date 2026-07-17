using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of
/// <c>document.write</c> / <c>document.writeln</c> (<see cref="DocumentWriteBinding"/>). The callbacks
/// — previously the bridge's <c>JsRegistrationWrite036Core</c>/<c>JsRegistrationWriteln037Core</c> in
/// the shared JsFunctionCallbacks/Registration.cs grab-bag — are now co-located; the document root,
/// element list, current-script index and fragment parser are reached through the narrow
/// <see cref="IDocumentWriteHost"/> contract. The characterization builds its assertion markers at
/// runtime and appends them via <c>createElement</c> (never via <c>write</c>), so they appear only in
/// the rendered output and prove the written nodes were actually inserted and are queryable.
/// </summary>
public sealed class DocumentWriteBindingModuleTests
{
    [Fact]
    public void DocumentWrite_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(DocumentWriteBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);
        Assert.NotNull(moduleType.GetMethod("Write", BindingFlags.Public | BindingFlags.Static));
        Assert.NotNull(moduleType.GetMethod("Writeln", BindingFlags.Public | BindingFlags.Static));

        Assert.False(typeof(IDocumentWriteHost).IsPublic);
        Assert.True(typeof(IDocumentWriteHost).IsAssignableFrom(typeof(Broiler.HtmlBridge.DomBridge)));
    }

    [Fact]
    public void Write_And_Writeln_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        Assert.Null(bridge.GetMethod("JsRegistrationWrite036Core", all));
        Assert.Null(bridge.GetMethod("JsRegistrationWriteln037Core", all));
    }

    [Fact]
    public void Write_And_Writeln_Insert_Queryable_Nodes_Through_The_Bridge()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""anchor"">anchor</div>
<script>
document.write('<p id=""wmark"">hi</p>');
document.writeln('<span id=""wlmark"">bye</span>');
var w = document.getElementById('wmark');
var wl = document.getElementById('wlmark');
var out = document.createElement('div');
out.id = 'result';
out.textContent =
  'write=' + (w !== null) +
  '|wtext=' + (w ? w.textContent : 'none') +
  '|writeln=' + (wl !== null) +
  '|wltext=' + (wl ? wl.textContent : 'none');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("write=true|wtext=hi|writeln=true|wltext=bye", result);
    }
}
