using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Broiler.Code.Core.Diagnostics;
using Broiler.Code.Core.Review;
using Broiler.Code.Review;
using Broiler.Code.Core.Templates;
using Broiler.Code.Workspaces;
using Broiler.Code.Workspaces.Model;
using Broiler.Code.Workspaces.Storage;
using Broiler.Graphics;
using Broiler.UI;
using Broiler.UI.Button;
using Broiler.UI.CodeEditor;
using Broiler.UI.Edit;
using Broiler.UI.Label;
using Broiler.UI.Menu;
using Broiler.UI.Panel;
using Broiler.UI.Splitter;
using Broiler.UI.TabView;
using Broiler.UI.Toolbar;
using Broiler.UI.TreeView;

namespace Broiler.Code.Core.Shell;

/// <summary>
/// The controls a host supplies. They are abstractions, so this assembly never
/// references a Standard implementation and a head remains free to substitute
/// its own — which is also what keeps the shell testable without a platform.
/// </summary>
public sealed record CodeShellControls
{
    public required UiPanel Root { get; init; }

    public required UiPanel Body { get; init; }

    public required UiMenu Menu { get; init; }

    public required UiToolbar Toolbar { get; init; }

    public required UiTreeView Explorer { get; init; }

    public required UiSplitter ExplorerSplitter { get; init; }

    public required UiPanel DocumentArea { get; init; }

    public required UiTabView Tabs { get; init; }

    public required UiCodeEditor Editor { get; init; }

    public required UiTreeView Problems { get; init; }

    /// <summary>
    /// The Human Review pane, and the splitter that sizes it.
    ///
    /// Optional, unlike everything above it. A head that supplies neither gets
    /// the shell it had before the review workspace existed, which is what lets
    /// the Android and browser heads adopt it when they are ready rather than
    /// when this file changed. Supplying one and not the other composes the
    /// pane without a splitter, which is a poor experience but not a broken one.
    /// </summary>
    public UiTreeView? Review { get; init; }

    public UiSplitter? ReviewSplitter { get; init; }

    /// <summary>
    /// Holds the review tree and the note field. Supplied separately because a
    /// tree is not a container: without it the review tree is docked on its own
    /// and there is nowhere to type a note.
    /// </summary>
    public UiPanel? ReviewPane { get; init; }

    /// <summary>
    /// Where a new review note is typed.
    ///
    /// A single-line field, which is the honest shape of what Broiler.UI has
    /// today: <c>UiEdit</c> is single-line and <c>UiRichEdit</c> is a formatted
    /// document, not a plain-text box. A note is one or two sentences far more
    /// often than it is a paragraph, so this is a real constraint rather than a
    /// crippling one — and the record format already carries multi-line text, so
    /// a multi-line control later needs no migration.
    /// </summary>
    public UiEdit? ReviewNoteInput { get; init; }

    public required UiLabel Status { get; init; }

    public required UiLabel Output { get; init; }

    /// <summary>
    /// Makes a toolbar button. A factory rather than a list of buttons because
    /// the shell decides which commands the toolbar carries and the host decides
    /// what a button is — the same split as every other control here.
    /// </summary>
    public required Func<UiButton> CreateButton { get; init; }
}

/// <summary>
/// The composed IDE shell: menu, toolbar, Solution Explorer, editor tabs,
/// Problems and Output panes, splitter, and status line.
///
/// This is the piece that turns the tested parts into something on screen. The
/// coordination it drives — document lifetime, view state, save prompts — lives
/// in <see cref="DocumentCoordinator"/>; the tree contents in
/// <see cref="SolutionExplorerSource"/>; the problem rows in
/// <see cref="ProblemsModel"/>. This file is layout, command wiring, and the
/// status text, and deliberately holds no logic those three already own.
/// </summary>
public sealed class CodeShell : IDisposable
{
    /// <summary>
    /// What the toolbar carries, in order. A deliberate subset of the menu: a
    /// toolbar that mirrors every command is a second menu that is harder to
    /// read.
    /// </summary>
    private static readonly string[] ToolbarCommands =
    [
        CodeCommandNames.New,
        CodeCommandNames.Open,
        CodeCommandNames.Save,
        CodeCommandNames.SaveAll,
        CodeCommandNames.Build,
        CodeCommandNames.Cancel,
    ];

    private readonly CodeShellControls _controls;
    private readonly Dictionary<string, UiButton> _toolbarButtons = [];
    private readonly ProblemsModel _problems = new();
    private readonly ProblemsTreeSource _problemsSource;
    private ReviewController? _review;
    private ReviewPaneSource? _reviewSource;
    private CancellationTokenSource? _reviewLoad;
    private CodeWorkspace? _workspace;
    private SolutionExplorerSource? _explorerSource;
    private DocumentCoordinator? _coordinator;
    private CodeCommandSet _commands;
    private IFileDialogService? _fileDialogs;
    private bool _disposed;

    public CodeShell(CodeShellControls controls)
    {
        _controls = controls ?? throw new ArgumentNullException(nameof(controls));
        _problemsSource = new ProblemsTreeSource(_problems);
        _commands = new CodeCommandSet(() => _workspace, () => _coordinator?.ActiveDocument ?? WorkspaceItemId.None);

        Compose();
        _controls.Menu.ItemInvoked += OnMenuItemInvoked;
        _controls.Explorer.NodeActivated += OnExplorerNodeActivated;
        _controls.Problems.NodeActivated += OnProblemActivated;

        // The review pane follows whatever the editor is showing. Subscribed
        // here rather than through DocumentCoordinator because the coordinator
        // raises no active-document event, and the tab strip's selection is the
        // same fact seen one level lower.
        _controls.Tabs.SelectionChanged += OnActiveDocumentChanged;
        RefreshCommands();
        SetStatus("No workspace open. File ▸ Open…");
    }

