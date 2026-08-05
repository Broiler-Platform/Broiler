using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Broiler.Code.Core.Shell;
using Broiler.Code.Workspaces.Storage;

namespace Broiler.Code.Windows;

/// <summary>
/// The common file dialogs, over comdlg32.
///
/// The dialog is where the grant comes from: whatever the user picks, this
/// returns storage rooted at that file's own directory and nothing wider. A
/// document opened or saved this way therefore reaches exactly one directory,
/// and the workspace never has to widen its root to accommodate it.
///
/// comdlg32 rather than IFileDialog: it needs no COM apartment setup beyond the
/// STA the head already declares, and this head asks for one file at a time.
/// </summary>
[SupportedOSPlatform("windows7.0")]
internal sealed partial class WindowsFileDialogs(IntPtr owner) : IFileDialogService
{
    private const int MaxPath = 32768;

    public ValueTask<FileGrant?> RequestOpenAsync(
        FileDialogRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        // FILE_MUST_EXIST | PATH_MUST_EXIST | HIDEREADONLY | EXPLORER
        return ValueTask.FromResult(Show(request, save: false, 0x00001000 | 0x00000800 | 0x00000004 | 0x00080000));
    }

    public ValueTask<FileGrant?> RequestSaveAsync(
        FileDialogRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        // OVERWRITEPROMPT | PATH_MUST_EXIST | HIDEREADONLY | EXPLORER. The
        // overwrite prompt is the dialog's, so the save path below does not
        // need an expected-revision check the user has already answered.
        return ValueTask.FromResult(Show(request, save: true, 0x00000002 | 0x00000800 | 0x00000004 | 0x00080000));
    }

    private FileGrant? Show(FileDialogRequest request, bool save, int flags)
    {
        char[] buffer = new char[MaxPath];
        if (request.SuggestedName is { Length: > 0 } suggested)
        {
            int length = Math.Min(suggested.Length, MaxPath - 1);
            suggested.AsSpan(0, length).CopyTo(buffer);
        }

        unsafe
        {
            fixed (char* file = buffer)
            fixed (char* filter = BuildFilter(request.Filters))
            fixed (char* title = request.Title + '\0')
            {
                var name = new OpenFileName
                {
                    StructureSize = sizeof(OpenFileName),
                    Owner = owner,
                    Filter = (IntPtr)filter,
                    FilterIndex = 1,
                    File = (IntPtr)file,
                    MaxFile = MaxPath,
                    Title = (IntPtr)title,
                    Flags = flags,
                };

                bool chosen = save ? GetSaveFileName(ref name) : GetOpenFileName(ref name);
                if (!chosen)
                    return null;
            }
        }

        int terminator = Array.IndexOf(buffer, '\0');
        string full = new(buffer, 0, terminator < 0 ? buffer.Length : terminator);
        if (full.Length == 0)
            return null;

        full = Path.GetFullPath(full);
        string? directory = Path.GetDirectoryName(full);
        if (directory is null)
            return null;

        return new FileGrant(
            new FileSystemWorkspaceStorage(directory), Path.GetFileName(full), full);
    }

    /// <summary>
    /// comdlg32's filter format: label, NUL, pattern, NUL, … and a final NUL.
    /// An empty list still needs one entry, or the dialog shows no files at all.
    /// </summary>
    private static char[] BuildFilter(IReadOnlyList<FileDialogFilter> filters)
    {
        var text = new StringBuilder();
        if (filters.Count == 0)
            filters = [FileDialogFilter.All];

        foreach (FileDialogFilter filter in filters)
        {
            var patterns = new List<string>(filter.Extensions.Count);
            foreach (string extension in filter.Extensions)
                patterns.Add(extension == "*" ? "*.*" : $"*.{extension}");

            string joined = string.Join(";", patterns);
            text.Append(filter.Label).Append(" (").Append(joined).Append(')').Append('\0');
            text.Append(joined).Append('\0');
        }

        text.Append('\0');
        return [.. text.ToString()];
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenFileName
    {
        public int StructureSize;
        public IntPtr Owner;
        public IntPtr Instance;
        public IntPtr Filter;
        public IntPtr CustomFilter;
        public int MaxCustomFilter;
        public int FilterIndex;
        public IntPtr File;
        public int MaxFile;
        public IntPtr FileTitle;
        public int MaxFileTitle;
        public IntPtr InitialDirectory;
        public IntPtr Title;
        public int Flags;
        public short FileOffset;
        public short FileExtension;
        public IntPtr DefaultExtension;
        public IntPtr CustomData;
        public IntPtr Hook;
        public IntPtr TemplateName;
        public IntPtr ReservedPointer;
        public int ReservedInt;
        public int FlagsEx;
    }

    [LibraryImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetOpenFileName(ref OpenFileName name);

    [LibraryImport("comdlg32.dll", EntryPoint = "GetSaveFileNameW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSaveFileName(ref OpenFileName name);
}
