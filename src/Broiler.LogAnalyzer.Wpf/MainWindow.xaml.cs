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

namespace Broiler.LogAnalyzer.Wpf;

/// <summary>
/// Main window for the Broiler Log Analyzer WPF application.
/// Provides a DataGrid for browsing log entries with column sorting,
/// color-coded status rows, and a quick-filter toolbar.
/// </summary>
public partial class MainWindow : Window
{
    private string? _selectedPath;
    private IReadOnlyList<LogEntry>? _allEntries;
    private LogAnalyzerService? _analyzer;
    private int _top = 10;

    public MainWindow()
    {
        InitializeComponent();
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

    private void Filter_Changed(object sender, RoutedEventArgs e) => ApplyFilters();
    private void Filter_Changed(object sender, TextChangedEventArgs e) => ApplyFilters();
    private void Filter_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilters();

    private void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        FilterStatusCombo.SelectedIndex = 0;
        if (FilterMethodCombo.Items.Count > 0)
            FilterMethodCombo.SelectedIndex = 0;
        FilterIpTextBox.Text = string.Empty;
        FilterEndpointTextBox.Text = string.Empty;
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        if (_allEntries is null)
            return;

        IEnumerable<LogEntry> filtered = _allEntries;

        // Status code filter
        if (FilterStatusCombo.SelectedItem is ComboBoxItem statusItem)
        {
            var statusText = statusItem.Content?.ToString();
            if (statusText == "2xx")
                filtered = filtered.Where(e => e.StatusCode >= 200 && e.StatusCode < 300);
            else if (statusText == "3xx")
                filtered = filtered.Where(e => e.StatusCode >= 300 && e.StatusCode < 400);
            else if (statusText == "4xx")
                filtered = filtered.Where(e => e.StatusCode >= 400 && e.StatusCode < 500);
            else if (statusText == "5xx")
                filtered = filtered.Where(e => e.StatusCode >= 500 && e.StatusCode < 600);
        }

        // Method filter
        if (FilterMethodCombo.SelectedItem is ComboBoxItem methodItem)
        {
            var methodText = methodItem.Content?.ToString();
            if (methodText is not null && methodText != "All")
                filtered = filtered.Where(e => e.Method.Equals(methodText, StringComparison.OrdinalIgnoreCase));
        }

        // IP filter
        var ipFilter = FilterIpTextBox.Text?.Trim();
        if (!string.IsNullOrEmpty(ipFilter))
            filtered = filtered.Where(e => e.RemoteHost.Contains(ipFilter, StringComparison.OrdinalIgnoreCase));

        // Endpoint filter
        var endpointFilter = FilterEndpointTextBox.Text?.Trim();
        if (!string.IsNullOrEmpty(endpointFilter))
            filtered = filtered.Where(e => e.Endpoint.Contains(endpointFilter, StringComparison.OrdinalIgnoreCase));

        var list = filtered.ToList();
        LogEntriesGrid.ItemsSource = list;
        StatusText.Text = $"Showing {list.Count:N0} of {_allEntries.Count:N0} entries.";
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
    private static readonly SolidColorBrush Success = new(Color.FromRgb(0xD4, 0xED, 0xDA)); // green
    private static readonly SolidColorBrush Redirect = new(Color.FromRgb(0xFF, 0xF3, 0xCD)); // yellow
    private static readonly SolidColorBrush ClientError = new(Color.FromRgb(0xFF, 0xE0, 0xB2)); // orange
    private static readonly SolidColorBrush ServerError = new(Color.FromRgb(0xF8, 0xD7, 0xDA)); // red
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
