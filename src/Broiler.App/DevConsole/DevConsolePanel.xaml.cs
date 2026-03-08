using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Broiler.App.Rendering;
using Broiler.DevConsole;
using TheArtOfDev.HtmlRenderer.Core.Dom;

namespace Broiler.App.DevConsole;

/// <summary>
/// Interactive developer console panel with Log Viewer, DOM Inspector,
/// and JavaScript Console tabs.  Toggled via F12 from the main window.
/// </summary>
public partial class DevConsolePanel : UserControl, IDisposable
{
    private const double DefaultCanvasWidth = 280;
    private const double DefaultCanvasHeight = 120;

    private readonly List<RenderLogEntry> _logEntries = [];
    private readonly ErrorOverlayService _errorOverlay = new();
    private int _errorCount;

    /// <summary>Raised when the user clicks the close button.</summary>
    public event Action? CloseRequested;

    /// <summary>
    /// Optional function for evaluating JS expressions in the REPL.
    /// Set by the host (MainWindow) to inject the current JSContext.
    /// </summary>
    public Func<string, string>? JsEvaluator { get; set; }

    public DevConsolePanel()
    {
        InitializeComponent();
        RenderLogger.EntryLogged += OnLogEntry;
        _errorOverlay.ErrorCaptured += OnErrorCaptured;
    }

    // ─── Error Overlay ─────────────────────────────────────────────────

    private void OnErrorCaptured(RenderErrorInfo error)
    {
        _errorCount++;
        Dispatcher.BeginInvoke(() =>
        {
            ErrorBadge.Visibility = Visibility.Visible;
            ErrorCount.Text = _errorCount == 1 ? "1 error" : $"{_errorCount} errors";
        });
    }

    /// <summary>
    /// Returns all captured rendering errors for overlay visualisation.
    /// </summary>
    public IReadOnlyList<RenderErrorInfo> GetErrors() => _errorOverlay.GetErrors();

    /// <summary>
    /// Clears the error overlay and resets the badge count.
    /// </summary>
    public void ClearErrors()
    {
        _errorOverlay.Clear();
        _errorCount = 0;
        ErrorBadge.Visibility = Visibility.Collapsed;
    }

    // ─── Log Viewer ────────────────────────────────────────────────────

    private void OnLogEntry(RenderLogEntry entry)
    {
        lock (_logEntries)
            _logEntries.Add(entry);

        Dispatcher.BeginInvoke(() =>
        {
            LogList.Items.Add(FormatLogEntry(entry));
            LogList.ScrollIntoView(LogList.Items[^1]);
        });
    }

    private static string FormatLogEntry(RenderLogEntry e)
    {
        var ex = e.Exception != null ? $" | {e.Exception.GetType().Name}: {e.Exception.Message}" : string.Empty;
        return $"[{e.Timestamp:HH:mm:ss.fff}] [{e.Level}] [{e.Category}/{e.Context}] {e.Message}{ex}";
    }

    private void LogFilter_Changed(object sender, object e) => RefreshLogView();

    private void ClearLog_Click(object sender, RoutedEventArgs e)
    {
        lock (_logEntries)
            _logEntries.Clear();
        LogList.Items.Clear();
    }

    private void RefreshLogView()
    {
        LogLevel? level = LogLevelFilter.SelectedIndex switch
        {
            1 => LogLevel.Debug,
            2 => LogLevel.Info,
            3 => LogLevel.Warning,
            4 => LogLevel.Error,
            _ => null,
        };

        LogCategory? category = LogCategoryFilter?.SelectedIndex switch
        {
            1 => LogCategory.HtmlRenderer,
            2 => LogCategory.JavaScript,
            _ => null,
        };

        var search = string.IsNullOrWhiteSpace(LogSearchBox?.Text) ? null : LogSearchBox.Text;

        IEnumerable<RenderLogEntry> entries;
        lock (_logEntries)
            entries = _logEntries.ToList();

        if (level.HasValue)
            entries = entries.Where(e => e.Level >= level.Value);

        if (category.HasValue)
            entries = entries.Where(e => e.Category == category.Value);

        if (!string.IsNullOrWhiteSpace(search))
            entries = entries.Where(e =>
                e.Message.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                e.Context.Contains(search, StringComparison.OrdinalIgnoreCase));

        LogList?.Items.Clear();
        foreach (var entry in entries)
            LogList.Items.Add(FormatLogEntry(entry));
    }

    // ─── DOM Inspector ─────────────────────────────────────────────────

    /// <summary>
    /// Populates the DOM tree from the given root CssBox.
    /// Called by the host when a page finishes rendering.
    /// </summary>
    internal void LoadBoxTree(CssBox root)
    {
        DomTree.Items.Clear();
        StylesList.Items.Clear();

        var treeRoot = ConsoleService.BuildBoxTree(root);
        DomTree.Items.Add(BuildTreeViewItem(treeRoot));
    }

    private static TreeViewItem BuildTreeViewItem(BoxTreeNode node)
    {
        var label = node.Tag;
        if (!string.IsNullOrEmpty(node.Id))
            label += $"#{node.Id}";
        if (!string.IsNullOrEmpty(node.CssClass))
            label += $".{node.CssClass.Replace(' ', '.')}";
        label += $"  ({node.Display})";

        var item = new TreeViewItem
        {
            Header = label,
            Tag = node,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            IsExpanded = node.Depth < 3,
        };

        foreach (var child in node.Children)
            item.Items.Add(BuildTreeViewItem(child));

        return item;
    }

