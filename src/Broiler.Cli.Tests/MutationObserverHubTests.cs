using Broiler.Dom;
using Broiler.HtmlBridge.Dom.Runtime;
using Broiler.JavaScript.Runtime;

namespace Broiler.Cli.Tests;

/// <summary>
/// Direct unit tests for the P2.5 observer authority (<see cref="MutationObserverHub"/>):
/// registration (with observe() replace semantics), disconnect, snapshotting and clear. End-to-end
/// delivery is covered by the existing MutationObserver bridge tests.
/// </summary>
public sealed class MutationObserverHubTests
{
    private static DomNode NewNode() => new DomDocument().CreateElement("div");
    private static DomMutationObserverOptions ChildListOptions() => new() { ChildList = true };

    [Fact]
    public void Register_Adds_An_Observer()
    {
        var hub = new MutationObserverHub();
        var observer = new JSObject();
        var target = NewNode();

        hub.Register(observer, target, ChildListOptions());

        Assert.Equal(1, hub.Count);
        var snapshot = hub.Snapshot();
        Assert.Single(snapshot);
        Assert.Same(observer, snapshot[0].Observer);
        Assert.Same(target, snapshot[0].Target);
    }

    [Fact]
    public void Re_Observe_Same_Observer_And_Target_Replaces_Options()
    {
        var hub = new MutationObserverHub();
        var observer = new JSObject();
        var target = NewNode();
        var newOptions = ChildListOptions();

        hub.Register(observer, target, ChildListOptions());
        hub.Register(observer, target, newOptions);

        Assert.Equal(1, hub.Count); // not duplicated
        Assert.Same(newOptions, hub.Snapshot()[0].Options);
    }

    [Fact]
    public void Same_Observer_Different_Targets_Are_Distinct_Registrations()
    {
        var hub = new MutationObserverHub();
        var observer = new JSObject();

        hub.Register(observer, NewNode(), ChildListOptions());
        hub.Register(observer, NewNode(), ChildListOptions());

        Assert.Equal(2, hub.Count);
    }

    [Fact]
    public void Unregister_Removes_All_Registrations_For_An_Observer()
    {
        var hub = new MutationObserverHub();
        var observer = new JSObject();
        hub.Register(observer, NewNode(), ChildListOptions());
        hub.Register(observer, NewNode(), ChildListOptions());

        hub.Unregister(observer);

        Assert.Equal(0, hub.Count);
    }

    [Fact]
    public void Clear_Drops_Every_Observer()
    {
        var hub = new MutationObserverHub();
        hub.Register(new JSObject(), NewNode(), ChildListOptions());
        hub.Register(new JSObject(), NewNode(), ChildListOptions());

        hub.Clear();

        Assert.Equal(0, hub.Count);
        Assert.Empty(hub.Snapshot());
    }
}
