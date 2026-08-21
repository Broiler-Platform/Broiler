using Broiler.HtmlBridge.Dom.Features;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Storage;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the Web
/// Storage areas (<see cref="WebStorageBinding"/>) — <c>getItem</c>/<c>setItem</c>/<c>removeItem</c>/
/// <c>clear</c>/<c>key</c>/<c>length</c> over an in-memory string map. A fully self-contained slice
/// (no bridge state), so — like <c>ClassListBinding</c> — the module is an internal static class with
/// <b>no host contract</b>. Was the bridge's <c>BuildLocalStorageObject</c> plus
/// <c>JsUtilitiesGetItem029Core</c>..<c>Clear032Core</c>. The characterization drives the built
/// object's callbacks directly.
/// </summary>
public sealed class WebStorageBindingModuleTests
{
    private static JSFunction Fn(JSObject o, string name) => (JSFunction)o[(KeyString)name]!;

    private static string? GetItem(JSObject storage, string key) =>
        Fn(storage, "getItem").InvokeFunction(new Arguments(storage, new JSString(key))) is JSString s ? s.ToString() : null;

    private static void SetItem(JSObject storage, string key, string value) =>
        Fn(storage, "setItem").InvokeFunction(new Arguments(storage, new JSString(key), new JSString(value)));

    [Fact(Timeout = 600000)]
    public void WebStorage_Feature_Module_Is_Internal_And_Has_No_Host_Contract()
    {
        var moduleType = typeof(WebStorageBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);

        // No IWebStorageHost contract — the module is fully self-contained.
        Assert.Null(moduleType.Assembly.GetType("Broiler.HtmlBridge.Dom.Features.IWebStorageHost"));
    }

    [Fact(Timeout = 600000)]
    public void Storage_Object_Round_Trips_Through_Its_Callbacks()
    {
        var storage = WebStorageBinding.BuildStorage();

        // setItem then getItem
        SetItem(storage, "a", "1");
        SetItem(storage, "b", "2");
        Assert.Equal("1", GetItem(storage, "a"));
        Assert.Equal("2", GetItem(storage, "b"));
        // bracket-notation mirror: setItem also writes the value onto the storage object itself
        Assert.Equal("2", (storage[(KeyString)"b"] as JSString)?.ToString());
        // missing key
        Assert.Null(GetItem(storage, "nope"));

        // removeItem only affects its key
        Fn(storage, "removeItem").InvokeFunction(new Arguments(storage, new JSString("a")));
        Assert.Null(GetItem(storage, "a"));
        Assert.Equal("2", GetItem(storage, "b"));
        Assert.True((storage[(KeyString)"a"] ?? JSUndefined.Value).IsUndefined);

        // clear removes everything
        SetItem(storage, "c", "3");
        Fn(storage, "clear").InvokeFunction(new Arguments(storage));
        Assert.Null(GetItem(storage, "b"));
        Assert.Null(GetItem(storage, "c"));
    }

    [Fact(Timeout = 600000)]
    public void Length_And_Key_Enumerate_The_Area_In_Insertion_Order()
    {
        // How a page walks an area it did not write itself: `for (i = 0; i < s.length; i++) s.key(i)`.
        var storage = WebStorageBinding.BuildStorage();
        Assert.Equal(0d, (storage[(KeyString)"length"] as JSNumber)?.DoubleValue);

        SetItem(storage, "first", "1");
        SetItem(storage, "second", "2");
        SetItem(storage, "first", "1-again");   // an overwrite is not a second key

        Assert.Equal(2d, (storage[(KeyString)"length"] as JSNumber)?.DoubleValue);

        string? Key(double index) =>
            Fn(storage, "key").InvokeFunction(new Arguments(storage, new JSNumber(index))) is JSString s ? s.ToString() : null;

        Assert.Equal("first", Key(0));
        Assert.Equal("second", Key(1));
        // Past the end is null, not a throw — the loop guard above relies on it.
        Assert.Null(Key(2));
        Assert.Null(Key(-1));

        Fn(storage, "removeItem").InvokeFunction(new Arguments(storage, new JSString("first")));
        Assert.Equal(1d, (storage[(KeyString)"length"] as JSNumber)?.DoubleValue);
        Assert.Equal("second", Key(0));
    }

    [Fact(Timeout = 600000)]
    public void A_Directly_Assigned_Property_Is_Stored_As_An_Item()
    {
        // Storage is a legacy platform object with named property setters, so `s.foo = 1` and
        // `s.setItem('foo', '1')` are one item — including for length/key, which counted only what
        // came through setItem before.
        var storage = WebStorageBinding.BuildStorage();

        storage[(KeyString)"foo"] = new JSString("bar");

        Assert.Equal("bar", GetItem(storage, "foo"));
        Assert.Equal(1d, (storage[(KeyString)"length"] as JSNumber)?.DoubleValue);

        Fn(storage, "clear").InvokeFunction(new Arguments(storage));
        Assert.Null(GetItem(storage, "foo"));
        Assert.Equal(0d, (storage[(KeyString)"length"] as JSNumber)?.DoubleValue);
    }

    [Fact(Timeout = 600000)]
    public void An_Item_Named_Like_A_Method_Does_Not_Replace_The_Method()
    {
        // A browser survives this because its methods sit on Storage.prototype and the named
        // property merely shadows them; the bridge carries members directly, so mirroring here
        // would leave the area without the method the page is about to call.
        var storage = WebStorageBinding.BuildStorage();

        SetItem(storage, "clear", "not-a-function");

        Assert.Equal("not-a-function", GetItem(storage, "clear"));
        Assert.IsAssignableFrom<JSFunction>(storage[(KeyString)"clear"]);

        Fn(storage, "clear").InvokeFunction(new Arguments(storage));
        Assert.Null(GetItem(storage, "clear"));
    }

    [Fact(Timeout = 600000)]
    public void Each_Built_Area_Has_A_Store_Of_Its_Own()
    {
        // localStorage and sessionStorage are separate areas: one Build call per area.
        var local = WebStorageBinding.BuildStorage();
        var session = WebStorageBinding.BuildStorage();

        SetItem(local, "shared-key", "local-value");
        SetItem(session, "shared-key", "session-value");

        Assert.Equal("local-value", GetItem(local, "shared-key"));
        Assert.Equal("session-value", GetItem(session, "shared-key"));

        Fn(session, "clear").InvokeFunction(new Arguments(session));
        Assert.Equal("local-value", GetItem(local, "shared-key"));
    }
}
