using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

public class WebMessagingTests
{
    [Fact]
    public void Window_PostMessage_From_Parent_To_Iframe_Uses_StructuredClone()
    {
        const string html = """
<!DOCTYPE html>
<html><body>
<iframe id="frame" srcdoc="<!DOCTYPE html><html><body></body></html>"></iframe>
</body></html>
""";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");
        bridge.FireWindowLoadEvent();

        context.Eval("""
            (() => {
                var frame = document.getElementById('frame');
                var payload = { nested: { value: 1 } };
                frame.contentWindow.addEventListener('message', function(event) {
                    frame.contentDocument.body.setAttribute('data-message', event.data.nested.value);
                    event.data.nested.value = 99;
                });

                frame.contentWindow.postMessage(payload, '*');
                document.body.setAttribute('data-after-send', payload.nested.value);
            })();
            """);

        bridge.FlushTimers();

        var result = context.Eval("""
            [
                document.body.getAttribute('data-after-send'),
                document.getElementById('frame').contentDocument.body.getAttribute('data-message')
            ].join('|')
            """);

        Assert.Equal("1|1", result.ToString());
    }

    [Fact]
    public void Window_PostMessage_From_Iframe_To_Parent_Exposes_Source_And_Origin()
    {
        const string html = """
<!DOCTYPE html>
<html><body>
<div id="result"></div>
<iframe id="frame" srcdoc="<!DOCTYPE html><html><body><script>parent.postMessage({ value: 7 }, '*');</script></body></html>"></iframe>
</body></html>
""";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");
        context.Eval("""
            window.addEventListener('message', function(event) {
              document.getElementById('result').textContent = [
                event.data.value,
                event.source !== null,
                event.source.location.href,
                event.origin
              ].join('|');
            });
            """);
        bridge.FireWindowLoadEvent();
        bridge.FlushTimers();

        var result = context.Eval("document.getElementById('result').textContent");
        Assert.Equal("7|true|about:srcdoc|https://example.com", result.ToString());
    }

    [Fact]
    public void Window_PostMessage_Options_Object_Transfers_MessagePort()
    {
        const string html = """
<!DOCTYPE html>
<html><body>
<iframe id="frame" srcdoc="<!DOCTYPE html><html><body></body></html>"></iframe>
</body></html>
""";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");
        bridge.FireWindowLoadEvent();

        context.Eval("""
            (() => {
                var frame = document.getElementById('frame');
                var channel = new MessageChannel();

                channel.port1.onmessage = function(event) {
                    document.body.setAttribute('data-parent', [
                        event.data,
                        event.source === null,
                        event.origin,
                        event.ports.length
                    ].join('|'));
                };

                frame.contentWindow.addEventListener('message', function(event) {
                    document.body.setAttribute('data-window', [
                        event.data,
                        event.origin,
                        event.ports.length
                    ].join('|'));
                    event.ports[0].postMessage('reply');
                });

                frame.contentWindow.postMessage('hello', {
                    targetOrigin: '*',
                    transfer: [channel.port2]
                });
            })();
            """);

        bridge.FlushTimers();
        bridge.FlushTimers();

        var result = context.Eval("""
            [
                document.getElementById('frame').contentDocument.body.getAttribute('data-window'),
                document.body.getAttribute('data-parent')
            ].join('||')
            """);

        Assert.Equal("hello|https://example.com|1||reply|true||0", result.ToString());
    }

    [Fact]
    public void Window_PostMessage_Options_Object_TargetOrigin_Matches_NonDefault_Port()
    {
        const string html = """
<!DOCTYPE html>
<html><body>
<iframe id="frame" srcdoc="<!DOCTYPE html><html><body></body></html>"></iframe>
</body></html>
""";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com:8443/index.html");
        bridge.FireWindowLoadEvent();

        context.Eval("""
            (() => {
                var frame = document.getElementById('frame');
                frame.contentWindow.addEventListener('message', function(event) {
                    document.body.setAttribute('data-result', [
                        event.data,
                        event.origin
                    ].join('|'));
                });

                frame.contentWindow.postMessage('port-check', {
                    targetOrigin: 'https://example.com:8443'
                });
            })();
            """);

        bridge.FlushTimers();

        var result = context.Eval("document.getElementById('frame').contentDocument.body.getAttribute('data-result')");
        Assert.Equal("port-check|https://example.com:8443", result.ToString());
    }

