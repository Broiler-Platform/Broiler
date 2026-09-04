using System;
using System.Collections.Generic;
using System.Linq;

namespace Broiler.Code.Core.Shell;

/// <summary>
/// The commands the shell exposes. Named rather than anonymous handlers so the
/// menu, the toolbar, and a keyboard shortcut all drive the same thing and
/// report the same enabled state.
/// </summary>
public static class CodeCommandNames
{
    public const string New = "code.new";
    public const string NewProject = "code.newProject";
    public const string Open = "code.open";
    public const string Save = "code.save";
    public const string SaveAs = "code.saveAs";
    public const string SaveAll = "code.saveAll";
    public const string Close = "code.close";
    public const string Build = "code.build";
    public const string Rebuild = "code.rebuild";
    public const string Cancel = "code.cancel";

    // Human Review. Named commands like every other action, so the menu, a
    // keyboard shortcut, and a later toolbar button all drive one path and
    // report one enabled state — which for these matters more than usual,
    // because "why is Mark Reviewed greyed out?" has a specific answer the
    // command set is what produces.
    public const string MarkReviewed = "code.review.reviewed";
    public const string MarkInReview = "code.review.inReview";
    public const string MarkQuestion = "code.review.question";
    public const string MarkNeedsChange = "code.review.needsChange";
    public const string ClearReview = "code.review.clear";
    public const string AddNote = "code.review.addNote";
    public const string ReviewCoverage = "code.review.coverage";
}

public enum CommandAvailability
{
    /// <summary>Runnable now.</summary>
    Enabled,

    /// <summary>Exists, but not applicable to the current state.</summary>
    Disabled,

    /// <summary>
    /// Not implemented on this host or in this phase. Distinguished from
    /// Disabled so the UI can say why rather than showing a dead control the
    /// user keeps trying.
    /// </summary>
    Unavailable,
}

public sealed record CodeCommand(
    string Name,
    string Text,
    CommandAvailability Availability,
    string? Reason = null,
    char? AccessKey = null)
{
    public bool IsEnabled => Availability == CommandAvailability.Enabled;
}

/// <summary>
/// Command state for the shell, recomputed from the workspace rather than
/// tracked by hand — a flag that has to be updated in five places is a flag
/// that will be wrong in one of them.
/// </summary>
public sealed class CodeCommandSet
{
    private readonly Func<Workspaces.CodeWorkspace?> _workspace;
    private readonly Func<Workspaces.Model.WorkspaceItemId> _activeDocument;

