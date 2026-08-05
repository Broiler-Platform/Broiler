using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Broiler.Code.Core.Diagnostics;
using Broiler.Code.Core.Templates;
using Broiler.Code.Workspaces;
using Broiler.Code.Workspaces.Model;
using Broiler.Code.Workspaces.Storage;
using Broiler.Graphics;
using Broiler.UI;
using Broiler.UI.Button;
using Broiler.UI.CodeEditor;
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
        };

        // Expand to the projects so the tree opens showing something, rather
        // than one collapsed row the user has to discover.
        foreach (TreeRow row in _controls.Explorer.Rows.ToArray())
            _controls.Explorer.Expand(row.Id);

        RefreshCommands();
        SetStatus($"{workspace.Projects.Count} projects, {workspace.Items.Count} items");
        ShowWorkspaceDiagnostics();
    }

    /// <summary>
    /// Asked before a dirty document closes. A host that sets nothing gets
    /// Cancel, because losing someone's work is not a default.
    /// </summary>
    public DirtyClosePrompt? DirtyClosePrompt { get; set; }

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

        body.AddChild(documents);
        body.SetDock(documents, UiDock.Fill);
        root.AddChild(body);
        root.SetDock(body, UiDock.Fill);

        _controls.Problems.DataSource = _problemsSource;
        _controls.ExplorerSplitter.Orientation = UiSplitterOrientation.Vertical;
        ComposeToolbar();
    }

    private void RefreshCommands()
    {
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
