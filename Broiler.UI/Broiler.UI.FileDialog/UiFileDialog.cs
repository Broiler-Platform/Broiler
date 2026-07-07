using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    private UiFileDialogFilter[] _fileTypeFilters = [];
    private int _selectedFileTypeFilterIndex = -1;

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

    public IReadOnlyList<UiFileDialogFilter> FileTypeFilters => _fileTypeFilters;

    public int SelectedFileTypeFilterIndex
    {
        get => _selectedFileTypeFilterIndex;
        set
        {
            ThrowIfDisposed();
            int normalized = NormalizeSelectedFilterIndex(value);
            if (_selectedFileTypeFilterIndex == normalized)
                return;

            _selectedFileTypeFilterIndex = normalized;
            ApplySelectedFileTypeFilter();
            OnSelectedFileTypeFilterChanged();
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public UiFileDialogFilter? SelectedFileTypeFilter =>
        _selectedFileTypeFilterIndex >= 0 && _selectedFileTypeFilterIndex < _fileTypeFilters.Length
            ? _fileTypeFilters[_selectedFileTypeFilterIndex]
            : null;

    public string SelectedPath =>
        string.IsNullOrWhiteSpace(FileName)
            ? CurrentDirectory
            : Path.GetFullPath(Path.Combine(CurrentDirectory, ApplyDefaultExtension(FileName)));

    public void SetFileTypeFilters(IEnumerable<UiFileDialogFilter>? filters, int selectedIndex = 0)
    {
        ThrowIfDisposed();
        _fileTypeFilters = filters?.ToArray() ?? [];
        if (_fileTypeFilters.Any(static filter => filter is null))
            throw new ArgumentException("File dialog filters cannot contain null values.", nameof(filters));

        _selectedFileTypeFilterIndex = NormalizeSelectedFilterIndex(selectedIndex);
        ApplySelectedFileTypeFilter();
        OnFileTypeFiltersChanged();
        OnSelectedFileTypeFilterChanged();
        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

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

    protected virtual void OnFileTypeFiltersChanged()
    {
    }

    protected virtual void OnSelectedFileTypeFilterChanged()
    {
    }

    private void ApplySelectedFileTypeFilter()
    {
        UiFileDialogFilter? filter = SelectedFileTypeFilter;
        if (filter is null)
            return;

        FileNameFilter = filter.Pattern;
        DefaultExtension = filter.DefaultExtension;
    }

    private int NormalizeSelectedFilterIndex(int index)
    {
        if (_fileTypeFilters.Length == 0)
            return -1;

        return Math.Clamp(index, 0, _fileTypeFilters.Length - 1);
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
