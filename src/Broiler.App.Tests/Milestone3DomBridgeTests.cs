using Broiler.App.Rendering;

namespace Broiler.App.Tests;

[Trait("Category", "Unit")]
[Trait("Engine", "Broiler")]
public class Milestone3DomBridgeTests
{
    private readonly ScriptEngine _engine = new();

    [Fact]
    public void CustomEvent_Constructor_IsAvailable()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
            ["var e = new CustomEvent('test', { detail: 42 }); if (e.type !== 'test') throw new Error('wrong type');"],
            html);
        Assert.True(result);
    }

    [Fact]
    public void CustomEvent_Detail_IsAccessible()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
            ["var e = new CustomEvent('myevent', { detail: 'hello' }); if (e.detail !== 'hello') throw new Error('wrong detail');"],
            html);
        Assert.True(result);
    }

    [Fact]
    public void CustomEvent_Bubbles_DefaultsFalse()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
            ["var e = new CustomEvent('test'); if (e.bubbles !== false) throw new Error('expected bubbles=false');"],
            html);
        Assert.True(result);
    }

    [Fact]
    public void MutationObserver_Constructor_IsAvailable()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
            ["if (typeof MutationObserver !== 'function') throw new Error('MutationObserver not available');"],
            html);
        Assert.True(result);
    }

    [Fact]
    public void MutationObserver_Observe_DoesNotThrow()
    {
        var html = "<html><body><div id='test'></div></body></html>";
        var result = _engine.Execute(
            [
                @"var observer = new MutationObserver(function(mutations) {});
                  var target = document.getElementById('test');
                  observer.observe(target, { childList: true });"
            ],
            html);
        Assert.True(result);
    }

    [Fact]
    public void MutationObserver_Disconnect_DoesNotThrow()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
            [
                @"var observer = new MutationObserver(function(mutations) {});
                  observer.disconnect();"
            ],
            html);
        Assert.True(result);
    }

    [Fact]
    public void MutationObserver_TakeRecords_ReturnsArray()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
            [
                @"var observer = new MutationObserver(function(mutations) {});
                  var records = observer.takeRecords();
                  if (!Array.isArray(records)) throw new Error('expected array');"
            ],
            html);
        Assert.True(result);
    }

    [Fact]
    public void Canvas_GetContext2D_ReturnsContext()
    {
        var html = "<html><body><canvas id='c' width='200' height='100'></canvas></body></html>";
        var result = _engine.Execute(
            [
                @"var canvas = document.getElementById('c');
                  var ctx = canvas.getContext('2d');
                  if (ctx === null || ctx === undefined) throw new Error('ctx is null');"
            ],
            html);
        Assert.True(result);
    }

    [Fact]
    public void Canvas_FillRect_DoesNotThrow()
    {
        var html = "<html><body><canvas id='c'></canvas></body></html>";
        var result = _engine.Execute(
            [
                @"var ctx = document.getElementById('c').getContext('2d');
                  ctx.fillRect(0, 0, 100, 100);"
            ],
            html);
        Assert.True(result);
    }

    [Fact]
    public void Canvas_FillStyle_IsSettable()
    {
        var html = "<html><body><canvas id='c'></canvas></body></html>";
        var result = _engine.Execute(
            [
                @"var ctx = document.getElementById('c').getContext('2d');
                  ctx.fillStyle = 'red';
                  if (ctx.fillStyle !== 'red') throw new Error('fillStyle not set');"
            ],
            html);
        Assert.True(result);
    }

    [Fact]
    public void Canvas_MeasureText_ReturnsWidth()
    {
        var html = "<html><body><canvas id='c'></canvas></body></html>";
        var result = _engine.Execute(
            [
                @"var ctx = document.getElementById('c').getContext('2d');
                  var m = ctx.measureText('Hello');
                  if (!(m.width > 0)) throw new Error('expected width > 0');"
            ],
            html);
        Assert.True(result);
    }

    [Fact]
    public void Canvas_GetContext_NonCanvas_ReturnsNull()
    {
        var html = "<html><body><div id='d'></div></body></html>";
        var result = _engine.Execute(
            [
                @"var el = document.getElementById('d');
                  var ctx = el.getContext('2d');
                  if (ctx !== null) throw new Error('expected null for non-canvas');"
            ],
            html);
        Assert.True(result);
    }

    [Fact]
    public void Iframe_ContentWindow_IsAvailable()
    {
        var html = "<html><body><iframe id='f' src='about:blank'></iframe></body></html>";
        var result = _engine.Execute(
            [
                @"var iframe = document.getElementById('f');
                  var win = iframe.contentWindow;
                  if (win === null || win === undefined) throw new Error('contentWindow is null');"
            ],
            html);
        Assert.True(result);
    }

    [Fact]
    public void Iframe_ContentDocument_IsAvailable()
    {
        var html = "<html><body><iframe id='f'></iframe></body></html>";
        var result = _engine.Execute(
            [
                @"var iframe = document.getElementById('f');
                  var doc = iframe.contentDocument;
                  if (doc === null || doc === undefined) throw new Error('contentDocument is null');"
            ],
            html);
        Assert.True(result);
    }

    [Fact]
    public void DocumentCreateDocumentFragment_IsAvailable()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
            [
                @"var frag = document.createDocumentFragment();
                  if (frag === null || frag === undefined) throw new Error('fragment is null');"
            ],
            html);
        Assert.True(result);
    }
}
