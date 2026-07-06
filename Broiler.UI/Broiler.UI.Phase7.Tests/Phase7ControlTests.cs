using Broiler.Graphics;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.Input.Text;
using Broiler.UI.Dialog;
using Broiler.UI.Dialog.Standard;
using Broiler.UI.Edit.Standard;
using Broiler.UI.Standard;
using Broiler.UI.Window;
using Broiler.UI.Window.Standard;

namespace Broiler.UI.Phase7.Tests;

public sealed class Phase7ControlTests
{
    [Fact]
    public async Task Dialog_Modal_Result_Completes_Asynchronously_Restores_Focus_And_Captures_Input()
    {
        var host = new TestHost(new BSize(420, 240));
        using UiSession session = CreateSession(host);
        var owner = new StandardWindow { Title = "Owner" };
        var focusAnchor = new FixedElement(new BSize(40, 20), BColor.FromArgb(0xFF, 0xAA, 0xCC, 0xEE));
        owner.AddChild(focusAnchor);
        session.AddRoot(owner);
        session.RenderFrame();
        session.SetFocus(focusAnchor);

        var dialog = new StandardDialog { Title = "Confirm" };
        Task<UiDialogResult> result = dialog.ShowModal(owner, new BRect(40, 40, 220, 120));

        Assert.False(result.IsCompleted);
        Assert.True(dialog.IsModal);
        Assert.Equal(UiWindowKind.Dialog, dialog.Kind);
        Assert.Same(dialog, session.FocusedElement);
        Assert.Same(dialog, session.ModalElement);
        Assert.Null(session.CapturedElement);
        UiSemanticNode semantic = dialog.GetSemanticNode();
        Assert.Equal(UiSemanticRole.Dialog, semantic.Role);
        Assert.True(semantic.State.HasFlag(UiSemanticState.Modal));

        var route = new StandardInputRoute(session);
        Assert.True(route.Dispatch(Key("Escape", BVirtualKey.Escape, KeyboardKeyTransition.Down)));

        UiDialogResult completed = await result;
        Assert.Equal(UiDialogResultKind.Cancelled, completed.Kind);
        Assert.True(dialog.IsResultCompleted);
        Assert.True(dialog.IsDisposed);
        Assert.Null(session.CapturedElement);
        Assert.Null(session.ModalElement);
        Assert.Same(focusAnchor, session.FocusedElement);
        Assert.DoesNotContain(dialog, owner.Children);
    }

    [Fact]
    public void Dialog_Modal_Blocking_Survives_Child_Capture_And_Routes_Outside_Pointer_To_Dialog()
    {
        var host = new TestHost(new BSize(420, 240));
        using UiSession session = CreateSession(host);
        var owner = new StandardWindow { Title = "Owner" };
        session.AddRoot(owner);
        session.RenderFrame();
        var dialog = new StandardDialog { Title = "Input" };
        var edit = new StandardEdit { PreferredSize = new BSize(180, 32) };
        dialog.AddChild(edit);
        dialog.ShowModal(owner, new BRect(80, 50, 240, 140));
        session.RenderFrame();
        var route = new StandardInputRoute(session);

        Assert.True(route.Dispatch(MouseButtonInput(edit.Bounds.Left + 4, edit.Bounds.Top + 4, MouseButtonTransition.Down)));
        Assert.Same(edit, session.FocusedElement);
        Assert.Same(edit, session.CapturedElement);
        Assert.True(route.Dispatch(MouseButtonInput(edit.Bounds.Left + 4, edit.Bounds.Top + 4, MouseButtonTransition.Up)));
        Assert.Null(session.CapturedElement);
        Assert.Same(dialog, session.ModalElement);

        Assert.True(route.Dispatch(MouseButtonInput(4, 4, MouseButtonTransition.Down)));
        Assert.Same(dialog, session.FocusedElement);
        Assert.Null(session.CapturedElement);

        Assert.True(dialog.Cancel());
    }

    [Fact]
    public async Task Dialog_Modeless_Does_Not_Capture_And_Owner_Close_Completes_TopDown()
    {
        var host = new TestHost(new BSize(480, 260));
        using UiSession session = CreateSession(host);
        var owner = new StandardWindow { Title = "Owner" };
        session.AddRoot(owner);
        session.RenderFrame();

        var parentDialog = new StandardDialog { Title = "Parent" };
        Task<UiDialogResult> parentResult = parentDialog.ShowModeless(owner, new BRect(32, 32, 260, 160));
        var childDialog = new StandardDialog { Title = "Child" };
        Task<UiDialogResult> childResult = childDialog.ShowModeless(parentDialog, new BRect(16, 16, 160, 96));

        Assert.Null(session.CapturedElement);
        Assert.False(parentResult.IsCompleted);
        Assert.False(childResult.IsCompleted);

        Assert.True(owner.Close(UiWindowCloseReason.User));

        UiDialogResult completedParent = await parentResult;
        UiDialogResult completedChild = await childResult;
        Assert.Equal(UiDialogResultKind.Closed, completedParent.Kind);
        Assert.Equal(UiWindowCloseReason.OwnerClosed, completedParent.CloseReason);
        Assert.Equal(UiDialogResultKind.Closed, completedChild.Kind);
        Assert.Equal(UiWindowCloseReason.OwnerClosed, completedChild.CloseReason);
        Assert.True(parentDialog.IsDisposed);
        Assert.True(childDialog.IsDisposed);
    }