    public CodeCommandSet(
        Func<Workspaces.CodeWorkspace?> workspace,
        Func<Workspaces.Model.WorkspaceItemId> activeDocument)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _activeDocument = activeDocument ?? throw new ArgumentNullException(nameof(activeDocument));
    }

    /// <summary>
    /// Set once a build service exists. Until Phase 4 lands one, the build
    /// commands report Unavailable with a reason rather than pretending.
    /// </summary>
    public bool HasBuildService { get; set; }

    public bool IsBuildRunning { get; set; }

    /// <summary>
    /// Set when the host can ask the user for a file. Without it Open and Save
    /// As are Unavailable with a reason — a menu entry that opens no dialog and
    /// reports nothing is worse than one that says the host cannot ask.
    /// </summary>
    public bool HasFileDialogs { get; set; }

    /// <summary>
    /// Set when the head composed a review pane. Without it the review commands
    /// report Unavailable with a reason, in keeping with the rest of this set:
    /// a menu entry that silently does nothing is worse than one that says the
    /// host does not carry the feature.
    /// </summary>
    public bool HasReview { get; set; }

    /// <summary>Set once the host knows who is reviewing. An approval with no name is not evidence.</summary>
    public bool HasReviewer { get; set; }

    public IReadOnlyList<CodeCommand> GetCommands()
    {
        Workspaces.CodeWorkspace? workspace = _workspace();
        bool hasWorkspace = workspace is not null;
        Workspaces.Model.WorkspaceItemId active = _activeDocument();
        bool hasDocument = !active.IsNone;
        bool dirty = workspace?.HasUnsavedChanges ?? false;

        // Asked of the buffer here rather than cached in a property the shell
        // refreshes. A keystroke makes a document dirty without going anywhere
        // near the command set, so a flag would say "clean" until something else
        // happened to refresh it — and the four commands that must never run on
        // a dirty document would be enabled exactly when it matters.
        bool activeIsDirty =
            hasDocument && workspace?.FindOpenDocument(active) is { IsDirty: true };

        return
        [
            // New needs somewhere to put the document, which is the workspace,
            // not a file dialog: an untitled buffer is created in memory and
            // only asks for a location when it is saved.
            new CodeCommand(
                CodeCommandNames.New, "New File",
                hasWorkspace ? CommandAvailability.Enabled : CommandAvailability.Disabled,
                AccessKey: 'N'),

            // A project, unlike a file, has to exist on storage the moment it is
            // created — a .csproj nobody can find is not a project — so this one
            // does need somewhere to put it before it can do anything.
            Picker(CodeCommandNames.NewProject, "New Project…", 'P'),
            Picker(CodeCommandNames.Open, "Open…", 'O'),
            new CodeCommand(
                CodeCommandNames.Save, "Save",
                hasDocument ? CommandAvailability.Enabled : CommandAvailability.Disabled,
                AccessKey: 'S'),
            HasFileDialogs
                ? new CodeCommand(
                    CodeCommandNames.SaveAs, "Save As…",
                    hasDocument ? CommandAvailability.Enabled : CommandAvailability.Disabled,
                    AccessKey: 'A')
                : Picker(CodeCommandNames.SaveAs, "Save As…", 'A'),
            new CodeCommand(
                CodeCommandNames.SaveAll, "Save All",
                dirty ? CommandAvailability.Enabled : CommandAvailability.Disabled,
                AccessKey: 'L'),
            new CodeCommand(
                CodeCommandNames.Close, "Close Document",
                hasDocument ? CommandAvailability.Enabled : CommandAvailability.Disabled,
                AccessKey: 'C'),
            Build(CodeCommandNames.Build, "Build", hasWorkspace, 'B'),
            Build(CodeCommandNames.Rebuild, "Rebuild", hasWorkspace, 'R'),
            new CodeCommand(
                CodeCommandNames.Cancel, "Cancel Build",
                IsBuildRunning ? CommandAvailability.Enabled : CommandAvailability.Disabled,
                AccessKey: 'X'),

            Review(CodeCommandNames.MarkReviewed, "Mark Reviewed", hasDocument, activeIsDirty, 'R'),
            Review(CodeCommandNames.MarkInReview, "Mark In Review", hasDocument, activeIsDirty, 'I'),
            Review(CodeCommandNames.MarkQuestion, "Mark Open Question", hasDocument, activeIsDirty, 'Q'),
            Review(CodeCommandNames.MarkNeedsChange, "Mark Needs Change", hasDocument, activeIsDirty, 'N'),

            // Clearing is allowed on a dirty document, and is the only review
            // command that is. It records nothing about content, so there is no
            // content for it to be wrong about — and a reviewer who marked the
            // wrong file should not have to save it to undo that.
            HasReview
                ? new CodeCommand(
                    CodeCommandNames.ClearReview, "Clear Review",
                    hasDocument ? CommandAvailability.Enabled : CommandAvailability.Disabled,
                    AccessKey: 'C')
                : NoReview(CodeCommandNames.ClearReview, "Clear Review", 'C'),

            // A note is a question, not an attestation, so unlike the four
            // decisions it is allowed on a document with unsaved changes: a
            // reviewer who has to save before writing down what confuses them
            // stops writing it down. It still needs a name and a file.
            HasReview
                ? new CodeCommand(
                    CodeCommandNames.AddNote, "Add Note",
                    hasDocument && HasReviewer
                        ? CommandAvailability.Enabled
                        : CommandAvailability.Disabled,
                    hasDocument
                        ? HasReviewer ? null : "Set a reviewer name first — a note has to say who is asking."
                        : "Open a file to write a note about it.",
                    'T')
                : NoReview(CodeCommandNames.AddNote, "Add Note", 'T'),

            HasReview
                ? new CodeCommand(
                    CodeCommandNames.ReviewCoverage, "Review Coverage",
                    hasWorkspace ? CommandAvailability.Enabled : CommandAvailability.Disabled,
                    AccessKey: 'V')
                : NoReview(CodeCommandNames.ReviewCoverage, "Review Coverage", 'V'),
        ];
    }

    public CodeCommand? Find(string name) =>
        GetCommands().FirstOrDefault(command => command.Name == name);

    private CodeCommand Picker(string name, string text, char accessKey) => HasFileDialogs
        ? new CodeCommand(name, text, CommandAvailability.Enabled, AccessKey: accessKey)
        : new CodeCommand(
            name, text, CommandAvailability.Unavailable,
            "This host has no way to ask for a file.", accessKey);

    /// <summary>
    /// A command that records a review decision.
    ///
    /// Every refusal carries the reason, because each one is a different problem
    /// with a different fix: compose a review pane, name a reviewer, open a file,
    /// or save it. A single greyed-out entry saying none of that is how a feature
    /// meant to be used daily stops being used.
    /// </summary>
    private CodeCommand Review(
        string name, string text, bool hasDocument, bool activeIsDirty, char accessKey)
    {
        if (!HasReview)
            return NoReview(name, text, accessKey);

        if (!HasReviewer)
        {
            return new CodeCommand(
                name, text, CommandAvailability.Disabled,
                "Set a reviewer name first — a review record has to say who made it.", accessKey);
        }

        if (!hasDocument)
            return new CodeCommand(name, text, CommandAvailability.Disabled, "Open a file to review it.", accessKey);

        if (activeIsDirty)
        {
            return new CodeCommand(
                name, text, CommandAvailability.Disabled,
                "Save the file first — a review records the content on disk.", accessKey);
        }

        return new CodeCommand(name, text, CommandAvailability.Enabled, AccessKey: accessKey);
    }

    private static CodeCommand NoReview(string name, string text, char accessKey) =>
        new(name, text, CommandAvailability.Unavailable,
            "This host does not compose the Human Review pane.", accessKey);

    private CodeCommand Build(string name, string text, bool hasWorkspace, char accessKey)
    {
        if (!HasBuildService)
        {
            return new CodeCommand(
                name, text, CommandAvailability.Unavailable,
                "No build service is attached to this host yet.", accessKey);
        }

        if (IsBuildRunning)
        {
            return new CodeCommand(
                name, text, CommandAvailability.Disabled, "A build is already running.", accessKey);
        }

        return new CodeCommand(
            name, text,
            hasWorkspace ? CommandAvailability.Enabled : CommandAvailability.Disabled,
            AccessKey: accessKey);
    }
}