    /// <summary>Raised when a command runs, so a host can act on Open/New.</summary>
    public event EventHandler<CodeCommandEventArgs>? CommandInvoked;

    public UiElement RootElement => _controls.Root;

    public ProblemsModel Problems => _problems;

    public CodeCommandSet Commands => _commands;

    public CodeWorkspace? Workspace => _workspace;

    public DocumentCoordinator? Coordinator => _coordinator;

    /// <summary>
    /// The Human Review state for the open workspace, or null when the head
    /// composed no review pane.
    /// </summary>
    public ReviewController? Review => _review;

    /// <summary>
    /// Binds a workspace. The explorer, the tabs, and the commands all follow
    /// from this; before it, the shell is present and inert rather than absent.
    /// </summary>
    public void AttachWorkspace(CodeWorkspace workspace, Workspaces.Recovery.RecoveryJournal? journal = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(workspace);

        DetachWorkspace();
        _workspace = workspace;
        _explorerSource = new SolutionExplorerSource(workspace);
        _controls.Explorer.DataSource = _explorerSource;

        _coordinator = new DocumentCoordinator(workspace, _controls.Editor, _controls.Tabs, journal);
        _coordinator.DirtyClosePrompt = DirtyClosePrompt;
        _commands = new CodeCommandSet(() => _workspace, () => _coordinator?.ActiveDocument ?? WorkspaceItemId.None)
        {
            HasFileDialogs = _fileDialogs is not null,
            HasBuildService = _commands.HasBuildService,
            HasReview = _controls.Review is not null,
            HasReviewer = !string.IsNullOrWhiteSpace(Reviewer),
        };

        // Expand to the projects so the tree opens showing something, rather
        // than one collapsed row the user has to discover.
        foreach (TreeRow row in _controls.Explorer.Rows.ToArray())
            _controls.Explorer.Expand(row.Id);

        AttachReview(workspace);
        RefreshCommands();
        SetStatus($"{workspace.Projects.Count} projects, {workspace.Items.Count} items");
        ShowWorkspaceDiagnostics();
    }

    /// <summary>
    /// Binds the review workspace to the open workspace.
    ///
    /// The records are read once, in the background, rather than on the way in.
    /// A repository the size of this one has thousands of them, and blocking the
    /// window on that read would make opening a workspace feel like the tool had
    /// hung. Until it completes the explorer simply shows no badges, which is the
    /// same thing it shows for a file nobody has reviewed.
    /// </summary>
    private void AttachReview(CodeWorkspace workspace)
    {
        if (_controls.Review is null)
            return;

        _review = new ReviewController(workspace, RevisionProvider, Reviewer, Dispatcher);
        _reviewSource = new ReviewPaneSource(_review);
        _controls.Review.DataSource = _reviewSource;

        if (_explorerSource is { } explorer)
            explorer.ReviewStateOf = _review.StateFor;

        _review.Changed += OnReviewChanged;

        // Cancelled by DetachWorkspace, so closing a workspace stops a load that
        // is still reading rather than leaving it to finish filling a controller
        // nobody is showing.
        _reviewLoad = new CancellationTokenSource();
        _ = LoadReviewAsync(_review, _reviewLoad.Token);
    }

