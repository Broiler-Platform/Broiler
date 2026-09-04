using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Broiler.Code.Core.Hosting;
using Broiler.Code.Core.Shell;
using Broiler.Graphics;
using Broiler.UI;
using Broiler.UI.CodeEditor.Standard;
using Broiler.UI.Standard;

namespace Broiler.Code.Windows;

/// <summary>
/// Composes the session for the Windows head.
///
/// The composition is the head's only real job. Everything it wires — the
/// editor, the workspace, the shell — is built elsewhere; this decides which
/// platform services back them.
/// </summary>
[SupportedOSPlatform("windows7.0")]
internal static class CodeHost
{
    /// <summary>
    /// The service report without creating a window, so the support claim can
    /// be inspected on a machine with no display.
    /// </summary>
    public static HostServiceReport DescribeServices() => new("Broiler Code (Windows)",
    [
        new HostService(
            HostServiceReport.Dispatcher,
            HostServiceQuality.Native,
            "queued and drained on the window's message-loop thread"),
        new HostService(
            HostServiceReport.InputRouting,
            HostServiceQuality.Native,
            "explicit Broiler.Input events with device identity and a monotonic sequence"),
        new HostService(
            HostServiceReport.Clipboard,
            HostServiceQuality.Native,
            "Win32 clipboard, with no in-memory fallback"),
        new HostService(
            HostServiceReport.TextInput,
            HostServiceQuality.Native,
            "IMM32 composition with candidate placement at the caret"),
        new HostService(
            HostServiceReport.FileDialogs,
            HostServiceQuality.Native,
            "the common dialogs, through comdlg32"),
    ]);

    public static int Run(CodeWindow window, string? workspacePath = null)
    {
        ArgumentNullException.ThrowIfNull(window);

        var host = new CodeUiHost(window);
        (CodeShell shell, StandardCodeEditor editor) =
            CodeShellFactory.Create(new BSize(1280, 840));

        // The real dispatcher, not the Standard immediate one. Everything the
        // analysis and build layers post arrives on a worker thread, and this
        // is what moves it to the UI thread before it touches a control.
        UiSession session = new StandardUiSessionBuilder()
            .WithDispatcher(window.Dispatcher)
            .Build(host);

        // The shell's root, not the bare editor. Adding the editor alone was
        // the earlier mistake: it produced a window with no menu, no toolbar,
        // no explorer, and no document — so every keystroke was refused.
        session.AddRoot(shell.RootElement);
        window.AttachServices(session, editor);
        window.AttachShell(shell);
        host.Attach(window.Clipboard);

        // The same dispatcher the session uses. The review load reads every
        // record and every file it describes on a pool thread; without this it
        // would apply the result there too, racing the paint that reads it.
        shell.Dispatcher = window.Dispatcher;

        // The dialogs are what make Open and Save As real commands rather than
        // menu entries that raise an intent nobody handles.
        shell.FileDialogs = new WindowsFileDialogs(window.NativeHandle);

        // Cancel on a dirty close, until the head has a dialog to ask with.
        // Refusing to close is recoverable; discarding someone's work is not.
        shell.DirtyClosePrompt = (_, _) => ValueTask.FromResult(DirtyCloseChoice.Cancel);

        // Opened before the loop starts so the first frame already has a
        // document. The editor refuses every edit while its document is null.
        CodeShellFactory.OpenWorkspaceAsync(shell, workspacePath).AsTask().GetAwaiter().GetResult();

        // Focus is the session's, not the control's. Setting only
        // editor.HasFocus left UiSession.FocusedElement null, so every key and
        // character was routed by hit-testing the event's position — which for
        // a keyboard event is the origin, landing on the menu. The editor
        // rendered a caret and received nothing.
        window.FocusEditor();

        using (shell)
            return window.Run();
    }
}

/// <summary>
/// The UI host. Clipboard access is delegated to the Win32 implementation and
/// is absent until the window exists — there is deliberately no private string
/// standing in for it, so a missing clipboard is visible rather than silently
/// working only inside this process.
/// </summary>
[SupportedOSPlatform("windows7.0")]
internal sealed class CodeUiHost(CodeWindow window) : IUiHost, IUiClipboardHost
{
    private IUiClipboardHost? _clipboard;

    public BSize ViewportSize => window.ClientSize;

    public double Scale => window.DpiScale;

    public void Attach(IUiClipboardHost? clipboard) => _clipboard = clipboard;

    public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

    public void Invalidate(UiInvalidation invalidation) => window.Invalidate();

    public void Present(BRenderList renderList)
    {
        // Presentation belongs to the window: it asks the session for a render
        // list in BuildRenderList and submits it as part of its own frame. A
        // second submission from here would paint the same frame twice, out of
        // step with the swap chain.
        ArgumentNullException.ThrowIfNull(renderList);
    }

    public bool TryGetText(out string text)
    {
        if (_clipboard is not null)
            return _clipboard.TryGetText(out text);

        text = string.Empty;
        return false;
    }

    public void SetText(string text) => _clipboard?.SetText(text);
}
