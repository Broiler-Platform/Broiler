using Broiler.Code.Core.Review;
using Broiler.Code.Core.Shell;
using Broiler.Code.Review;
using Broiler.Code.Workspaces;
using Broiler.Code.Workspaces.Model;
using Broiler.Code.Workspaces.Storage;
using Broiler.Code.Workspaces.Text;
using Broiler.Graphics;
using Broiler.UI;
using Broiler.UI.Button.Standard;
using Broiler.UI.CodeEditor.Standard;
using Broiler.UI.Edit.Standard;
using Broiler.UI.Label.Standard;
using Broiler.UI.Menu;
using Broiler.UI.Menu.Standard;
using Broiler.UI.Panel.Standard;
using Broiler.UI.Splitter.Standard;
using Broiler.UI.TabView.Standard;
using Broiler.UI.Toolbar.Standard;
using Broiler.UI.TreeView;
using Broiler.UI.TreeView.Standard;

namespace Broiler.Code.Core.Tests;

/// <summary>
/// The Human Review workspace as the user meets it: three panes, a status badge
/// on every file, and commands that refuse to record something they cannot
/// stand behind.
///
/// The refusals get as much cover as the successes, because they are what makes
/// the record evidence rather than decoration. A tool that will mark an unsaved
/// file reviewed, or record an approval with nobody's name on it, produces a
/// number that cannot be checked by anyone.
/// </summary>
public sealed class ReviewWorkspaceTests : IDisposable
{
    private const string AlphaSource = "class Alpha { }\n";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "broiler-review-shell", Guid.NewGuid().ToString("n"));

    public ReviewWorkspaceTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "src"));
        File.WriteAllText(Path.Combine(_root, "src", "Alpha.cs"), AlphaSource);
        File.WriteAllText(Path.Combine(_root, "src", "Beta.cs"), "class Beta { }\n");
    }

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
    public void The_Review_Pane_Is_Composed_Beside_The_Editor()
    {
        (CodeShell shell, CodeShellControls controls) = CreateShell();

        Assert.Contains(Flatten(shell.RootElement), element => ReferenceEquals(element, controls.Review));
        Assert.Contains(Flatten(shell.RootElement), element => ReferenceEquals(element, controls.ReviewNoteInput));
        shell.Dispose();
    }

    /// <summary>
    /// A head that composes no review pane must get the shell it had before the
    /// feature existed. That is what lets the Android and browser heads adopt it
    /// when they are ready rather than when this file changed.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Host_Without_A_Review_Pane_Still_Composes()
    {
        (CodeShell shell, _) = CreateShell(withReview: false);
        shell.AttachWorkspace(CreateWorkspace());

        Assert.Null(shell.Review);
        Assert.Equal(
            CommandAvailability.Unavailable,
            shell.Commands.Find(CodeCommandNames.MarkReviewed)!.Availability);

        shell.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void The_Menu_Carries_The_Review_Commands()
    {
        (CodeShell shell, _) = CreateShell();

        UiMenuItem review = Assert.Single(
            ((UiMenu)Flatten(shell.RootElement).First(element => element is UiMenu)).Items,
            item => item.Id == "review");

        Assert.Contains(review.Children, item => item.CommandName == CodeCommandNames.MarkReviewed);
        Assert.Contains(review.Children, item => item.CommandName == CodeCommandNames.MarkNeedsChange);
        Assert.Contains(review.Children, item => item.CommandName == CodeCommandNames.ReviewCoverage);
        shell.Dispose();
    }

    [Fact(Timeout = 600000)]
    public async Task Marking_A_File_Reviewed_Writes_A_Record_And_Badges_The_Explorer()
    {
        (CodeShell shell, CodeShellControls controls) = CreateShell();
        CodeWorkspace workspace = CreateWorkspace();
        shell.AttachWorkspace(workspace);
        await OpenAlphaAsync(shell, workspace);

        Assert.True(await shell.InvokeAsync(CodeCommandNames.MarkReviewed));

        // On disk, committable, and describing the exact content.
        string record = Path.Combine(_root, ".broiler-review", "src", "Alpha.cs.review.json");
        Assert.True(File.Exists(record));
        Assert.Contains("\"status\": \"reviewed\"", await File.ReadAllTextAsync(record), StringComparison.Ordinal);

        Assert.True(shell.Review!.StateFor("src/Alpha.cs").IsVerified);

        // And visible where the reviewer chose the file, not only in the pane.
        Assert.Equal("reviewed", PresentationOf(controls.Explorer, "Alpha.cs").SecondaryLabel);
        shell.Dispose();
    }

    /// <summary>
    /// The behaviour the whole feature turns on, driven end to end: edit the
    /// file after approving it and the explorer says so.
    /// </summary>
    [Fact(Timeout = 600000)]
    public async Task Editing_A_Reviewed_File_Shows_It_As_Stale()
    {
        (CodeShell shell, CodeShellControls controls) = CreateShell();
        CodeWorkspace workspace = CreateWorkspace();
        shell.AttachWorkspace(workspace);
        SourceDocument document = await OpenAlphaAsync(shell, workspace);

        Assert.True(await shell.InvokeAsync(CodeCommandNames.MarkReviewed));
        Assert.True(shell.Review!.StateFor("src/Alpha.cs").IsVerified);

        Append(document, "// added after the review\n");
        shell.Review.SetCurrentDocument(document.Id);

        ReviewState state = shell.Review.StateFor("src/Alpha.cs");
        Assert.True(state.IsStaleApproval);
        Assert.Equal("reviewed, then modified", state.ToDisplayString());
        Assert.Equal(
            TreeNodeDecoration.Warning,
            ReviewPaneSource.DecorationFor(state));

        shell.Dispose();
    }

    /// <summary>
    /// A review names the content a human read. Unsaved text is content nobody
    /// else can fetch, so an approval of it could never be checked — by CI, by a
    /// second reviewer, or by the same reviewer tomorrow.
    /// </summary>
    [Fact(Timeout = 600000)]
    public async Task A_Dirty_Document_Cannot_Be_Marked_Reviewed()
    {
        (CodeShell shell, _) = CreateShell();
        CodeWorkspace workspace = CreateWorkspace();
        shell.AttachWorkspace(workspace);
        SourceDocument document = await OpenAlphaAsync(shell, workspace);

        Append(document, "// unsaved\n");

        // Disabled with the reason, rather than failing after the click.
        CodeCommand command = shell.Commands.Find(CodeCommandNames.MarkReviewed)!;
        Assert.Equal(CommandAvailability.Disabled, command.Availability);
        Assert.Contains("Save the file first", command.Reason!, StringComparison.Ordinal);

        Assert.False(await shell.InvokeAsync(CodeCommandNames.MarkReviewed));
        Assert.False(File.Exists(Path.Combine(_root, ".broiler-review", "src", "Alpha.cs.review.json")));
        shell.Dispose();
    }

    /// <summary>An approval with nobody's name on it is not evidence of a human review.</summary>
    [Fact(Timeout = 600000)]
    public async Task A_Review_Cannot_Be_Recorded_Without_A_Reviewer()
    {
        (CodeShell shell, _) = CreateShell(reviewer: "");
        CodeWorkspace workspace = CreateWorkspace();
        shell.AttachWorkspace(workspace);
        await OpenAlphaAsync(shell, workspace);

        CodeCommand command = shell.Commands.Find(CodeCommandNames.MarkReviewed)!;
        Assert.Equal(CommandAvailability.Disabled, command.Availability);
        Assert.Contains("reviewer name", command.Reason!, StringComparison.Ordinal);
        shell.Dispose();
    }

    /// <summary>
    /// Clearing is the one review command allowed on a dirty document: it
    /// records nothing about content, so there is nothing for it to be wrong
    /// about, and undoing a mistake should not require a save.
    /// </summary>
    [Fact(Timeout = 600000)]
    public async Task Clearing_A_Review_Is_Allowed_While_Editing()
    {
        (CodeShell shell, _) = CreateShell();
        CodeWorkspace workspace = CreateWorkspace();
        shell.AttachWorkspace(workspace);
        SourceDocument document = await OpenAlphaAsync(shell, workspace);

        Assert.True(await shell.InvokeAsync(CodeCommandNames.MarkReviewed));
        Append(document, "// unsaved\n");

        Assert.Equal(
            CommandAvailability.Enabled,
            shell.Commands.Find(CodeCommandNames.ClearReview)!.Availability);
        Assert.True(await shell.InvokeAsync(CodeCommandNames.ClearReview));
        Assert.False(File.Exists(Path.Combine(_root, ".broiler-review", "src", "Alpha.cs.review.json")));
        shell.Dispose();
    }

    /// <summary>
    /// A note is a question, not an attestation, so it is allowed while the
    /// document is dirty. A reviewer who has to save before writing down what
    /// confuses them stops writing it down.
    /// </summary>
    [Fact(Timeout = 600000)]
    public async Task A_Note_Can_Be_Added_While_Editing_And_Shows_In_The_Pane()
    {
        (CodeShell shell, CodeShellControls controls) = CreateShell();
        CodeWorkspace workspace = CreateWorkspace();
        shell.AttachWorkspace(workspace);
        SourceDocument document = await OpenAlphaAsync(shell, workspace);
        Append(document, "// unsaved\n");

        ReviewActionResult result = await shell.Review!
            .AddNoteAsync(ReviewNoteKind.Question, "Why is the conversion done before Get()?", 0, 0);

        Assert.True(result.Succeeded);
        Assert.Equal(1, shell.Review.CurrentReview.OpenNoteCount);

        // The pane shows it, with the question counted as open.
        Assert.Contains(
            RowsOf(controls.Review!),
            row => row.Label.Contains("Why is the conversion", StringComparison.Ordinal));

        shell.Dispose();
    }

    /// <summary>
    /// A file carrying an open question has been looked at even though it is not
    /// approved, and the explorer says so rather than showing it as untouched.
    /// </summary>
    [Fact(Timeout = 600000)]
    public async Task An_Open_Note_Is_Visible_In_The_Explorer()
    {
        (CodeShell shell, CodeShellControls controls) = CreateShell();
        CodeWorkspace workspace = CreateWorkspace();
        shell.AttachWorkspace(workspace);
        await OpenAlphaAsync(shell, workspace);

        await shell.Review!.AddNoteAsync(ReviewNoteKind.Question, "Is this reachable?", 0, 0);

        Assert.Equal(1, shell.Review.StateFor("src/Alpha.cs").OpenNotes);
        Assert.Equal("needs review", PresentationOf(controls.Explorer, "Alpha.cs").SecondaryLabel);
        shell.Dispose();
    }

    [Fact(Timeout = 600000)]
    public async Task Coverage_Counts_Every_Workspace_File_Not_Only_Reviewed_Ones()
    {
        (CodeShell shell, _) = CreateShell();
        CodeWorkspace workspace = CreateWorkspace();
        shell.AttachWorkspace(workspace);
        await OpenAlphaAsync(shell, workspace);
        Assert.True(await shell.InvokeAsync(CodeCommandNames.MarkReviewed));

        ReviewCoverageTotals totals = ReviewCoverage.Overall(shell.Review!.Snapshot());

        // Two files in the workspace, one reviewed. A denominator of "files that
        // have a record" would report 100% by construction.
        Assert.Equal(2, totals.Total);
        Assert.Equal(1, totals.Verified);
        Assert.Equal(50, totals.VerifiedPercent);
        shell.Dispose();
    }

    /// <summary>
    /// The state of a closed file comes from its content on storage, not from
    /// "no document is open, so I cannot tell".
    ///
    /// This is the case a freshly opened workspace is entirely made of: nothing
    /// is open yet, so if a closed record could not be evaluated the explorer
    /// would badge a fully reviewed repository as unknown and Review Coverage
    /// would report 0 %, while the command-line tool reported the truth from the
    /// same records. The two share <see cref="ReviewStateEvaluator"/> so they
    /// cannot disagree about the rule; they must not disagree about the content
    /// either.
    /// </summary>
    [Fact(Timeout = 600000)]
    public async Task A_Reviewed_File_Stays_Reviewed_After_The_Workspace_Is_Reopened()
    {
        (CodeShell first, _) = CreateShell();
        CodeWorkspace workspace = CreateWorkspace();
        first.AttachWorkspace(workspace);
        await OpenAlphaAsync(first, workspace);
        Assert.True(await first.InvokeAsync(CodeCommandNames.MarkReviewed));
        first.Dispose();

        // A second session over the same directory, with nothing open in it.
        (CodeShell second, CodeShellControls controls) = CreateShell();
        CodeWorkspace reopened = CreateWorkspace();
        second.AttachWorkspace(reopened);
        await second.Review!.LoadAsync();

        Assert.True(second.Review.StateFor("src/Alpha.cs").IsVerified);
        Assert.Equal("reviewed", PresentationOf(controls.Explorer, "Alpha.cs").SecondaryLabel);

        ReviewCoverageTotals totals = ReviewCoverage.Overall(second.Review.Snapshot());
        Assert.Equal(1, totals.Verified);
        Assert.Equal(50, totals.VerifiedPercent);
        second.Dispose();
    }

    /// <summary>
    /// A record a merge left conflict markers in is reported on the status line
    /// rather than silently making its file look unreviewed.
    /// </summary>
    [Fact(Timeout = 600000)]
    public async Task An_Unreadable_Record_Is_Reported_Rather_Than_Skipped()
    {
        string broken = Path.Combine(_root, ".broiler-review", "src", "Beta.cs.review.json");
        Directory.CreateDirectory(Path.GetDirectoryName(broken)!);
        await File.WriteAllTextAsync(broken, "not a review record");

        (CodeShell shell, _) = CreateShell();
        shell.AttachWorkspace(CreateWorkspace());
        await shell.Review!.LoadAsync();

        StorageFailure failure = Assert.Single(shell.Review.Unreadable);
        Assert.Contains("src/Beta.cs", failure.Message, StringComparison.Ordinal);
        shell.Dispose();
    }

    private static void Append(SourceDocument document, string text)
    {
        TextSnapshot current = document.Buffer.Current;
        document.Buffer.Apply(new EditTransaction(
            current.Version, TextChange.Insert(current.Length, text), "test edit"));
    }

    private static async Task<SourceDocument> OpenAlphaAsync(CodeShell shell, CodeWorkspace workspace)
    {
        WorkspaceItem item = workspace.FindItem("src/Alpha.cs")!;
        SourceDocument document = (await workspace.OpenDocumentAsync(item.Id)).Value!;
        await shell.Coordinator!.OpenAsync(item.Id);
        shell.Review?.SetCurrentDocument(item.Id);
        return document;
    }

    private static TreeNodePresentation PresentationOf(UiTreeView tree, string label)
    {
        ITreeDataSource source = tree.DataSource!;
        return Descend(source, source.Root)
            .Select(source.GetPresentation)
            .First(presentation => presentation.Label == label);
    }

    private static IReadOnlyList<TreeNodePresentation> RowsOf(UiTreeView tree)
    {
        ITreeDataSource source = tree.DataSource!;
        return [.. Descend(source, source.Root).Select(source.GetPresentation)];
    }

    private static IEnumerable<TreeNodeId> Descend(ITreeDataSource source, TreeNodeId node)
    {
        for (int i = 0; i < source.GetChildCount(node); i++)
        {
            TreeNodeId child = source.GetChild(node, i);
            yield return child;
            foreach (TreeNodeId descendant in Descend(source, child))
                yield return descendant;
        }
    }

    private static IEnumerable<UiElement> Flatten(UiElement element)
    {
        yield return element;
        foreach (UiElement child in element.Children)
        {
            foreach (UiElement descendant in Flatten(child))
                yield return descendant;
        }
    }

    /// <summary>
    /// A workspace with a solution and a project, not only loose items. The
    /// explorer only builds file rows underneath a project, so a badge on a file
    /// row cannot be asserted without one.
    /// </summary>
    private CodeWorkspace CreateWorkspace()
    {
        var workspace = new CodeWorkspace(new FileSystemWorkspaceStorage(_root));
        var project = new CodeProject
        {
            Id = new WorkspaceItemId(1000),
            Name = "Sample",
            RelativePath = "src/Sample.csproj",
            TargetFrameworks = ["net10.0"],
            Items =
            [
                workspace.AddItem("src/Alpha.cs", WorkspaceItemKind.SourceDocument).Id,
                workspace.AddItem("src/Beta.cs", WorkspaceItemKind.SourceDocument).Id,
            ],
        };

        workspace.AddProject(project);
        workspace.AddSolution(new CodeSolution
        {
            Id = new WorkspaceItemId(1001),
            Name = "Sample",
            RelativePath = "Sample.slnx",
            Projects = [project.Id],
        });

        return workspace;
    }

    private static (CodeShell Shell, CodeShellControls Controls) CreateShell(
        bool withReview = true, string reviewer = "Enrico")
    {
        var controls = new CodeShellControls
        {
            Root = new StandardPanel(),
            Body = new StandardPanel(),
            DocumentArea = new StandardPanel(),
            Menu = new StandardMenu(),
            Toolbar = new StandardToolbar(),
            Explorer = new StandardTreeView(),
            ExplorerSplitter = new StandardSplitter(),
            Tabs = new StandardTabView(),
            Editor = new StandardCodeEditor { PreferredSize = new BSize(900, 600) },
            Problems = new StandardTreeView(),
            ReviewPane = withReview ? new StandardPanel() : null,
            Review = withReview ? new StandardTreeView() : null,
            ReviewSplitter = withReview ? new StandardSplitter() : null,
            ReviewNoteInput = withReview ? new StandardEdit() : null,
            Status = new StandardLabel(),
            Output = new StandardLabel(),
            CreateButton = () => new StandardButton(),
        };

        return (new CodeShell(controls) { Reviewer = reviewer }, controls);
    }
}
