using Broiler.Graphics;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.Input.Pen;
using Broiler.Input.Text;
using Broiler.Input.Touch;
using Broiler.UI;
using Broiler.UI.Standard;

namespace Broiler.UI.Standard.Tests;

public sealed class Phase2StandardServicesTests
{
    [Fact]
    public void Synthetic_Tree_Replays_Input_And_Emits_Deterministic_Render_List()
    {
        var host = new Phase2Host(new BSize(80, 40));
        using UiSession session = CreateSession(host, out _);
        var root = new Phase2Element("root", BColor.Blue);
        var child = new Phase2Element("child", BColor.Red) { HandlesInput = true };
        root.AddChild(child);
        session.AddRoot(root);

        BRenderList first = StandardRenderTraversal.Render(session);
        BRenderList second = StandardRenderTraversal.Render(session);

        Assert.Equal(RenderSignature(first), RenderSignature(second));

        var route = new StandardInputRoute(session);
        Assert.True(route.Dispatch(CreateMouseButton(10, 8)));
        Assert.True(route.Dispatch(CreateMouseMove(10, 8)));
        Assert.True(route.Dispatch(CreateTouch(10, 8)));
        Assert.True(route.Dispatch(CreatePen(10, 8)));

        session.SetFocus(child);
        Assert.True(route.Dispatch(CreateKey("Enter")));
        Assert.True(route.Dispatch(CreateKeyboardText("x")));
        Assert.True(route.Dispatch(CreateText("y")));
        Assert.True(route.Dispatch(CreateComposition("ime")));

        Assert.Contains(child.InputKinds, kind => kind == UiInputEventKind.PointerButton);
        Assert.Contains(child.InputKinds, kind => kind == UiInputEventKind.KeyboardKey);
        Assert.Contains(child.InputKinds, kind => kind == UiInputEventKind.TextComposition);
    }

    [Fact]
    public void Focus_Capture_Routing_Timing_And_Invalidation_Are_Isolated_And_Reentrant()
    {
        var host = new Phase2Host(new BSize(64, 32));
        using UiSession session = CreateSession(host, out ManualClock clock);
        var root = new InvalidatingRenderElement("root", BColor.Green);
        var child = new Phase2Element("child", BColor.Red) { HandlesInput = true };
        root.AddChild(child);
        session.AddRoot(root);

        var focus = new StandardFocusScope(session);
        Assert.True(focus.TryFocus(child));
        Assert.Same(child, focus.FocusedElement);

        session.CaptureInput(child);
        Assert.True(new StandardInputRoute(session).Dispatch(CreateMouseButton(500, 500)));
        Assert.Equal(UiInputEventKind.PointerButton, child.InputKinds.Last());
        session.ReleaseInputCapture(child);

        var dirty = new StandardDirtyRootScheduler();
        foreach (UiInvalidation invalidation in host.Invalidations)
            dirty.Record(invalidation);
        child.Invalidate(UiInvalidationKind.Render);
        dirty.Record(host.Invalidations.Last());

        Assert.True(dirty.HasDirtyRoots);
        Assert.Same(root, Assert.Single(dirty.ConsumeDirtyRoots()));
        Assert.False(dirty.HasDirtyRoots);

        StandardRenderTraversal.Render(session);
        Assert.NotEmpty(session.Invalidations);

        int animationTicks = 0;
        var scheduler = new StandardAnimationScheduler(session);
        using IDisposable registration = scheduler.Register(TimeSpan.FromMilliseconds(16), _ => animationTicks++);

        Assert.Equal(0, scheduler.Tick());
        clock.Advance(TimeSpan.FromMilliseconds(15));
        Assert.Equal(0, scheduler.Tick());
        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(1, scheduler.Tick());
        Assert.Equal(1, animationTicks);
    }

