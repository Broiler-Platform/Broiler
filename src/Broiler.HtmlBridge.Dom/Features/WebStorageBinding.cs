using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.Runtime;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The Web Storage <c>localStorage</c> object — <c>getItem</c>/<c>setItem</c>/<c>removeItem</c>/
/// <c>clear</c> over an in-memory string map — co-located as an HtmlBridge feature module (Phase 3).
/// A fully self-contained slice: the store is a plain <see cref="Dictionary{TKey,TValue}"/> and the
/// callbacks touch no bridge state, so — like <c>ClassListBinding</c> — it is an <b>internal static
/// class with no host contract</b>. Bracket-notation access (<c>localStorage["key"]</c>) naturally
/// falls through to JSObject property lookup, which is why <c>setItem</c>/<c>removeItem</c> also mirror
/// the value onto the storage object. Was the bridge's <c>BuildLocalStorageObject</c> plus
/// <c>JsUtilitiesGetItem029Core</c>..<c>Clear032Core</c>.
/// </summary>
internal static class WebStorageBinding
{
    public static JSObject BuildLocalStorage()
    {
        var storage = new JSObject();
        var store = new Dictionary<string, string>();

        storage.FastAddValue((KeyString)"getItem",
            new JSFunction((in a) => GetItem(store, in a), "getItem", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        storage.FastAddValue((KeyString)"setItem",
            new JSFunction((in a) => SetItem(storage, store, in a), "setItem", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        storage.FastAddValue((KeyString)"removeItem",
            new JSFunction((in a) => RemoveItem(storage, store, in a), "removeItem", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        storage.FastAddValue((KeyString)"clear",
            new JSFunction((in a) => Clear(storage, store, in a), "clear", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        return storage;
    }

    private static JSValue GetItem(Dictionary<string, string> store, in Arguments a)
    {
        if (a.Length == 0)
            return JSNull.Value;
        var key = a[0].ToString();
        return store.TryGetValue(key, out var val) ? new JSString(val) : JSNull.Value;
    }

    private static JSValue SetItem(JSObject storage, Dictionary<string, string> store, in Arguments a)
    {
        if (a.Length >= 2)
        {
            var key = a[0].ToString();
            var val = a[1].ToString();
            store[key] = val;
            storage[(KeyString)key] = new JSString(val);
        }

        return JSUndefined.Value;
    }

    private static JSValue RemoveItem(JSObject storage, Dictionary<string, string> store, in Arguments a)
    {
        if (a.Length > 0)
        {
            var key = a[0].ToString();
            store.Remove(key);
            storage.Delete((KeyString)key);
        }

        return JSUndefined.Value;
    }

    private static JSValue Clear(JSObject storage, Dictionary<string, string> store, in Arguments _)
    {
        foreach (var key in store.Keys.ToList())
            storage.Delete((KeyString)key);
        store.Clear();
        return JSUndefined.Value;
    }
}
