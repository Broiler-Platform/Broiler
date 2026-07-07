using System;
using System.IO;
using System.Threading.Tasks;
using Broiler.Graphics;
using Broiler.UI.Dialog;
using Broiler.UI.Window;

namespace Broiler.UI.FileDialog;

public abstract class UiFileDialog : UiDialog
{
    private UiFileDialogMode _mode;
    private string _currentDirectory = NormalizeDirectory(null);
    private string _fileName = string.Empty;
    private string _defaultExtension = string.Empty;
    private string _fileNameFilter = "*";

    public UiFileDialogMode Mode
    {
        get => _mode;
        set
        {
            ThrowIfDisposed();
            if (_mode == value)
                return;

            _mode = value;
            OnModeChanged();
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public string CurrentDirectory
    {
        get => _currentDirectory;
        set
        {
            ThrowIfDisposed();
            string normalized = NormalizeDirectory(value);
            if (StringComparer.Ordinal.Equals(_currentDirectory, normalized))
                return;

            _currentDirectory = normalized;
            OnCurrentDirectoryChanged();
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public string FileName
    {
        get => _fileName;
        set
        {
            ThrowIfDisposed();
            string normalized = Path.GetFileName(value ?? string.Empty);
            if (StringComparer.Ordinal.Equals(_fileName, normalized))
                return;

            _fileName = normalized;
            OnFileNameChanged();
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public string DefaultExtension
    {
        get => _defaultExtension;
        set
        {
            ThrowIfDisposed();
            string normalized = NormalizeExtension(value);
            if (StringComparer.Ordinal.Equals(_defaultExtension, normalized))
                return;

            _defaultExtension = normalized;
            OnDefaultExtensionChanged();
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public string FileNameFilter
    {
        get => _fileNameFilter;
        set
        {
            ThrowIfDisposed();
            string normalized = string.IsNullOrWhiteSpace(value) ? "*" : value.Trim();
            if (StringComparer.Ordinal.Equals(_fileNameFilter, normalized))
                return;

            _fileNameFilter = normalized;
            OnFileNameFilterChanged();
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public string SelectedPath =>
        string.IsNullOrWhiteSpace(FileName)
            ? CurrentDirectory
            : Path.GetFullPath(Path.Combine(CurrentDirectory, ApplyDefaultExtension(FileName)));

    public string ApplyDefaultExtension(string fileName)
    {
        ThrowIfDisposed();
        string normalized = Path.GetFileName(fileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(normalized) || string.IsNullOrWhiteSpace(DefaultExtension) || Path.HasExtension(normalized))
            return normalized;

        return normalized + DefaultExtension;
    }

    public Task<UiDialogResult> ShowOpenModal(UiWindow owner, BRect placement = default)
    {
        Mode = UiFileDialogMode.Open;
        return ShowModal(owner, placement);
    }

    public Task<UiDialogResult> ShowSaveModal(UiWindow owner, BRect placement = default)
    {
        Mode = UiFileDialogMode.Save;
        return ShowModal(owner, placement);
    }

    protected virtual void OnModeChanged()
    {
    }

    protected virtual void OnCurrentDirectoryChanged()
    {
    }

    protected virtual void OnFileNameChanged()
    {
    }

    protected virtual void OnDefaultExtensionChanged()
    {
    }

    protected virtual void OnFileNameFilterChanged()
    {
    }

    private static string NormalizeDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            directory = Environment.CurrentDirectory;

        return Path.GetFullPath(directory);
    }

    private static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return string.Empty;

        string trimmed = extension.Trim();
        if (StringComparer.Ordinal.Equals(trimmed, "*"))
            return string.Empty;

        return trimmed.StartsWith(".", StringComparison.Ordinal) ? trimmed : "." + trimmed;
    }
}
