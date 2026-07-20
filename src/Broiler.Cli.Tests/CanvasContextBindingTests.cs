using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 6 (P6.1) internalization of the Canvas 2D
/// context. The <c>canvas.getContext("2d")</c> binding formerly recorded every drawing call into a
/// <c>CanvasDrawCommand</c> list in the standalone <c>Broiler.HtmlBridge.Rendering</c> project — a list
/// no renderer ever read. That unused command storage was removed and the context internalized into the
/// Canvas binding, keeping only the script-observable state (current styles + the save/restore stack).
/// These tests pin that observable behaviour: the context exists, style properties round-trip,
/// save/restore is a real state stack, and the (now no-op) drawing methods stay callable without throwing.
/// </summary>
public sealed class CanvasContextBindingTests
{
    private static string Eval(string script)
    {
        const string html = "<!DOCTYPE html><html><body><canvas id='c' width='120' height='80'></canvas></body></html>";
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///canvas.html");
        return context.Eval(script).ToString();
    }

    [Fact]
    public void GetContext_2d_Returns_A_NonNull_Context_Object()
    {
        // The 2d context is a live object exposing the drawing-state properties and methods.
        Assert.Equal("object,true", Eval(
            "(function(){var ctx=document.getElementById('c').getContext('2d');" +
            "return (typeof ctx)+','+(ctx!==null&&typeof ctx.fillRect==='function');})()"));
    }

    [Fact]
    public void GetContext_Non2d_And_NonCanvas_Return_Null()
    {
        Assert.Equal("true", Eval(
            "(function(){var c=document.getElementById('c');" +
            "return (c.getContext('webgl')===null)+'';})()"));
    }

    [Fact]
    public void FillStyle_And_StrokeStyle_Round_Trip()
    {
        Assert.Equal("#ff0000|#00ff00", Eval(
            "(function(){var ctx=document.getElementById('c').getContext('2d');" +
            "ctx.fillStyle='#ff0000';ctx.strokeStyle='#00ff00';" +
            "return ctx.fillStyle+'|'+ctx.strokeStyle;})()"));
    }

    [Fact]
    public void Save_Restore_Is_A_State_Stack()
    {
        // save() snapshots the style state; mutating then restore() reverts it — script-observable.
        Assert.Equal("#111111->#222222->#111111", Eval(
            "(function(){var ctx=document.getElementById('c').getContext('2d');" +
            "ctx.fillStyle='#111111';ctx.save();ctx.fillStyle='#222222';" +
            "var mid=ctx.fillStyle;ctx.restore();" +
            "return '#111111->'+mid+'->'+ctx.fillStyle;})()"));
    }

    [Fact]
    public void Drawing_Methods_Are_Callable_And_Do_Not_Throw()
    {
        // The drawing surface is a no-op on a headless canvas, but the JS API must stay callable.
        Assert.Equal("ok", Eval(
            "(function(){var ctx=document.getElementById('c').getContext('2d');" +
            "ctx.beginPath();ctx.moveTo(0,0);ctx.lineTo(10,10);ctx.arc(5,5,3,0,6.28);ctx.closePath();" +
            "ctx.fillRect(0,0,10,10);ctx.strokeRect(1,1,8,8);ctx.clearRect(0,0,5,5);" +
            "ctx.fill();ctx.stroke();ctx.fillText('hi',2,2);ctx.strokeText('hi',2,2);" +
            "return 'ok';})()"));
    }
}
