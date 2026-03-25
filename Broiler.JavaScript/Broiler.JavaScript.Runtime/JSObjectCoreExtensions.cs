using Broiler.JavaScript.Storage;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Broiler.JavaScript.Runtime;

/// <summary>
/// Extension methods that moved alongside JSObject from Core to Runtime.
/// These helpers have no dependency on Core-only types.
/// </summary>
internal static class JSObjectCoreExtensions
{
    // ── JSProperty.ToJSValue ────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static JSValue PropertyToJSValue(in JSProperty px)
    {
        var t = JSValue.BooleanTrue;
        var f = JSValue.BooleanFalse;
        JSObject obj;

        if (px.IsValue)
        {
            obj = JSObject.NewWithProperties()
                .AddProperty(KeyStrings.configurable, px.IsConfigurable ? t : f)
                .AddProperty(KeyStrings.enumerable, px.IsEnumerable ? t : f)
                .AddProperty(KeyStrings.writable, !px.IsReadOnly ? t : f)
                .AddProperty(KeyStrings.value, (JSValue)px.value);
        }
        else
        {
            obj = JSObject.NewWithProperties()
                .AddProperty(KeyStrings.configurable, px.IsConfigurable ? t : f)
                .AddProperty(KeyStrings.enumerable, px.IsEnumerable ? t : f)
                .AddProperty(KeyStrings.@get, (JSValue)px.get)
                .AddProperty(KeyStrings.@set, (JSValue)px.set);
        }

        return obj;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    private static JSObject AddProperty(this JSObject target, in KeyString key, JSValue value, JSPropertyAttributes attributes = JSPropertyAttributes.EnumerableConfigurableValue)
    {
        target.GetOwnProperties().Put(in key, value, attributes);
        return target;
    }

    // ── KeyString.ToJSValue ─────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static JSValue KeyStringToJSValue(KeyString ks) => JSValue.CreateStringWithKey(ks.ToString(), ks);

    // ── KeyStringCoreExtensions.GetJSString ─────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static JSValue GetJSString(uint id)
    {
        var name = KeyStrings.GetName(id);
        return JSValue.CreateStringWithKey(name.ToString(), name);
    }

    // ── JSValue.Call ────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static JSValue CallWith(JSValue fx, JSValue @this, JSValue arg0, JSValue arg1)
    {
        var a = new Arguments(@this, arg0, arg1);
        return fx.InvokeFunction(in a);
    }

    // ── JSValue.InvokeMethod ────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static JSValue InvokeMethodOn(JSValue @this, in KeyString name)
    {
        var fx = @this.GetMethod(name) ?? throw JSValue.NewTypeError($"Method {name} not found in {@this}");
        var a = new Arguments(@this);
        return fx(a);
    }

    // ── JSValue.GetAllEntries ───────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static IEnumerable<(JSValue Key, JSValue Value)> GetAllEntries(this JSValue value, bool showEnumerableOnly = true)
    {
        if (value is not JSObject @object)
            yield break;

        var elements = @object.GetElements();
        if (!elements.IsNull)
        {
            var len = elements.Length;
            for (uint Key = 0; Key < len; Key++)
            {
                var Value = elements[Key];
                if (showEnumerableOnly)
                {
                    if (!Value.IsEnumerable)
                        continue;
                }

                yield return (JSValue.CreateNumber(Key), value.GetValue(in Value));
            }
        }

        var en = @object.GetOwnProperties(false).GetEnumerator();
        while (en.MoveNext(out var p))
        {
            if (showEnumerableOnly)
            {
                if (!p.IsEnumerable)
                    continue;
            }

            yield return (GetJSString(p.key), value.GetValue(in p));
        }

        var @base = (value.prototypeChain as IJSPrototype)?.Object as JSObject;
        if (@base != value && @base != null)
        {
            foreach (var bp in @base.GetAllEntries(showEnumerableOnly))
                yield return bp;
        }
    }
}