    [Fact]
    public void Theme_Semantics_Commands_And_HitTesting_Are_Reusable_Services()
    {
        var host = new Phase2Host(new BSize(50, 50));
        using UiSession session = CreateSession(host, out _);
        var root = new Phase2Element("root", BColor.Blue);
        var child = new Phase2Element("child", BColor.Red);
        root.AddChild(child);
        session.AddRoot(root);
        StandardRenderTraversal.Render(session);

        var theme = new StandardThemeResolver();
        var overrideTokens = new StandardThemeTokens(BColor.Black, BColor.White, BColor.Green, BColor.Red);
        theme.SetOverride(root, overrideTokens);
        Assert.Same(overrideTokens, theme.Resolve(child));

        StandardSemanticSnapshot semantics = StandardSemanticSnapshot.Capture(session);
        UiSemanticNode semanticRoot = Assert.Single(semantics.Roots);
        Assert.Equal("Phase2Element", semanticRoot.Name);
        Assert.Single(semanticRoot.Children);

        var hitTest = new StandardHitTestService(session);
        Assert.Same(child, hitTest.HitTest(new BPoint(2, 2)));

        int executed = 0;
        var commands = new StandardCommandDispatcher();
        commands.Add(new StandardCommand("submit", () => executed++));

        Assert.True(commands.TryExecute("submit"));
        Assert.Equal(1, executed);
        Assert.False(commands.TryExecute("missing"));
    }

    [Fact]
    public void Multiple_Sessions_Do_Not_Share_Focus_Capture_Render_Or_Theme_State()
    {
        var hostA = new Phase2Host(new BSize(20, 20));
        var hostB = new Phase2Host(new BSize(30, 30));
        using UiSession sessionA = CreateSession(hostA, out _);
        using UiSession sessionB = CreateSession(hostB, out _);
        var elementA = new Phase2Element("a", BColor.Red) { HandlesInput = true };
        var elementB = new Phase2Element("b", BColor.Blue) { HandlesInput = true };

        sessionA.AddRoot(elementA);
        sessionB.AddRoot(elementB);
        sessionA.SetFocus(elementA);
        sessionB.SetFocus(elementB);
        sessionA.CaptureInput(elementA);

        StandardRenderTraversal.Render(sessionA);
        StandardRenderTraversal.Render(sessionB);
        new StandardInputRoute(sessionA).Dispatch(CreateMouseButton(100, 100));

        Assert.Same(elementA, sessionA.FocusedElement);
        Assert.Same(elementB, sessionB.FocusedElement);
        Assert.Same(elementA, sessionA.CapturedElement);
        Assert.Null(sessionB.CapturedElement);
        Assert.Single(hostA.Presented);
        Assert.Single(hostB.Presented);
        Assert.Single(elementA.InputKinds);
        Assert.Empty(elementB.InputKinds);

        var themeA = new StandardThemeResolver();
        var themeB = new StandardThemeResolver();
        StandardThemeTokens custom = new(BColor.Black, BColor.White, BColor.Red, BColor.Green);
        themeA.SetOverride(elementA, custom);

        Assert.Same(custom, themeA.Resolve(elementA));
        Assert.NotSame(custom, themeB.Resolve(elementB));
    }

    [Fact]
    public void Legacy_Graphics_Adapter_Is_Isolated_And_Marked_For_Removal()
    {
#pragma warning disable CS0618
        var adapter = new StandardLegacyGraphicsInputAdapter();

        UiInputEvent input = adapter.FromPointerMove(new BPointerEventArgs(new BPoint(3, 4), BMouseButtons.Left));

        Assert.Equal(UiInputEventKind.PointerMove, input.Kind);
        Assert.Equal(new BPoint(3, 4), input.Position);
        Assert.True(typeof(StandardLegacyGraphicsInputAdapter).GetCustomAttributes(typeof(ObsoleteAttribute), inherit: false).Length == 1);
#pragma warning restore CS0618
    }

    private static UiSession CreateSession(Phase2Host host, out ManualClock clock)
    {
        clock = new ManualClock();
        return new StandardUiSessionBuilder()
            .WithDispatcher(new ImmediateUiDispatcher())
            .WithClock(clock)
            .Build(host);
    }