    private async Task LoadReviewAsync(ReviewController controller, CancellationToken cancellationToken)
    {
        try
        {
            // Task.Run, despite LoadAsync being async, because the desktop
            // storage provider is synchronous underneath its async signatures —
            // FileSystemWorkspaceStorage reads with File.ReadAllBytes and returns
            // ValueTask.FromResult, so nothing ever yields. Awaiting it directly
            // would read every record in the repository, and every file those
            // records describe, on the UI thread — exactly the freeze the
            // background load exists to avoid.
            //
            // The result is not applied here. LoadAsync posts it through the
            // dispatcher, so the maps are mutated on the UI thread even though
            // the reads finished on a pool thread.
            await Task.Run(
                () => controller.LoadAsync(cancellationToken).AsTask(), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The workspace closed while the records were being read. Nothing to
            // report: the controller it was filling has already been discarded.
        }
        catch (ObjectDisposedException)
        {
            // The same race seen from the other side — the cancellation source
            // was disposed between the check and the throw.
        }
    }

    /// <summary>
    /// Asked before a dirty document closes. A host that sets nothing gets
    /// Cancel, because losing someone's work is not a default.
    /// </summary>
    public DirtyClosePrompt? DirtyClosePrompt { get; set; }

    /// <summary>
    /// Who is recording reviews. Until a head sets it the review commands stay
    /// disabled with that as the reason: a record saying a file was approved,
    /// with no name on it, is not evidence of a human review.
    /// </summary>
    public string Reviewer
    {
        get;
        set
        {
            field = value ?? string.Empty;
            if (_review is not null)
                _review.Reviewer = field;
            RefreshCommands();
        }
    } = string.Empty;

    /// <summary>
    /// Supplies the revision a review is recorded at, when the head has one.
    /// Provenance only — see <see cref="IRevisionProvider"/>.
    /// </summary>
    public IRevisionProvider? RevisionProvider { get; set; }

    /// <summary>
    /// Marshals background work onto the UI thread.
    ///
    /// Set by a head before the workspace attaches. Without it the review load
    /// applies its result on whichever thread finished the last read, which
    /// races every repaint — the reason
    /// <see cref="Hosting.UiThreadDispatcher"/> exists rather than the Standard
    /// immediate dispatcher.
    /// </summary>
    public UI.IUiDispatcher? Dispatcher { get; set; }

    /// <summary>Runs a named command. The single path the menu, toolbar, and keys share.</summary>
    public async ValueTask<bool> InvokeAsync(string name, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        CodeCommand? command = _commands.Find(name);
        if (command is null)
            return false;

        if (!command.IsEnabled)
        {
            SetStatus(command.Reason ?? $"{command.Text} is not available right now.");
            return false;
        }

        bool handled = name switch
        {
            CodeCommandNames.New => await NewDocumentAsync(cancellationToken).ConfigureAwait(false),
            CodeCommandNames.NewProject => await NewProjectAsync(cancellationToken).ConfigureAwait(false),
            CodeCommandNames.Open => await OpenAsync(cancellationToken).ConfigureAwait(false),
            CodeCommandNames.Save => await SaveActiveAsync(cancellationToken).ConfigureAwait(false),
            CodeCommandNames.SaveAs => await SaveActiveAsAsync(cancellationToken).ConfigureAwait(false),
            CodeCommandNames.SaveAll => await SaveAllAsync(cancellationToken).ConfigureAwait(false),
            CodeCommandNames.Close => _coordinator is not null &&
                await _coordinator.CloseAsync(_coordinator.ActiveDocument, cancellationToken).ConfigureAwait(false),
            CodeCommandNames.MarkReviewed =>
                await RecordReviewAsync(ReviewStatus.Reviewed, cancellationToken).ConfigureAwait(false),
            CodeCommandNames.MarkInReview =>
                await RecordReviewAsync(ReviewStatus.InReview, cancellationToken).ConfigureAwait(false),
            CodeCommandNames.MarkQuestion =>
                await RecordReviewAsync(ReviewStatus.Question, cancellationToken).ConfigureAwait(false),
            CodeCommandNames.MarkNeedsChange =>
                await RecordReviewAsync(ReviewStatus.NeedsChange, cancellationToken).ConfigureAwait(false),
            CodeCommandNames.ClearReview =>
                await RecordReviewAsync(ReviewStatus.Unreviewed, cancellationToken).ConfigureAwait(false),
            CodeCommandNames.AddNote => await AddNoteFromInputAsync(cancellationToken).ConfigureAwait(false),
            CodeCommandNames.ReviewCoverage => ShowReviewCoverage(),
            _ => false,
        };

        // Raised for every command, including the ones handled here, so a host
        // can react — retitle its window, drive a build — without the shell
        // knowing what it wants.
        CommandInvoked?.Invoke(this, new CodeCommandEventArgs(name, handled));
        RefreshCommands();
        return handled;
    }

    /// <summary>
    /// Creates an untitled document and shows it. It is editable at once and
    /// asks for a location only when it is saved, so a user can start typing
    /// without first deciding where the file will live.
    /// </summary>
    public async ValueTask<bool> NewDocumentAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_workspace is null || _coordinator is null)
        {
            SetStatus("There is no workspace to add a document to.");
            return false;
        }

        SourceDocument document = _workspace.CreateUntitledDocument(_workspace.NextUntitledName());
        if (!await _coordinator.OpenAsync(document.Id, cancellationToken).ConfigureAwait(false))
        {
            SetStatus("The new document could not be opened.");
            return false;
        }

