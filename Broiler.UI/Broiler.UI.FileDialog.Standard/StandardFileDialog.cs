using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Broiler.Graphics;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Button.Standard;
using Broiler.UI.Edit.Standard;
using Broiler.UI.ListView;
using Broiler.UI.ListView.Standard;
using Broiler.UI.Standard;
using Broiler.UI.Window;

namespace Broiler.UI.FileDialog.Standard;

public sealed class StandardFileDialog : UiFileDialog, IStandardThemedControl
{
    private readonly StandardListView _placesList;
    private readonly StandardEdit _fileNameEdit;
    private readonly StandardListView _filesList;
    private readonly StandardListView _directoriesList;
    private readonly StandardButton _upButton;
    private readonly StandardButton _formatButton;
    private readonly StandardButton _okButton;
    private readonly StandardButton _cancelButton;
    private readonly Dictionary<string, string> _placePaths = [];
    private readonly Dictionary<string, string> _filePaths = [];
    private readonly Dictionary<string, FileInfo> _fileInfos = [];
    private readonly Dictionary<string, string> _directoryPaths = [];
    private BRect _pathBounds;
    private BRect _descriptionBounds;
    private BRect _placesHeaderBounds;
    private BRect _placesPanelBounds;
    private BRect _foldersHeaderBounds;
    private BRect _filesHeaderBounds;
    private BRect _fileNameLabelBounds;
    private BRect _formatLabelBounds;
    private BRect _statusBounds;
    private BRect _footerRuleBounds;
    private bool _refreshing;
    private bool _syncingFileName;

