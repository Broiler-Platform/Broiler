using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Broiler.Graphics;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Button.Standard;
using Broiler.UI.Edit.Standard;
using Broiler.UI.ListView;
using Broiler.UI.ListView.Standard;
using Broiler.UI.Window;

namespace Broiler.UI.FileDialog.Standard;

public sealed class StandardFileDialog : UiFileDialog
{
    private readonly StandardEdit _fileNameEdit;
    private readonly StandardListView _filesList;
    private readonly StandardListView _directoriesList;
    private readonly StandardButton _upButton;
    private readonly StandardButton _formatButton;
    private readonly StandardButton _okButton;
    private readonly StandardButton _cancelButton;
    private readonly Dictionary<string, string> _filePaths = [];
    private readonly Dictionary<string, string> _directoryPaths = [];
    private BRect _pathBounds;
    private bool _refreshing;
    private bool _syncingFileName;

    public StandardFileDialog()
    {
        Title = "Open";

        _filesList = new StandardListView
        {
            PreferredSize = new BSize(190, 158),
            ItemHeight = 22,
            CornerRadius = 0,
        };
        _directoriesList = new StandardListView
        {
            PreferredSize = new BSize(190, 158),
            ItemHeight = 22,
            CornerRadius = 0,
        };
        _fileNameEdit = new StandardEdit
        {
            PreferredSize = new BSize(388, 28),
            CornerRadius = 0,
            PaddingX = 5,
            PaddingY = 4,
        };
        _upButton = new StandardButton
        {
            Text = "Up",
            PreferredSize = new BSize(72, 28),
            CornerRadius = 0,
            PaddingX = 8,
            PaddingY = 5,
        };
        _formatButton = new StandardButton
        {
            Text = "Format",
            PreferredSize = new BSize(388, 28),
            CornerRadius = 0,
            PaddingX = 8,
            PaddingY = 5,
        };
        _okButton = new StandardButton
        {
            Text = "Open",
            IsDefault = true,
            PreferredSize = new BSize(72, 28),
            CornerRadius = 0,
            PaddingX = 8,
            PaddingY = 5,
        };
        _cancelButton = new StandardButton
        {
            Text = "Cancel",
            IsCancel = true,
            PreferredSize = new BSize(72, 28),
            CornerRadius = 0,
            PaddingX = 8,
            PaddingY = 5,
        };

        _filesList.SelectionChanged += (_, e) => SelectFile(e.NewItemId);
        _directoriesList.SelectionChanged += (_, e) => NavigateToDirectory(e.NewItemId);
        _fileNameEdit.Submitted += (_, _) => AcceptSelection();
        _upButton.Clicked += (_, _) => NavigateUp();
        _formatButton.Clicked += (_, _) => CycleFileTypeFilter();
        _okButton.Clicked += (_, _) => AcceptSelection();
        _cancelButton.Clicked += (_, _) => Cancel();

        AddChild(_filesList);
        AddChild(_directoriesList);
        AddChild(_fileNameEdit);
        AddChild(_upButton);
        AddChild(_formatButton);
        AddChild(_okButton);
        AddChild(_cancelButton);

        SyncFormatButton();
        Refresh();
    }

    public BColor Background { get; set; } = BColor.FromArgb(0xFF, 0xC0, 0xC0, 0xC0);

    public BColor TitleBarBackground { get; set; } = BColor.FromArgb(0xFF, 0x00, 0x00, 0x80);

    public BColor TitleForeground { get; set; } = BColor.White;

    public BColor BorderColor { get; set; } = BColor.Black;

    public BFontStyle TitleFont { get; set; } = BFontStyle.Default;

    public BFontStyle PathFont { get; set; } = BFontStyle.Default;

    public BColor PathForeground { get; set; } = BColor.Black;

    public BSize PreferredSize { get; set; } = new(520, 250);

    public double TitleBarHeight { get; set; } = 24;

    public double PathRowHeight { get; set; } = 20;

    public double Padding { get; set; } = 8;

    public double Gap { get; set; } = 8;

    public StandardEdit FileNameEdit => _fileNameEdit;

