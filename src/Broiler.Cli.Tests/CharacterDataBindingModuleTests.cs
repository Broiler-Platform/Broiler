using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the DOM
/// <c>CharacterData</c> interface (<see cref="CharacterDataBinding"/>) — <c>data</c>, <c>length</c>,
/// <c>splitText</c>, <c>substringData</c>/<c>appendData</c>/<c>deleteData</c>/<c>insertData</c>/
/// <c>replaceData</c> — the first slice off the 1599-line JsFunctionCallbacks/JsObjects.cs member file.
/// The callbacks — previously the bridge's <c>JsJsObjectsGetData045Core</c>..<c>ReplaceData053Core</c>
/// — are now co-located; the notifying setter, text-node factory, wrapper factory and wrapper-cache
/// invalidation are reached through the <see cref="ICharacterDataHost"/> contract. The characterization
/// drives the whole interface end-to-end through the bridge.
/// </summary>
public sealed class CharacterDataBindingModuleTests
{
    [Fact]
    public void CharacterData_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(CharacterDataBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);

        Assert.False(typeof(ICharacterDataHost).IsPublic);
        Assert.True(typeof(ICharacterDataHost).IsAssignableFrom(typeof(Broiler.HtmlBridge.DomBridge)));
    }

    [Fact]
    public void CharacterData_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var name in new[]
                 {
                     "JsJsObjectsGetData045Core", "JsJsObjectsSetData046Core", "JsJsObjectsGetLength047Core",
                     "JsJsObjectsSplitText048Core", "JsJsObjectsSubstringData049Core", "JsJsObjectsAppendData050Core",
                     "JsJsObjectsDeleteData051Core", "JsJsObjectsInsertData052Core", "JsJsObjectsReplaceData053Core",
                 })
        {
            Assert.Null(bridge.GetMethod(name, all));
        }
    }

    [Fact]
    public void CharacterData_Interface_Flows_Through_The_Bridge()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<script>
var el = document.createElement('div');
document.body.appendChild(el);

var t = document.createTextNode('Hello World');
el.appendChild(t);
var data0 = t.data;              // 'Hello World'
var len0 = t.length;             // 11
t.appendData('!');               // 'Hello World!'
t.insertData(0, '>> ');          // '>> Hello World!'
var sub = t.substringData(3, 5); // 'Hello'
t.deleteData(0, 3);              // 'Hello World!'
t.replaceData(0, 5, 'Howdy');    // 'Howdy World!'
var dataFinal = t.data;

var t2 = document.createTextNode('AAABBB');
el.appendChild(t2);
var split = t2.splitText(3);     // t2 -> 'AAA', split -> 'BBB'

var out = document.createElement('div');
out.id = 'result';
out.textContent =
  'data0=' + data0 + '|len0=' + len0 + '|sub=' + sub +
  '|final=' + dataFinal + '|split=' + split.data + '|t2=' + t2.data;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">data0=Hello World|len0=11|sub=Hello|final=Howdy World!|split=BBB|t2=AAA<", result);
    }
}
