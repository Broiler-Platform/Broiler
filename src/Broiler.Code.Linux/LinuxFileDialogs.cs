using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Broiler.Code.Core.Shell;
using Broiler.Code.Workspaces.Storage;

namespace Broiler.Code.Linux;

/// <summary>
/// File dialogs through the desktop's own helper — zenity on GTK desktops,
/// kdialog on KDE.
///
/// There is no toolkit-neutral file chooser on Linux, and this head deliberately
/// does not take a GTK or Qt dependency to get one. Running the helper the
/// desktop already ships is what a toolkit-less application does: the dialog the
/// user sees is their desktop's real chooser, with their bookmarks and their
/// recent files, not an imitation drawn by us.
///
/// When neither helper is installed there is no dialog, and
/// <see cref="IsAvailable"/> is false so the head reports Open and Save As as
/// unavailable rather than opening nothing.
/// </summary>
internal sealed class LinuxFileDialogs : IFileDialogService
{
    private readonly string? _helper;

    public LinuxFileDialogs() => _helper = FindHelper();

    /// <summary>Which helper was found, for the support claim.</summary>
    public string? Helper => _helper;

    public bool IsAvailable => _helper is not null;

    public async ValueTask<FileGrant?> RequestOpenAsync(
        FileDialogRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Grant(await RunAsync(request, save: false, cancellationToken).ConfigureAwait(false));
    }

    public async ValueTask<FileGrant?> RequestSaveAsync(
        FileDialogRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Grant(await RunAsync(request, save: true, cancellationToken).ConfigureAwait(false));
    }

    private async ValueTask<string?> RunAsync(
        FileDialogRequest request, bool save, CancellationToken cancellationToken)
    {
        if (_helper is null)
            return null;

        var start = new ProcessStartInfo(_helper)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (string argument in Arguments(_helper, request, save))
            start.ArgumentList.Add(argument);

        using Process? process = Process.Start(start);
        if (process is null)
            return null;

        string output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        // A non-zero exit is how both helpers report that the user cancelled,
        // which is a normal outcome and not an error to surface.
        if (process.ExitCode != 0)
            return null;

        string path = output.Trim();
        return path.Length == 0 ? null : path;
    }

    private static IEnumerable<string> Arguments(
        string helper, FileDialogRequest request, bool save)
    {
        bool isKde = helper.EndsWith("kdialog", StringComparison.Ordinal);
        if (isKde)
        {
            // kdialog takes a start path and one space-separated filter string.
            yield return save ? "--getsavefilename" : "--getopenfilename";
            yield return request.SuggestedName is { Length: > 0 } suggested ? suggested : ".";
            yield return KdialogFilter(request.Filters);
            if (request.Title is { Length: > 0 })
            {
                yield return "--title";
                yield return request.Title;
            }

            yield break;
        }

        yield return "--file-selection";
        if (save)
        {
            yield return "--save";

            // The overwrite prompt is the dialog's job. Without this a save over
            // an existing file happens with no warning at all.
            yield return "--confirm-overwrite";
        }

        if (request.Title is { Length: > 0 })
            yield return "--title=" + request.Title;
        if (request.SuggestedName is { Length: > 0 } name)
            yield return "--filename=" + name;

        foreach (FileDialogFilter filter in request.Filters)
            yield return "--file-filter=" + ZenityFilter(filter);
    }

    private static string ZenityFilter(FileDialogFilter filter)
    {
        var patterns = new List<string>(filter.Extensions.Count);
        foreach (string extension in filter.Extensions)
            patterns.Add(extension == "*" ? "*" : "*." + extension);

        return filter.Label + " | " + string.Join(' ', patterns);
    }

    private static string KdialogFilter(IReadOnlyList<FileDialogFilter> filters)
    {
        var groups = new List<string>(filters.Count);
        foreach (FileDialogFilter filter in filters)
        {
            var patterns = new List<string>(filter.Extensions.Count);
            foreach (string extension in filter.Extensions)
                patterns.Add(extension == "*" ? "*" : "*." + extension);

            groups.Add(string.Join(' ', patterns) + "|" + filter.Label);
        }

        return groups.Count == 0 ? "*|All files" : string.Join('\n', groups);
    }

    private static FileGrant? Grant(string? absolutePath)
    {
        if (absolutePath is null)
            return null;

        string full = Path.GetFullPath(absolutePath);
        string? directory = Path.GetDirectoryName(full);
        if (directory is null)
            return null;

        // The chooser is the grant: storage scoped to the directory the user
        // picked and nothing wider.
        return new FileGrant(
            new FileSystemWorkspaceStorage(directory), Path.GetFileName(full), full);
    }

    private static string? FindHelper()
    {
        if (!OperatingSystem.IsLinux())
            return null;

        // Looked up on PATH rather than assumed at a fixed location: a helper
        // that is not installed must read as absent, not as a broken dialog.
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (path is null)
            return null;

        foreach (string name in new[] { "zenity", "kdialog" })
        {
            foreach (string directory in path.Split(Path.PathSeparator))
            {
                if (directory.Length == 0)
                    continue;

                string candidate = Path.Combine(directory, name);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }
}
