using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Broiler.Code.Language.CSharp.Roslyn;

/// <summary>
/// Whether the user has agreed to let a workspace's project logic run.
///
/// MSBuild evaluation executes code: SDK targets, imported props, and anything
/// the project author wrote. Opening a folder cannot imply consent to that, so
/// evaluation is gated on an explicit, scoped decision and an untrusted
/// workspace stays in declared, non-evaluating mode.
/// </summary>
public enum WorkspaceTrust
{
    Untrusted = 0,
    Trusted,
}

/// <summary>
/// Design-time project evaluation, out of process.
///
/// It runs the SDK's own structured query — <c>-getItem</c> and
/// <c>-getProperty</c> — rather than loading MSBuild into the IDE. Two reasons,
/// both load-bearing: evaluation runs arbitrary project code, which does not
/// belong in the process holding the user's unsaved edits; and the output is
/// JSON, so nothing here parses localized build text.
///
/// No analyzer or source generator runs as part of this. The query stops at
/// evaluation; generators run during a build, inside the worker's trust
/// boundary, and their outputs reach the language service as recorded inputs
/// rather than by being executed here.
/// </summary>
public sealed class DesignTimeEvaluator
{
    private readonly string _workspaceRoot;

    public DesignTimeEvaluator(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        _workspaceRoot = Path.GetFullPath(workspaceRoot);
    }

    public WorkspaceTrust Trust { get; set; } = WorkspaceTrust.Untrusted;

    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Evaluates one project for one target framework. Returns the reason
    /// rather than a partial graph when it cannot: a compilation assembled from
    /// assumptions produces diagnostics that look authoritative and are not.
    /// </summary>
    public async ValueTask<(EvaluatedProjectGraph? Graph, GraphUnavailable? Unavailable)> EvaluateAsync(
        string projectRelativePath,
        string? targetFramework,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRelativePath);

        if (Trust != WorkspaceTrust.Trusted)
        {
            return (null, new GraphUnavailable(
                projectRelativePath,
                GraphUnavailableReason.WorkspaceNotTrusted,
                "This workspace has not been trusted, so its projects have not been evaluated. " +
                "Editing and syntax colouring work; semantic diagnostics need an explicit trust decision."));
        }

        string projectPath = Path.GetFullPath(Path.Combine(_workspaceRoot, projectRelativePath));
        if (!IsInsideWorkspace(projectPath) || !File.Exists(projectPath))
        {
            return (null, new GraphUnavailable(
                projectRelativePath,
                GraphUnavailableReason.UnsupportedProject,
                $"'{projectRelativePath}' is not a project inside the granted workspace roots."));
        }

        var startInfo = new ProcessStartInfo("dotnet")
        {
            // The project's own directory, so the CLI applies the workspace's
            // global.json exactly as a build would. Resolving the SDK from the
            // IDE's own location would evaluate against a version the user did
            // not select.
            WorkingDirectory = Path.GetDirectoryName(projectPath)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (string argument in new[]
        {
            "build", projectPath, "-t:ResolveReferences", "--nologo",
            "-getItem:Compile", "-getItem:ReferencePath",
            "-getProperty:DefineConstants", "-getProperty:LangVersion",
            "-getProperty:Nullable", "-getProperty:TargetFramework",
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (targetFramework is { Length: > 0 })
            startInfo.ArgumentList.Add($"-p:TargetFramework={targetFramework}");

        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return (null, new GraphUnavailable(
                projectRelativePath, GraphUnavailableReason.WorkerUnavailable, exception.Message));
        }

        Task<string> output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // A superseded evaluation must not keep an SDK process alive; the
            // whole tree goes, because MSBuild starts worker nodes.
            TryKill(process);
            throw;
        }

        if (process.ExitCode != 0)
        {
            return (null, new GraphUnavailable(
                projectRelativePath,
                GraphUnavailableReason.EvaluationFailed,
                $"Evaluating '{projectRelativePath}' failed with exit code {process.ExitCode}. " +
                $"Display-only output: {Tail(await error.ConfigureAwait(false), 400)}"));
        }

        try
        {
            return (Parse(projectRelativePath, targetFramework, await output.ConfigureAwait(false)), null);
        }
        catch (JsonException exception)
        {
            return (null, new GraphUnavailable(
                projectRelativePath,
                GraphUnavailableReason.EvaluationFailed,
                $"The evaluation output for '{projectRelativePath}' could not be read: {exception.Message}"));
        }
    }

    /// <summary>
    /// Reads MSBuild's structured output. Public so a worker on another machine
    /// can produce the JSON and this side can still parse it.
    /// </summary>
    public static EvaluatedProjectGraph Parse(
        string projectPath, string? requestedFramework, string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        var compiles = new List<string>();
        var references = new List<string>();
        var generated = new List<string>();
        if (root.TryGetProperty("Items", out JsonElement items))
        {
            foreach (string path in FullPaths(items, "Compile"))
            {
                // A compile input under obj/ came from a generator, not from
                // the user. A diagnostic on one has to be presented as
                // generated rather than as a file they can edit.
                //
                // Both separators are checked rather than the local one: this
                // parses output that may have been produced by a worker on
                // another platform.
                if (IsUnderIntermediateOutput(path))
                    generated.Add(path);
                else
                    compiles.Add(path);
            }

            references.AddRange(FullPaths(items, "ReferencePath"));
        }

        string? defines = null;
        string? languageVersion = null;
        string? nullable = null;
        string? evaluatedFramework = null;
        if (root.TryGetProperty("Properties", out JsonElement properties))
        {
            defines = GetString(properties, "DefineConstants");
            languageVersion = GetString(properties, "LangVersion");
            nullable = GetString(properties, "Nullable");
            evaluatedFramework = GetString(properties, "TargetFramework");
        }

        return new EvaluatedProjectGraph
        {
            ProjectPath = projectPath,
            TargetFramework = evaluatedFramework ?? requestedFramework ?? string.Empty,
            CompilePaths = compiles,
            GeneratedCompilePaths = generated,
            MetadataReferencePaths = references,
            PreprocessorSymbols = defines?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [],
            LanguageVersion = languageVersion,
            NullableContext = nullable,
        };
    }

    private static bool IsUnderIntermediateOutput(string path)
    {
        string normalized = path.Replace('\\', '/');
        return normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("obj/", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsInsideWorkspace(string candidate)
    {
        string prefix = _workspaceRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> FullPaths(JsonElement items, string itemName)
    {
        if (!items.TryGetProperty(itemName, out JsonElement element))
            yield break;

        foreach (JsonElement entry in element.EnumerateArray())
        {
            if (entry.TryGetProperty("FullPath", out JsonElement path) &&
                path.GetString() is { Length: > 0 } value)
            {
                yield return value;
            }
        }
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) ? value.GetString() : null;

    private static string Tail(string text, int limit) =>
        text.Length <= limit ? text : text[^limit..];

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }
}
