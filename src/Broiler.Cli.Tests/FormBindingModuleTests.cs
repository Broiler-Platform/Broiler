using System.Reflection;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 ninth slice (P3.9): HTMLFormElement
/// (elements/length/action) and constraint validation (checkValidity/reportValidity) are now a
/// co-located binding module (<see cref="FormBinding"/>). The form-controls collection's only bridge
/// coupling — wrapping a control as a JS object — goes through the narrow <see cref="IFormHost"/>
/// contract (replacing the old collection's DomBridge back-reference). The behavior characterizations
/// exercise the extracted interface end-to-end through the bridge with no layout dependency.
/// </summary>
public sealed class FormBindingModuleTests
{
    [Fact]
    public void Form_Feature_Module_Is_Co_Located_And_Internal()
    {
        var moduleType = typeof(FormBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);
        Assert.False(typeof(IFormHost).IsPublic);
    }

    [Fact]
    public void DomBridge_Consumes_Form_Through_The_Host_Contract()
    {
        Assert.True(typeof(IFormHost).IsAssignableFrom(typeof(DomBridge)));
        Assert.Contains(
            typeof(DomBridge).GetFields(BindingFlags.NonPublic | BindingFlags.Instance),
            static field => field.FieldType == typeof(FormBinding));
    }

    [Fact]
    public void Elements_Collection_Indexed_Named_And_Length_Through_The_Module()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<form id=""f"">
  <input name=""username"" value=""u"">
  <select name=""role""><option>admin</option></select>
  <textarea name=""bio""></textarea>
</form>
<script>
var f = document.getElementById('f');
var els = f.elements;
var byIndex = els[0].tagName.toLowerCase();
var byName = els['role'].tagName.toLowerCase();
var missing = (els['nope'] === null);
var out = document.createElement('div');
out.id = 'result';
out.textContent = 'len=' + els.length + '|formLen=' + f.length + '|i0=' + byIndex + '|named=' + byName + '|missing=' + missing;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("len=3|formLen=3|i0=input|named=select|missing=true", result);
    }

    [Fact]
    public void Action_Getter_And_Setter_Through_The_Module()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<form id=""f"" action=""/submit""></form>
<script>
var f = document.getElementById('f');
var before = f.action;
f.action = '/other';
var out = document.createElement('div');
out.id = 'result';
out.textContent = 'before=' + before + '|after=' + f.getAttribute('action');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("before=/submit|after=/other", result);
    }

    [Fact]
    public void CheckValidity_Reflects_Required_Controls_Through_The_Module()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<form id=""bad""><input required></form>
<form id=""good""><input required value=""x""></form>
<script>
var bad = document.getElementById('bad');
var good = document.getElementById('good');
var out = document.createElement('div');
out.id = 'result';
out.textContent = 'bad=' + bad.checkValidity() + '|good=' + good.checkValidity();
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("bad=false|good=true", result);
    }
}