    public StandardListView FilesList => _filesList;

    public StandardListView DirectoriesList => _directoriesList;

    public StandardButton UpButton => _upButton;

    public StandardButton FormatButton => _formatButton;

    public StandardButton OkButton => _okButton;

    public StandardButton CancelButton => _cancelButton;

    public void Refresh()
    {
        ThrowIfDisposed();
        RefreshDirectoryEntries();
        SyncFileNameEdit();
    }

    public bool AcceptSelection()
    {
        ThrowIfDisposed();
        string fileName = _fileNameEdit.Text.Trim();
        if (fileName.Length == 0)
            return false;

        FileName = fileName;
        return Accept(SelectedPath);
    }

    public bool NavigateUp()
    {
        ThrowIfDisposed();
        DirectoryInfo? parent = Directory.GetParent(CurrentDirectory);
        if (parent is null)
            return false;

        CurrentDirectory = parent.FullName;
        return true;
    }

    protected override void OnModeChanged()
    {
        Title = Mode == UiFileDialogMode.Save ? "Save As" : "Open";
        _okButton.Text = Mode == UiFileDialogMode.Save ? "Save" : "Open";
    }

    protected override void OnCurrentDirectoryChanged()
    {
        if (!_refreshing)
            RefreshDirectoryEntries();
    }

    protected override void OnFileNameChanged()
    {
        if (!_syncingFileName)
            SyncFileNameEdit();

        SelectMatchingFile();
    }

    protected override void OnDefaultExtensionChanged()
    {
        SelectMatchingFile();
    }

    protected override void OnFileNameFilterChanged()
    {
        if (!_refreshing)
            RefreshDirectoryEntries();
    }

    protected override void OnFileTypeFiltersChanged()
    {
        SyncFormatButton();
    }

    protected override void OnSelectedFileTypeFilterChanged()
    {
        SyncFormatButton();
    }

    protected override BSize MeasureCore(BSize availableSize)
    {
        BSize clientAvailable = new(
            Math.Max(0, availableSize.Width - Padding * 2),
            Math.Max(0, availableSize.Height - TitleBarHeight - Padding * 2));

        foreach (UiElement child in Children)
            child.Measure(clientAvailable);

        return new BSize(
            ClampDesired(PreferredSize.Width, availableSize.Width),
            ClampDesired(PreferredSize.Height, availableSize.Height));
    }

    protected override void ArrangeCore(BRect finalRect)
    {
        if (Session is not null)
            BindViewport(new UiViewportBinding(finalRect.Size, Session.Host.Scale));

        BRect client = GetClientBounds(finalRect);
        double buttonWidth = Math.Min(84, Math.Max(64, client.Width * 0.16));
        double buttonHeight = 28;
        double editHeight = 28;
        bool showFormatSelector = FileTypeFilters.Count > 0;
        double formatHeight = showFormatSelector ? 28 : 0;
        double formatGap = showFormatSelector ? Gap : 0;
        double pathHeight = Math.Min(PathRowHeight, Math.Max(0, client.Height));
        double rightX = Math.Max(client.Left, client.Right - buttonWidth);
        double listAreaWidth = Math.Max(0, rightX - client.Left - Gap);
        double listTop = client.Top + pathHeight + Gap;
        double listHeight = Math.Max(0, client.Height - pathHeight - Gap - editHeight - formatGap - formatHeight - Gap);
        double fileNameTop = listTop + listHeight + Gap;
        double formatTop = fileNameTop + editHeight + formatGap;
        double fileWidth = Math.Max(0, (listAreaWidth - Gap) / 2);
        double directoryWidth = Math.Max(0, listAreaWidth - fileWidth - Gap);

        _pathBounds = new BRect(client.Left, client.Top, listAreaWidth, pathHeight);
        _filesList.Arrange(new BRect(client.Left, listTop, fileWidth, listHeight));
        _directoriesList.Arrange(new BRect(client.Left + fileWidth + Gap, listTop, directoryWidth, listHeight));
        _fileNameEdit.Arrange(new BRect(client.Left, fileNameTop, listAreaWidth, editHeight));
        _formatButton.Arrange(showFormatSelector
            ? new BRect(client.Left, formatTop, listAreaWidth, formatHeight)
            : new BRect(client.Left, client.Bottom, 0, 0));
        _upButton.Arrange(new BRect(rightX, client.Top, buttonWidth, buttonHeight));
        _okButton.Arrange(new BRect(rightX, listTop, buttonWidth, buttonHeight));
        _cancelButton.Arrange(new BRect(rightX, listTop + buttonHeight + Gap, buttonWidth, buttonHeight));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        context.RenderList.FillRect(Bounds, Background);
        context.RenderList.FillRect(new BRect(Bounds.Left, Bounds.Top, Bounds.Width, Math.Min(TitleBarHeight, Bounds.Height)), TitleBarBackground);
        if (!string.IsNullOrWhiteSpace(Title))
            context.RenderList.DrawText(new BTextRun(Title, TitleFont, TitleForeground), new BPoint(Bounds.Left + Padding, Bounds.Top + 4));

        DrawCurrentDirectory(context);
        base.RenderCore(context);
        context.RenderList.StrokeRect(Bounds, BorderColor, 1);
    }

