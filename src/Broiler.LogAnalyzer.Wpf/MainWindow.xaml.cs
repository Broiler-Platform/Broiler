using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using WpfColor = System.Windows.Media.Color;

namespace Broiler.LogAnalyzer.Wpf;

/// <summary>
/// Main window for the Broiler Log Analyzer WPF application.
/// Provides a DataGrid for browsing log entries with column sorting,
/// color-coded status rows, a quick-filter toolbar, drag-and-drop support,
/// dark/light theme toggle, chart export, and heatmap visualization.
/// </summary>
public partial class MainWindow : Window
{
    private string? _selectedPath;
    private IReadOnlyList<LogEntry>? _allEntries;
    private LogAnalyzerService? _analyzer;
    private int _top = 10;
    private bool _chartsLoaded;
    private bool _perHostLoaded;
    private bool _heatmapLoaded;
    private bool _isDarkTheme;

    public MainWindow()
    {
        InitializeComponent();
    }

    // ── Drag-and-drop support (TODO 17) ─────────────────────────────

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (files is null || files.Length == 0)
            return;

        // Use the first dropped item (file or directory)
        var droppedPath = files[0];
        _selectedPath = droppedPath;
        FilePathTextBox.Text = _selectedPath;
        AnalyzeButton.IsEnabled = true;
        ResultsTextBox.Text = string.Empty;
        LogEntriesGrid.ItemsSource = null;

