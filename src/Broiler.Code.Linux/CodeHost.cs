using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Broiler.App;
using Broiler.Code.Core.Hosting;
using Broiler.Code.Core.Shell;
using Broiler.Graphics;
using Broiler.UI;
using Broiler.UI.CodeEditor.Standard;
using Broiler.UI.Standard;

namespace Broiler.Code.Linux;

/// <summary>
/// Composition for the Linux head.
///
/// The Linux picture differs from Windows in a way that matters for the support
/// claim, so it is stated here rather than discovered later.
///
/// The Linux graphics stack is an X11 surface driven by a polling loop, and
/// input arrives through evdev — raw device events, not X11 key events. That is
/// fine for the dispatcher and for input routing, both of which are real. It is
/// not fine for text input: an input method on Linux speaks XIM, ibus, or the
/// Wayland text-input protocol, and none of those are reachable from evdev.
/// Nothing in the repository implements <c>ITextInputProvider</c> for Linux;
/// the only implementation is Android's.
///
/// So this head reports text input as unavailable rather than substituting
/// something that appears to work. A user typing with an IME on Linux gets no
/// composition, and the product says so instead of silently dropping their
/// candidates.
/// </summary>
internal static class CodeHost
{
    /// <summary>
    /// What this head provides. <paramref name="hasClipboard"/> and
    /// <paramref name="fileDialogHelper"/> are passed in rather than probed
    /// here, so the report describes the services this run actually opened.
    /// </summary>
    public static HostServiceReport DescribeServices(string? fileDialogHelper = null, bool hasClipboard = false) =>
        new("Broiler Code (Linux)",
    [
        new HostService(
            HostServiceReport.Dispatcher,
            HostServiceQuality.Native,
            "queued and drained on the thread that owns the event loop"),
        new HostService(
            HostServiceReport.InputRouting,
            HostServiceQuality.Native,
            "explicit Broiler.Input events; evdev devices keep their own identity"),
        // Native rather than a substitute: the process owns the X11 CLIPBOARD
        // selection and answers other applications' requests for it, so a copy
        // here is a paste anywhere on the display.
        hasClipboard
            ? new HostService(
                HostServiceReport.Clipboard,
                HostServiceQuality.Native,
                "the X11 CLIPBOARD and PRIMARY selections, owned on the head's " +
                "own display connection")
            : new HostService(
                HostServiceReport.Clipboard,
                HostServiceQuality.Unavailable,
                "no X display to own a selection on; copy and paste are " +
                "disabled rather than backed by an in-process buffer"),
        new HostService(
            HostServiceReport.TextInput,
            HostServiceQuality.Unavailable,
            "no XIM, ibus, or Wayland text-input backend exists; evdev cannot " +
            "carry composition, so IME input is unavailable. Unmodified Latin " +
            "typing works; anything needing composition does not"),

        // Native rather than a substitute: the desktop's own chooser is the
        // real dialog, with the user's bookmarks and recent files. A stand-in
        // would be one we drew ourselves.
        fileDialogHelper is null
            ? new HostService(
                HostServiceReport.FileDialogs,
                HostServiceQuality.Unavailable,
                "neither zenity nor kdialog is installed, so Open and Save As " +
                "report themselves unavailable")
            : new HostService(
                HostServiceReport.FileDialogs,
                HostServiceQuality.Native,
                $"the desktop's own chooser, through {fileDialogHelper}"),
    ]);

    /// <summary>
    /// The commands a host must not offer, derived from what it does not have.
    /// Deriving it means a service that later becomes native enables its
    /// commands automatically, and one that regresses disables them.
    /// </summary>
    public static IReadOnlyList<string> UnavailableCommands(HostServiceReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var unavailable = new List<string>();
        if (report.QualityOf(HostServiceReport.Clipboard) != HostServiceQuality.Native)
        {
            unavailable.Add("Cut");
            unavailable.Add("Copy");
            unavailable.Add("Paste");
        }

        if (report.QualityOf(HostServiceReport.FileDialogs) != HostServiceQuality.Native)
        {
            unavailable.Add("Open");
            unavailable.Add("Save As");
            unavailable.Add("New Project");
        }

        return unavailable;
    }

    /// <summary>
    /// Composes the session and runs the window.
    ///
    /// The composition is the head's only real job. Everything it wires — the
    /// editor, the workspace, the shell — is built elsewhere; this decides which
    /// platform services back them, and which of them are honestly missing.
    /// </summary>
    public static async Task<int> RunAsync(
        string? workspacePath, bool ignoreFocus, CancellationToken cancellationToken)
    {
        var size = new BSize(1280, 840);
        var dialogs = new LinuxFileDialogs();

        // The window opens first because it owns the clipboard connection, and
        // the report describes the services this run actually has.
        await using var window = new CodeWindow(size, Console.WriteLine, ignoreFocus);

        Console.WriteLine(DescribeServices(dialogs.Helper, window.HasClipboard).Describe());
        Console.WriteLine();

        (CodeShell shell, StandardCodeEditor editor) = CodeShellFactory.Create(size);

        // The real dispatcher, not the Standard immediate one. Everything the
        // analysis and build layers post arrives on a worker thread, and this
        // is what moves it to the loop thread before it touches a control.
        UiSession session = new StandardUiSessionBuilder()
            .WithDispatcher(window.Dispatcher)
            .Build(window);

        session.AddRoot(shell.RootElement);
        window.Attach(session, shell);

        // The same dispatcher the session uses. The review load reads every
        // record and every file it describes on a pool thread; without this it
        // would apply the result there too, racing the paint that reads it.
        shell.Dispatcher = window.Dispatcher;

        // Null when no helper is installed, which is what makes Open and Save
        // As report Unavailable with a reason instead of doing nothing.
        shell.FileDialogs = dialogs.IsAvailable ? dialogs : null;

        // Cancel on a dirty close, until the head has a dialog to ask with.
        // Refusing to close is recoverable; discarding someone's work is not.
        shell.DirtyClosePrompt = (_, _) => ValueTask.FromResult(DirtyCloseChoice.Cancel);

        // Opened before the loop starts so the first frame already has a
        // document. The editor refuses every edit while its document is null.
        await CodeShellFactory.OpenWorkspaceAsync(shell, workspacePath, cancellationToken)
            .ConfigureAwait(false);

        // Focus is the session's, not the control's: UiSession routes keys to
        // FocusedElement and otherwise hit-tests the event position, which for
        // a keyboard event is the origin.
        window.FocusEditor();
        _ = editor;

        using (shell)
            return await window.RunAsync(cancellationToken).ConfigureAwait(false);
    }
}
