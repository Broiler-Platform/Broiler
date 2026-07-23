using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Storage;
using Broiler.Dom;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The HTML <c>canvas.getContext("2d")</c> binding and its 2D drawing context, co-located as an HtmlBridge
/// feature module (Phase 3): <c>getContext</c> resolves a <c>&lt;canvas&gt;</c> element to a
/// <see cref="CanvasRenderingContext2D"/>-backed JS object exposing the drawing-state properties
/// (<c>fillStyle</c>/<c>strokeStyle</c>/<c>lineWidth</c>/<c>font</c>/<c>globalAlpha</c>), the
/// <c>save</c>/<c>restore</c> state stack, and the (headless no-op) drawing methods. Was the bridge's
/// <c>JsJsObjectsGetContext134Core</c> + <c>BuildCanvas2DContext</c> + the scattered
/// <c>JsUtilities…034…058Core</c> canvas callbacks.
/// </summary>
/// <remarks>
/// This was the last element-member callback still living in the mixed <c>JsObjects.cs</c> file. It could not
/// be extracted earlier because the canvas context was tied to the standalone <c>Broiler.HtmlBridge.Rendering</c>
/// project's <c>CanvasCommandRecorder</c>; Phase 6 (P6.1) internalized the context into the Canvas binding and
/// Phase 8 F1 (P8.9) dissolved <c>Rendering</c> into this <c>Dom</c> assembly, which unblocks this move. The
/// binding is fully static — it navigates with <see cref="DomBridge.TryGetAttribute"/> and needs no host
/// contract. The <c>#if BROILER_CLI</c> guard preserves the original behaviour: the CLI build has no 2D
/// context, so <c>getContext("2d")</c> returns <c>null</c> there.
/// </remarks>
internal static class CanvasBinding
{
    /// <summary>Installs the <c>getContext</c> method on <paramref name="obj"/> (for <c>&lt;canvas&gt;</c>).</summary>
    public static void Install(JSObject obj, DomElement element)
    {
        obj.FastAddValue((KeyString)"getContext",
            new JSFunction((in a) => GetContext(element, in a), "getContext", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
    }

    // getContext(contextType) — returns the 2D context for a <canvas>, else null (only "2d" is supported).
    private static JSValue GetContext(DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSNull.Value;
        var contextType = a[0].ToString();
        if (!string.Equals(contextType, "2d", StringComparison.OrdinalIgnoreCase))
            return JSNull.Value;
        if (!string.Equals(element.TagName, "canvas", StringComparison.OrdinalIgnoreCase))
            return JSNull.Value;
#if BROILER_CLI
        return JSNull.Value; // Canvas 2D context not available in CLI mode
#else
        return BuildCanvas2DContext(element);
#endif
    }

#if !BROILER_CLI
    /// <summary>
    /// Builds a minimal Canvas 2D rendering context exposing basic drawing operations as defined in the HTML
    /// Canvas 2D Context specification. Drawing commands are no-ops on a headless canvas (nothing rasterises
    /// them); only the script-observable style/save-restore state round-trips.
    /// </summary>
    private static JSObject BuildCanvas2DContext(DomElement canvas)
    {
        var ctx = new JSObject();
        int width = 300, height = 150;
        if (DomBridge.TryGetAttribute(canvas, "width", out var w) && int.TryParse(w, out var pw)) width = pw;
        if (DomBridge.TryGetAttribute(canvas, "height", out var h) && int.TryParse(h, out var ph)) height = ph;

        var context2d = new CanvasRenderingContext2D(width, height);

        // fillStyle (get/set)
        ctx.FastAddProperty((KeyString)"fillStyle",
            new JSFunction((in _) => new JSString(context2d.FillStyle), "get fillStyle"),
            new JSFunction((in a) => SetFillStyle(context2d, in a), "set fillStyle"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // strokeStyle (get/set)
        ctx.FastAddProperty((KeyString)"strokeStyle",
            new JSFunction((in _) => new JSString(context2d.StrokeStyle), "get strokeStyle"),
            new JSFunction((in a) => SetStrokeStyle(context2d, in a), "set strokeStyle"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // lineWidth (get/set)
        ctx.FastAddProperty((KeyString)"lineWidth",
            new JSFunction((in _) => new JSNumber(context2d.LineWidth), "get lineWidth"),
            new JSFunction((in a) => SetLineWidth(context2d, in a), "set lineWidth"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // font (get/set)
        ctx.FastAddProperty((KeyString)"font",
            new JSFunction((in _) => new JSString(context2d.Font), "get font"),
            new JSFunction((in a) => SetFont(context2d, in a), "set font"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // globalAlpha (get/set)
        ctx.FastAddProperty((KeyString)"globalAlpha",
            new JSFunction((in _) => new JSNumber(context2d.GlobalAlpha), "get globalAlpha"),
            new JSFunction((in a) => SetGlobalAlpha(context2d, in a), "set globalAlpha"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // canvas property
        ctx.FastAddProperty((KeyString)"canvas",
            new JSFunction((in _) => new JSObject(), "get canvas"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // Drawing methods
        ctx.FastAddValue((KeyString)"fillRect", new JSFunction((in a) => FillRect(context2d, in a), "fillRect", 4), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"strokeRect", new JSFunction((in a) => StrokeRect(context2d, in a), "strokeRect", 4), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"clearRect", new JSFunction((in a) => ClearRect(context2d, in a), "clearRect", 4), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"beginPath", new JSFunction((in _) => BeginPath(context2d, in _), "beginPath", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"moveTo", new JSFunction((in a) => MoveTo(context2d, in a), "moveTo", 2), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"lineTo", new JSFunction((in a) => LineTo(context2d, in a), "lineTo", 2), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"arc", new JSFunction((in a) => Arc(context2d, in a), "arc", 5), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"closePath", new JSFunction((in _) => ClosePath(context2d, in _), "closePath", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"fill", new JSFunction((in _) => Fill(context2d, in _), "fill", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"stroke", new JSFunction((in _) => Stroke(context2d, in _), "stroke", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"fillText", new JSFunction((in a) => FillText(context2d, in a), "fillText", 3), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"strokeText", new JSFunction((in a) => StrokeText(context2d, in a), "strokeText", 3), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"save", new JSFunction((in _) => Save(context2d, in _), "save", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        ctx.FastAddValue((KeyString)"restore", new JSFunction((in _) => Restore(context2d, in _), "restore", 0), JSPropertyAttributes.EnumerableConfigurableValue);

        // measureText(text) — returns { width: ... }
        ctx.FastAddValue((KeyString)"measureText", new JSFunction((in a) => MeasureText(in a), "measureText", 1), JSPropertyAttributes.EnumerableConfigurableValue);

        return ctx;
    }

    private static JSValue SetFillStyle(CanvasRenderingContext2D context2d, in Arguments a)
    {
        if (a.Length > 0)
            context2d.FillStyle = a[0].ToString();
        return JSUndefined.Value;
    }

    private static JSValue SetStrokeStyle(CanvasRenderingContext2D context2d, in Arguments a)
    {
        if (a.Length > 0)
            context2d.StrokeStyle = a[0].ToString();
        return JSUndefined.Value;
    }

    private static JSValue SetLineWidth(CanvasRenderingContext2D context2d, in Arguments a)
    {
        if (a.Length > 0 && a[0] is JSNumber n)
            context2d.LineWidth = (float)n.DoubleValue;
        return JSUndefined.Value;
    }

    private static JSValue SetFont(CanvasRenderingContext2D context2d, in Arguments a)
    {
        if (a.Length > 0)
            context2d.Font = a[0].ToString();
        return JSUndefined.Value;
    }

    private static JSValue SetGlobalAlpha(CanvasRenderingContext2D context2d, in Arguments a)
    {
        if (a.Length > 0 && a[0] is JSNumber n)
            context2d.GlobalAlpha = (float)n.DoubleValue;
        return JSUndefined.Value;
    }

    private static JSValue FillRect(CanvasRenderingContext2D context2d, in Arguments a)
    {
        if (a.Length >= 4)
            context2d.FillRect((float)a[0].DoubleValue, (float)a[1].DoubleValue, (float)a[2].DoubleValue, (float)a[3].DoubleValue);
        return JSUndefined.Value;
    }

    private static JSValue StrokeRect(CanvasRenderingContext2D context2d, in Arguments a)
    {
        if (a.Length >= 4)
            context2d.StrokeRect((float)a[0].DoubleValue, (float)a[1].DoubleValue, (float)a[2].DoubleValue, (float)a[3].DoubleValue);
        return JSUndefined.Value;
    }

    private static JSValue ClearRect(CanvasRenderingContext2D context2d, in Arguments a)
    {
        if (a.Length >= 4)
            context2d.ClearRect((float)a[0].DoubleValue, (float)a[1].DoubleValue, (float)a[2].DoubleValue, (float)a[3].DoubleValue);
        return JSUndefined.Value;
    }

    private static JSValue BeginPath(CanvasRenderingContext2D context2d, in Arguments _)
    {
        context2d.BeginPath();
        return JSUndefined.Value;
    }

    private static JSValue MoveTo(CanvasRenderingContext2D context2d, in Arguments a)
    {
        if (a.Length >= 2)
            context2d.MoveTo((float)a[0].DoubleValue, (float)a[1].DoubleValue);
        return JSUndefined.Value;
    }

    private static JSValue LineTo(CanvasRenderingContext2D context2d, in Arguments a)
    {
        if (a.Length >= 2)
            context2d.LineTo((float)a[0].DoubleValue, (float)a[1].DoubleValue);
        return JSUndefined.Value;
    }

    private static JSValue Arc(CanvasRenderingContext2D context2d, in Arguments a)
    {
        if (a.Length >= 5)
            context2d.Arc((float)a[0].DoubleValue, (float)a[1].DoubleValue, (float)a[2].DoubleValue, (float)a[3].DoubleValue, (float)a[4].DoubleValue);
        return JSUndefined.Value;
    }

    private static JSValue ClosePath(CanvasRenderingContext2D context2d, in Arguments _)
    {
        context2d.ClosePath();
        return JSUndefined.Value;
    }

    private static JSValue Fill(CanvasRenderingContext2D context2d, in Arguments _)
    {
        context2d.Fill();
        return JSUndefined.Value;
    }

    private static JSValue Stroke(CanvasRenderingContext2D context2d, in Arguments _)
    {
        context2d.Stroke();
        return JSUndefined.Value;
    }

    private static JSValue FillText(CanvasRenderingContext2D context2d, in Arguments a)
    {
        if (a.Length >= 3)
            context2d.FillText(a[0].ToString(), (float)a[1].DoubleValue, (float)a[2].DoubleValue);
        return JSUndefined.Value;
    }

    private static JSValue StrokeText(CanvasRenderingContext2D context2d, in Arguments a)
    {
        if (a.Length >= 3)
            context2d.StrokeText(a[0].ToString(), (float)a[1].DoubleValue, (float)a[2].DoubleValue);
        return JSUndefined.Value;
    }

    private static JSValue Save(CanvasRenderingContext2D context2d, in Arguments _)
    {
        context2d.Save();
        return JSUndefined.Value;
    }

    private static JSValue Restore(CanvasRenderingContext2D context2d, in Arguments _)
    {
        context2d.Restore();
        return JSUndefined.Value;
    }

    private static JSValue MeasureText(in Arguments a)
    {
        var text = a.Length > 0 ? a[0].ToString() : string.Empty;
        var result = new JSObject();
        result.FastAddValue((KeyString)"width", new JSNumber(text.Length * 8.0), JSPropertyAttributes.EnumerableConfigurableValue);
        return result;
    }
#endif
}