    [Fact]
    public void StandardEdit_Publishes_Caret_Geometry_Composition_And_Text_Semantics()
    {
        var host = new TestHost(new BSize(320, 80));
        using UiSession session = CreateSession(host);
        var edit = new StandardEdit { PlaceholderText = "Address", PreferredSize = new BSize(240, 32) };
        session.AddRoot(edit);
        session.RenderFrame();
        session.SetFocus(edit);
        var route = new StandardInputRoute(session);

        Assert.True(route.Dispatch(Text("broiler")));
        edit.SetSelection(2, 3);
        session.RenderFrame();

        Assert.NotNull(host.LastCaret);
        Assert.Same(edit, host.LastCaret.Owner);
        Assert.Equal(edit.CaretIndex, host.LastCaret.CaretIndex);
        Assert.Equal(2, host.LastCaret.SelectionStart);
        Assert.Equal(3, host.LastCaret.SelectionLength);
        Assert.False(host.LastCaret.IsCompositionActive);
        Assert.True(host.LastCaret.Bounds.Left >= edit.Bounds.Left);
        Assert.True(host.LastCaret.Bounds.Right <= edit.Bounds.Right);

        UiSemanticNode semantic = edit.GetSemanticNode();
        Assert.Equal("broiler", semantic.TextInfo?.Value);
        Assert.Equal(edit.CaretIndex, semantic.TextInfo?.CaretIndex);
        Assert.True(semantic.TextInfo?.IsEditable);
        Assert.False(semantic.TextInfo?.IsCompositionActive);

        Assert.True(route.Dispatch(Composition("ime", TextCompositionState.Updated)));
        session.RenderFrame();

        Assert.True(host.LastCaret.IsCompositionActive);
        Assert.True(edit.GetSemanticNode().TextInfo?.IsCompositionActive);

        session.RemoveRoot(edit);
        Assert.Same(edit, host.ClearedCaretOwner);
    }

    [Fact]
    public void StandardEdit_Ignores_Control_Text_And_Repeated_Backspace_Deletes_Text()
    {
        var host = new TestHost(new BSize(240, 80));
        using UiSession session = CreateSession(host);
        var edit = new StandardEdit { Text = "abcd", PreferredSize = new BSize(120, 32) };
        session.AddRoot(edit);
        session.RenderFrame();
        edit.SetSelection(edit.Text.Length, 0);
        session.SetFocus(edit);
        var route = new StandardInputRoute(session);

        Assert.True(route.Dispatch(Key("Backspace", BVirtualKey.Back, KeyboardKeyTransition.Down)));
        Assert.False(route.Dispatch(Text("\b")));
        Assert.Equal("abc", edit.Text);
        Assert.Equal(3, edit.CaretIndex);

        Assert.True(route.Dispatch(Key("Backspace", BVirtualKey.Back, KeyboardKeyTransition.Down)));
        Assert.False(route.Dispatch(Text("\b")));
        Assert.Equal("ab", edit.Text);
        Assert.Equal(2, edit.CaretIndex);
    }

    [Fact]
    public void Host_Service_Ports_Record_Neutral_Cursor_DragDrop_Accessibility_And_Settings()
    {
        var host = new TestHost(new BSize(240, 120))
        {
            SettingsSnapshot = new UiSystemSettings(UiContrastPreference.More, 1.25, ReducedMotion: true, UiFlowDirection.RightToLeft),
        };
        using UiSession session = CreateSession(host);
        var edit = new StandardEdit { Text = "value" };
        session.AddRoot(edit);
        session.RenderFrame();

        host.SetCursor(UiCursorShape.Text);
        bool dragStarted = host.BeginDrag(new UiDragStartRequest(
            edit,
            new BPoint(4, 4),
            new UiDragDataPackage(Text: "value", StringData: new Dictionary<string, string> { ["text/plain"] = "value" }),
            UiDragDropEffect.Copy | UiDragDropEffect.Move));
        host.PublishSemanticSnapshot(StandardSemanticSnapshot.Capture(session).Roots);
        host.NotifySemanticChanged(edit, UiSemanticChangeKind.ValueChanged);

        Assert.True(dragStarted);
        Assert.Equal(UiCursorShape.Text, host.Cursor);
        Assert.Equal(UiDragDropEffect.Copy | UiDragDropEffect.Move, host.LastDrag?.AllowedEffects);
        Assert.Equal("value", host.LastDrag?.Data.Text);
        Assert.Single(host.LastSemanticSnapshot);
        Assert.Equal(UiSemanticRole.Edit, host.LastSemanticSnapshot[0].Role);
        Assert.Equal(UiSemanticChangeKind.ValueChanged, host.LastSemanticChange);
        Assert.Equal(UiFlowDirection.RightToLeft, host.Settings.FlowDirection);
        Assert.True(host.Settings.ReducedMotion);
    }