    [Fact]
    public void Window_PostMessage_Throws_DataCloneError_For_Invalid_Transferred_Value()
    {
        const string html = """
<!DOCTYPE html>
<html><body>
<iframe id="frame" srcdoc="<!DOCTYPE html><html><body></body></html>"></iframe>
</body></html>
""";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");
        bridge.FireWindowLoadEvent();

        var result = context.Eval("""
            (() => {
                try {
                    document.getElementById('frame').contentWindow.postMessage('hello', {
                        targetOrigin: '*',
                        transfer: [{}]
                    });
                    return 'no-throw';
                } catch (e) {
                    return [
                        e instanceof DOMException,
                        e.name,
                        e.code
                    ].join('|');
                }
            })();
            """);

        Assert.Equal("true|DataCloneError|25", result.ToString());
    }

    [Fact]
    public void Window_PostMessage_Throws_DataCloneError_For_Duplicate_Transferred_Port()
    {
        const string html = """
<!DOCTYPE html>
<html><body>
<iframe id="frame" srcdoc="<!DOCTYPE html><html><body></body></html>"></iframe>
</body></html>
""";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");
        bridge.FireWindowLoadEvent();

        var result = context.Eval("""
            (() => {
                var channel = new MessageChannel();
                try {
                    document.getElementById('frame').contentWindow.postMessage('hello', {
                        targetOrigin: '*',
                        transfer: [channel.port1, channel.port1]
                    });
                    return 'no-throw';
                } catch (e) {
                    return [
                        e instanceof DOMException,
                        e.name,
                        e.code
                    ].join('|');
                }
            })();
            """);

        Assert.Equal("true|DataCloneError|25", result.ToString());
    }

    [Fact]
    public void Window_PostMessage_Throws_DataCloneError_For_Uncloneable_Payload()
    {
        const string html = """
<!DOCTYPE html>
<html><body>
<iframe id="frame" srcdoc="<!DOCTYPE html><html><body></body></html>"></iframe>
</body></html>
""";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");
        bridge.FireWindowLoadEvent();

        var result = context.Eval("""
            (() => {
                try {
                    document.getElementById('frame').contentWindow.postMessage(function nope() {}, '*');
                    return 'no-throw';
                } catch (e) {
                    return [
                        e instanceof DOMException,
                        e.name,
                        e.code
                    ].join('|');
                }
            })();
            """);

        Assert.Equal("true|DataCloneError|25", result.ToString());
    }

    [Fact]
    public void MessageChannel_PostMessage_Delivers_Cloned_Data()
    {
        const string html = """
<!DOCTYPE html>
<html><body><div id="result"></div></body></html>
""";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");

        context.Eval("""
            (() => {
                var channel = new MessageChannel();
                var payload = { nested: { value: 3 } };

                channel.port1.onmessage = function(event) {
                    document.getElementById('result').textContent = [
                        event.data.nested.value,
                        event.source === null,
                        event.ports.length
                    ].join('|');
                    event.data.nested.value = 42;
                };

                channel.port2.postMessage(payload);
                document.body.setAttribute('data-after-send', payload.nested.value);
            })();
            """);

        bridge.FlushTimers();

        var result = context.Eval("""
            [
                document.body.getAttribute('data-after-send'),
                document.getElementById('result').textContent
            ].join('|')
            """);

        Assert.Equal("3|3|true|0", result.ToString());
    }

