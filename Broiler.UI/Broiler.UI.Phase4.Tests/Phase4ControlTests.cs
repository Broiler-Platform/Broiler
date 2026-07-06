using Broiler.Graphics;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.Input.Text;
using Broiler.UI.Button;
using Broiler.UI.Button.Standard;
using Broiler.UI.Edit;
using Broiler.UI.Edit.Standard;
using Broiler.UI.Label.Standard;
using Broiler.UI.Panel;
using Broiler.UI.Panel.Standard;
using Broiler.UI.Standard;
using Broiler.UI.Window.Standard;

namespace Broiler.UI.Phase4.Tests;

public sealed class Phase4ControlTests
{
    [Fact]
    public void Button_Activates_By_Pointer_Keyboard_Command_And_Exposes_Semantics()
    {
        var host = new TestHost(new BSize(220, 80));
        using UiSession session = CreateSession(host);
        int clicks = 0;
        int commandCount = 0;
        var dispatcher = new StandardCommandDispatcher();
        dispatcher.Add(new StandardCommand("go", () => commandCount++));
        var button = new StandardButton
        {
            Text = "Go",
            CommandName = "go",
            CommandDispatcher = dispatcher,
            IsDefault = true,
        };
        button.Clicked += (_, _) => clicks++;
        session.AddRoot(button);
        session.RenderFrame();

        var route = new StandardInputRoute(session);
        Assert.True(route.Dispatch(MouseButtonInput(10, 10, MouseButtonTransition.Down)));
        Assert.Same(button, session.FocusedElement);
        Assert.True(button.IsPressed);
        Assert.True(route.Dispatch(MouseButtonInput(10, 10, MouseButtonTransition.Up)));
        Assert.False(button.IsPressed);
        Assert.Equal(1, clicks);
        Assert.Equal(1, commandCount);

        Assert.True(route.Dispatch(Key("Space", BVirtualKey.Space, KeyboardKeyTransition.Down)));
        Assert.True(route.Dispatch(Key("Space", BVirtualKey.Space, KeyboardKeyTransition.Up)));
        Assert.True(route.Dispatch(Key("Enter", BVirtualKey.Enter, KeyboardKeyTransition.Down)));

        Assert.Equal(3, clicks);
        Assert.Equal(3, commandCount);
        UiSemanticNode semantic = button.GetSemanticNode();
        Assert.Equal(UiSemanticRole.Button, semantic.Role);
        Assert.Equal("Go", semantic.Name);
        Assert.True(semantic.State.HasFlag(UiSemanticState.Enabled));
        Assert.True(semantic.State.HasFlag(UiSemanticState.Focused));
    }

    [Fact]
    public void Edit_Handles_Text_Selection_Clipboard_Undo_Composition_Submit_And_Accessibility()
    {
        var host = new TestHost(new BSize(320, 80));
        using UiSession session = CreateSession(host);
        var edit = new StandardEdit { PlaceholderText = "Address" };
        string submitted = string.Empty;
        edit.Submitted += (_, e) => submitted = e.Text;
        session.AddRoot(edit);
        session.RenderFrame();
        var route = new StandardInputRoute(session);

        Assert.True(route.Dispatch(MouseButtonInput(10, 10, MouseButtonTransition.Down)));
        Assert.Same(edit, session.FocusedElement);
        Assert.True(route.Dispatch(Text("broiler")));
        Assert.Equal("broiler", edit.Text);

        edit.SetSelection(0, 7);
        Assert.True(edit.Copy());
        Assert.Equal("broiler", host.ClipboardText);
        host.ClipboardText = "toast";
        Assert.True(route.Dispatch(Key("V", 0x56, KeyboardKeyTransition.Down, KeyboardModifierState.Control)));
        Assert.Equal("toast", edit.Text);
        Assert.True(route.Dispatch(Key("Z", 0x5A, KeyboardKeyTransition.Down, KeyboardModifierState.Control)));
        Assert.Equal("broiler", edit.Text);

        edit.SetSelection(3, 0);
        Assert.True(route.Dispatch(Composition("ime", TextCompositionState.Updated)));
        Assert.Equal("ime", edit.CompositionText);
        Assert.True(route.Dispatch(Composition("X", TextCompositionState.Cancelled)));
        Assert.Equal(string.Empty, edit.CompositionText);
        Assert.True(route.Dispatch(Composition("-", TextCompositionState.Committed)));
        Assert.Equal("bro-iler", edit.Text);

        Assert.True(route.Dispatch(Key("Enter", BVirtualKey.Enter, KeyboardKeyTransition.Down)));
        Assert.Equal("bro-iler", submitted);
        UiSemanticNode semantic = edit.GetSemanticNode();
        Assert.Equal(UiSemanticRole.Edit, semantic.Role);
        Assert.Equal("bro-iler", semantic.Name);
        Assert.True(semantic.State.HasFlag(UiSemanticState.Enabled));
        Assert.True(semantic.State.HasFlag(UiSemanticState.Focused));
    }