        // Refreshed here rather than relying on InvokeAsync to do it after:
        // EnsureDocumentAsync calls this directly on startup, and without it
        // the first document opens with Save and Close still showing disabled.
        RefreshCommands();
        SetStatus($"{document.Item.RelativePath} — not saved yet");
        return true;
    }

    /// <summary>
    /// Creates a solution with one console project and opens it.
    ///
    /// The save dialog supplies both halves of the answer at once: the directory
    /// to create it in and, from the file name, what to call it. That avoids a
    /// text-input dialog this shell does not have, and it is the same gesture
    /// the user already knows.
    ///
    /// What lands on disk is a plain <c>.slnx</c>, <c>.csproj</c>, and
    /// <c>Program.cs</c>. Nothing Broiler-specific is written, so the result
    /// builds with <c>dotnet build</c> and opens in any other tool.
    /// </summary>
    public async ValueTask<bool> NewProjectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (FileDialogs is null)
        {
            SetStatus("This host has no way to ask where to create a project.");
            return false;
        }

        FileGrant? grant = await FileDialogs.RequestSaveAsync(
            new FileDialogRequest
            {
                Title = "New Project",
                SuggestedName = "MyApp.slnx",
                Filters = [FileDialogFilter.Solutions],
            },
            cancellationToken).ConfigureAwait(false);

        if (grant is null)
            return false;

        string name = StemOf(grant.RelativePath);
        TemplateResult solution = CodeTemplateService.PlanSolution(
            name, [$"src/{name}/{name}.csproj"]);
        if (!solution.Succeeded)
        {
            SetStatus(solution.Message!);
            return false;
        }

        TemplateResult project = CodeTemplateService.PlanProject(
            name, ProjectTemplateKind.ConsoleApplication);
        if (!project.Succeeded)
        {
            SetStatus(project.Message!);
            return false;
        }

        // Written as one plan, so the existing-file check covers every file
        // before any of them is created. A half-written project is worse than
        // none: it looks like something the user can open.
        var templates = new CodeTemplateService(grant.Storage);
        TemplateResult written = await templates
            .WriteAsync(new TemplateResult(true, [.. solution.Files, .. project.Files]), cancellationToken)
            .ConfigureAwait(false);

        if (!written.Succeeded)
        {
            SetStatus(written.Message!);
            return false;
        }

        // The new solution replaces the open one, so unsaved work is asked
        // about first — and if the user declines, the project still exists.
        if (_coordinator is not null &&
            !await _coordinator.CloseAllAsync(cancellationToken).ConfigureAwait(false))
        {
            SetStatus($"Created {name}, but the open workspace was kept: a document has unsaved changes.");
            return false;
        }

        await WorkspaceBootstrap.OpenAsync(this, grant.Storage, cancellationToken).ConfigureAwait(false);
        SetStatus($"Created {name} at {grant.DisplayPath}");
        return true;
    }

    /// <summary>
    /// Asks for a file and opens it. A solution replaces the workspace; a source
    /// file is opened through the grant the dialog created, which is how a file
    /// outside the current root is reached without widening that root.
    /// </summary>
    public async ValueTask<bool> OpenAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (FileDialogs is null)
        {
            SetStatus("This host has no way to ask for a file.");
            return false;
        }

        FileGrant? grant = await FileDialogs.RequestOpenAsync(
            new FileDialogRequest
            {
                Title = "Open",
                Filters = [FileDialogFilter.Sources, FileDialogFilter.Solutions, FileDialogFilter.All],
            },
            cancellationToken).ConfigureAwait(false);

        if (grant is null)
            return false;

        if (IsSolution(grant.RelativePath))
            return await OpenSolutionAsync(grant, cancellationToken).ConfigureAwait(false);

        if (_workspace is null)
        {
            SetStatus("There is no workspace to open a document into.");
            return false;
        }

        StorageResult<SourceDocument> opened = await _workspace
            .OpenGrantedDocumentAsync(grant.Storage, grant.RelativePath, cancellationToken)
            .ConfigureAwait(false);

        if (!opened.Succeeded)
        {
            SetStatus($"{grant.DisplayPath} could not be opened: {opened.Failure!.Message}");
            return false;
        }

        if (_coordinator is null ||
            !await _coordinator.OpenAsync(opened.Value!.Id, cancellationToken).ConfigureAwait(false))
        {
            SetStatus($"{grant.DisplayPath} could not be shown.");
            return false;
        }

        _controls.Explorer.Refresh();
        SetStatus(grant.DisplayPath);
        return true;
    }

    /// <summary>
    /// Asks where to write the active document, writes it, and rebinds it
    /// there. The document keeps its ID, so its tab, buffer, and undo history
    /// come with it.
    /// </summary>
    public async ValueTask<bool> SaveActiveAsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_workspace is null || _coordinator is null || _coordinator.ActiveDocument.IsNone)
        {
            SetStatus("There is no document to save.");
            return false;
        }

        if (FileDialogs is null)
        {
            SetStatus("This host has no way to ask where to save.");
            return false;
        }

        WorkspaceItemId id = _coordinator.ActiveDocument;
        WorkspaceItem? item = _workspace.FindItem(id);

        FileGrant? grant = await FileDialogs.RequestSaveAsync(
            new FileDialogRequest
            {
                Title = "Save As",
                SuggestedName = item?.Name,
                Filters = [FileDialogFilter.Sources, FileDialogFilter.All],
            },
            cancellationToken).ConfigureAwait(false);

        if (grant is null)
            return false;

        SaveOutcome outcome = await _workspace
            .SaveDocumentAsAsync(id, grant.Storage, grant.RelativePath, cancellationToken)
            .ConfigureAwait(false);

        if (!outcome.Succeeded)
        {
            SetStatus(outcome.Message ?? $"{grant.DisplayPath} could not be written.");
            return false;
        }

        _coordinator.RenameTab(id, outcome.RelativePath);
        _controls.Explorer.Refresh();
        RefreshCommands();
        SetStatus($"Saved {grant.DisplayPath}");
        return true;
    }

    /// <summary>
    /// Asks the user for a file. Null on a host that cannot ask, which is what
    /// makes Open and Save As report Unavailable rather than doing nothing.
    /// </summary>
    public IFileDialogService? FileDialogs
    {
        get => _fileDialogs;
        set
        {
            _fileDialogs = value;
            _commands.HasFileDialogs = value is not null;
            RefreshCommands();
        }
    }

    /// <summary>
    /// Guarantees something editable is on screen. Called after a workspace is
    /// attached: a window whose editor has no document refuses every keystroke,
    /// and an empty untitled buffer is a better first impression than a surface
    /// that silently ignores typing.
    /// </summary>
    public async ValueTask<bool> EnsureDocumentAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_coordinator is null)
            return false;
        if (!_coordinator.ActiveDocument.IsNone)
            return true;

        return await NewDocumentAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Which control should take keyboard focus for a pointer hit, so a host can
    /// route focus without knowing the shell's layout. Null leaves focus where
    /// it is — clicking the toolbar should not steal the caret from the editor.
    /// </summary>
    public UiElement? ResolveFocusTarget(UiElement? hit)
    {
        ThrowIfDisposed();
        for (UiElement? element = hit; element is not null; element = element.Parent)
        {
            if (ReferenceEquals(element, _controls.Editor))
                return _controls.Editor;
            if (ReferenceEquals(element, _controls.Explorer))
                return _controls.Explorer;
            if (ReferenceEquals(element, _controls.Problems))
                return _controls.Problems;
        }

        return null;
    }

    /// <summary>The editor, so a host can give it focus when the window opens.</summary>
    public UiCodeEditor Editor => _controls.Editor;

    private async ValueTask<bool> OpenSolutionAsync(FileGrant grant, CancellationToken cancellationToken)
    {
        StorageResult<CodeWorkspace> loaded = await WorkspaceLoader
            .LoadSolutionAsync(grant.Storage, grant.RelativePath, cancellationToken)
            .ConfigureAwait(false);

        if (!loaded.Succeeded)
        {
            SetStatus($"{grant.DisplayPath} could not be loaded: {loaded.Failure!.Message}");
            return false;
        }

        // Every open document belongs to the workspace being replaced, so the
        // user is asked about unsaved work before it goes.
        if (_coordinator is not null &&
            !await _coordinator.CloseAllAsync(cancellationToken).ConfigureAwait(false))
        {
            SetStatus("The open solution was kept: a document with unsaved changes was not closed.");
            return false;
        }

        AttachWorkspace(loaded.Value!);
        return true;
    }

    /// <summary>The file name without its extension, and without its folders.</summary>
    private static string StemOf(string relativePath)
    {
        int slash = relativePath.LastIndexOf('/');
        string name = slash < 0 ? relativePath : relativePath[(slash + 1)..];
        int dot = name.LastIndexOf('.');
        return dot <= 0 ? name : name[..dot];
    }

    private static bool IsSolution(string relativePath) =>
        relativePath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase) ||
        relativePath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase);

    /// <summary>Opens a document and shows it, from the explorer or a host.</summary>
    public async ValueTask<bool> OpenDocumentAsync(
        WorkspaceItemId id, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_coordinator is null)
            return false;

        bool opened = await _coordinator.OpenAsync(id, cancellationToken).ConfigureAwait(false);
        if (!opened)
        {
            SetStatus("That item could not be opened.");
            return false;
        }

        WorkspaceItem? item = _workspace?.FindItem(id);
        SetStatus(item is null ? "Opened." : $"{item.RelativePath}");
        RefreshCommands();
        return true;
    }

    /// <summary>Publishes diagnostics for a document into the Problems pane.</summary>
    public void SetDocumentProblems(
        string documentPath, IEnumerable<MergedDiagnostic> diagnostics, ICodeTextSnapshot snapshot)
    {
        ThrowIfDisposed();
        _problems.SetDocumentEntries(documentPath, ProblemsModel.ToEntries(diagnostics, snapshot));
        _problemsSource.Refresh();
        _controls.Problems.Refresh();
        RefreshStatusCounts();
    }

    public void SetAnalysisMode(CodeAnalysisMode mode)
    {
        ThrowIfDisposed();
        _problems.Mode = mode;
        _controls.Editor.AnalysisMode = mode;
        RefreshStatusCounts();
    }

    public void SetOutput(string text)
    {
        ThrowIfDisposed();
        _controls.Output.Text = text ?? string.Empty;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _controls.Menu.ItemInvoked -= OnMenuItemInvoked;
        _controls.Explorer.NodeActivated -= OnExplorerNodeActivated;
        _controls.Problems.NodeActivated -= OnProblemActivated;
        if (_controls.Review is { } review)
            review.NodeActivated -= OnReviewNodeActivated;
        if (_controls.ReviewNoteInput is { } noteInput)
            noteInput.Submitted -= OnReviewNoteSubmitted;
        _controls.Tabs.SelectionChanged -= OnActiveDocumentChanged;
        foreach (UiButton button in _toolbarButtons.Values)
            button.Clicked -= OnToolbarButtonClicked;
        _toolbarButtons.Clear();
        DetachWorkspace();
    }

    /// <summary>
    /// The layout. Docked rather than absolutely positioned so the panes keep
    /// their relationship as the window resizes.
    /// </summary>
    private void Compose()
    {
        UiPanel root = _controls.Root;
        root.LayoutMode = UiPanelLayoutMode.Dock;
        root.AddChild(_controls.Menu);
        root.SetDock(_controls.Menu, UiDock.Top);
        root.AddChild(_controls.Toolbar);
        root.SetDock(_controls.Toolbar, UiDock.Top);

        // Status first among the bottom-docked children, so it stays the
        // outermost strip and the Output pane sits above it.
        root.AddChild(_controls.Status);
        root.SetDock(_controls.Status, UiDock.Bottom);
        root.AddChild(_controls.Output);
        root.SetDock(_controls.Output, UiDock.Bottom);
        root.AddChild(_controls.Problems);
        root.SetDock(_controls.Problems, UiDock.Bottom);

        UiPanel body = _controls.Body;
        body.LayoutMode = UiPanelLayoutMode.Dock;
        body.AddChild(_controls.Explorer);
        body.SetDock(_controls.Explorer, UiDock.Left);
        body.AddChild(_controls.ExplorerSplitter);
        body.SetDock(_controls.ExplorerSplitter, UiDock.Left);

        UiPanel documents = _controls.DocumentArea;
        documents.LayoutMode = UiPanelLayoutMode.Dock;
        documents.AddChild(_controls.Tabs);
        documents.SetDock(_controls.Tabs, UiDock.Top);
        documents.AddChild(_controls.Editor);
        documents.SetDock(_controls.Editor, UiDock.Fill);

        ComposeReview(body);

        // Added last so it takes what the docked panes left, which is what Fill
        // means here. Adding it before the review pane would give the editor the
        // whole remainder and leave the review pane nothing to dock into.
        body.AddChild(documents);
        body.SetDock(documents, UiDock.Fill);
        root.AddChild(body);
        root.SetDock(body, UiDock.Fill);

        _controls.Problems.DataSource = _problemsSource;
        _controls.ExplorerSplitter.Orientation = UiSplitterOrientation.Vertical;
        ComposeToolbar();
    }

    /// <summary>
    /// Docks the Human Review pane to the right of the editor, giving the shell
    /// the three columns the review workspace is built around: what to review on
    /// the left, the code in the middle, and what is known about it on the right.
    ///
    /// Does nothing when the head supplied no review tree, so a host that has
    /// not adopted the review workspace composes exactly the shell it did before.
    /// </summary>
    private void ComposeReview(UiPanel body)
    {
        if (_controls.Review is not { } review)
            return;

        // The pane goes to the far right and the splitter to its left, mirroring
        // the explorer's arrangement on the other side. The splitter is a grip
        // and nothing more today: UiSplitter.Value is read by no one here, and
        // the explorer's has been inert the same way since it was composed —
        // pane width comes from the PreferredSize the head sets. Applying Value
        // to pane layout is one change for both splitters, not something to
        // solve for this pane alone.
        if (_controls.ReviewPane is { } pane)
        {
            pane.LayoutMode = UiPanelLayoutMode.Dock;

            if (_controls.ReviewNoteInput is { } input)
            {
                input.PlaceholderText = "Add a review note…";
                input.Submitted += OnReviewNoteSubmitted;
                pane.AddChild(input);
                pane.SetDock(input, UiDock.Bottom);
            }

            pane.AddChild(review);
            pane.SetDock(review, UiDock.Fill);
            body.AddChild(pane);
            body.SetDock(pane, UiDock.Right);
        }
        else
        {
            body.AddChild(review);
            body.SetDock(review, UiDock.Right);
        }

        if (_controls.ReviewSplitter is { } splitter)
        {
            splitter.Orientation = UiSplitterOrientation.Vertical;
            body.AddChild(splitter);
            body.SetDock(splitter, UiDock.Right);
        }

        review.NodeActivated += OnReviewNodeActivated;
    }

    private void RefreshCommands()
    {
        SyncReviewState();
        var items = new List<UiMenuItem>();

        // Mnemonics go in AccessKey, never in the text. UiMenu renders Text
        // verbatim, so "&File" is drawn with the ampersand in it — this shell
        // did exactly that, and the menu bar read "&File" and "&Build".
        var file = new UiMenuItem("file", "File") { AccessKey = 'F' };
        AddItem(file, CodeCommandNames.New);
        AddItem(file, CodeCommandNames.NewProject);
        AddItem(file, CodeCommandNames.Open);
        file.Children.Add(new UiMenuItem("file.sep", string.Empty) { IsSeparator = true });
        AddItem(file, CodeCommandNames.Save);
        AddItem(file, CodeCommandNames.SaveAs);
        AddItem(file, CodeCommandNames.SaveAll);
        AddItem(file, CodeCommandNames.Close);
        items.Add(file);

        var build = new UiMenuItem("build", "Build") { AccessKey = 'B' };
        AddItem(build, CodeCommandNames.Build);
        AddItem(build, CodeCommandNames.Rebuild);
        AddItem(build, CodeCommandNames.Cancel);
        items.Add(build);

        // A menu of its own rather than entries under File. Human review is not
        // a file operation: it is the thing this shell exists to make routine,
        // and burying it three items deep would say the opposite.
        var review = new UiMenuItem("review", "Review") { AccessKey = 'R' };
        AddItem(review, CodeCommandNames.MarkReviewed);
        AddItem(review, CodeCommandNames.MarkInReview);
        AddItem(review, CodeCommandNames.MarkQuestion);
        AddItem(review, CodeCommandNames.MarkNeedsChange);
        review.Children.Add(new UiMenuItem("review.sep", string.Empty) { IsSeparator = true });
        AddItem(review, CodeCommandNames.AddNote);
        AddItem(review, CodeCommandNames.ClearReview);
        AddItem(review, CodeCommandNames.ReviewCoverage);
        items.Add(review);

        _controls.Menu.SetItems(items);
        RefreshToolbar();

        void AddItem(UiMenuItem parent, string name)
        {
            CodeCommand? command = _commands.Find(name);
            if (command is null)
                return;

            parent.Children.Add(new UiMenuItem(name, MenuText(command))
            {
                CommandName = name,
                IsEnabled = command.IsEnabled,
                AccessKey = command.AccessKey,
            });
        }
    }

    /// <summary>
    /// Points the review pane at the active document and refreshes the state the
    /// review commands are enabled from.
    ///
    /// Driven from the command refresh because every path that can change either
    /// — a tab switch, a save, an edit, a recorded decision — already ends
    /// there. A second notification path would be one more thing to forget.
    /// </summary>
    private void SyncReviewState()
    {
        WorkspaceItemId active = _coordinator?.ActiveDocument ?? WorkspaceItemId.None;

        if (_review is not null && _review.CurrentDocument != active)
            _review.SetCurrentDocument(active);

        _commands.HasReview = _controls.Review is not null;
        _commands.HasReviewer = !string.IsNullOrWhiteSpace(Reviewer);
    }

    /// <summary>
    /// An unavailable command stays visible and disabled with the reason in its
    /// text. Hiding it would leave the user wondering whether the feature exists
    /// at all.
    /// </summary>
    private static string MenuText(CodeCommand command) =>
        command.Availability == CommandAvailability.Unavailable
            ? $"{command.Text} (unavailable)"
            : command.Text;

    /// <summary>
    /// The toolbar's buttons are made once and then only updated. Rebuilding
    /// them on every command refresh would replace the element under the
    /// pointer mid-click and discard focus on every keystroke.
    /// </summary>
    private void ComposeToolbar()
    {
        _controls.Toolbar.Title = "Broiler Code";

        foreach (string name in ToolbarCommands)
        {
            UiButton button = _controls.CreateButton();
            button.CommandName = name;
            button.Clicked += OnToolbarButtonClicked;
            _controls.Toolbar.AddChild(button);
            _toolbarButtons[name] = button;
        }

        // The file group and the build group are different kinds of action.
        if (_toolbarButtons.TryGetValue(CodeCommandNames.Build, out UiButton? buildButton))
            _controls.Toolbar.SetSeparatorBefore(buildButton, true);
    }

    private void RefreshToolbar()
    {
        foreach ((string name, UiButton button) in _toolbarButtons)
        {
            if (button.IsDisposed)
                continue;

            CodeCommand? command = _commands.Find(name);
            if (command is null)
                continue;

            button.Text = command.Text;
            button.IsEnabled = command.IsEnabled;

            // Sized from the label rather than left at a default: a button
            // narrower than its text renders as an unreadable stub.
            button.PreferredSize = new BSize((command.Text.Length * 7.5) + 20, 28);
        }
    }

    private void OnToolbarButtonClicked(object? sender, UiButtonClickEventArgs e)
    {
        if (sender is UiButton { CommandName: { Length: > 0 } name })
            _ = InvokeAsync(name);
    }

    private void OnMenuItemInvoked(object? sender, UiMenuItemInvokedEventArgs e)
    {
        if (e.Item?.CommandName is { Length: > 0 } name)
            _ = InvokeAsync(name);
    }

    private void OnExplorerNodeActivated(object? sender, TreeNodeEventArgs e)
    {
        if (_explorerSource is null)
            return;

        WorkspaceItemId id = _explorerSource.ItemFor(e.Node);
        if (!id.IsNone && _workspace?.FindItem(id) is { Kind: WorkspaceItemKind.SourceDocument })
            _ = OpenDocumentAsync(id);
    }

    private void OnProblemActivated(object? sender, TreeNodeEventArgs e)
    {
        if (_problemsSource.EntryFor(e.Node) is not { } entry || entry.IsProjectLevel)
            return;

        // Navigation: open the document, then put the caret on the diagnostic.
        WorkspaceItem? item = _workspace?.FindItem(entry.DocumentPath);
        if (item is null)
            return;

        _ = NavigateAsync(item.Id, entry);
    }

    private void OnActiveDocumentChanged(object? sender, UiTabSelectionChangedEventArgs e) =>
        RefreshCommands();

    private void OnReviewChanged(object? sender, EventArgs e)
    {
        // An unreadable record makes its file look unreviewed, so it is said out
        // loud rather than left for the reviewer to notice a badge that quietly
        // went missing.
        if (_review is { Unreadable.Count: > 0 } review)
        {
            SetStatus(review.Unreadable.Count == 1
                ? review.Unreadable[0].Message
                : $"{review.Unreadable.Count} review records could not be read; those files show as unreviewed.");
        }

        // The explorer badges come from the same controller, so a recorded
        // decision has to redraw the tree as well as the pane. Refresh() rather
        // than a targeted change: a single decision can move one row, and a
        // completed background load moves thousands.
        _explorerSource?.Refresh();
        RefreshCommands();
    }

    /// <summary>
    /// Activating a note in the review pane puts the caret on the code it is
    /// about. The note's placed line is used, not its recorded one, so this lands
    /// correctly on a file that has been edited since the note was written.
    /// </summary>
    private void OnReviewNodeActivated(object? sender, TreeNodeEventArgs e)
    {
        if (_reviewSource?.NoteFor(e.Node) is not { } note ||
            note.Status is ReviewAnchorStatus.FileLevel or ReviewAnchorStatus.Orphaned)
        {
            return;
        }

        ICodeTextSnapshot snapshot = _controls.Editor.Snapshot;
        if (note.StartLine >= 0 && note.StartLine < snapshot.LineCount)
        {
            _controls.Editor.Selection = CodeSelection.Caret(snapshot.GetLineStart(note.StartLine));
            _controls.Editor.EnsureCaretVisible();
        }
    }

    /// <summary>
    /// Adds a note from the pane's input field, anchored to the line the caret
    /// is on.
    ///
    /// The caret rather than a separate "pick a line" gesture: the reviewer is
    /// already reading the line they are asking about, and a note-taking step
    /// that first asks where is a note-taking step people skip.
    /// </summary>
    private void OnReviewNoteSubmitted(object? sender, UiEditSubmittedEventArgs e) =>
        _ = InvokeAsync(CodeCommandNames.AddNote);

    /// <summary>
    /// Writes the note in the pane's input field, anchored to the line the caret
    /// is on.
    ///
    /// The caret rather than a separate "pick a line" gesture: the reviewer is
    /// already reading the line they are asking about, and a note-taking step
    /// that first asks where is a note-taking step people skip.
    ///
    /// Every note is a Question. The other three kinds exist in the record
    /// format and are unreachable from the product until the pane can offer a
    /// choice — see the limitations in
    /// <c>docs/architecture/broiler-code-review.md</c>.
    /// </summary>
    private async ValueTask<bool> AddNoteFromInputAsync(CancellationToken cancellationToken)
    {
        if (_review is null || _controls.ReviewNoteInput is not { } input)
        {
            SetStatus("This host has no way to write a review note.");
            return false;
        }

        string text = input.Text;
        if (text.Trim().Length == 0)
        {
            SetStatus("Type the note first.");
            return false;
        }

        ICodeTextSnapshot snapshot = _controls.Editor.Snapshot;
        int line = snapshot.GetLineFromPosition(
            Math.Clamp(_controls.Editor.Selection.Focus, 0, snapshot.Length));

        ReviewActionResult result = await _review
            .AddNoteAsync(ReviewNoteKind.Question, text, line, line, cancellationToken: cancellationToken)
            .ConfigureAwait(true);

        if (result.Succeeded && !input.IsDisposed)
            input.Text = string.Empty;

        SetStatus(result.Message);
        return result.Succeeded;
    }

    private async ValueTask<bool> RecordReviewAsync(ReviewStatus status, CancellationToken cancellationToken)
    {
        if (_review is null)
        {
            SetStatus("This host does not compose the Human Review pane.");
            return false;
        }

        ReviewActionResult result = await _review
            .RecordDecisionAsync(status, cancellationToken).ConfigureAwait(true);

        SetStatus(result.Message);
        return result.Succeeded;
    }

    /// <summary>
    /// Writes the coverage summary to the Output line.
    ///
    /// The counterpart to the Test262 and WPT numbers, computed over the open
    /// workspace rather than the whole platform: this is the shell's answer, and
    /// the CI tool's is the one that covers every component.
    /// </summary>
    private bool ShowReviewCoverage()
    {
        if (_review is null)
        {
            SetStatus("This host does not compose the Human Review pane.");
            return false;
        }

        ReviewCoverageTotals totals = ReviewCoverage.Overall(_review.Snapshot());
        _controls.Output.Text =
            $"Human review: {totals.Verified}/{totals.Total} files verified " +
            $"({totals.FormatPercent(totals.VerifiedPercent)}), " +
            $"{totals.StaleApprovals} modified since review, " +
            $"{totals.Unreviewed} never reviewed, {totals.OpenNotes} open notes.";

        SetStatus("Review coverage written to the Output pane.");
        return true;
    }

    private async ValueTask NavigateAsync(WorkspaceItemId id, ProblemEntry entry)
    {
        if (!await OpenDocumentAsync(id).ConfigureAwait(false))
            return;

        ICodeTextSnapshot snapshot = _controls.Editor.Snapshot;
        if (entry.Line < snapshot.LineCount)
        {
            int position = snapshot.GetLineStart(entry.Line) +
                Math.Min(entry.Column, snapshot.GetLineLength(entry.Line));
            _controls.Editor.Selection = CodeSelection.Caret(position);
            _controls.Editor.EnsureCaretVisible();
        }
    }

    private async ValueTask<bool> SaveActiveAsync(CancellationToken cancellationToken)
    {
        if (_workspace is null || _coordinator is null)
            return false;

        SaveOutcome outcome = await _workspace
            .SaveDocumentAsync(_coordinator.ActiveDocument, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // A document that has never been saved has nowhere to go, so Save
        // becomes Save As. Reporting "the save failed" for a brand-new file
        // would be both wrong and unactionable.
        if (outcome.Kind == SaveOutcomeKind.NeedsLocation)
            return await SaveActiveAsAsync(cancellationToken).ConfigureAwait(false);

        SetStatus(outcome.Kind switch
        {
            SaveOutcomeKind.Saved => $"Saved {outcome.RelativePath}",
            SaveOutcomeKind.NotDirty => "No changes to save.",
            SaveOutcomeKind.Conflict => $"{outcome.RelativePath} changed on disk since it was opened.",
            _ => outcome.Message ?? "The save failed.",
        });
        return outcome.Succeeded;
    }

    private async ValueTask<bool> SaveAllAsync(CancellationToken cancellationToken)
    {
        if (_coordinator is null)
            return false;

        SaveAllReport report = await _coordinator.SaveAllAsync(cancellationToken).ConfigureAwait(false);

        // A never-saved document is not a failure, it is a question. Each is
        // brought forward and asked about in turn, so Save All does not quietly
        // skip the documents most likely to be lost.
        var unsaved = new List<string>();
        int saved = report.SavedCount;

        foreach (SaveOutcome outcome in report.Outcomes)
        {
            if (outcome.Succeeded)
                continue;

            if (outcome.Kind != SaveOutcomeKind.NeedsLocation)
            {
                unsaved.Add(outcome.RelativePath);
                continue;
            }

            bool located =
                await _coordinator.OpenAsync(outcome.Id, cancellationToken).ConfigureAwait(false) &&
                await SaveActiveAsAsync(cancellationToken).ConfigureAwait(false);

            if (located)
                saved++;
            else
                unsaved.Add(outcome.RelativePath);
        }

        // Partial failure is named, not summarised away: the user needs to know
        // which files are still unsaved.
        SetStatus(unsaved.Count == 0
            ? $"Saved {saved} documents."
            : $"Saved {saved}; not saved: {string.Join(", ", unsaved)}");
        return unsaved.Count == 0;
    }

    private void ShowWorkspaceDiagnostics()
    {
        if (_workspace is null)
            return;

        _problems.SetProjectEntries(_workspace.Diagnostics.Select(diagnostic =>
            ProblemsModel.ProjectEntry(
                diagnostic.RelativePath ?? "workspace", diagnostic.Code, diagnostic.Message)));
        _problemsSource.Refresh();
        _controls.Problems.Refresh();
        RefreshStatusCounts();
    }

    private void RefreshStatusCounts() =>
        SetStatus(_problems.Counts.Describe(_problems.Mode));

    private void SetStatus(string text)
    {
        if (!_controls.Status.IsDisposed)
            _controls.Status.Text = text;
    }

    private void DetachWorkspace()
    {
        _coordinator?.Dispose();
        _coordinator = null;
        _explorerSource?.Dispose();
        _explorerSource = null;

        // Cancelled before the controller is disposed, so an in-flight load stops
        // at its next read instead of running to completion against a workspace
        // that has gone.
        _reviewLoad?.Cancel();
        _reviewLoad?.Dispose();
        _reviewLoad = null;

        if (_review is not null)
            _review.Changed -= OnReviewChanged;
        _reviewSource?.Dispose();
        _reviewSource = null;
        _review?.Dispose();
        _review = null;

        if (_controls.Review is { IsDisposed: false } review)
            review.DataSource = null;

        // A host may dispose its session — and with it the whole element tree —
        // before the shell. Detaching then has nothing left to detach from, and
        // throwing out of Dispose over it would turn an ordering detail into a
        // crash on the way out.
        if (!_controls.Explorer.IsDisposed)
            _controls.Explorer.DataSource = null;

        _workspace = null;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}

public sealed class CodeCommandEventArgs(string name, bool handled) : EventArgs
{
    public string Name { get; } = name;

    /// <summary>
    /// False when the shell raised the intent but did not act — New and Open
    /// need a picker, which belongs to the host.
    /// </summary>
    public bool Handled { get; } = handled;
}
