using System.Reflection;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 seventh slice (P3.7): the dialog /
/// popover / details JS API (showModal/show/close/open/returnValue plus showPopover/hidePopover) is
/// now a co-located binding module (<see cref="DialogBinding"/>) that drives the open attribute and
/// the modal/popover/top-layer/return-value runtime state through the narrow
/// <see cref="IDialogHost"/> contract. The behavior characterizations exercise the JS-observable
/// effects end-to-end through the bridge (backdrop rendering is out of scope here).
/// </summary>
public sealed class DialogBindingModuleTests
{
    [Fact]
    public void Dialog_Feature_Module_Is_Co_Located_And_Internal()
    {
        var moduleType = typeof(DialogBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);
        Assert.False(typeof(IDialogHost).IsPublic);
    }

    [Fact]
    public void DomBridge_Consumes_Dialog_Through_The_Host_Contract()
    {
        Assert.True(typeof(IDialogHost).IsAssignableFrom(typeof(DomBridge)));
        Assert.Contains(
            typeof(DomBridge).GetFields(BindingFlags.NonPublic | BindingFlags.Instance),
            static field => field.FieldType == typeof(DialogBinding));
    }

    [Fact]
    public void ShowModal_Then_Close_Toggles_Open_And_Sets_ReturnValue_Through_The_Module()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<dialog id=""d""></dialog>
<script>
var d = document.getElementById('d');
d.showModal();
var openAfterShow = d.open + '/' + (d.getAttribute('open') !== null);
d.close('ok');
var openAfterClose = d.open + '/' + (d.getAttribute('open') !== null);
var out = document.createElement('div');
out.id = 'result';
out.textContent = 'show=' + openAfterShow + '|close=' + openAfterClose + '|rv=' + d.returnValue;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("show=true/true|close=false/false|rv=ok", result);
    }

    [Fact]
    public void Dialog_Open_Setter_Reflects_The_Attribute_Through_The_Module()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<dialog id=""d""></dialog>
<script>
var d = document.getElementById('d');
d.open = true;
var a = d.getAttribute('open') !== null;
d.open = false;
var b = d.getAttribute('open') !== null;
var out = document.createElement('div');
out.id = 'result';
out.textContent = 'a=' + a + '|b=' + b;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("a=true|b=false", result);
    }

    [Fact]
    public void Details_Open_Property_Toggles_The_Attribute_Through_The_Module()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<details id=""x""><summary>s</summary>body</details>
<script>
var x = document.getElementById('x');
var before = x.open;
x.open = true;
var out = document.createElement('div');
out.id = 'result';
out.textContent = 'before=' + before + '|after=' + (x.getAttribute('open') !== null);
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("before=false|after=true", result);
    }
}