    [Fact]
    public void Edit_Password_Mode_Redacts_Rendering_Semantics_And_Copy()
    {
        var host = new TestHost(new BSize(320, 80));
        using UiSession session = CreateSession(host);
        var edit = new StandardEdit { IsPassword = true, PlaceholderText = "Password" };
        session.AddRoot(edit);
        session.SetFocus(edit);
        Assert.True(new StandardInputRoute(session).Dispatch(Text("secret")));
        edit.SelectAll();

        Assert.False(edit.Copy());
        BRenderList renderList = session.RenderFrame();

        Assert.DoesNotContain(renderList.Commands.OfType<BRenderCommand.DrawText>(), command => command.Text.Text.Contains("secret", StringComparison.Ordinal));
        Assert.Contains(renderList.Commands.OfType<BRenderCommand.DrawText>(), command => command.Text.Text == "******");
        Assert.Equal("Password", edit.GetSemanticNode().Name);
    }

    [Fact]
    public void Toolbar_Proof_Navigates_By_Mouse_And_Keyboard_Without_Native_Controls()
    {
        var host = new TestHost(new BSize(720, 120));
        using UiSession session = CreateSession(host);
        ToolbarProof proof = ToolbarProof.Create();
        session.AddRoot(proof.Window);
        session.RenderFrame();
        var route = new StandardInputRoute(session);

        session.SetFocus(proof.Address);
        Assert.True(route.Dispatch(Text("https://example.test")));
        Assert.True(route.Dispatch(Key("Enter", BVirtualKey.Enter, KeyboardKeyTransition.Down)));
        Assert.Equal("https://example.test", proof.CurrentUrl);
        Assert.Equal("Loaded https://example.test", proof.Status.Text);

        proof.Address.Text = "https://broiler.test";
        proof.Address.SetSelection(proof.Address.Text.Length, 0);
        Assert.True(route.Dispatch(MouseButtonInput(proof.Go.Bounds.Left + 4, proof.Go.Bounds.Top + 4, MouseButtonTransition.Down)));
        Assert.True(route.Dispatch(MouseButtonInput(proof.Go.Bounds.Left + 4, proof.Go.Bounds.Top + 4, MouseButtonTransition.Up)));

        Assert.Equal("https://broiler.test", proof.CurrentUrl);
        Assert.Equal(["https://example.test", "https://broiler.test"], proof.History);
        Assert.DoesNotContain(proof.Window.GetType().Assembly.GetExportedTypes(), type => type.Name.Contains("Control", StringComparison.Ordinal));
    }

    private static UiSession CreateSession(TestHost host) =>
        new StandardUiSessionBuilder()
            .WithDispatcher(new ImmediateUiDispatcher())
            .Build(host);

