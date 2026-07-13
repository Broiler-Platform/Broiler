using Broiler.HtmlBridge.Dom.Runtime;
using Broiler.JavaScript.Runtime;

namespace Broiler.Cli.Tests;

/// <summary>
/// Direct unit tests for the P2.6 message-port authority (<see cref="MessagePortRegistry"/>):
/// entanglement, close/start marks, and the queue-then-deliver flow. End-to-end MessageChannel
/// delivery is covered by the existing messaging bridge tests.
/// </summary>
public sealed class MessagePortRegistryTests
{
    [Fact]
    public void Link_Entangles_Both_Ends()
    {
        var reg = new MessagePortRegistry();
        var a = new JSObject();
        var b = new JSObject();

        reg.Link(a, b);

        Assert.True(reg.HasPeer(a));
        Assert.True(reg.HasPeer(b));
        Assert.True(reg.TryGetPeer(a, out var peerOfA));
        Assert.Same(b, peerOfA);
        Assert.True(reg.TryGetPeer(b, out var peerOfB));
        Assert.Same(a, peerOfB);
    }

    [Fact]
    public void Unlinked_Port_Has_No_Peer()
    {
        var reg = new MessagePortRegistry();
        Assert.False(reg.HasPeer(new JSObject()));
        Assert.False(reg.TryGetPeer(new JSObject(), out _));
    }

    [Fact]
    public void Close_Marks_Closed_And_Drops_Queued_Messages()
    {
        var reg = new MessagePortRegistry();
        var port = new JSObject();
        reg.Enqueue(port, new JSObject());

        reg.Close(port);

        Assert.True(reg.IsClosed(port));
        Assert.Null(reg.TakeQueued(port)); // queued messages dropped on close
    }

    [Fact]
    public void Start_Marks_Started()
    {
        var reg = new MessagePortRegistry();
        var port = new JSObject();

        Assert.False(reg.IsStarted(port));
        reg.Start(port);
        Assert.True(reg.IsStarted(port));
    }

    [Fact]
    public void Enqueue_Then_TakeQueued_Returns_Messages_In_Order_Then_Clears()
    {
        var reg = new MessagePortRegistry();
        var port = new JSObject();
        var m1 = new JSObject();
        var m2 = new JSObject();
        reg.Enqueue(port, m1);
        reg.Enqueue(port, m2);

        var taken = reg.TakeQueued(port);

        Assert.NotNull(taken);
        Assert.Equal(new[] { m1, m2 }, taken);
        Assert.Null(reg.TakeQueued(port)); // drained
    }

    [Fact]
    public void TakeQueued_Returns_Null_When_Empty() =>
        Assert.Null(new MessagePortRegistry().TakeQueued(new JSObject()));

    [Fact]
    public void Clear_Drops_All_Port_State()
    {
        var reg = new MessagePortRegistry();
        var a = new JSObject();
        var b = new JSObject();
        reg.Link(a, b);
        reg.Start(a);
        reg.Close(b);
        reg.Enqueue(a, new JSObject());

        reg.Clear();

        Assert.False(reg.HasPeer(a));
        Assert.False(reg.IsStarted(a));
        Assert.False(reg.IsClosed(b));
        Assert.Null(reg.TakeQueued(a));
    }
}