    [Fact]
    public void MessageChannel_AddEventListener_Queues_Until_Start()
    {
        const string html = """
<!DOCTYPE html>
<html><body><div id="result"></div></body></html>
""";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");

        context.Eval("""
            (() => {
                var channel = new MessageChannel();
                window.__channel = channel;

                channel.port1.addEventListener('message', function(event) {
                    document.getElementById('result').textContent = [
                        event.data,
                        event.source === null,
                        event.ports.length
                    ].join('|');
                });

                channel.port2.postMessage('queued');
            })();
            """);

        bridge.FlushTimers();
        Assert.Equal(string.Empty, context.Eval("document.getElementById('result').textContent").ToString());

        context.Eval("window.__channel.port1.start();");
        bridge.FlushTimers();

        var result = context.Eval("document.getElementById('result').textContent");
        Assert.Equal("queued|true|0", result.ToString());
    }

    [Fact]
    public void MessageChannel_OnMessage_Assignment_AutoStarts_Queued_Port()
    {
        const string html = """
<!DOCTYPE html>
<html><body><div id="result"></div></body></html>
""";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");

        context.Eval("""
            (() => {
                var channel = new MessageChannel();
                window.__channel = channel;
                channel.port2.postMessage('auto-start');
            })();
            """);

        bridge.FlushTimers();
        Assert.Equal(string.Empty, context.Eval("document.getElementById('result').textContent").ToString());

        context.Eval("""
            window.__channel.port1.onmessage = function(event) {
                document.getElementById('result').textContent = [
                    event.data,
                    event.source === null,
                    event.ports.length
                ].join('|');
            };
            """);
        bridge.FlushTimers();

        var result = context.Eval("document.getElementById('result').textContent");
        Assert.Equal("auto-start|true|0", result.ToString());
    }

    [Fact]
    public void MessagePort_PostMessage_Options_Object_Transfers_MessagePort()
    {
        const string html = """
<!DOCTYPE html>
<html><body></body></html>
""";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");

        context.Eval("""
            (() => {
                var channel = new MessageChannel();
                var replyChannel = new MessageChannel();

                replyChannel.port1.onmessage = function(event) {
                    document.body.setAttribute('data-reply', [
                        event.data,
                        event.source === null,
                        event.ports.length
                    ].join('|'));
                };

                channel.port1.onmessage = function(event) {
                    document.body.setAttribute('data-main', [
                        event.data,
                        event.source === null,
                        event.ports.length
                    ].join('|'));
                    event.ports[0].postMessage('reply');
                };

                channel.port2.postMessage('hello', {
                    transfer: [replyChannel.port2]
                });
            })();
            """);

        bridge.FlushTimers();
        bridge.FlushTimers();

        var result = context.Eval("""
            [
                document.body.getAttribute('data-main'),
                document.body.getAttribute('data-reply')
            ].join('||')
            """);

        Assert.Equal("hello|true|1||reply|true|0", result.ToString());
    }

    [Fact]
    public void MessagePort_PostMessage_Throws_DataCloneError_For_Invalid_Transferred_Value()
    {
        const string html = """
<!DOCTYPE html>
<html><body></body></html>
""";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");

        var result = context.Eval("""
            (() => {
                var channel = new MessageChannel();
                try {
                    channel.port1.postMessage('hello', {
                        transfer: [{}]
                    });
                    return 'no-throw';
                } catch (e) {
                    return [
                        e instanceof DOMException,
                        e.name,
                        e.code
                    ].join('|');
                }
            })();
            """);

        Assert.Equal("true|DataCloneError|25", result.ToString());
    }

    [Fact]
    public void MessagePort_PostMessage_Throws_DataCloneError_For_Duplicate_Transferred_Port()
    {
        const string html = """
<!DOCTYPE html>
<html><body></body></html>
""";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");

        var result = context.Eval("""
            (() => {
                var channel = new MessageChannel();
                try {
                    channel.port1.postMessage('hello', {
                        transfer: [channel.port2, channel.port2]
                    });
                    return 'no-throw';
                } catch (e) {
                    return [
                        e instanceof DOMException,
                        e.name,
                        e.code
                    ].join('|');
                }
            })();
            """);

        Assert.Equal("true|DataCloneError|25", result.ToString());
    }
}