    private static string RenderSignature(BRenderList renderList) =>
        string.Join("|", renderList.Commands.Select(static command => command.ToString()));

    private static InputEventHeader Header(string id = "input", long sequence = 1) =>
        new(
            InputDeviceId.FromOpaqueValue(id),
            new InputTimestamp(sequence, TimeSpan.TicksPerSecond, "phase2"),
            sequence);

    private static MouseMoveEvent CreateMouseMove(double x, double y) =>
        new(Header("mouse", 2), InputPoint.ClientDeviceIndependentPixels(x, y), MouseButtons.Left, InputEventSource.Synthetic);

    private static MouseButtonEvent CreateMouseButton(double x, double y) =>
        new(Header("mouse", 1), InputPoint.ClientDeviceIndependentPixels(x, y), MouseButtons.Left, MouseButton.Left, MouseButtonTransition.Down, InputEventSource.Synthetic);

    private static TouchContactEvent CreateTouch(double x, double y) =>
        new(Header("touch", 3), 7, InputPoint.ClientDeviceIndependentPixels(x, y), TouchContactState.Pressed, 0.5, InputEventSource.Synthetic);

    private static PenContactEvent CreatePen(double x, double y) =>
        new(Header("pen", 4), InputPoint.ClientDeviceIndependentPixels(x, y), PenContactState.Pressed, PenButtons.Barrel, 0.6, Source: InputEventSource.Synthetic);

    private static KeyboardKeyEvent CreateKey(string name) =>
        new(Header("keyboard", 5), KeyboardKey.FromName(name), KeyboardKeyTransition.Down, KeyboardModifierState.None, 0, 0, 0, false, false, Source: InputEventSource.Synthetic);

    private static KeyboardTextEvent CreateKeyboardText(string text) =>
        new(Header("keyboard", 6), text, false, InputEventSource.Synthetic);

    private static TextInputEvent CreateText(string text) =>
        new(Header("text", 7), text, InputEventSource.Synthetic);

    private static TextCompositionEvent CreateComposition(string text) =>
        new(Header("text", 8), text, TextCompositionState.Updated, Source: InputEventSource.Synthetic);

    private sealed class Phase2Host : IUiHost
    {
        public Phase2Host(BSize viewportSize)
        {
            ViewportSize = viewportSize;
        }

        public BSize ViewportSize { get; }

        public double Scale => 1.0;

        public List<UiInvalidation> Invalidations { get; } = [];

        public List<BRenderList> Presented { get; } = [];

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation) => Invalidations.Add(invalidation);

        public void Present(BRenderList renderList) => Presented.Add(renderList);
    }

    private sealed class ManualClock : IUiClock
    {
        public UiTimestamp Now { get; private set; }

        public void Advance(TimeSpan elapsed) => Now = new UiTimestamp(Now.Elapsed + elapsed);
    }

    private class Phase2Element : UiElement
    {
        private readonly BColor _color;

        public Phase2Element(string id, BColor color)
        {
            Id = id;
            _color = color;
        }

        public string Id { get; }

        public bool HandlesInput { get; init; }

        public List<UiInputEventKind> InputKinds { get; } = [];

        protected override BSize MeasureCore(BSize availableSize)
        {
            base.MeasureCore(availableSize);
            return availableSize;
        }

        protected override void RenderCore(UiRenderContext context)
        {
            context.RenderList.FillRect(Bounds, _color);
            base.RenderCore(context);
        }

        protected override bool OnInput(UiInputEvent input)
        {
            InputKinds.Add(input.Kind);
            return HandlesInput;
        }
    }

    private sealed class InvalidatingRenderElement : Phase2Element
    {
        public InvalidatingRenderElement(string id, BColor color)
            : base(id, color)
        {
        }

        protected override void RenderCore(UiRenderContext context)
        {
            base.RenderCore(context);
            Invalidate(UiInvalidationKind.Render);
        }
    }
}