    [Fact]
    public void Password_Edit_Redacts_Text_Info_While_Preserving_Caret_And_Selection()
    {
        var host = new TestHost(new BSize(320, 80));
        using UiSession session = CreateSession(host);
        var edit = new StandardEdit { IsPassword = true, PlaceholderText = "Password" };
        session.AddRoot(edit);
        session.SetFocus(edit);

        Assert.True(new StandardInputRoute(session).Dispatch(Text("secret")));
        edit.SetSelection(1, 2);

        UiSemanticNode semantic = edit.GetSemanticNode();
        Assert.Equal("Password", semantic.Name);
        Assert.Null(semantic.TextInfo?.Value);
        Assert.True(semantic.TextInfo?.IsPassword);
        Assert.Equal(1, semantic.TextInfo?.SelectionStart);
        Assert.Equal(2, semantic.TextInfo?.SelectionLength);
    }

    private static UiSession CreateSession(TestHost host) =>
        new StandardUiSessionBuilder()
            .WithDispatcher(new ImmediateUiDispatcher())
            .Build(host);

    private static KeyboardKeyEvent Key(string name, int nativeKeyCode, KeyboardKeyTransition transition) =>
        new(Header("keyboard"), KeyboardKey.FromName(name), transition, KeyboardModifierState.None, nativeKeyCode, 0, 0, false, false, Source: InputEventSource.Synthetic);

    private static MouseButtonEvent MouseButtonInput(double x, double y, MouseButtonTransition transition)
    {
        MouseButtons buttons = transition == MouseButtonTransition.Down ? MouseButtons.Left : MouseButtons.None;
        return new(Header("mouse"), InputPoint.ClientDeviceIndependentPixels(x, y), buttons, MouseButton.Left, transition, InputEventSource.Synthetic);
    }

    private static TextInputEvent Text(string text) =>
        new(Header("text"), text, InputEventSource.Synthetic);

    private static TextCompositionEvent Composition(string text, TextCompositionState state) =>
        new(Header("composition"), text, state, Source: InputEventSource.Synthetic);

    private static InputEventHeader Header(string id) =>
        new(InputDeviceId.FromOpaqueValue(id), new InputTimestamp(1, TimeSpan.TicksPerSecond, "phase7"), 1);

    private sealed class TestHost :
        IUiHost,
        IUiClipboardHost,
        IUiTextInputHost,
        IUiCursorHost,
        IUiDragDropHost,
        IUiAccessibilityHost,
        IUiSystemSettingsHost
    {
        public TestHost(BSize viewportSize)
        {
            ViewportSize = viewportSize;
        }

        public BSize ViewportSize { get; set; }

        public double Scale => 1;

        public string ClipboardText { get; set; } = string.Empty;

        public UiTextCaretInfo LastCaret { get; private set; } = null!;

        public UiElement? ClearedCaretOwner { get; private set; }

        public UiCursorShape Cursor { get; private set; }

        public UiDragStartRequest? LastDrag { get; private set; }

        public IReadOnlyList<UiSemanticNode> LastSemanticSnapshot { get; private set; } = [];

        public UiSemanticChangeKind LastSemanticChange { get; private set; }

        public UiSystemSettings SettingsSnapshot { get; set; } = UiSystemSettings.Default;

        public UiSystemSettings Settings => SettingsSnapshot;

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

        public void PublishCaret(UiTextCaretInfo caret) => LastCaret = caret;

        public void ClearCaret(UiElement owner) => ClearedCaretOwner = owner;

        public void SetCursor(UiCursorShape shape) => Cursor = shape;

        public bool BeginDrag(UiDragStartRequest request)
        {
            LastDrag = request;
            return true;
        }

        public void PublishSemanticSnapshot(IReadOnlyList<UiSemanticNode> roots) => LastSemanticSnapshot = roots;

        public void NotifySemanticChanged(UiElement element, UiSemanticChangeKind change) => LastSemanticChange = change;
    }

    private sealed class FixedElement : UiElement
    {
        private readonly BSize _size;
        private readonly BColor _color;

        public FixedElement(BSize size, BColor color)
        {
            _size = size;
            _color = color;
        }

        protected override BSize MeasureCore(BSize availableSize) => _size;

        protected override void RenderCore(UiRenderContext context) => context.RenderList.FillRect(Bounds, _color);
    }
}