    protected override bool OnInput(UiInputEvent input)
    {
        if (base.OnInput(input))
            return true;

        if (input.Kind == UiInputEventKind.PointerButton)
            return HandlePointerButton(input);
        if (input.Kind == UiInputEventKind.KeyboardKey)
            return HandleKeyboard(input);

        return false;
    }

    protected override bool HitTestMoveGrip(BPoint position) =>
        new BRect(Bounds.Left, Bounds.Top, Bounds.Width, Math.Min(TitleBarHeight, Bounds.Height)).Contains(position);

    private void RefreshDirectoryEntries()
    {
        _refreshing = true;
        try
        {
            DirectoryInfo directory = new(CurrentDirectory);
            var files = new List<UiListItem>();
            var directories = new List<UiListItem>();
            _filePaths.Clear();
            _directoryPaths.Clear();

            if (directory.Exists)
            {
                DirectoryInfo? parent = directory.Parent;
                if (parent is not null)
                    AddDirectoryItem(directories, "..", parent.FullName);

                foreach (DirectoryInfo child in EnumerateDirectories(directory))
                    AddDirectoryItem(directories, child.Name, child.FullName);

                foreach (FileInfo file in EnumerateFiles(directory).Where(FileMatchesFilter))
                    AddFileItem(files, file.Name, file.FullName);
            }

            _directoriesList.SetItems(directories);
            _directoriesList.SelectedItemId = null;
            _filesList.SetItems(files);
            SelectMatchingFile();
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void AddFileItem(List<UiListItem> items, string text, string path)
    {
        string id = "file:" + path;
        _filePaths[id] = path;
        items.Add(new UiListItem(id, text));
    }

    private void AddDirectoryItem(List<UiListItem> items, string text, string path)
    {
        string id = "dir:" + path;
        _directoryPaths[id] = path;
        items.Add(new UiListItem(id, text));
    }

    private void SelectFile(string? itemId)
    {
        if (_refreshing || itemId is null || !_filePaths.TryGetValue(itemId, out string? path))
            return;

        FileName = Path.GetFileName(path);
    }

    private void NavigateToDirectory(string? itemId)
    {
        if (_refreshing || itemId is null || !_directoryPaths.TryGetValue(itemId, out string? path))
            return;

        CurrentDirectory = path;
    }

    private void CycleFileTypeFilter()
    {
        if (FileTypeFilters.Count < 2)
            return;

        SelectedFileTypeFilterIndex = (SelectedFileTypeFilterIndex + 1) % FileTypeFilters.Count;
    }

    private void SyncFileNameEdit()
    {
        _syncingFileName = true;
        try
        {
            _fileNameEdit.Text = FileName;
        }
        finally
        {
            _syncingFileName = false;
        }
    }

    private void SelectMatchingFile()
    {
        string expected = Path.Combine(CurrentDirectory, ApplyDefaultExtension(FileName));
        string? selected = _filePaths
            .Where(pair => StringComparer.OrdinalIgnoreCase.Equals(pair.Value, expected))
            .Select(static pair => pair.Key)
            .FirstOrDefault();

        _filesList.SelectedItemId = selected;
    }

    private void SyncFormatButton()
    {
        UiFileDialogFilter? filter = SelectedFileTypeFilter;
        _formatButton.Text = filter is null ? "Format" : "Format: " + filter.Name;
        _formatButton.IsEnabled = FileTypeFilters.Count > 1;
    }

    private bool HandlePointerButton(UiInputEvent input)
    {
        if (input.MouseButton != MouseButton.Left || input.MouseButtonTransition != MouseButtonTransition.Down)
            return false;

        Activate();
        Session?.SetFocus(this);
        return true;
    }

    private bool HandleKeyboard(UiInputEvent input)
    {
        if (input.KeyTransition != KeyboardKeyTransition.Down)
            return false;

        if (IsKey(input, BVirtualKey.Escape, "Escape"))
            return Cancel();
        if (IsKey(input, BVirtualKey.Back, "Backspace"))
            return NavigateUp();
        if (IsKey(input, BVirtualKey.Enter, "Enter"))
            return AcceptSelection();

        return false;
    }

    private void DrawCurrentDirectory(UiRenderContext context)
    {
        if (_pathBounds.IsEmpty)
            return;

        double y = _pathBounds.Top + Math.Max(0, (_pathBounds.Height - BTextMeasurer.GetLineHeight(PathFont)) / 2);
        context.RenderList.PushClip(_pathBounds);
        context.RenderList.DrawText(new BTextRun(CurrentDirectory, PathFont, PathForeground), new BPoint(_pathBounds.Left + 4, y));
        context.RenderList.PopClip();
    }

    private bool FileMatchesFilter(FileInfo file)
    {
        if (string.IsNullOrWhiteSpace(FileNameFilter) || StringComparer.Ordinal.Equals(FileNameFilter, "*"))
            return true;

        foreach (string pattern in FileNameFilter.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (PatternMatches(file.Name, pattern))
                return true;
        }

        return false;
    }

    private static bool PatternMatches(string fileName, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern) ||
            StringComparer.Ordinal.Equals(pattern, "*") ||
            StringComparer.Ordinal.Equals(pattern, "*.*"))
            return true;

        return WildcardMatches(fileName, pattern);
    }

