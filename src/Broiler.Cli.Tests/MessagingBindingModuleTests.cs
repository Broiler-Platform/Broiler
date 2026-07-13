using System.Reflection;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom.Features;
using Broiler.HtmlBridge.Dom.Runtime;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 tenth slice (P3.10): web messaging —
/// <c>window.postMessage</c>, <c>MessageChannel</c>/<c>MessagePort</c> and the generic
/// <c>EventTarget</c> dispatch shared with sub-windows — is now a co-located binding module
/// (<see cref="MessagingBinding"/>) that owns the Phase 2 <see cref="MessagePortRegistry"/> state
/// authority and reaches the document's browsing-context operations through the narrow
/// <see cref="IMessagingHost"/> contract. The characterizations exercise the extracted feature
/// end-to-end through the bridge with no layout dependency.
/// </summary>
public sealed class MessagingBindingModuleTests
{
    [Fact]
    public void Messaging_Feature_Module_Is_Co_Located_And_Internal()
    {
        var moduleType = typeof(MessagingBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);
        Assert.False(typeof(IMessagingHost).IsPublic);
    }

    [Fact]
    public void DomBridge_Consumes_Messaging_Through_The_Host_Contract()
    {
        Assert.True(typeof(IMessagingHost).IsAssignableFrom(typeof(DomBridge)));
        Assert.Contains(
            typeof(DomBridge).GetFields(BindingFlags.NonPublic | BindingFlags.Instance),
            static field => field.FieldType == typeof(MessagingBinding));
    }

    [Fact]
    public void MessagingBinding_Owns_The_MessagePortRegistry_State_Authority()
    {
        // The module owns the port registry; the bridge no longer holds one directly.
        Assert.Contains(
            typeof(MessagingBinding).GetFields(BindingFlags.NonPublic | BindingFlags.Instance),
            static field => field.FieldType == typeof(MessagePortRegistry));
        Assert.DoesNotContain(
            typeof(DomBridge).GetFields(BindingFlags.NonPublic | BindingFlags.Instance),
            static field => field.FieldType == typeof(MessagePortRegistry));
    }

    [Fact]
    public void MessageChannel_Ports_Round_Trip_Through_The_Module()
    {
        const string html = "<!DOCTYPE html><html><body><div id=\"result\"></div></body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");
        bridge.FireWindowLoadEvent();

        context.Eval("""
            (() => {
                var channel = new MessageChannel();
                channel.port1.onmessage = function(event) {
                    document.getElementById('result').setAttribute('data-got', event.data);
                };
                channel.port2.postMessage('ping');
            })();
            """);

        bridge.FlushTimers();

        var result = context.Eval("document.getElementById('result').getAttribute('data-got')");
        Assert.Equal("ping", result.ToString());
    }

    [Fact]
    public void MessagePort_Queues_Messages_Until_Onmessage_Is_Assigned()
    {
        const string html = "<!DOCTYPE html><html><body><div id=\"result\"></div></body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");
        bridge.FireWindowLoadEvent();

        // Post before the receiving port has a handler — the message must queue and be delivered
        // once onmessage is assigned (implicit start), not be dropped.
        context.Eval("""
            (() => {
                window._channel = new MessageChannel();
                window._channel.port2.postMessage('queued');
            })();
            """);
        bridge.FlushTimers();

        context.Eval("""
            window._channel.port1.onmessage = function(event) {
                document.getElementById('result').setAttribute('data-got', event.data);
            };
            """);
        bridge.FlushTimers();

        var result = context.Eval("document.getElementById('result').getAttribute('data-got')");
        Assert.Equal("queued", result.ToString());
    }

    [Fact]
    public void Window_PostMessage_Delivers_Asynchronously_Through_The_Module()
    {
        const string html = "<!DOCTYPE html><html><body><div id=\"result\"></div></body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");
        bridge.FireWindowLoadEvent();

        // window.postMessage to the top window itself: the module queues a frame action (async
        // delivery per the HTML messaging model), so nothing arrives before the drain, and the
        // 'message' listener sees the structured-cloned data and the correct origin afterwards.
        context.Eval("""
            (() => {
                window.addEventListener('message', function(event) {
                    document.getElementById('result').setAttribute('data-got', event.data + '@' + event.origin);
                });
                window.postMessage('hello', '*');
                document.getElementById('result').setAttribute('data-sync', document.getElementById('result').getAttribute('data-got'));
            })();
            """);

        var beforeDrain = context.Eval("document.getElementById('result').getAttribute('data-sync')");
        Assert.Equal("null", beforeDrain.ToString());

        bridge.FlushTimers();

        var result = context.Eval("document.getElementById('result').getAttribute('data-got')");
        Assert.Equal("hello@https://example.com", result.ToString());
    }
}
