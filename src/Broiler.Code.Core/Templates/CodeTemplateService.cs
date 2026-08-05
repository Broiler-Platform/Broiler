using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Broiler.Code.Workspaces.Model;
using Broiler.Code.Workspaces.Storage;

namespace Broiler.Code.Core.Templates;

public enum ProjectTemplateKind
{
    ConsoleApplication,
    ClassLibrary,
}

public sealed record TemplateFile(string RelativePath, string Content);

public sealed record TemplateResult(
    bool Succeeded,
    IReadOnlyList<TemplateFile> Files,
    string? Message = null)
{
    public static TemplateResult Fail(string message) => new(false, [], message);
}

/// <summary>
/// Creates solutions, projects, and source files.
///
/// Everything it emits is a standard SDK file that the .NET CLI would accept and
/// a person would recognise. It writes no Broiler-specific metadata, no marker
/// file, and no property that only this IDE understands: a workspace created
/// here has to remain a workspace anyone can open with any tool, including
/// after Broiler Code is uninstalled.
/// </summary>
public sealed class CodeTemplateService
{
    private readonly IWorkspaceStorage _storage;

    public CodeTemplateService(IWorkspaceStorage storage) =>
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));

    /// <summary>
    /// The files a new solution consists of, without writing them, so a caller
    /// can preview or test the output.
    /// </summary>
    public static TemplateResult PlanSolution(string solutionName, IReadOnlyList<string> projectPaths)
    {
        if (!IsValidIdentifier(solutionName))
            return TemplateResult.Fail($"'{solutionName}' is not a usable solution name.");

        var builder = new System.Text.StringBuilder();
        builder.Append("<Solution>\n");
        builder.Append("  <Configurations>\n");
        builder.Append("    <BuildType Name=\"Debug\" />\n");
        builder.Append("    <BuildType Name=\"Release\" />\n");
        builder.Append("  </Configurations>\n");
        if (projectPaths.Count > 0)
        {
            builder.Append("  <Folder Name=\"/src/\">\n");
            foreach (string path in projectPaths)
                builder.Append("    <Project Path=\"").Append(Escape(path)).Append("\" />\n");
            builder.Append("  </Folder>\n");
        }

        builder.Append("</Solution>\n");

        return new TemplateResult(true, [new TemplateFile($"{solutionName}.slnx", builder.ToString())]);
    }

    public static TemplateResult PlanProject(
        string projectName,
        ProjectTemplateKind kind,
        string targetFramework = "net10.0",
        IReadOnlyList<string>? projectReferences = null)
    {
        if (!IsValidIdentifier(projectName))
            return TemplateResult.Fail($"'{projectName}' is not a usable project name.");

        var files = new List<TemplateFile>();
        string directory = $"src/{projectName}";

        var project = new System.Text.StringBuilder();
        project.Append("<Project Sdk=\"Microsoft.NET.Sdk\">\n\n");
        project.Append("  <PropertyGroup>\n");
        if (kind == ProjectTemplateKind.ConsoleApplication)
            project.Append("    <OutputType>Exe</OutputType>\n");
        project.Append("    <TargetFramework>").Append(Escape(targetFramework)).Append("</TargetFramework>\n");
        project.Append("    <ImplicitUsings>enable</ImplicitUsings>\n");
        project.Append("    <Nullable>enable</Nullable>\n");
        project.Append("  </PropertyGroup>\n");

        if (projectReferences is { Count: > 0 })
        {
            project.Append("\n  <ItemGroup>\n");
            foreach (string reference in projectReferences)
            {
                project.Append("    <ProjectReference Include=\"")
                    .Append(Escape(reference.Replace('/', '\\')))
                    .Append("\" />\n");
            }

            project.Append("  </ItemGroup>\n");
        }

        project.Append("\n</Project>\n");
        files.Add(new TemplateFile($"{directory}/{projectName}.csproj", project.ToString()));

        files.Add(kind == ProjectTemplateKind.ConsoleApplication
            ? new TemplateFile(
                $"{directory}/Program.cs",
                "Console.WriteLine(\"Hello from " + projectName + "\");\n")
            : new TemplateFile(
                $"{directory}/Class1.cs",
                $"namespace {projectName};\n\npublic class Class1\n{{\n}}\n"));

        return new TemplateResult(true, files);
    }

    public static TemplateResult PlanSourceFile(string relativePath, string? namespaceName, string typeName)
    {
        if (!IsValidIdentifier(typeName))
            return TemplateResult.Fail($"'{typeName}' is not a usable type name.");

        string body = namespaceName is { Length: > 0 }
            ? $"namespace {namespaceName};\n\npublic class {typeName}\n{{\n}}\n"
            : $"public class {typeName}\n{{\n}}\n";
        return new TemplateResult(true, [new TemplateFile(relativePath, body)]);
    }

    /// <summary>
    /// Writes a plan. Refuses to overwrite: a template that clobbers an
    /// existing file turns "new project" into data loss, and the caller has
    /// more context than this does about what the user meant.
    /// </summary>
    public async ValueTask<TemplateResult> WriteAsync(
        TemplateResult plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.Succeeded)
            return plan;

        foreach (TemplateFile file in plan.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StorageResult<StorageEntry> existing = await _storage
                .StatAsync(file.RelativePath, cancellationToken).ConfigureAwait(false);
            if (existing.Succeeded)
                return TemplateResult.Fail($"'{file.RelativePath}' already exists.");
        }

        foreach (TemplateFile file in plan.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StorageResult<string> write = await _storage.WriteTextAsync(
                file.RelativePath,
                file.Content,
                TextEncodingInfo.Utf8NoBom,
                expectedRevision: null,
                cancellationToken).ConfigureAwait(false);
            if (!write.Succeeded)
                return TemplateResult.Fail($"'{file.RelativePath}': {write.Failure!.Message}");
        }

        return plan;
    }

    /// <summary>
    /// Adds a project reference to an existing project, going through the
    /// lossless provider so the rest of the file is untouched.
    /// </summary>
    public async ValueTask<TemplateResult> AddProjectReferenceAsync(
        string projectRelativePath, string referenceInclude, CancellationToken cancellationToken = default)
    {
        StorageResult<StorageTextContent> read = await _storage
            .ReadTextAsync(projectRelativePath, cancellationToken).ConfigureAwait(false);
        if (!read.Succeeded)
            return TemplateResult.Fail(read.Failure!.Message);

        Workspaces.Projects.DeclaredProjectFile project =
            Workspaces.Projects.DeclaredProjectFile.Parse(projectRelativePath, read.Value!.Text);
        Workspaces.Projects.ProjectEditResult edit = project.AddProjectReference(referenceInclude);
        if (!edit.Accepted)
            return TemplateResult.Fail($"{edit.Code}: {edit.Message}");

        StorageResult<string> write = await _storage.WriteTextAsync(
            projectRelativePath,
            edit.NewText!,
            read.Value.Encoding,
            read.Value.ExternalRevision,
            cancellationToken).ConfigureAwait(false);

        return write.Succeeded
            ? new TemplateResult(true, [new TemplateFile(projectRelativePath, edit.NewText!)])
            : TemplateResult.Fail(write.Failure!.Message);
    }

    private static bool IsValidIdentifier(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        // Conservative on purpose: the name becomes a directory, a file name,
        // an assembly name, and a namespace, and the intersection of what all
        // four accept is narrower than any one of them.
        foreach (char c in name)
        {
            if (!char.IsLetterOrDigit(c) && c != '_' && c != '.')
                return false;
        }

        return char.IsLetter(name[0]) || name[0] == '_';
    }

    private static string Escape(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal);
}