    private static MouseButtonEvent MouseButtonInput(double x, double y, MouseButtonTransition transition)
    {
        MouseButtons buttons = transition == MouseButtonTransition.Down ? MouseButtons.Left : MouseButtons.None;
        return new(Header("mouse"), InputPoint.ClientDeviceIndependentPixels(x, y), buttons, MouseButton.Left, transition, InputEventSource.Synthetic);
    }

    private static KeyboardKeyEvent Key(
        string name,
        int nativeKeyCode,
        KeyboardKeyTransition transition,
        KeyboardModifierState modifiers = KeyboardModifierState.None) =>
        new(Header("keyboard"), KeyboardKey.FromName(name), transition, modifiers, nativeKeyCode, 0, 0, false, false, Source: InputEventSource.Synthetic);

    private static TextInputEvent Text(string text) =>
        new(Header("text"), text, InputEventSource.Synthetic);

    private static TextCompositionEvent Composition(string text, TextCompositionState state) =>
        new(Header("composition"), text, state, Source: InputEventSource.Synthetic);

    private static InputEventHeader Header(string id) =>
        new(InputDeviceId.FromOpaqueValue(id), new InputTimestamp(1, TimeSpan.TicksPerSecond, "phase4"), 1);

    private sealed class TestHost : IUiHost, IUiClipboardHost
    {
        public TestHost(BSize viewportSize)
        {
            ViewportSize = viewportSize;
        }

        public BSize ViewportSize { get; }

        public double Scale => 1;

        public string ClipboardText { get; set; } = string.Empty;

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation)
        {
        }

        public void Present(BRenderList renderList)
        {
        }

        public bool TryGetText(out string text)
        {
            text = ClipboardText;
            return true;
        }

        public void SetText(string text) => ClipboardText = text;
    }

    private sealed class ToolbarProof
    {
        private ToolbarProof(StandardWindow window, StandardEdit address, StandardButton go, StandardLabel status)
        {
            Window = window;
            Address = address;
            Go = go;
            Status = status;
        }

        public StandardWindow Window { get; }

        public StandardEdit Address { get; }

        public StandardButton Go { get; }

        public StandardLabel Status { get; }

        public List<string> History { get; } = [];

        public string CurrentUrl { get; private set; } = string.Empty;

        public static ToolbarProof Create()
        {
            var window = new StandardWindow { Title = "Toolbar proof" };
            var root = new StandardPanel { LayoutMode = UiPanelLayoutMode.Stack, Spacing = 6 };
            var toolbar = new StandardPanel { LayoutMode = UiPanelLayoutMode.Stack, StackOrientation = UiStackOrientation.Horizontal, Spacing = 6 };
            var back = new StandardButton { Text = "<", PreferredSize = new BSize(40, 32) };
            var forward = new StandardButton { Text = ">", PreferredSize = new BSize(40, 32) };
            var address = new StandardEdit { PlaceholderText = "URL", PreferredSize = new BSize(420, 32) };
            var go = new StandardButton { Text = "Go", PreferredSize = new BSize(56, 32), IsDefault = true };
            var status = new StandardLabel { Text = "Ready" };
            toolbar.AddChild(back);
            toolbar.AddChild(forward);
            toolbar.AddChild(address);
            toolbar.AddChild(go);
            root.AddChild(toolbar);
            root.AddChild(status);
            window.AddChild(root);

            var proof = new ToolbarProof(window, address, go, status);
            address.Submitted += (_, _) => proof.Navigate(address.Text);
            go.Clicked += (_, _) => proof.Navigate(address.Text);
            back.Clicked += (_, _) => proof.Status.Text = "Back requested";
            forward.Clicked += (_, _) => proof.Status.Text = "Forward requested";
            return proof;
        }

        private void Navigate(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            CurrentUrl = url.Trim();
            History.Add(CurrentUrl);
            Status.Text = "Loaded " + CurrentUrl;
        }
    }
}