    public StandardFileDialog()
    {
        Title = "Open";

        _placesList = new StandardListView
        {
            PreferredSize = new BSize(150, 260),
            ItemHeight = 28,
            CornerRadius = StandardControlPaint.SmallRadius,
            Background = StandardControlPaint.SurfaceAlt,
        };
        _filesList = new StandardListView
        {
            PreferredSize = new BSize(250, 230),
            ItemHeight = 24,
            CornerRadius = StandardControlPaint.SmallRadius,
        };
        _directoriesList = new StandardListView
        {
            PreferredSize = new BSize(210, 230),
            ItemHeight = 24,
            CornerRadius = StandardControlPaint.SmallRadius,
        };
        _fileNameEdit = new StandardEdit
        {
            PreferredSize = new BSize(430, 30),
            CornerRadius = StandardControlPaint.SmallRadius,
            PaddingX = 8,
            PaddingY = 5,
        };
        _upButton = new StandardButton
        {
            Text = "Up one level",
            PreferredSize = new BSize(104, 30),
            CornerRadius = StandardControlPaint.SmallRadius,
            PaddingX = 8,
            PaddingY = 5,
        };
        _formatButton = new StandardButton
        {
            Text = "Format",
            PreferredSize = new BSize(430, 30),
            CornerRadius = StandardControlPaint.SmallRadius,
            PaddingX = 8,
            PaddingY = 5,
        };
        _okButton = new StandardButton
        {
            Text = "Open",
            IsDefault = true,
            PreferredSize = new BSize(92, 30),
            CornerRadius = StandardControlPaint.SmallRadius,
            PaddingX = 8,
            PaddingY = 5,
        };
        _cancelButton = new StandardButton
        {
            Text = "Cancel",
            IsCancel = true,
            PreferredSize = new BSize(92, 30),
            CornerRadius = StandardControlPaint.SmallRadius,
            PaddingX = 8,
            PaddingY = 5,
        };

        _placesList.SelectionChanged += (_, e) => NavigateToPlace(e.NewItemId);
        _filesList.SelectionChanged += (_, e) => SelectFile(e.NewItemId);
        _directoriesList.SelectionChanged += (_, e) => NavigateToDirectory(e.NewItemId);
        _fileNameEdit.Submitted += (_, _) => AcceptSelection();
        _upButton.Clicked += (_, _) => NavigateUp();
        _formatButton.Clicked += (_, _) => CycleFileTypeFilter();
        _okButton.Clicked += (_, _) => AcceptSelection();
        _cancelButton.Clicked += (_, _) => Cancel();

        AddChild(_placesList);
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

    public void ApplyTheme(StandardThemeTokens theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        Background = theme.Surface;
        PanelBackground = theme.SurfaceAlt;
        TitleBarBackground = theme.Accent;
        TitleForeground = theme.OnAccent;
        BorderColor = theme.BorderStrong;
        DividerColor = theme.Border;
        PathBackground = theme.Surface;
        PathForeground = theme.Text;
        LabelForeground = theme.TextMuted;
        HeaderForeground = theme.Text;
        StatusForeground = theme.TextMuted;

        foreach (UiElement child in Children)
        {
            if (child is IStandardThemedControl themed)
                themed.ApplyTheme(theme);
        }

        _placesList.Background = theme.SurfaceAlt;
        _placesList.SelectedBackground = theme.AccentSoft;
    }

    public BColor Background { get; set; } = StandardControlPaint.Surface;

    public BColor PanelBackground { get; set; } = StandardControlPaint.SurfaceAlt;

    public BColor TitleBarBackground { get; set; } = StandardControlPaint.Accent;

    public BColor TitleForeground { get; set; } = BColor.White;

    public BColor BorderColor { get; set; } = StandardControlPaint.BorderStrong;

    public BColor DividerColor { get; set; } = StandardControlPaint.Border;

    public BColor PathBackground { get; set; } = StandardControlPaint.Surface;

    public BFontStyle TitleFont { get; set; } = new("Segoe UI", 14, BFontWeight.SemiBold);

    public BFontStyle DescriptionFont { get; set; } = new("Segoe UI", 12);

    public BFontStyle HeaderFont { get; set; } = new("Segoe UI", 12, BFontWeight.SemiBold);

    public BFontStyle LabelFont { get; set; } = new("Segoe UI", 12);

    public BFontStyle PathFont { get; set; } = new("Segoe UI", 12);

    public BFontStyle StatusFont { get; set; } = new("Segoe UI", 12);

    public BColor PathForeground { get; set; } = StandardControlPaint.Text;

    public BColor LabelForeground { get; set; } = StandardControlPaint.TextMuted;

    public BColor HeaderForeground { get; set; } = StandardControlPaint.Text;

    public BColor StatusForeground { get; set; } = StandardControlPaint.TextMuted;

    public BSize PreferredSize { get; set; } = new(740, 430);

    public double TitleBarHeight { get; set; } = 34;

    public double PathRowHeight { get; set; } = 28;

    public double Padding { get; set; } = 14;

    public double Gap { get; set; } = 10;

    public StandardEdit FileNameEdit => _fileNameEdit;

    public StandardListView PlacesList => _placesList;

    public StandardListView FilesList => _filesList;

    public StandardListView DirectoriesList => _directoriesList;

    public StandardButton UpButton => _upButton;

    public StandardButton FormatButton => _formatButton;

    public StandardButton OkButton => _okButton;

    public StandardButton CancelButton => _cancelButton;

    public void Refresh()
    {
        ThrowIfDisposed();
        RefreshPlaces();
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
        {
            RefreshPlaces();
            RefreshDirectoryEntries();
        }
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
        double buttonWidth = Math.Min(112, Math.Max(90, client.Width * 0.14));
        double buttonHeight = 30;
        double editHeight = 30;
        bool showFormatSelector = FileTypeFilters.Count > 0;
        double formatHeight = showFormatSelector ? 30 : 0;
        double formatGap = showFormatSelector ? 8 : 0;
        double labelHeight = 18;
        double topInfoHeight = 58;
        double footerHeight = labelHeight + editHeight + formatGap + formatHeight + 26;
        double placesWidth = Math.Min(178, Math.Max(138, client.Width * 0.24));
        double rightX = Math.Max(client.Left, client.Right - buttonWidth);
        double contentLeft = client.Left + placesWidth + Gap;
        double contentWidth = Math.Max(0, rightX - contentLeft - Gap);
        double footerTop = Math.Max(client.Top + topInfoHeight + labelHeight + Gap, client.Bottom - footerHeight);
        double listHeaderTop = client.Top + topInfoHeight;
        double listTop = listHeaderTop + labelHeight;
        double listHeight = Math.Max(0, footerTop - listTop - Gap);
        double folderWidth = Math.Min(Math.Max(160, contentWidth * 0.38), Math.Max(0, contentWidth - 160 - Gap));
        double fileWidth = Math.Max(0, contentWidth - folderWidth - Gap);
        double fileNameTop = footerTop + labelHeight;
        double formatTop = fileNameTop + editHeight + formatGap;

        _placesHeaderBounds = new BRect(client.Left, client.Top, placesWidth, labelHeight);
        _placesPanelBounds = new BRect(client.Left, client.Top, placesWidth, Math.Max(0, footerTop - client.Top - Gap));
        _descriptionBounds = new BRect(contentLeft, client.Top, contentWidth, 20);
        _pathBounds = new BRect(contentLeft, client.Top + 24, contentWidth, Math.Min(PathRowHeight, Math.Max(0, topInfoHeight - 28)));
        _foldersHeaderBounds = new BRect(contentLeft, listHeaderTop, folderWidth, labelHeight);
        _filesHeaderBounds = new BRect(contentLeft + folderWidth + Gap, listHeaderTop, fileWidth, labelHeight);
        _fileNameLabelBounds = new BRect(contentLeft, footerTop, contentWidth, labelHeight);
        _formatLabelBounds = showFormatSelector
            ? new BRect(contentLeft, fileNameTop + editHeight, contentWidth, labelHeight)
            : default;
        _statusBounds = new BRect(client.Left, client.Bottom - 20, Math.Max(0, client.Width - buttonWidth - Gap), 20);
        _footerRuleBounds = new BRect(client.Left, Math.Max(client.Top, footerTop - 1), client.Width, 1);

        _placesList.Arrange(new BRect(client.Left, client.Top + labelHeight, placesWidth, Math.Max(0, footerTop - client.Top - labelHeight - Gap)));
        _directoriesList.Arrange(new BRect(contentLeft, listTop, folderWidth, listHeight));
        _filesList.Arrange(new BRect(contentLeft + folderWidth + Gap, listTop, fileWidth, listHeight));
        _fileNameEdit.Arrange(new BRect(contentLeft, fileNameTop, contentWidth, editHeight));
        _formatButton.Arrange(showFormatSelector
            ? new BRect(contentLeft, formatTop, contentWidth, formatHeight)
            : new BRect(client.Left, client.Bottom, 0, 0));
        _upButton.Arrange(new BRect(rightX, client.Top + 24, buttonWidth, buttonHeight));
        _okButton.Arrange(new BRect(rightX, fileNameTop, buttonWidth, buttonHeight));
        _cancelButton.Arrange(new BRect(rightX, fileNameTop + buttonHeight + Gap, buttonWidth, buttonHeight));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        context.RenderList.FillRect(Bounds, Background);
        context.RenderList.FillRect(new BRect(Bounds.Left, Bounds.Top, Bounds.Width, Math.Min(TitleBarHeight, Bounds.Height)), TitleBarBackground);
        if (!string.IsNullOrWhiteSpace(Title))
            context.RenderList.DrawText(new BTextRun(Title, TitleFont, TitleForeground), new BPoint(Bounds.Left + Padding, Bounds.Top + 7));

        if (!_placesPanelBounds.IsEmpty)
            StandardControlPaint.FillRounded(context.RenderList, _placesPanelBounds, PanelBackground, StandardControlPaint.SmallRadius);

        DrawDialogText(context);
        DrawCurrentDirectory(context);
        base.RenderCore(context);
        DrawStatus(context);
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
            _fileInfos.Clear();
            _directoryPaths.Clear();

            if (directory.Exists)
            {
                DirectoryInfo? parent = directory.Parent;
                if (parent is not null)
                    AddDirectoryItem(directories, "..", parent.FullName);

                foreach (DirectoryInfo child in EnumerateDirectories(directory))
                    AddDirectoryItem(directories, child.Name, child.FullName);

                foreach (FileInfo file in EnumerateFiles(directory).Where(FileMatchesFilter))
                    AddFileItem(files, file);
            }

            _directoriesList.SetItems(directories);
            _directoriesList.SelectedItemId = null;
            _filesList.SetItems(files);
            SelectMatchingFile();
            SyncSelectedPlace();
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void AddFileItem(List<UiListItem> items, FileInfo file)
    {
        string id = "file:" + file.FullName;
        _filePaths[id] = file.FullName;
        _fileInfos[id] = file;
        items.Add(new UiListItem(id, file.Name));
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

    private void NavigateToPlace(string? itemId)
    {
        if (_refreshing || itemId is null || !_placePaths.TryGetValue(itemId, out string? path) || !Directory.Exists(path))
            return;

        CurrentDirectory = path;
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

        StandardControlPaint.FillRounded(context.RenderList, _pathBounds, PathBackground, StandardControlPaint.SmallRadius);
        StandardControlPaint.StrokeRounded(context.RenderList, _pathBounds, DividerColor, StandardControlPaint.SmallRadius, 1);

        double y = _pathBounds.Top + Math.Max(0, (_pathBounds.Height - BTextMeasurer.GetLineHeight(PathFont)) / 2);
        context.RenderList.PushClip(_pathBounds);
        context.RenderList.DrawText(new BTextRun(CurrentDirectory, PathFont, PathForeground), new BPoint(_pathBounds.Left + 8, y));
        context.RenderList.PopClip();
    }

    private void DrawDialogText(UiRenderContext context)
    {
        if (!_footerRuleBounds.IsEmpty)
            context.RenderList.FillRect(_footerRuleBounds, DividerColor);

        DrawText(context, "Places", _placesHeaderBounds, HeaderFont, HeaderForeground);
        DrawText(context, BuildModeDescription(), _descriptionBounds, DescriptionFont, LabelForeground);
        DrawText(context, "Folders (" + _directoriesList.Items.Count.ToString(CultureInfo.InvariantCulture) + ")", _foldersHeaderBounds, HeaderFont, HeaderForeground);
        DrawText(context, BuildFilesHeader(), _filesHeaderBounds, HeaderFont, HeaderForeground);
        DrawText(context, "File name", _fileNameLabelBounds, LabelFont, LabelForeground);

        if (!_formatLabelBounds.IsEmpty)
            DrawText(context, "File type", _formatLabelBounds, LabelFont, LabelForeground);
    }

    private void DrawStatus(UiRenderContext context) =>
        DrawText(context, BuildStatusText(), _statusBounds, StatusFont, StatusForeground);

    private void DrawText(UiRenderContext context, string text, BRect bounds, BFontStyle font, BColor color)
    {
        if (bounds.IsEmpty || string.IsNullOrWhiteSpace(text))
            return;

        double y = bounds.Top + Math.Max(0, (bounds.Height - BTextMeasurer.GetLineHeight(font)) / 2);
        context.RenderList.PushClip(bounds);
        context.RenderList.DrawText(new BTextRun(text, font, color), new BPoint(bounds.Left, y));
        context.RenderList.PopClip();
    }

    private string BuildModeDescription() =>
        Mode == UiFileDialogMode.Save
            ? "Choose a folder, name the document, and pick the format to save."
            : "Choose a document to open, or jump to a common location from Places.";

    private string BuildFilesHeader()
    {
        string filter = SelectedFileTypeFilter?.Name ?? FileNameFilter;
        return "Files (" + _filesList.Items.Count.ToString(CultureInfo.InvariantCulture) + ") - " + filter;
    }

    private string BuildStatusText()
    {
        string? selectedId = _filesList.SelectedItemId;
        if (selectedId is not null && _fileInfos.TryGetValue(selectedId, out FileInfo? file))
        {
            return "Selected: " + file.Name + " | " + FormatByteSize(file.Length) + " | Modified " +
                file.LastWriteTime.ToString("g", CultureInfo.CurrentCulture);
        }

        string displayName = ApplyDefaultExtension(FileName);
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return (Mode == UiFileDialogMode.Save ? "Ready to save: " : "Typed file: ") +
                displayName + " in " + DescribeDirectory(CurrentDirectory);
        }

        return _directoriesList.Items.Count.ToString(CultureInfo.InvariantCulture) + " folders and " +
            _filesList.Items.Count.ToString(CultureInfo.InvariantCulture) + " matching files in " +
            DescribeDirectory(CurrentDirectory);
    }

    private void RefreshPlaces()
    {
        _refreshing = true;
        try
        {
            var items = new List<UiListItem>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _placePaths.Clear();

            AddPlace(items, seen, "Home", GetKnownFolder(Environment.SpecialFolder.UserProfile));
            AddPlace(items, seen, "Desktop", GetKnownFolder(Environment.SpecialFolder.DesktopDirectory));
            AddPlace(items, seen, "Documents", GetKnownFolder(Environment.SpecialFolder.MyDocuments));
            AddPlace(items, seen, "Downloads", GetDownloadsFolder());
            AddPlace(items, seen, "Pictures", GetKnownFolder(Environment.SpecialFolder.MyPictures));
            AddPlace(items, seen, "Working folder", Environment.CurrentDirectory);

            foreach (string drive in EnumerateDriveRoots())
                AddPlace(items, seen, DescribeDriveRoot(drive), drive);

            _placesList.SetItems(items);
            SyncSelectedPlace();
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void AddPlace(List<UiListItem> items, HashSet<string> seen, string label, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        string normalized;
        try
        {
            normalized = Path.GetFullPath(path);
        }
        catch (Exception ex) when (IsFileSystemReadException(ex) || ex is ArgumentException or NotSupportedException)
        {
            return;
        }

        if (!Directory.Exists(normalized) || !seen.Add(NormalizeForComparison(normalized)))
            return;

        string id = "place:" + items.Count.ToString(CultureInfo.InvariantCulture);
        _placePaths[id] = normalized;
        items.Add(new UiListItem(id, label));
    }

    private void SyncSelectedPlace()
    {
        string current = NormalizeForComparison(CurrentDirectory);
        string? selected = _placePaths
            .Where(pair => current.StartsWith(AddTrailingSeparator(NormalizeForComparison(pair.Value)), StringComparison.OrdinalIgnoreCase) ||
                           StringComparer.OrdinalIgnoreCase.Equals(current, NormalizeForComparison(pair.Value)))
            .OrderByDescending(pair => NormalizeForComparison(pair.Value).Length)
            .Select(static pair => pair.Key)
            .FirstOrDefault();

        _placesList.SelectedItemId = selected;
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

    private static string? GetKnownFolder(Environment.SpecialFolder folder)
    {
        try
        {
            string path = Environment.GetFolderPath(folder);
            return string.IsNullOrWhiteSpace(path) ? null : path;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static string? GetDownloadsFolder()
    {
        string? home = GetKnownFolder(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrWhiteSpace(home) ? null : Path.Combine(home, "Downloads");
    }

    private static string[] EnumerateDriveRoots()
    {
        try
        {
            return Directory.GetLogicalDrives();
        }
        catch (Exception ex) when (IsFileSystemReadException(ex) || ex is NotSupportedException)
        {
            return [];
        }
    }

    private static string DescribeDriveRoot(string path)
    {
        string root = Path.GetPathRoot(path) ?? path;
        if (StringComparer.Ordinal.Equals(root, Path.DirectorySeparatorChar.ToString()))
            return "File system";

        return "Drive " + root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string DescribeDirectory(string path)
    {
        string root = Path.GetPathRoot(path) ?? string.Empty;
        if (StringComparer.OrdinalIgnoreCase.Equals(AddTrailingSeparator(root), AddTrailingSeparator(path)))
            return string.IsNullOrWhiteSpace(root) ? path : root;

        string? name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(name) ? path : name;
    }

    private static string FormatByteSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = Math.Max(0, bytes);
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        string format = unit == 0 ? "0" : "0.#";
        return value.ToString(format, CultureInfo.CurrentCulture) + " " + units[unit];
    }

    private static string NormalizeForComparison(string path)
    {
        try
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception ex) when (IsFileSystemReadException(ex) || ex is ArgumentException or NotSupportedException)
        {
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

    private static string AddTrailingSeparator(string path)
    {
        if (string.IsNullOrEmpty(path) ||
            path.EndsWith(Path.DirectorySeparatorChar) ||
            path.EndsWith(Path.AltDirectorySeparatorChar))
        {
            return path;
        }

        return path + Path.DirectorySeparatorChar;
    }

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));
}
