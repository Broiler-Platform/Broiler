using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// CSS Font Loading (css-font-loading-3) — <c>FontFace</c> and the <c>document.fonts</c>
/// <c>FontFaceSet</c>.
/// <para>
/// The API did not exist, so <c>document.fonts</c> was undefined and <c>document.fonts.load(…)</c>
/// a TypeError. That does not stop at the font code asking for it: on google.com the very *first*
/// inline script is a font preloader whose entire body is one <c>document.fonts.load</c> loop, so
/// the script died on its first statement.
/// </para>
/// </summary>
/// <remarks>
/// What this models: Broiler resolves fonts synchronously against what it already has when it lays
/// text out, so from a page's side there is never a load in flight — <c>status</c> is "loaded",
/// <c>ready</c> is already resolved, <c>check()</c> is true, and <c>load()</c> resolves. The failure
/// mode worth avoiding is a promise that never settles, which strands any page that waits behind
/// <c>document.fonts.ready</c> before rendering.
/// </remarks>
public sealed class FontLoadingTests
{
    private static (JSContext Context, DomBridge Bridge) Attach()
    {
        var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, "<!doctype html><html><body></body></html>", "https://example.com/");
        return (context, bridge);
    }

    private static string Eval(JSContext context, string expression) =>
        context.Eval($"String({expression})").ToString();

    /// <summary>Both names exist. Shipping the set without the constructor is the shape that let
    /// the <c>AbortSignal</c> gap hide for so long — <c>fonts.add(new FontFace(…))</c> needs both.</summary>
    [Fact(Timeout = 600000)]
    public void TheInterfacesExist()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("object", Eval(context, "typeof document.fonts"));
        Assert.Equal("function", Eval(context, "typeof FontFace"));
        Assert.Equal("function", Eval(context, "typeof FontFaceSet"));
        Assert.Equal("true", Eval(context, "document.fonts instanceof FontFaceSet"));
    }

    /// <summary>The reported failure, reduced: the call has to return a thenable, not throw.</summary>
    [Fact(Timeout = 600000)]
    public void LoadReturnsAPromise()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("function", Eval(context, "typeof document.fonts.load('400 10pt Google Sans').then"));
        Assert.Equal("function", Eval(context, "typeof document.fonts.load('400 10pt X').catch"));
    }

    /// <summary>
    /// google.com's first inline script, verbatim in shape: a nested loop calling
    /// <c>document.fonts.load(…).catch(…)</c> for each weight of each family. It has to run to
    /// completion, because everything later in that script depends on it not throwing.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void TheGoogleFontPreloaderShapeRunsToCompletion()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("ran", Eval(context,
            "(function(){var w=['Google Sans',[400,500,700]];(function(){" +
            "for(var a=0;a<w.length;a+=2)" +
            "for(var b=w[a],e=w[a+1],d=0,c=void 0;c=e[d];++d)" +
            "b==='Noto Color Emoji'" +
            "?document.fonts.load(c+' 10pt '+b,'\\ud83c\\uddfa').catch(function(){})" +
            ":document.fonts.load(c+' 10pt '+b).catch(function(){})" +
            "})();return 'ran';})()"));
    }

    /// <summary>Nothing is ever in flight, so the set reports itself loaded and ready.</summary>
    [Fact(Timeout = 600000)]
    public void TheSetIsLoadedAndReady()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("loaded", Eval(context, "document.fonts.status"));
        Assert.Equal("function", Eval(context, "typeof document.fonts.ready.then"));
        Assert.Equal("true", Eval(context, "document.fonts.check('400 10pt Google Sans')"));
    }

    /// <summary>
    /// <c>ready</c> hands back the same promise every time. A fresh one per access is a quiet way
    /// to strand a page that awaits it twice.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ReadyIsTheSamePromiseEachTime()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("true", Eval(context, "document.fonts.ready === document.fonts.ready"));
    }

    /// <summary>An absent or empty font shorthand is a SyntaxError, as the spec requires.</summary>
    [Fact(Timeout = 600000)]
    public void AnEmptyFontShorthandIsASyntaxError()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("SyntaxError", Eval(context,
            "(function(){ try { document.fonts.check(''); return 'no throw'; } catch (e) { return e.name; } })()"));
        Assert.Equal("SyntaxError", Eval(context,
            "(function(){ try { document.fonts.check(); return 'no throw'; } catch (e) { return e.name; } })()"));
    }

    /// <summary>
    /// <c>load()</c> reports a bad shorthand by returning a rejected promise, not by throwing where
    /// it was called — the difference that matters to a caller which only attached a
    /// <c>.catch()</c>. (Settlement itself is delivered on a microtask, so this asserts the
    /// synchronous contract; that the handler eventually runs is ordinary promise behaviour.)
    /// </summary>
    [Fact(Timeout = 600000)]
    public void LoadRejectsAnEmptyFontShorthandRatherThanThrowing()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("returned a thenable", Eval(context,
            "(function(){ try { var p = document.fonts.load('');" +
            " return typeof p.then === 'function' && typeof p.catch === 'function'" +
            " ? 'returned a thenable' : 'returned ' + typeof p; }" +
            " catch (e) { return 'threw synchronously'; } })()"));
    }

    /// <summary>The set is Set-like: add/has/delete/clear/size, forEach, and iteration.</summary>
    [Fact(Timeout = 600000)]
    public void TheSetIsSetLike()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("0", Eval(context, "document.fonts.size"));
        Assert.Equal("1,true", Eval(context,
            "(function(){ var f = new FontFace('X', 'url(x.woff2)'); document.fonts.add(f);" +
            " return document.fonts.size + ',' + document.fonts.has(f); })()"));
        Assert.Equal("true,0", Eval(context,
            "(function(){ var f = document.fonts.values().next().value;" +
            " return document.fonts.delete(f) + ',' + document.fonts.size; })()"));
    }

    /// <summary>Iteration and forEach see what was added, and clear() empties the set.</summary>
    [Fact(Timeout = 600000)]
    public void TheSetIteratesAndClears()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("2,2", Eval(context,
            "(function(){ document.fonts.add(new FontFace('A', 'url(a)'));" +
            " document.fonts.add(new FontFace('B', 'url(b)'));" +
            " var viaForEach = 0; document.fonts.forEach(function(){ viaForEach++; });" +
            " var viaIteration = 0; for (var f of document.fonts) viaIteration++;" +
            " return viaForEach + ',' + viaIteration; })()"));
        Assert.Equal("0", Eval(context,
            "(function(){ document.fonts.add(new FontFace('A', 'url(a)')); document.fonts.clear();" +
            " return document.fonts.size; })()"));
    }

    /// <summary>
    /// A FontFace from a url() source is unloaded until asked; one built from binary data is loaded
    /// on construction, which is the spec's split between the two constructor forms.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AFontFaceLoadsOnDemandUnlessBuiltFromBinaryData()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("unloaded", Eval(context, "new FontFace('X', 'url(x.woff2)').status"));
        Assert.Equal("loaded", Eval(context,
            "(function(){ var f = new FontFace('X', 'url(x.woff2)'); f.load(); return f.status; })()"));
        Assert.Equal("loaded", Eval(context, "new FontFace('X', new ArrayBuffer(4)).status"));
        Assert.Equal("function", Eval(context, "typeof new FontFace('X', 'url(x)').loaded.then"));
    }

    /// <summary>Descriptors passed to the constructor are carried on the face.</summary>
    [Fact(Timeout = 600000)]
    public void FontFaceCarriesItsDescriptors()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("X,700,italic", Eval(context,
            "(function(){ var f = new FontFace('X', 'url(x)', { weight: '700', style: 'italic' });" +
            " return f.family + ',' + f.weight + ',' + f.style; })()"));
        Assert.Equal("normal", Eval(context, "new FontFace('X', 'url(x)').weight"));
    }
}
