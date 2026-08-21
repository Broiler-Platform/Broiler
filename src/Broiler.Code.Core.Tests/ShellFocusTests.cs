using Broiler.Code.Core.Shell;
using Broiler.Code.Workspaces.Storage;
using Broiler.Graphics;
using Broiler.Input;
using Broiler.Input.Text;
using Broiler.UI;
using Broiler.UI.Button.Standard;
using Broiler.UI.CodeEditor.Standard;
using Broiler.UI.Label.Standard;
using Broiler.UI.Menu.Standard;
using Broiler.UI.Panel.Standard;
using Broiler.UI.Splitter.Standard;
using Broiler.UI.Standard;
using Broiler.UI.TabView.Standard;
using Broiler.UI.Toolbar.Standard;
using Broiler.UI.TreeView.Standard;

namespace Broiler.Code.Core.Tests;

/// <summary>
/// Where typed characters actually go.
///
/// The session routes keyboard and text input to <c>FocusedElement</c>, and
/// falls back to hit-testing the event's position when there is none. A
/// keyboard event has no meaningful position, so that fallback sends every
/// keystroke to whatever sits at the origin — the menu bar. The head set
/// <c>editor.HasFocus</c>, which draws a caret but tells the session nothing, so
/// the editor showed a caret and received not one character. These pin the
/// distinction down.
/// </summary>
public sealed class ShellFocusTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "broiler-focus", Guid.NewGuid().ToString("n"));

    public ShellFocusTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact(Timeout = 600000)]
    public async Task A_Caret_Without_Session_Focus_Receives_Nothing()
    {
        using Harness harness = await Harness.CreateAsync(_root);

        // Exactly what the head used to do, and the reason the window looked
        // read-only: the control believes it is focused, the session does not.
        harness.Editor.HasFocus = true;

        harness.Session.DispatchInput(Text("x"));

        Assert.Null(harness.Session.FocusedElement);
        Assert.Equal(0, harness.Editor.Snapshot.Length);
    }

    [Fact(Timeout = 600000)]
    public async Task Focusing_Through_The_Session_Delivers_Typed_Text()
    {
        using Harness harness = await Harness.CreateAsync(_root);

        harness.Session.SetFocus(harness.Editor);
        harness.Editor.HasFocus = true;

        harness.Session.DispatchInput(Text("a"));
        harness.Session.DispatchInput(Text("b"));

        Assert.Equal("ab", harness.Editor.Snapshot.GetText(0, 2));
    }

    [Fact(Timeout = 600000)]
    public async Task A_Hit_In_The_Editor_Resolves_To_The_Editor()
    {
        using Harness harness = await Harness.CreateAsync(_root);

        Assert.Same(harness.Editor, harness.Shell.ResolveFocusTarget(harness.Editor));
        Assert.Same(harness.Controls.Explorer, harness.Shell.ResolveFocusTarget(harness.Controls.Explorer));
    }

    [Fact(Timeout = 600000)]
    public async Task Clicking_Chrome_Leaves_Focus_Where_It_Was()
    {
        using Harness harness = await Harness.CreateAsync(_root);

        // A toolbar press must not take the caret out of the editor: the point
        // of pressing it is to act on the document you are editing.
        Assert.Null(harness.Shell.ResolveFocusTarget(harness.Controls.Toolbar));
        Assert.Null(harness.Shell.ResolveFocusTarget(harness.Controls.Menu));
        Assert.Null(harness.Shell.ResolveFocusTarget(null));
    }

    private static UiInputEvent Text(string text) => UiInputEvent.FromTextInput(
        new TextInputEvent(
            new InputEventHeader(
                InputDeviceId.FromOpaqueValue("keyboard"),
                new InputTimestamp(1, TimeSpan.TicksPerSecond, "focus"),
                1),
            text,
            InputEventSource.Synthetic));

    private sealed class Harness(
        CodeShell shell, StandardCodeEditor editor, CodeShellControls controls, UiSession session)
        : IDisposable
    {
        public CodeShell Shell { get; } = shell;

        public StandardCodeEditor Editor { get; } = editor;

        public CodeShellControls Controls { get; } = controls;

        public UiSession Session { get; } = session;

        public static async ValueTask<Harness> CreateAsync(string root)
        {
            var editor = new StandardCodeEditor { PreferredSize = new BSize(900, 600) };
            var controls = new CodeShellControls
            {
                Root = new StandardPanel(),
                Body = new StandardPanel(),
                DocumentArea = new StandardPanel(),
                Menu = new StandardMenu(),
                Toolbar = new StandardToolbar(),
                Explorer = new StandardTreeView { PreferredSize = new BSize(240, 600) },
                ExplorerSplitter = new StandardSplitter(),
                Tabs = new StandardTabView { PreferredSize = new BSize(1000, 28) },
                Editor = editor,
                Problems = new StandardTreeView { PreferredSize = new BSize(1000, 120) },
                Status = new StandardLabel(),
                Output = new StandardLabel(),
                CreateButton = () => new StandardButton(),
            };

            var shell = new CodeShell(controls);
            await WorkspaceBootstrap.OpenAsync(shell, new FileSystemWorkspaceStorage(root));

            UiSession session = new StandardUiSessionBuilder()
                .Build(new TestHost(new BSize(1000, 700)));
            session.AddRoot(controls.Root);

            // Arranged once, so hit-testing and caret geometry have real
            // rectangles to work with rather than zero-sized ones.
            session.RenderFrame();
            return new Harness(shell, editor, controls, session);
        }

        public void Dispose()
        {
            // Shell first: disposing the session disposes the element tree, and
            // the shell still wants to detach from its controls.
            Shell.Dispose();
            Session.Dispose();
        }
    }

    private sealed class TestHost(BSize viewportSize) : IUiHost
    {
        public BSize ViewportSize { get; } = viewportSize;

        public double Scale => 1;

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation)
        {
        }

        public void Present(BRenderList renderList)
        {
        }
    }
}