    private void DomTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is TreeViewItem { Tag: BoxTreeNode node } && node.Box != null)
        {
            ShowComputedStyles(node.Box);
            ShowBoxModel(node.Box);
        }
    }

    private void ShowComputedStyles(CssBox box)
    {
        StylesList.Items.Clear();
        var styles = ConsoleService.GetComputedStyles(box);

        string? currentCategory = null;
        foreach (var style in styles)
        {
            if (style.Category != currentCategory)
            {
                currentCategory = style.Category;
                StylesList.Items.Add(new ListBoxItem
                {
                    Content = $"── {currentCategory} ──",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0)),
                    FontWeight = FontWeights.Bold,
                    IsEnabled = false,
                });
            }

            StylesList.Items.Add(new ListBoxItem
            {
                Content = $"  {style.Name}: {style.Value}",
                Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            });
        }
    }

    private void ShowBoxModel(CssBox box)
    {
        BoxModelCanvas.Children.Clear();
        var model = ConsoleService.GetBoxModel(box);

        var canvasWidth = BoxModelCanvas.ActualWidth > 0 ? BoxModelCanvas.ActualWidth : DefaultCanvasWidth;
        var canvasHeight = BoxModelCanvas.ActualHeight > 0 ? BoxModelCanvas.ActualHeight : DefaultCanvasHeight;

        // Draw nested rectangles: margin > border > padding > content
        DrawBoxModelRect(0, 0, canvasWidth, canvasHeight,
            Color.FromArgb(0x80, 0xF9, 0xCC, 0x9D), // margin - orange
            $"margin: {model.Margin.Top:F0} {model.Margin.Right:F0} {model.Margin.Bottom:F0} {model.Margin.Left:F0}");

        var mx = 20.0; var my = 16.0;
        DrawBoxModelRect(mx, my, canvasWidth - 2 * mx, canvasHeight - 2 * my,
            Color.FromArgb(0x80, 0xFD, 0xD8, 0x35), // border - yellow
            $"border: {model.Border.Top:F0} {model.Border.Right:F0} {model.Border.Bottom:F0} {model.Border.Left:F0}");

        var bx = mx + 16; var by = my + 12;
        DrawBoxModelRect(bx, by, canvasWidth - 2 * bx, canvasHeight - 2 * by,
            Color.FromArgb(0x80, 0xC3, 0xE8, 0x8D), // padding - green
            $"padding: {model.Padding.Top:F0} {model.Padding.Right:F0} {model.Padding.Bottom:F0} {model.Padding.Left:F0}");

        var px = bx + 16; var py = by + 12;
        DrawBoxModelRect(px, py, canvasWidth - 2 * px, canvasHeight - 2 * py,
            Color.FromArgb(0x80, 0x8D, 0xB6, 0xCD), // content - blue
            $"{model.ContentWidth:F0} × {model.ContentHeight:F0}");
    }

    private void DrawBoxModelRect(double x, double y, double w, double h, Color fill, string label)
    {
        w = Math.Max(0, w);
        h = Math.Max(0, h);

        var rect = new Rectangle
        {
            Width = w,
            Height = h,
            Fill = new SolidColorBrush(fill),
            Stroke = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
            StrokeThickness = 1,
        };
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        BoxModelCanvas.Children.Add(rect);

        var text = new TextBlock
        {
            Text = label,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 9,
        };
        Canvas.SetLeft(text, x + 2);
        Canvas.SetTop(text, y + 1);
        BoxModelCanvas.Children.Add(text);
    }

    // ─── JS Console ────────────────────────────────────────────────────

    private void JsInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        var expr = JsInput.Text;
        if (string.IsNullOrWhiteSpace(expr)) return;

        JsOutput.Items.Add($"> {expr}");
        JsInput.Clear();

        if (JsEvaluator != null)
        {
            try
            {
                var result = JsEvaluator(expr);
                JsOutput.Items.Add(result);
            }
            catch (Exception ex)
            {
                JsOutput.Items.Add($"Error: {ex.Message}");
            }
        }
        else
        {
            JsOutput.Items.Add("(No JS context available — load a page first)");
        }

        JsOutput.ScrollIntoView(JsOutput.Items[^1]);
    }

    // ─── Tab switching ─────────────────────────────────────────────────

    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb) return;

        // Guard against null — panels may not be initialised yet during
        // InitializeComponent when the default IsChecked fires.
        if (LogPanel is null || DomPanel is null || JsPanel is null)
            return;

        LogPanel.Visibility = Visibility.Collapsed;
        DomPanel.Visibility = Visibility.Collapsed;
        JsPanel.Visibility = Visibility.Collapsed;

        switch (rb.Tag as string)
        {
            case "Log":
                LogPanel.Visibility = Visibility.Visible;
                break;
            case "Dom":
                DomPanel.Visibility = Visibility.Visible;
                break;
            case "Js":
                JsPanel.Visibility = Visibility.Visible;
                break;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke();

    /// <inheritdoc />
    public void Dispose()
    {
        RenderLogger.EntryLogged -= OnLogEntry;
        _errorOverlay.Dispose();
    }
}
