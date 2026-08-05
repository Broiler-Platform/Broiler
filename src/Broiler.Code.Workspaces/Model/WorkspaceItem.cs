using System;
using System.Collections.Generic;

namespace Broiler.Code.Workspaces.Model;

public enum WorkspaceItemKind
{
    /// <summary>Compiled source.</summary>
    SourceDocument,

    /// <summary>A project file, itself editable and itself a build input.</summary>
    ProjectFile,

    /// <summary>An embedded resource.</summary>
    Resource,

    /// <summary>Non-code content copied to output.</summary>
    Content,

    /// <summary>global.json, NuGet.config, Directory.Build.props, and the like.</summary>
    Configuration,

    /// <summary>A folder node, which carries no bytes of its own.</summary>
    Folder,
}

/// <summary>
/// How a file's line endings are handled on save. The declared model preserves
/// what a file already had; it does not impose a convention on a file it did
/// not create.
/// </summary>
public enum LineEndingPolicy
{
    /// <summary>Keep whatever the file used when it was read.</summary>
    Preserve,

    Lf,
    CrLf,
}

/// <summary>
/// Text encoding as read, so a save writes the file back the way it was found.
/// Silently rewriting a UTF-8 file without its BOM, or a Latin-1 file as UTF-8,
/// is a data-losing change disguised as a save.
/// </summary>
public readonly record struct TextEncodingInfo(string WebName, bool HasByteOrderMark)
{
    public static TextEncodingInfo Utf8NoBom => new("utf-8", false);
}

/// <summary>
/// One item in the workspace. Immutable: a change produces a new instance, and
/// the workspace snapshot that holds it is replaced wholesale.
/// </summary>
public sealed record WorkspaceItem
{
    public required WorkspaceItemId Id { get; init; }

    public required WorkspaceItemKind Kind { get; init; }

    /// <summary>
    /// Where the bytes actually are, relative to the workspace root, using
    /// forward slashes. This is the item's identity: a file linked into two
    /// projects is one item with one buffer, because two buffers over one file
    /// would disagree about whether it is dirty.
    /// </summary>
    public required string RelativePath { get; init; }

    /// <summary>
    /// The project that declares this item, or None for solution-level files.
    /// </summary>
    public WorkspaceItemId OwningProject { get; init; } = WorkspaceItemId.None;

    /// <summary>
    /// True when the item reaches its project through a linked
    /// <c>&lt;Compile Include=".." Link=".." /&gt;</c> rather than by living
    /// under the project directory.
    /// </summary>
    public bool IsLinked { get; init; }

    /// <summary>
    /// Where the owning project displays a linked item, relative to the
    /// workspace root. Presentation only — <see cref="RelativePath"/> is where
    /// the bytes are, and is what identity, reads, and writes use.
    /// </summary>
    public string? LinkedAs { get; init; }

    public TextEncodingInfo Encoding { get; init; } = TextEncodingInfo.Utf8NoBom;

    public LineEndingPolicy LineEndings { get; init; } = LineEndingPolicy.Preserve;

    /// <summary>
    /// The provider's opaque token for the version on disk when the item was
    /// last read or written. Comparing it before a save is what turns "someone
    /// else changed this file" into a conflict the user is asked about instead
    /// of an overwrite they find out about later.
    /// </summary>
    public string? ExternalRevision { get; init; }

    /// <summary>
    /// A durable provider file identity, when the provider has one. Without it,
    /// an external rename cannot be matched back to this item and the rename
    /// is reported as a remove and an add.
    /// </summary>
    public string? DurableFileId { get; init; }

    /// <summary>
    /// True when the declared model could not fully represent this item, so it
    /// is readable but not editable. The reason is carried by the workspace's
    /// diagnostics rather than by a bool, because the user has to be told
    /// which construct is responsible.
    /// </summary>
    public bool IsReadOnlyDeclaration { get; init; }

    /// <summary>
    /// True for a document that has never been written anywhere.
    /// <see cref="RelativePath"/> is then a display name rather than a location,
    /// and a plain Save has nowhere to go — it has to become a Save As. This is
    /// what lets an editor open with something editable before the user has
    /// chosen a file, instead of a read-only surface that swallows keystrokes.
    /// </summary>
    public bool IsUntitled { get; init; }

    public string Name
    {
        get
        {
            int slash = RelativePath.LastIndexOf('/');
            return slash < 0 ? RelativePath : RelativePath[(slash + 1)..];
        }
    }
}

/// <summary>
/// A source document's open state: the buffer, its dirty overlay, and the
/// per-document view state that has to survive a tab switch.
/// </summary>
public sealed class SourceDocument
{
    internal SourceDocument(WorkspaceItem item, Text.SourceBuffer buffer)
    {
        Item = item;
        Buffer = buffer;
    }

    public WorkspaceItem Item { get; internal set; }

    /// <summary>The sole authority for this document's text and undo history.</summary>
    public Text.SourceBuffer Buffer { get; }

    public WorkspaceItemId Id => Item.Id;

    public bool IsDirty => Buffer.IsDirty;

    /// <summary>
    /// Caret, selection, and scroll, kept by the workspace rather than by the
    /// editor control. A tab switch disposes the control's view state; the user
    /// expects to come back to the line they left.
    /// </summary>
    public DocumentViewState ViewState { get; set; } = DocumentViewState.Default;
}

/// <summary>Per-document view state, preserved across tab switches.</summary>
public readonly record struct DocumentViewState(
    int SelectionAnchor,
    int SelectionFocus,
    int FirstVisibleLine,
    double HorizontalOffset)
{
    public static DocumentViewState Default => default;
}

/// <summary>
/// A project as declared, before any trusted evaluation. Everything here was
/// read from the project file; nothing was resolved by running MSBuild.
/// </summary>
public sealed record CodeProject
{
    public required WorkspaceItemId Id { get; init; }

    public required string Name { get; init; }

    /// <summary>The .csproj itself, relative to the workspace root.</summary>
    public required string RelativePath { get; init; }

    /// <summary>Declared target frameworks, verbatim and in declared order.</summary>
    public IReadOnlyList<string> TargetFrameworks { get; init; } = [];

    /// <summary>Project references by workspace-relative path, as declared.</summary>
    public IReadOnlyList<string> ProjectReferences { get; init; } = [];

    public IReadOnlyList<WorkspaceItemId> Items { get; init; } = [];

    /// <summary>
    /// True when the project contains a construct the declared model can read
    /// but not safely edit. Editing is refused until a trusted evaluation
    /// supplies a target-specific graph.
    /// </summary>
    public bool HasUnsupportedConstructs { get; init; }
}

/// <summary>A solution as declared, with its folder structure preserved.</summary>
public sealed record CodeSolution
{
    public required WorkspaceItemId Id { get; init; }

    public required string Name { get; init; }

    public required string RelativePath { get; init; }

    public IReadOnlyList<WorkspaceItemId> Projects { get; init; } = [];

    /// <summary>Solution folders by path, e.g. "/src/". Preserved on save.</summary>
    public IReadOnlyList<string> Folders { get; init; } = [];

    /// <summary>Solution-level files, such as global.json.</summary>
    public IReadOnlyList<WorkspaceItemId> SolutionItems { get; init; } = [];
}
