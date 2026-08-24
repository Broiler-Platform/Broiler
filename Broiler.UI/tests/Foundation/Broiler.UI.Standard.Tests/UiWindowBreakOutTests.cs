using Broiler.Graphics;
using Broiler.Input;
using Broiler.Input.Mouse;
using Broiler.UI.Dialog;
using Broiler.UI.Dialog.Standard;
using Broiler.UI.Window;
using Broiler.UI.Window.Standard;

namespace Broiler.UI.Standard.Tests;

public sealed class UiWindowBreakOutTests
{
    [Fact(Timeout = 600000)]
    public void CanBreakOut_Is_False_Without_Window_Host_Capability()
    {
        var host = new PlainHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var owner = new StandardWindow();
        session.AddRoot(owner);
        var child = new StandardWindow();
        owner.OpenOwnedWindow(child);

        Assert.False(child.CanBreakOut);
        Assert.False(child.BreakOut());
        Assert.False(child.IsBrokenOut);
        Assert.Contains(child, owner.OwnedWindows);
    }

    [Fact(Timeout = 600000)]
    public void CanBreakOut_Is_False_For_A_Top_Level_Window()
    {
        var host = new FakeWindowHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var top = new StandardWindow();
        session.AddRoot(top);

        Assert.Null(top.Owner);
        Assert.False(top.CanBreakOut);
    }

    [Fact(Timeout = 600000)]
    public void BreakOut_Reparents_Owned_Window_Into_A_New_Host_Window_Session()
    {
        var host = new FakeWindowHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var owner = new StandardWindow { Title = "Main" };
        session.AddRoot(owner);
        var child = new StandardWindow { Title = "Panel" };
        owner.OpenOwnedWindow(child, new BRect(10, 10, 200, 150));

        Assert.True(child.CanBreakOut);
        Assert.True(child.BreakOut());

        Assert.True(child.IsBrokenOut);
        Assert.Null(child.Owner);
        Assert.Equal(UiWindowKind.TopLevel, child.Kind);
        Assert.DoesNotContain(child, owner.OwnedWindows);

        FakeHostWindow created = Assert.Single(host.Created);
        Assert.Equal("Panel", created.Title);
        Assert.False(created.IsModal);
        Assert.NotNull(created.BoundSession);
        Assert.Same(child, Assert.Single(created.BoundSession!.Roots));
        Assert.NotSame(session, created.BoundSession);
        Assert.True(created.Activated);

        // Break-out is one-way for the window's lifetime.
        Assert.False(child.CanBreakOut);
        Assert.False(child.BreakOut());
    }

    [Fact(Timeout = 600000)]
    public void Broken_Out_Window_Reuses_Origin_Session_Services()
    {
        var host = new FakeWindowHost();
        var dispatcher = new InlineDispatcher();
        var clock = new ManualClock();
        using UiSession session = new StandardUiSessionBuilder()
            .WithDispatcher(dispatcher)
            .WithClock(clock)
            .Build(host);
        var owner = new StandardWindow();
        session.AddRoot(owner);
        var child = new StandardWindow();
        owner.OpenOwnedWindow(child);

        Assert.True(child.BreakOut());

        UiSession hosted = host.Created[0].BoundSession!;
        Assert.Same(dispatcher, hosted.Dispatcher);
        Assert.Same(clock, hosted.Clock);
        Assert.Same(session.Factories, hosted.Factories);
    }

    [Fact(Timeout = 600000)]
    public void Modal_Dialog_Break_Out_Blocks_Origin_Until_Completed()
    {
        var host = new FakeWindowHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var owner = new StandardWindow();
        session.AddRoot(owner);

        var dialog = new StandardDialog { Title = "Modal" };
        Task<UiDialogResult> result = dialog.ShowModal(owner, new BRect(20, 20, 200, 120));

        Assert.True(dialog.BreakOut());

        Assert.True(dialog.IsBrokenOut);
        Assert.True(host.Created[0].IsModal);
        Assert.True(session.IsBlockedByExternalModal);
        Assert.False(session.DispatchInput(CreateMouseDown(5, 5)));
        Assert.DoesNotContain(dialog, session.ModalElements);

        Assert.True(dialog.Accept("ok"));

        Assert.False(session.IsBlockedByExternalModal);
        Assert.True(result.IsCompletedSuccessfully);
        Assert.Equal(UiDialogResultKind.Accepted, dialog.CompletedResult.Kind);
        Assert.True(host.Created[0].IsDisposed);
    }

