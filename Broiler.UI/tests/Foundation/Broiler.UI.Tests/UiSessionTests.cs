using Broiler.Graphics;
using Broiler.Input;
using Broiler.Input.Mouse;

namespace Broiler.UI.Tests;

public sealed class UiSessionTests
{
    [Fact]
    public void AddRoot_Attaches_Tree_And_Rejects_Duplicate_Parents_And_Cycles()
    {
        using UiSession session = CreateSession(out _);
        var root = new TestElement("root");
        var child = new TestElement("child");
        var secondParent = new TestElement("second");

        root.AddChild(child);
        session.AddRoot(root);

        Assert.Same(session, root.Session);
        Assert.Same(session, child.Session);
        Assert.Same(root, child.Parent);
        Assert.Equal(1, root.AttachedCount);
        Assert.Equal(1, child.AttachedCount);

        Assert.Throws<InvalidOperationException>(() => secondParent.AddChild(child));
        Assert.Throws<InvalidOperationException>(() => child.AddChild(root));
    }

    [Fact]
    public void Fake_Element_Can_Attach_Layout_Render_Receive_Input_And_Dispose_Deterministically()
    {
        using UiSession session = CreateSession(out RecordingUiHost host);
        var disposeOrder = new List<string>();
        var root = new TestElement("root", disposeOrder);
        var child = new TestElement("child", disposeOrder) { HandlesInput = true };

        root.AddChild(child);
        session.AddRoot(root);

        BRenderList renderList = session.RenderFrame();
        Assert.Equal(2, renderList.Count);
        Assert.Single(host.Presented);
        Assert.Equal(1, root.MeasureCount);
        Assert.Equal(1, child.MeasureCount);
        Assert.Equal(1, root.ArrangeCount);
        Assert.Equal(1, child.ArrangeCount);
        Assert.Equal(1, root.RenderCount);
        Assert.Equal(1, child.RenderCount);

        bool handled = session.DispatchInput(CreateMouseDown(4, 4));
        Assert.True(handled);
        Assert.Equal(1, child.InputCount);

        session.Dispose();
        Assert.Equal(["child", "root"], disposeOrder);
        Assert.True(root.IsDisposed);
        Assert.True(child.IsDisposed);
    }

    [Fact]
    public void Invalidation_Reaches_Host_And_Is_Cleared_After_Render()
    {
        using UiSession session = CreateSession(out RecordingUiHost host);
        var root = new TestElement("root");

        session.AddRoot(root);
        root.Invalidate(UiInvalidationKind.Render);

        Assert.Contains(host.Invalidations, invalidation => invalidation.Element == root && invalidation.Kind.HasFlag(UiInvalidationKind.Render));
        Assert.NotEmpty(session.Invalidations);

        session.RenderFrame();

        Assert.Empty(session.Invalidations);
    }

    [Fact]
    public void Factory_Set_Rejects_Duplicate_Contracts()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new UiFactorySet([new TestFactory(), new TestFactory()]));
    }

    private static UiSession CreateSession(out RecordingUiHost host)
    {
        host = new RecordingUiHost(new BSize(100, 50));
        return new UiSession(host, new InlineUiDispatcher(), new ManualUiClock());
    }

    private static UiInputEvent CreateMouseDown(double x, double y)
    {
        var header = new InputEventHeader(
            InputDeviceId.FromOpaqueValue("mouse:test"),
            new InputTimestamp(1, TimeSpan.TicksPerSecond, "test"),
            1);

        return UiInputEvent.FromMouseButton(new MouseButtonEvent(
            header,
            InputPoint.ClientDeviceIndependentPixels(x, y),
            MouseButtons.Left,
            MouseButton.Left,
            MouseButtonTransition.Down,
            InputEventSource.Synthetic));
    }

    private sealed class TestFactory : IUiElementFactory
    {
        public Type ContractType => typeof(TestElement);

        public UiElement Create(UiElementFactoryContext context) => new TestElement("factory");
    }
}

