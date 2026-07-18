using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the
/// form-control IDL reflectors (<see cref="FormControlBinding"/>) — <c>value</c>, <c>checked</c>,
/// <c>type</c>, <c>name</c>, <c>disabled</c>, <c>hidden</c>, <c>tabIndex</c> and <c>required</c>,
/// registered on every element wrapper. The callbacks — previously the bridge's
/// <c>JsJsObjectsGetValue106Core</c>..<c>SetRequired121Core</c> — are now co-located; the input's dirty
/// IDL value/checked state, the <c>&lt;select&gt;</c> value resolution, the radio-group walk and
/// style-scope invalidation are reached through the <see cref="IFormControlHost"/> contract. The
/// characterizations drive each member end-to-end through the bridge.
/// </summary>
public sealed class FormControlBindingModuleTests
{
    [Fact]
    public void FormControl_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(FormControlBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);

        Assert.False(typeof(IFormControlHost).IsPublic);
        Assert.True(typeof(IFormControlHost).IsAssignableFrom(typeof(Broiler.HtmlBridge.DomBridge)));
    }

    [Fact]
    public void FormControl_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var name in new[]
                 {
                     "JsJsObjectsGetValue106Core", "JsJsObjectsSetValue107Core",
                     "JsJsObjectsGetChecked108Core", "JsJsObjectsSetChecked109Core",
                     "JsJsObjectsGetType110Core", "JsJsObjectsSetType111Core",
                     "JsJsObjectsGetName112Core", "JsJsObjectsSetName113Core",
                     "JsJsObjectsSetDisabled115Core", "JsJsObjectsSetHidden117Core",
                     "JsJsObjectsGetTabIndex118Core", "JsJsObjectsSetTabIndex119Core",
                     "JsJsObjectsSetRequired121Core",
                 })
        {
            Assert.Null(bridge.GetMethod(name, all));
        }
    }

    [Fact]
    public void Value_Type_Name_TabIndex_Reflect_Through_The_Bridge()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<input id=""i"" type=""TEXT"" name=""field"" value=""seed"">
<div id=""result""></div>
<script>
var i = document.getElementById('i');
var r = [];
r.push(i.value === 'seed');            // value falls back to the content attribute
i.value = 'typed';                     // IDL value (not reflected)
r.push(i.value === 'typed');
r.push(i.getAttribute('value') === 'seed');   // attribute unchanged by IDL write
r.push(i.type === 'text');             // getter lowercases
r.push(i.name === 'field');
i.name = 'renamed';
r.push(i.getAttribute('name') === 'renamed'); // name reflects
r.push(i.tabIndex === -1);             // default when absent
i.tabIndex = 3;
r.push(i.getAttribute('tabindex') === '3');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains(">true,true,true,true,true,true,true,true<", result);
    }

    [Fact]
    public void Boolean_Reflectors_Disabled_Hidden_Required_Round_Trip()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<input id=""i"" type=""text"">
<div id=""result""></div>
<script>
var i = document.getElementById('i');
var r = [];
r.push(i.disabled === false && i.hidden === false && i.required === false);
i.disabled = true; i.hidden = true; i.required = true;
r.push(i.hasAttribute('disabled') && i.hasAttribute('hidden') && i.hasAttribute('required'));
r.push(i.disabled === true && i.hidden === true && i.required === true);
i.disabled = false; i.hidden = false; i.required = false;
r.push(!i.hasAttribute('disabled') && !i.hasAttribute('hidden') && !i.hasAttribute('required'));
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains(">true,true,true,true<", result);
    }

    [Fact]
    public void Checked_Idl_And_Radio_Group_Mutual_Exclusion()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<form id=""f"">
  <input id=""a"" type=""radio"" name=""grp"">
  <input id=""b"" type=""radio"" name=""grp"">
</form>
<div id=""result""></div>
<script>
var a = document.getElementById('a'), b = document.getElementById('b');
var r = [];
r.push(a.checked === false);
a.checked = true;
r.push(a.checked === true);
b.checked = true;               // selecting b unchecks a (same group)
r.push(b.checked === true);
r.push(a.checked === false);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains(">true,true,true,true<", result);
    }

    [Fact]
    public void Select_Value_Resolves_Through_SelectBinding()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<select id=""s"">
  <option value=""one"">One</option>
  <option value=""two"" selected>Two</option>
</select>
<div id=""result""></div>
<script>
var s = document.getElementById('s');
var r = [];
r.push(s.value === 'two');       // selected option's value
s.value = 'one';                 // set routes through SelectBinding
r.push(s.value === 'one');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains(">true,true<", result);
    }
}
