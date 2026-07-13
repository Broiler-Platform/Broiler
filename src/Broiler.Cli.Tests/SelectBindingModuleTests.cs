using System.Reflection;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 eighth slice (P3.8): HTMLSelectElement
/// / HTMLOptionElement (add/options/selectedIndex/size/value plus option.defaultSelected) is now a
/// co-located binding module (<see cref="SelectBinding"/>) whose form-control state is reached
/// through the narrow <see cref="ISelectHost"/> contract. The behavior characterizations exercise the
/// extracted select logic end-to-end through the bridge with no layout dependency.
/// </summary>
public sealed class SelectBindingModuleTests
{
    [Fact]
    public void Select_Feature_Module_Is_Co_Located_And_Internal()
    {
        var moduleType = typeof(SelectBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);
        Assert.False(typeof(ISelectHost).IsPublic);
    }

    [Fact]
    public void DomBridge_Consumes_Select_Through_The_Host_Contract()
    {
        Assert.True(typeof(ISelectHost).IsAssignableFrom(typeof(DomBridge)));
        Assert.Contains(
            typeof(DomBridge).GetFields(BindingFlags.NonPublic | BindingFlags.Instance),
            static field => field.FieldType == typeof(SelectBinding));
    }

    [Fact]
    public void Options_And_Default_Selected_Index_Through_The_Module()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<select id=""s"">
  <option value=""a"">A</option>
  <option value=""b"" selected>B</option>
  <option value=""c"">C</option>
</select>
<script>
var s = document.getElementById('s');
var out = document.createElement('div');
out.id = 'result';
out.textContent = 'count=' + s.options.length + '|index=' + s.selectedIndex + '|value=' + s.value;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("count=3|index=1|value=b", result);
    }

    [Fact]
    public void SelectedIndex_Setter_Updates_Value_Through_The_Module()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<select id=""s"">
  <option value=""a"">A</option>
  <option value=""b"">B</option>
  <option value=""c"">C</option>
</select>
<script>
var s = document.getElementById('s');
s.selectedIndex = 2;
var out = document.createElement('div');
out.id = 'result';
out.textContent = 'index=' + s.selectedIndex + '|value=' + s.value;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("index=2|value=c", result);
    }

    [Fact]
    public void Value_Setter_Selects_Matching_Option_Through_The_Module()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<select id=""s"">
  <option value=""a"">A</option>
  <option value=""b"">B</option>
</select>
<script>
var s = document.getElementById('s');
s.value = 'b';
var out = document.createElement('div');
out.id = 'result';
out.textContent = 'index=' + s.selectedIndex + '|value=' + s.value;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("index=1|value=b", result);
    }

    [Fact]
    public void Add_Inserts_An_Option_And_Size_Round_Trips_Through_The_Module()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<select id=""s""><option value=""a"">A</option></select>
<script>
var s = document.getElementById('s');
var o = document.createElement('option');
o.value = 'b';
s.add(o);
s.size = 4;
var out = document.createElement('div');
out.id = 'result';
out.textContent = 'count=' + s.options.length + '|size=' + s.size;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("count=2|size=4", result);
    }
}
