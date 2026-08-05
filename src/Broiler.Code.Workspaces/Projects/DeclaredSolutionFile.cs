using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Broiler.Code.Workspaces.Projects;

public sealed record SolutionProjectEntry(
    string RelativePath,
    string Name,
    string? Folder,
    string? Guid);

/// <summary>
/// A solution file, in either supported format.
///
/// Both formats are read; both round-trip byte-identically when unchanged. The
/// classic <c>.sln</c> keeps its project GUIDs exactly: regenerating one
/// silently detaches per-user state and source-control history from the
/// project, and nothing about the file tells you that happened.
/// </summary>
public sealed class DeclaredSolutionFile
{
    private static readonly Regex SlnProjectPattern = new(
        """^Project\("\{(?<type>[^}]+)\}"\)\s*=\s*"(?<name>[^"]*)",\s*"(?<path>[^"]*)",\s*"\{(?<guid>[^}]+)\}"\s*$""",
        RegexOptions.Multiline | RegexOptions.ExplicitCapture | RegexOptions.Compiled);

    /// <summary>The GUID classic .sln uses for a solution folder rather than a project.</summary>
    private const string SolutionFolderTypeGuid = "2150E333-8FDC-42A3-9474-1A3956D46DE1";

    private readonly string _text;

    private DeclaredSolutionFile(
        string relativePath,
        string text,
        SolutionFormat format,
        IReadOnlyList<SolutionProjectEntry> projects,
        IReadOnlyList<string> folders,
        IReadOnlyList<string> solutionItems)
    {
        RelativePath = relativePath;
        _text = text;
        Format = format;
        Projects = projects;
        Folders = folders;
        SolutionItems = solutionItems;
    }

    public enum SolutionFormat
    {
        Slnx,
        Sln,
    }

    public string RelativePath { get; }

    public SolutionFormat Format { get; }

    public IReadOnlyList<SolutionProjectEntry> Projects { get; }

    public IReadOnlyList<string> Folders { get; }

    public IReadOnlyList<string> SolutionItems { get; }

    public override string ToString() => _text;

    public static DeclaredSolutionFile Parse(string relativePath, string text)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        ArgumentNullException.ThrowIfNull(text);

        return relativePath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)
            ? ParseSlnx(relativePath, text)
            : ParseSln(relativePath, text);
    }

    private static DeclaredSolutionFile ParseSlnx(string relativePath, string text)
    {
        XDocument document = XDocument.Parse(text, LoadOptions.PreserveWhitespace);
        var projects = new List<SolutionProjectEntry>();
        var folders = new List<string>();
        var items = new List<string>();

        foreach (XElement element in document.Descendants())
        {
            switch (element.Name.LocalName)
            {
                case "Folder":
                    if ((string?)element.Attribute("Name") is { Length: > 0 } name)
                        folders.Add(name);
                    break;

                case "Project":
                    if ((string?)element.Attribute("Path") is { Length: > 0 } path)
                    {
                        string normalized = path.Replace('\\', '/');
                        projects.Add(new SolutionProjectEntry(
                            normalized,
                            NameFromPath(normalized),
                            FolderOf(element),
                            Guid: null));
                    }

                    break;

                case "File":
                    if ((string?)element.Attribute("Path") is { Length: > 0 } file)
                        items.Add(file.Replace('\\', '/'));
                    break;
            }
        }

        return new DeclaredSolutionFile(
            relativePath, text, SolutionFormat.Slnx, projects, folders, items);
    }

    private static DeclaredSolutionFile ParseSln(string relativePath, string text)
    {
        var projects = new List<SolutionProjectEntry>();
        var folders = new List<string>();

        foreach (Match match in SlnProjectPattern.Matches(text))
        {
            string type = match.Groups["type"].Value;
            string name = match.Groups["name"].Value;
            string path = match.Groups["path"].Value.Replace('\\', '/');
            string guid = match.Groups["guid"].Value;

            // A solution folder is a Project entry with a reserved type GUID and
            // a path equal to its name. Treating it as a project would put a
            // folder in the build graph.
            if (string.Equals(type, SolutionFolderTypeGuid, StringComparison.OrdinalIgnoreCase))
            {
                folders.Add(name);
                continue;
            }

            projects.Add(new SolutionProjectEntry(path, name, Folder: null, guid));
        }

        return new DeclaredSolutionFile(
            relativePath, text, SolutionFormat.Sln, projects, folders, []);
    }

    private static string? FolderOf(XElement project)
    {
        XElement? parent = project.Parent;
        return parent?.Name.LocalName == "Folder" ? (string?)parent.Attribute("Name") : null;
    }

    private static string NameFromPath(string path)
    {
        int slash = path.LastIndexOf('/');
        string file = slash < 0 ? path : path[(slash + 1)..];
        int dot = file.LastIndexOf('.');
        return dot < 0 ? file : file[..dot];
    }

    /// <summary>
    /// A stable display order: by folder then by name, using an ordinal
    /// comparison so the tree looks the same on every platform.
    /// </summary>
    public IReadOnlyList<SolutionProjectEntry> InDisplayOrder() =>
    [
        .. Projects
            .OrderBy(entry => entry.Folder ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(entry => entry.Name, StringComparer.Ordinal),
    ];

    public string Describe() => string.Create(
        CultureInfo.InvariantCulture,
        $"{Format} solution with {Projects.Count} projects and {Folders.Count} folders");
}