    [Fact(Timeout = 600000)]
    public void Modeless_Dialog_Break_Out_Does_Not_Block_Origin()
    {
        var host = new FakeWindowHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var owner = new StandardWindow();
        session.AddRoot(owner);

        var dialog = new StandardDialog { Title = "Modeless" };
        _ = dialog.ShowModeless(owner, new BRect(20, 20, 200, 120));

        Assert.True(dialog.BreakOut());

        Assert.False(host.Created[0].IsModal);
        Assert.False(session.IsBlockedByExternalModal);
    }

    [Fact(Timeout = 600000)]
    public void Host_Window_Close_Request_Closes_And_Disposes_Everything()
    {
        var host = new FakeWindowHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var owner = new StandardWindow();
        session.AddRoot(owner);
        var dialog = new StandardDialog();
        _ = dialog.ShowModal(owner, new BRect(20, 20, 200, 120));
        Assert.True(dialog.BreakOut());

        FakeHostWindow created = host.Created[0];
        UiSession hosted = created.BoundSession!;

        created.RaiseCloseRequested();

        Assert.True(dialog.IsClosed);
        Assert.True(dialog.IsDisposed);
        Assert.True(created.IsDisposed);
        Assert.True(hosted.IsDisposed);
        Assert.False(session.IsBlockedByExternalModal);
    }

    [Fact(Timeout = 600000)]
    public void Closing_Broken_Out_Window_Programmatically_Tears_Down_Once()
    {
        var host = new FakeWindowHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var owner = new StandardWindow();
        session.AddRoot(owner);
        var child = new StandardWindow();
        owner.OpenOwnedWindow(child);
        Assert.True(child.BreakOut());

        FakeHostWindow created = host.Created[0];
        UiSession hosted = created.BoundSession!;

        Assert.True(child.Close());
        Assert.True(created.IsDisposed);
        Assert.True(hosted.IsDisposed);
        Assert.Equal(1, created.DisposeCount);
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

    private sealed class PlainHost : IUiHost
    {
        public BSize ViewportSize => new(640, 480);

        public double Scale => 1.0;

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation)
        {
        }

        public void Present(BRenderList renderList)
        {
        }
    }

    private sealed class FakeWindowHost : IUiHost, IUiWindowHost
    {
        public List<FakeHostWindow> Created { get; } = [];

        public BSize ViewportSize => new(800, 600);

        public double Scale => 1.0;

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation)
        {
        }

        public void Present(BRenderList renderList)
        {
        }

        public IUiHostWindow CreateHostWindow(UiHostWindowRequest request)
        {
            var window = new FakeHostWindow(request);
            Created.Add(window);
            return window;
        }
    }

    private sealed class FakeHostWindow : IUiHostWindow
    {
        public FakeHostWindow(UiHostWindowRequest request)
        {
            Title = request.Title;
            Placement = request.Placement;
            IsModal = request.IsModal;
        }

        public string Title { get; private set; }

        public BRect Placement { get; }

        public bool IsModal { get; }

        public UiSession? BoundSession { get; private set; }

        public bool Activated { get; private set; }

        public bool IsDisposed { get; private set; }

        public int DisposeCount { get; private set; }

        public event EventHandler? CloseRequested;

        public BSize ViewportSize => new(400, 300);

        public double Scale => 1.0;

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation)
        {
        }

        public void Present(BRenderList renderList)
        {
        }

        public void Bind(UiSession session) => BoundSession = session;

        public void SetTitle(string title) => Title = title;

        public void Activate() => Activated = true;

        public void RaiseCloseRequested() => CloseRequested?.Invoke(this, EventArgs.Empty);

        public void Dispose()
        {
            DisposeCount++;
            IsDisposed = true;
        }
    }

    private sealed class InlineDispatcher : IUiDispatcher
    {
        public bool CheckAccess() => true;

        public void Post(Action callback) => callback();
    }

    private sealed class ManualClock : IUiClock
    {
        public UiTimestamp Now { get; private set; }

        public void Advance(TimeSpan elapsed) => Now = new UiTimestamp(Now.Elapsed + elapsed);
    }
}