    private static bool WildcardMatches(string value, string pattern)
    {
        int valueIndex = 0;
        int patternIndex = 0;
        int lastStarIndex = -1;
        int valueAfterStar = 0;

        while (valueIndex < value.Length)
        {
            if (patternIndex < pattern.Length &&
                (pattern[patternIndex] == '?' ||
                 char.ToUpperInvariant(pattern[patternIndex]) == char.ToUpperInvariant(value[valueIndex])))
            {
                valueIndex++;
                patternIndex++;
                continue;
            }

            if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                lastStarIndex = patternIndex++;
                valueAfterStar = valueIndex;
                continue;
            }

            if (lastStarIndex >= 0)
            {
                patternIndex = lastStarIndex + 1;
                valueIndex = ++valueAfterStar;
                continue;
            }

            return false;
        }

        while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            patternIndex++;

        return patternIndex == pattern.Length;
    }

    private BRect GetClientBounds(BRect bounds) =>
        new(
            bounds.Left + Padding,
            bounds.Top + TitleBarHeight + Padding,
            Math.Max(0, bounds.Width - Padding * 2),
            Math.Max(0, bounds.Height - TitleBarHeight - Padding * 2));

    private static DirectoryInfo[] EnumerateDirectories(DirectoryInfo directory)
    {
        try
        {
            return directory
                .EnumerateDirectories()
                .OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex) when (IsFileSystemReadException(ex))
        {
            return [];
        }
    }

    private static FileInfo[] EnumerateFiles(DirectoryInfo directory)
    {
        try
        {
            return directory
                .EnumerateFiles()
                .OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex) when (IsFileSystemReadException(ex))
        {
            return [];
        }
    }

    private static bool IsFileSystemReadException(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException;

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));
}
