using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Broiler.Code.Core.Review;
using Broiler.Code.Core.Shell;
using Broiler.Code.Workspaces;
using Broiler.Code.Workspaces.Storage;
using Broiler.Graphics;
using Broiler.UI.Button.Standard;
using Broiler.UI.CodeEditor.Standard;
using Broiler.UI.Edit.Standard;
using Broiler.UI.Label.Standard;
using Broiler.UI.Menu.Standard;
using Broiler.UI.Panel.Standard;
using Broiler.UI.Splitter.Standard;
using Broiler.UI.TabView.Standard;
using Broiler.UI.Toolbar.Standard;
using Broiler.UI.TreeView.Standard;

namespace Broiler.Code.Linux;

/// <summary>
/// Chooses the Standard implementations for the shell.
///
/// Identical in shape to the Windows head's factory, and deliberately so: the
/// shell is written against abstractions, so the only thing a head decides is
/// which concrete controls back them. Everything below this line is shared.
/// </summary>
internal static class CodeShellFactory
{
    public static (CodeShell Shell, StandardCodeEditor Editor) Create(BSize size)
    {
        var editor = new StandardCodeEditor { PreferredSize = size };

        var controls = new CodeShellControls
        {
            Root = new StandardPanel(),
            Body = new StandardPanel(),
            DocumentArea = new StandardPanel(),
            Menu = new StandardMenu(),
            Toolbar = new StandardToolbar(),
            Explorer = new StandardTreeView { PreferredSize = new BSize(280, size.Height) },
            ExplorerSplitter = new StandardSplitter(),
            Tabs = new StandardTabView { PreferredSize = new BSize(size.Width, 28) },
            Editor = editor,
            Problems = new StandardTreeView { PreferredSize = new BSize(size.Width, 160) },

            // The Human Review pane. Composed here rather than in the shell
            // because a head is what decides which controls exist — and because
            // a host without one still gets a working editor, which is what
            // lets the Android and browser heads adopt this later.
            ReviewPane = new StandardPanel(),
            Review = new StandardTreeView { PreferredSize = new BSize(320, size.Height) },
            ReviewSplitter = new StandardSplitter(),
            ReviewNoteInput = new StandardEdit { PreferredSize = new BSize(320, 26) },
            Status = new StandardLabel { Text = "Ready" },
            Output = new StandardLabel { Text = string.Empty },
            CreateButton = () => new StandardButton(),
        };

        return (new CodeShell(controls), editor);
    }

    /// <summary>
    /// Opens a workspace so the editor has a document to edit. The bootstrap is
    /// <see cref="WorkspaceBootstrap"/>'s, in Core; this only decides which
    /// directory is granted.
    /// </summary>
    public static async ValueTask<CodeWorkspace> OpenWorkspaceAsync(
        CodeShell shell, string? path, CancellationToken cancellationToken = default)
    {
        string root = path is { Length: > 0 } && Directory.Exists(path)
            ? Path.GetFullPath(path)
            : ScratchRoot();

        // Both are set before the workspace attaches, because AttachWorkspace is
        // what builds the review controller from them. Neither asking git lives
        // here: a head decides which directory is granted, and Core owns what to
        // ask about it.
        shell.RevisionProvider = new GitRevisionProvider(root);
        shell.Reviewer = await GitIdentity
            .ResolveReviewerAsync(root, cancellationToken).ConfigureAwait(false);

        return await WorkspaceBootstrap
            .OpenAsync(shell, new FileSystemWorkspaceStorage(root), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Where an unsaved session lives until it is saved somewhere real.
    /// XDG_DATA_HOME when the session sets it, its documented default
    /// otherwise — not /tmp, because a recovery journal a cleaner can delete is
    /// not a recovery journal.
    /// </summary>
    private static string ScratchRoot()
    {
        string? dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrWhiteSpace(dataHome))
        {
            dataHome = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local",
                "share");
        }

        string root = Path.Combine(dataHome, "broiler", "code", "scratch");
        Directory.CreateDirectory(root);
        return root;
    }
}
