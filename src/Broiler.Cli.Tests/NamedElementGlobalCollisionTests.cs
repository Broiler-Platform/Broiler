using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Regression guards for the crash that gated several WPT tests (issue #1302,
/// signature "Initialize — Cannot assign to read only variable"): a document
/// whose script declares a global lexical <c>const</c>/<c>let</c>/<c>class</c>
/// with the same name as an element's <c>id</c> (e.g. the WPT
/// <c>&lt;img id="img"&gt;</c> + <c>const img = …</c> and
/// <c>&lt;iframe id="current"&gt;</c> + <c>const current = …</c> helpers) crashed
/// the whole render. <see cref="DomBridge.RegisterNamedElementGlobals"/> assigned
/// the element to the same-named global via the context indexer, which wrote
/// through the read-only lexical binding and threw. The "skip if already defined"
/// guard compared the engine's <c>JSBoolean</c> against C#'s "True" and so never
/// fired.
/// </summary>
public sealed class NamedElementGlobalCollisionTests
{
    [Fact]
    public void RegisterNamedElementGlobals_DoesNotThrow_WhenIdCollidesWithConst()
    {
        using var ctx = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(ctx, "<!doctype html><body><img id=img></body>", "file:///c.html");
        // The user's script runs first and publishes a read-only global lexical.
        ctx.Eval("const img = 42;");

        // Registering the same-named element must not crash (previously threw
        // "Cannot assign to read only variable").
        var ex = Record.Exception(() => bridge.RegisterNamedElementGlobals(ctx));
        Assert.Null(ex);

        // The user's const binding wins over named-element access.
        Assert.Equal("42", ctx.Eval("String(img)").ToString());
    }

    [Fact]
    public void RegisterNamedElementGlobals_DoesNotThrow_WhenIdCollidesWithClass()
    {
        using var ctx = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(ctx, "<!doctype html><body><div id=Widget></div></body>", "file:///c.html");
        ctx.Eval("class Widget {}");

        var ex = Record.Exception(() => bridge.RegisterNamedElementGlobals(ctx));
        Assert.Null(ex);
        Assert.Equal("function", ctx.Eval("typeof Widget").ToString());
    }

    [Fact]
    public void RegisterNamedElementGlobals_StillExposesUndeclaredIdAsGlobal()
    {
        using var ctx = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(ctx, "<!doctype html><body><div id=host></div></body>", "file:///c.html");

        // No user binding named `host` — named access on the window must still work.
        bridge.RegisterNamedElementGlobals(ctx);
        Assert.Equal("object", ctx.Eval("typeof host").ToString());
    }
}
