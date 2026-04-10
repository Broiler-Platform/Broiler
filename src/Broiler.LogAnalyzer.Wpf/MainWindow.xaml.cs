using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;

namespace Broiler.LogAnalyzer.Wpf;

/// <summary>
/// Main window for the Broiler Log Analyzer WPF application.
/// Allows users to select log files or directories, run analysis,
/// and view formatted results.
/// </summary>
public partial class MainWindow : Window
{
    private string? _selectedPath;

    public MainWindow()
    {
        InitializeComponent();
    }

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
            StatusText.Text = "Folder selected. Click Analyze to start.";
        }
    }

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

        AnalyzeButton.IsEnabled = false;
        BrowseFileButton.IsEnabled = false;
        BrowseFolderButton.IsEnabled = false;
        StatusText.Text = "Analyzing…";
        ResultsTextBox.Text = string.Empty;

        try
        {
            var result = await Task.Run(() => RunAnalysis(_selectedPath, top));
            ResultsTextBox.Text = result;
            StatusText.Text = "Analysis complete.";
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

    /// <summary>
    /// Runs the log analysis on a background thread and returns the formatted report.
    /// </summary>
    private static string RunAnalysis(string inputPath, int top)
    {
        var logFiles = LogFileDiscovery.Resolve(inputPath);
        if (logFiles.Count == 0)
        {
            return Directory.Exists(inputPath)
                ? $"No access log files found in directory: '{inputPath}'"
                : $"File or directory not found: '{inputPath}'";
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
            return sb.ToString();
        }

        var analyzer = new LogAnalyzerService(allEntries);
        int skipped = totalLines - allEntries.Count;

        FormatReport(sb, analyzer, top, skipped, filesProcessed);
        return sb.ToString();
    }

    /// <summary>
    /// Formats the analysis report into a string builder, mirroring the CLI output.
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
