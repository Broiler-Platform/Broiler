using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Storage;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the Web
/// Storage <c>localStorage</c> object (<see cref="WebStorageBinding"/>) — <c>getItem</c>/<c>setItem</c>/
/// <c>removeItem</c>/<c>clear</c> over an in-memory string map. A fully self-contained slice (no bridge
/// state), so — like <c>ClassListBinding</c> — the module is an internal static class with <b>no host
/// contract</b>. Was the bridge's <c>BuildLocalStorageObject</c> plus <c>JsUtilitiesGetItem029Core</c>..
/// <c>Clear032Core</c>. The characterization drives the built object's callbacks directly.
/// </summary>
public sealed class WebStorageBindingModuleTests
{
    [Fact]
    public void WebStorage_Feature_Module_Is_Internal_And_Has_No_Host_Contract()
    {
        var moduleType = typeof(WebStorageBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);

        // No IWebStorageHost contract — the module is fully self-contained.
        Assert.Null(moduleType.Assembly.GetType("Broiler.HtmlBridge.Dom.Features.IWebStorageHost"));
    }

    [Fact]
    public void WebStorage_Callbacks_And_Builder_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var name in new[]
                 {
                     "BuildLocalStorageObject", "JsUtilitiesGetItem029Core", "JsUtilitiesSetItem030Core",
                     "JsUtilitiesRemoveItem031Core", "JsUtilitiesClear032Core",
                 })
        {
            Assert.Null(bridge.GetMethod(name, all));
        }
    }

    [Fact]
    public void LocalStorage_Object_Round_Trips_Through_Its_Callbacks()
    {
        var storage = WebStorageBinding.BuildLocalStorage();

        static JSFunction Fn(JSObject o, string name) => (JSFunction)o[(KeyString)name]!;
        string? GetItem(string key) =>
            Fn(storage, "getItem").InvokeFunction(new Arguments(storage, new JSString(key))) is JSString s ? s.ToString() : null;

        // setItem then getItem
        Fn(storage, "setItem").InvokeFunction(new Arguments(storage, new JSString("a"), new JSString("1")));
        Fn(storage, "setItem").InvokeFunction(new Arguments(storage, new JSString("b"), new JSString("2")));
        Assert.Equal("1", GetItem("a"));
        Assert.Equal("2", GetItem("b"));
        // bracket-notation mirror: setItem also writes the value onto the storage object itself
        Assert.Equal("2", (storage[(KeyString)"b"] as JSString)?.ToString());
        // missing key
        Assert.Null(GetItem("nope"));

        // removeItem only affects its key
        Fn(storage, "removeItem").InvokeFunction(new Arguments(storage, new JSString("a")));
        Assert.Null(GetItem("a"));
        Assert.Equal("2", GetItem("b"));

        // clear removes everything
        Fn(storage, "setItem").InvokeFunction(new Arguments(storage, new JSString("c"), new JSString("3")));
        Fn(storage, "clear").InvokeFunction(new Arguments(storage));
        Assert.Null(GetItem("b"));
        Assert.Null(GetItem("c"));
    }
}