        if (Directory.Exists(droppedPath))
            StatusText.Text = "Folder dropped. Click Analyze to start.";
        else
            StatusText.Text = "File dropped. Click Analyze to start.";
    }

    // ── Theme toggle (TODO 19) ──────────────────────────────────────

    private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        _isDarkTheme = !_isDarkTheme;
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        if (_isDarkTheme)
        {
            // Dark theme
            Background = new SolidColorBrush(WpfColor.FromRgb(0x1E, 0x1E, 0x2E));
            Foreground = new SolidColorBrush(WpfColor.FromRgb(0xCD, 0xD6, 0xF4));
            ThemeToggleButton.Content = "☀️ Light";
        }
        else
        {
            // Light theme (default)
            Background = new SolidColorBrush(WpfColor.FromRgb(0xFF, 0xFF, 0xFF));
            Foreground = new SolidColorBrush(WpfColor.FromRgb(0x00, 0x00, 0x00));
            ThemeToggleButton.Content = "🌙 Dark";
        }
    }

    // ── Browse dialogs ───────────────────────────────────────────────

    private void BrowseFileButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Apache Access Log File",
            Filter = "Log files (*.log)|*.log|Gzip files (*.gz)|*.gz|All files (*.*)|*.*",
            FilterIndex = 3
        };

        if (dialog.ShowDialog() == true)
        {
            _selectedPath = dialog.FileName;
            FilePathTextBox.Text = _selectedPath;
            AnalyzeButton.IsEnabled = true;
            ResultsTextBox.Text = string.Empty;
            LogEntriesGrid.ItemsSource = null;
            StatusText.Text = "File selected. Click Analyze to start.";
        }
    }

    private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Directory Containing Access Log Files"
        };

        if (dialog.ShowDialog() == true)
        {
            _selectedPath = dialog.FolderName;
            FilePathTextBox.Text = _selectedPath;
            AnalyzeButton.IsEnabled = true;
            ResultsTextBox.Text = string.Empty;
            LogEntriesGrid.ItemsSource = null;
            StatusText.Text = "Folder selected. Click Analyze to start.";
        }
    }

    // ── Analyze ──────────────────────────────────────────────────────

    private async void AnalyzeButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedPath))
            return;

        if (!int.TryParse(TopNTextBox.Text, out var top) || top <= 0)
        {
            MessageBox.Show("'Top N results' must be a positive integer.", "Invalid Input",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _top = top;
        AnalyzeButton.IsEnabled = false;
        BrowseFileButton.IsEnabled = false;
        BrowseFolderButton.IsEnabled = false;
        FilterGroupBox.IsEnabled = false;
        StatusText.Text = "Analyzing…";
        ResultsTextBox.Text = string.Empty;
        LogEntriesGrid.ItemsSource = null;
        _chartsLoaded = false;
        _perHostLoaded = false;
        _heatmapLoaded = false;

        try
        {
            var result = await Task.Run(() => RunAnalysis(_selectedPath, top));
            _allEntries = result.Entries;
            _analyzer = result.Analyzer;

            // Populate DataGrid
            LogEntriesGrid.ItemsSource = _allEntries;

            // Populate summary
            ResultsTextBox.Text = result.Summary;

            // Populate method filter combo with discovered methods
            PopulateMethodFilter();

            FilterGroupBox.IsEnabled = true;
            StatusText.Text = $"Analysis complete — {_allEntries.Count:N0} entries loaded.";
        }
        catch (Exception ex)
        {
            ResultsTextBox.Text = $"Error: {ex.Message}";
            StatusText.Text = "Analysis failed.";
        }
        finally
        {
            AnalyzeButton.IsEnabled = true;
            BrowseFileButton.IsEnabled = true;
            BrowseFolderButton.IsEnabled = true;
        }
    }

    // ── Quick-filter logic ───────────────────────────────────────────

    private void PopulateMethodFilter()
    {
        FilterMethodCombo.Items.Clear();
        FilterMethodCombo.Items.Add(new ComboBoxItem { Content = "All", IsSelected = true });

        if (_allEntries is not null)
        {
            foreach (var method in _allEntries.Select(e => e.Method).Distinct().OrderBy(m => m))
            {
                FilterMethodCombo.Items.Add(new ComboBoxItem { Content = method });
            }
        }

        FilterMethodCombo.SelectedIndex = 0;
    }

    private void FilterText_Changed(object sender, TextChangedEventArgs e) => ApplyFilters();
    private void FilterSelection_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilters();

    private void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        FilterStatusCombo.SelectedIndex = 0;
        if (FilterMethodCombo.Items.Count > 0)
            FilterMethodCombo.SelectedIndex = 0;
        FilterIpTextBox.Text = string.Empty;
        FilterEndpointTextBox.Text = string.Empty;
        FilterSearchTextBox.Text = string.Empty;
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        if (_allEntries is null)
            return;

        int? minStatus = null;
        int? maxStatus = null;

        // Status code filter
        if (FilterStatusCombo.SelectedItem is ComboBoxItem statusItem)
        {
            var statusText = statusItem.Content?.ToString();
            if (statusText == "2xx")
            {
                minStatus = 200;
                maxStatus = 299;
            }
            else if (statusText == "3xx")
            {
                minStatus = 300;
                maxStatus = 399;
            }
            else if (statusText == "4xx")
            {
                minStatus = 400;
                maxStatus = 499;
            }
            else if (statusText == "5xx")
            {
                minStatus = 500;
                maxStatus = 599;
            }
        }

        var ipFilter = FilterIpTextBox.Text?.Trim();
        var endpointFilter = FilterEndpointTextBox.Text?.Trim();
        var searchFilter = FilterSearchTextBox.Text?.Trim();

        IEnumerable<LogEntry> filtered = new LogAnalyzerService(_allEntries)
            .Filter(
                minStatus: minStatus,
                maxStatus: maxStatus,
                ip: ipFilter,
                endpointPattern: endpointFilter,
                searchTerm: searchFilter)
            .Entries;

        // Method filter
        if (FilterMethodCombo.SelectedItem is ComboBoxItem methodItem)
        {
            var methodText = methodItem.Content?.ToString();
            if (methodText is not null && methodText != "All")
                filtered = filtered.Where(e => e.Method.Equals(methodText, StringComparison.OrdinalIgnoreCase));
        }

        var list = filtered.ToList();
        LogEntriesGrid.ItemsSource = list;
        StatusText.Text = $"Showing {list.Count:N0} of {_allEntries.Count:N0} entries.";
    }

    // ── Lazy tab loading ────────────────────────────────────────────

    private void ResultsTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_analyzer is null)
            return;

        if (ResultsTabControl.SelectedItem == ChartsTab && !_chartsLoaded)
        {
            PopulateCharts();
            _chartsLoaded = true;
        }
        else if (ResultsTabControl.SelectedItem == HeatmapTab && !_heatmapLoaded)
        {
            PopulateHeatmap();
            _heatmapLoaded = true;
        }
        else if (ResultsTabControl.SelectedItem == PerHostTab && !_perHostLoaded)
        {
            PopulatePerHostDetails();
            _perHostLoaded = true;
        }
    }

    private void PopulateCharts()
    {
        if (_analyzer is null)
            return;

        ChartsPanel.Children.Clear();

        // ── Status Code Bar Chart ──
        var statusData = _analyzer.StatusCodeDistribution();
        if (statusData.Count > 0)
        {
            var statusChart = new ScottPlot.WPF.WpfPlot { Height = 350 };
            var labels = statusData.Select(s => s.StatusCode.ToString()).ToArray();
            var values = statusData.Select(s => (double)s.Count).ToArray();
            var positions = Enumerable.Range(0, statusData.Count).Select(i => (double)i).ToArray();
            statusChart.Plot.Add.Bars(positions, values);
            statusChart.Plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(positions, labels);
            statusChart.Plot.Title("Status Code Distribution");
            statusChart.Plot.YLabel("Request Count");
            statusChart.Plot.XLabel("Status Code");
            statusChart.Refresh();

            ChartsPanel.Children.Add(new TextBlock
            {
                Text = "Status Code Distribution",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 8, 0, 4)
            });
            ChartsPanel.Children.Add(statusChart);
        }

        // ── Hourly Distribution Line Chart ──
        var hourlyData = _analyzer.HourlyDistribution();
        if (hourlyData.Count > 0)
        {
            var hourlyChart = new ScottPlot.WPF.WpfPlot { Height = 350 };
            var hours = hourlyData.Select(h => (double)h.Hour).ToArray();
            var counts = hourlyData.Select(h => (double)h.Count).ToArray();
            hourlyChart.Plot.Add.Scatter(hours, counts);
            hourlyChart.Plot.Title("Hourly Request Distribution");
            hourlyChart.Plot.YLabel("Request Count");
            hourlyChart.Plot.XLabel("Hour of Day");
            hourlyChart.Refresh();

            ChartsPanel.Children.Add(new TextBlock
            {
                Text = "Hourly Request Distribution",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 16, 0, 4)
            });
            ChartsPanel.Children.Add(hourlyChart);
        }

        // Enable chart export buttons
        ExportChartPngButton.IsEnabled = true;
        ExportChartSvgButton.IsEnabled = true;
    }

    private void PopulatePerHostDetails()
    {
        if (_analyzer is null)
            return;

        PerHostGrid.ItemsSource = _analyzer.PerHostStatistics(_top);
    }

    // ── Chart Export (TODO 21) ──────────────────────────────────────

    private void ExportChartPng_Click(object sender, RoutedEventArgs e)
    {
        if (_analyzer is null)
            return;

        var dialog = new SaveFileDialog
        {
            Title = "Export Charts as PNG",
            Filter = "PNG Image (*.png)|*.png",
            DefaultExt = ".png",
            FileName = "log-charts.png"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                ExportChartsToFile(dialog.FileName, isPng: true);
                StatusText.Text = $"Charts exported to {dialog.FileName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting charts: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ExportChartSvg_Click(object sender, RoutedEventArgs e)
    {
        if (_analyzer is null)
            return;

        var dialog = new SaveFileDialog
        {
            Title = "Export Charts as SVG",
            Filter = "SVG Image (*.svg)|*.svg",
            DefaultExt = ".svg",
            FileName = "log-charts.svg"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                ExportChartsToFile(dialog.FileName, isPng: false);
                StatusText.Text = $"Charts exported to {dialog.FileName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting charts: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ExportChartsToFile(string filePath, bool isPng)
    {
        if (_analyzer is null)
            return;

        // Build a combined chart with status code bars and hourly line chart
        var plot = new ScottPlot.Plot();

        var statusData = _analyzer.StatusCodeDistribution();
        if (statusData.Count > 0)
        {
            var labels = statusData.Select(s => s.StatusCode.ToString()).ToArray();
            var values = statusData.Select(s => (double)s.Count).ToArray();
            var positions = Enumerable.Range(0, statusData.Count).Select(i => (double)i).ToArray();
            plot.Add.Bars(positions, values);
            plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(positions, labels);
            plot.Title("Status Code Distribution");
            plot.YLabel("Request Count");
            plot.XLabel("Status Code");
        }

        if (isPng)
        {
            plot.SavePng(filePath, 1200, 800);
        }
        else
        {
            plot.SaveSvg(filePath, 1200, 800);
        }
    }

    // ── Heatmap (TODO 24) ───────────────────────────────────────────

    private void PopulateHeatmap()
    {
        if (_analyzer is null)
            return;

        HeatmapPanel.Children.Clear();

        var heatmapData = _analyzer.HourlyDayOfWeekDistribution();
        if (heatmapData.Count == 0)
        {
            HeatmapPanel.Children.Add(new TextBlock
            {
                Text = "No data available for heatmap.",
                FontSize = 14,
                Foreground = Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0)
            });
            return;
        }

        HeatmapPanel.Children.Add(new TextBlock
        {
            Text = "Requests by Hour × Day of Week",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 8, 0, 8)
        });

        // Build a 7×24 grid heatmap
        int maxCount = heatmapData.Count > 0 ? heatmapData.Max(d => d.Count) : 1;
        if (maxCount == 0) maxCount = 1;

        string[] dayNames = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

        var grid = new Grid();

        // 1 header row + 7 day rows
        for (int r = 0; r <= 7; r++)
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(r == 0 ? 25 : 30) });

        // 1 label column + 24 hour columns
        for (int c = 0; c <= 24; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(c == 0 ? 40 : 30) });

        // Header row (hours)
        for (int h = 0; h < 24; h++)
        {
            var header = new TextBlock
            {
                Text = h.ToString("D2"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 10
            };
            Grid.SetRow(header, 0);
            Grid.SetColumn(header, h + 1);
            grid.Children.Add(header);
        }

        // Lookup for quick access
        var lookup = heatmapData.ToDictionary(d => ((int)d.Day, d.Hour), d => d.Count);

        // Day rows
        for (int d = 0; d < 7; d++)
        {
            var dayLabel = new TextBlock
            {
                Text = dayNames[d],
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetRow(dayLabel, d + 1);
            Grid.SetColumn(dayLabel, 0);
            grid.Children.Add(dayLabel);

            for (int h = 0; h < 24; h++)
            {
                int count = lookup.GetValueOrDefault((d, h), 0);
                double intensity = (double)count / maxCount;

                var cell = new Border
                {
                    Background = new SolidColorBrush(HeatmapColor(intensity)),
                    Margin = new Thickness(1),
                    ToolTip = $"{dayNames[d]} {h:D2}:00 — {count:N0} requests",
                    CornerRadius = new CornerRadius(2)
                };
                Grid.SetRow(cell, d + 1);
                Grid.SetColumn(cell, h + 1);
                grid.Children.Add(cell);
            }
        }

        HeatmapPanel.Children.Add(grid);

        // Legend
        var legendPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 12, 0, 0)
        };
        legendPanel.Children.Add(new TextBlock
        {
            Text = "Low",
            Margin = new Thickness(40, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        for (int i = 0; i <= 10; i++)
        {
            legendPanel.Children.Add(new Border
            {
                Width = 20,
                Height = 15,
                Background = new SolidColorBrush(HeatmapColor(i / 10.0)),
                Margin = new Thickness(1, 0, 1, 0),
                CornerRadius = new CornerRadius(2)
            });
        }
        legendPanel.Children.Add(new TextBlock
        {
            Text = "High",
            Margin = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        HeatmapPanel.Children.Add(legendPanel);
    }

    /// <summary>
    /// Interpolates a color from light green (low) to dark red (high) for the heatmap.
    /// </summary>
    private static WpfColor HeatmapColor(double intensity)
    {
        intensity = Math.Clamp(intensity, 0, 1);

        // Gradient: #ebedf0 (zero) → #9be9a8 (low) → #40c463 (mid) → #30a14e (high) → #216e39 (max)
        if (intensity < 0.01)
            return WpfColor.FromRgb(0xEB, 0xED, 0xF0);

        byte r, g, b;
        if (intensity < 0.25)
        {
            double t = intensity / 0.25;
            r = (byte)(0xEB + t * (0x9B - 0xEB));
            g = (byte)(0xED + t * (0xE9 - 0xED));
            b = (byte)(0xF0 + t * (0xA8 - 0xF0));
        }
        else if (intensity < 0.5)
        {
            double t = (intensity - 0.25) / 0.25;
            r = (byte)(0x9B + t * (0x40 - 0x9B));
            g = (byte)(0xE9 + t * (0xC4 - 0xE9));
            b = (byte)(0xA8 + t * (0x63 - 0xA8));
        }
        else if (intensity < 0.75)
        {
            double t = (intensity - 0.5) / 0.25;
            r = (byte)(0x40 + t * (0x30 - 0x40));
            g = (byte)(0xC4 + t * (0xA1 - 0xC4));
            b = (byte)(0x63 + t * (0x4E - 0x63));
        }
        else
        {
            double t = (intensity - 0.75) / 0.25;
            r = (byte)(0x30 + t * (0x21 - 0x30));
            g = (byte)(0xA1 + t * (0x6E - 0xA1));
            b = (byte)(0x4E + t * (0x39 - 0x4E));
        }

        return WpfColor.FromRgb(r, g, b);
    }

    // ── Analysis engine (background thread) ──────────────────────────

    private sealed record AnalysisResult(
        IReadOnlyList<LogEntry> Entries,
        LogAnalyzerService Analyzer,
        string Summary);

    /// <summary>
    /// Runs the log analysis on a background thread and returns parsed entries,
    /// the analyzer instance, and a formatted summary string.
    /// </summary>
    private static AnalysisResult RunAnalysis(string inputPath, int top)
    {
        var logFiles = LogFileDiscovery.Resolve(inputPath);
        if (logFiles.Count == 0)
        {
            var msg = Directory.Exists(inputPath)
                ? $"No access log files found in directory: '{inputPath}'"
                : $"File or directory not found: '{inputPath}'";
            return new AnalysisResult([], new LogAnalyzerService([]), msg);
        }

        var sb = new StringBuilder();
        var allEntries = new List<LogEntry>();
        int totalLines = 0;
        int filesProcessed = 0;

        foreach (var logFile in logFiles)
        {
            try
            {
                var lines = LogFileDiscovery.ReadLines(logFile);
                var (entries, fileLines) = LogParser.ParseLines(lines);
                allEntries.AddRange(entries);
                totalLines += fileLines;
                filesProcessed++;
            }
            catch (IOException ex)
            {
                sb.AppendLine($"Warning: Error reading '{Path.GetFileName(logFile)}': {ex.Message}");
            }
        }

        if (allEntries.Count == 0)
        {
            sb.AppendLine("No valid log entries found.");
            return new AnalysisResult(allEntries, new LogAnalyzerService([]), sb.ToString());
        }

        var analyzer = new LogAnalyzerService(allEntries);
        int skipped = totalLines - allEntries.Count;

        FormatReport(sb, analyzer, top, skipped, filesProcessed);
        return new AnalysisResult(allEntries, analyzer, sb.ToString());
    }

    /// <summary>
    /// Formats the analysis report into a string builder, including new metrics.
    /// </summary>
    private static void FormatReport(StringBuilder sb, LogAnalyzerService analyzer, int top, int skippedLines, int filesProcessed)
    {
        sb.AppendLine("═══════════════════════════════════════════");
        sb.AppendLine("       Apache Access Log Analysis");
        sb.AppendLine("═══════════════════════════════════════════");
        sb.AppendLine();

        if (filesProcessed > 1)
            sb.AppendLine($"  Files Analyzed:      {filesProcessed:N0}");
        sb.AppendLine($"  Total Requests:      {analyzer.TotalRequests:N0}");
        sb.AppendLine($"  Unique IPs:          {analyzer.UniqueIpCount:N0}");
        sb.AppendLine($"  Bytes Transferred:   {FormatBytes(analyzer.TotalBytesTransferred)}");
        sb.AppendLine($"  Avg Response Size:   {FormatBytes((long)analyzer.AverageResponseSize)}");
        sb.AppendLine($"  Requests/sec:        {analyzer.RequestsPerSecond:F2}");
        if (skippedLines > 0)
            sb.AppendLine($"  Skipped Lines:       {skippedLines:N0}");
        sb.AppendLine();

        // ── Error Summary ──
        var errors = analyzer.ErrorSummary();
        if (errors.Count > 0)
        {
            sb.AppendLine("── Error Summary ─────────────────────────");
            foreach (var (category, count) in errors)
            {
                sb.AppendLine($"  {category,-20} {count,8:N0}  ({100.0 * count / analyzer.TotalRequests:F1}%)");
            }
            sb.AppendLine();
        }

        sb.AppendLine("── Status Code Distribution ──────────────");
        foreach (var (code, count) in analyzer.StatusCodeDistribution())
        {
            sb.AppendLine($"  {code}  {count,8:N0}  ({100.0 * count / analyzer.TotalRequests:F1}%)");
        }
        sb.AppendLine();

        sb.AppendLine("── HTTP Methods ──────────────────────────");
        foreach (var (method, count) in analyzer.MethodDistribution())
        {
            sb.AppendLine($"  {method,-8} {count,8:N0}  ({100.0 * count / analyzer.TotalRequests:F1}%)");
        }
        sb.AppendLine();

        // ── Hourly Distribution ──
        sb.AppendLine("── Hourly Request Distribution ───────────");
        foreach (var (hour, count) in analyzer.HourlyDistribution())
        {
            if (count > 0)
                sb.AppendLine($"  {hour:D2}:00  {count,8:N0}");
        }
        sb.AppendLine();

        sb.AppendLine($"── Top {top} Endpoints ─────────────────────");
        foreach (var (endpoint, count) in analyzer.TopEndpoints(top))
        {
            sb.AppendLine($"  {count,8:N0}  {endpoint}");
        }
        sb.AppendLine();

        sb.AppendLine($"── Top {top} IPs ────────────────────────────");
        foreach (var (ip, count) in analyzer.TopIps(top))
        {
            sb.AppendLine($"  {count,8:N0}  {ip}");
        }
        sb.AppendLine();

        // ── Top 404 Endpoints ──
        var top404 = analyzer.Top404Endpoints(top);
        if (top404.Count > 0)
        {
            sb.AppendLine($"── Top {top} 404 Endpoints ─────────────────");
            foreach (var (endpoint, count) in top404)
            {
                sb.AppendLine($"  {count,8:N0}  {endpoint}");
            }
            sb.AppendLine();
        }

        // ── Per-Host Statistics ──
        var hostStats = analyzer.PerHostStatistics(top);
        if (hostStats.Count > 0)
        {
            sb.AppendLine($"── Top {top} Host Statistics ──────────────");
            sb.AppendLine($"  {"Host",-20} {"Reqs",8} {"Bytes",12} {"Errors",8} {"Err%",7}");
            sb.AppendLine($"  {new string('─', 57)}");
            foreach (var h in hostStats)
            {
                sb.AppendLine($"  {h.Host,-20} {h.Requests,8:N0} {FormatBytes(h.BytesTransferred),12} {h.ErrorCount,8:N0} {h.ErrorRate,7:P1}");
            }
            sb.AppendLine();
        }

        // ── Top Referers ──
        var topReferers = analyzer.TopReferers(top);
        if (topReferers.Count > 0)
        {
            sb.AppendLine($"── Top {top} Referers ─────────────────────");
            foreach (var (referer, count) in topReferers)
            {
                sb.AppendLine($"  {count,8:N0}  {referer}");
            }
            sb.AppendLine();
        }

        // ── Top User Agents ──
        var topAgents = analyzer.TopUserAgents(top);
        if (topAgents.Count > 0)
        {
            sb.AppendLine($"── Top {top} User Agents ──────────────────");
            foreach (var (agent, count) in topAgents)
            {
                sb.AppendLine($"  {count,8:N0}  {agent}");
            }
            sb.AppendLine();
        }

        // ── Bot / Crawler Detection ──
        var bots = analyzer.DetectBots(top);
        sb.AppendLine("── Bot / Crawler Detection ───────────────");
        sb.AppendLine($"  Bot Requests:        {bots.BotRequests:N0}  ({bots.BotPercentage:F1}%)");
        sb.AppendLine($"  Human Requests:      {bots.HumanRequests:N0}  ({100.0 - bots.BotPercentage:F1}%)");
        if (bots.TopBots.Count > 0)
        {
            sb.AppendLine($"  Top {top} Bot User-Agents:");
            foreach (var (agent, count) in bots.TopBots)
            {
                sb.AppendLine($"    {count,8:N0}  {agent}");
            }
        }
        sb.AppendLine();

        // ── Suspicious Requests ──
        var suspicious = analyzer.DetectSuspiciousRequests();
        if (suspicious.Count > 0)
        {
            sb.AppendLine("── Suspicious Requests ───────────────────");
            sb.AppendLine($"  Total Flagged:       {suspicious.Count:N0}");
            var byCategory = suspicious
                .GroupBy(s => s.Category)
                .OrderByDescending(g => g.Count());
            foreach (var group in byCategory)
            {
                sb.AppendLine($"  {group.Key,-22} {group.Count(),6:N0}");
            }
            sb.AppendLine();
            foreach (var s in suspicious.Take(top))
            {
                sb.AppendLine($"    [{s.Category}] {s.Entry.Method} {s.Entry.Endpoint} (from {s.Entry.RemoteHost})");
                sb.AppendLine($"      → {s.Reason}");
            }
            if (suspicious.Count > top)
                sb.AppendLine($"    … and {suspicious.Count - top} more");
            sb.AppendLine();
        }

        // ── Automated Summary ──
        sb.AppendLine("── Summary ───────────────────────────────");
        sb.AppendLine($"  {analyzer.GenerateSummary()}");
        sb.AppendLine();
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        int index = 0;
        while (value >= 1024 && index < suffixes.Length - 1)
        {
            value /= 1024;
            index++;
        }
        return $"{value:F2} {suffixes[index]}";
    }
}

/// <summary>
/// Converts an HTTP status code to a background brush for DataGrid row coloring.
/// 2xx → light green, 3xx → light yellow, 4xx → light orange, 5xx → light red.
/// </summary>
public sealed class StatusCodeToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Success = new(WpfColor.FromRgb(0xD4, 0xED, 0xDA)); // green
    private static readonly SolidColorBrush Redirect = new(WpfColor.FromRgb(0xFF, 0xF3, 0xCD)); // yellow
    private static readonly SolidColorBrush ClientError = new(WpfColor.FromRgb(0xFF, 0xE0, 0xB2)); // orange
    private static readonly SolidColorBrush ServerError = new(WpfColor.FromRgb(0xF8, 0xD7, 0xDA)); // red
    private static readonly SolidColorBrush Default = Brushes.Transparent;

    static StatusCodeToBrushConverter()
    {
        Success.Freeze();
        Redirect.Freeze();
        ClientError.Freeze();
        ServerError.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int statusCode)
        {
            return statusCode switch
            {
                >= 200 and < 300 => Success,
                >= 300 and < 400 => Redirect,
                >= 400 and < 500 => ClientError,
                >= 500 and < 600 => ServerError,
                _ => Default,
            };
        }
        return Default;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
