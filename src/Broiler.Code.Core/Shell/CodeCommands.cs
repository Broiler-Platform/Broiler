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

    public IReadOnlyList<CodeCommand> GetCommands()
    {
        Workspaces.CodeWorkspace? workspace = _workspace();
        bool hasWorkspace = workspace is not null;
        bool hasDocument = !_activeDocument().IsNone;
        bool dirty = workspace?.HasUnsavedChanges ?? false;

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
        ];
    }

    public CodeCommand? Find(string name) =>
        GetCommands().FirstOrDefault(command => command.Name == name);

    private CodeCommand Picker(string name, string text, char accessKey) => HasFileDialogs
        ? new CodeCommand(name, text, CommandAvailability.Enabled, AccessKey: accessKey)
        : new CodeCommand(
            name, text, CommandAvailability.Unavailable,
            "This host has no way to ask for a file.", accessKey);

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
