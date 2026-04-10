using System.Text;

namespace Broiler.LogAnalyzer;

/// <summary>
/// Generates ASCII-art chart representations of log analysis data.
/// Supports horizontal bar charts and sparkline-style hourly distributions.
/// </summary>
public static class AsciiChartService
{
    private const int DefaultBarWidth = 40;
    private static readonly char[] SparkBlocks = ['▁', '▂', '▃', '▄', '▅', '▆', '▇', '█'];

    /// <summary>
    /// Renders a horizontal bar chart for a list of label–count pairs.
    /// Each bar is scaled relative to the maximum value in the data set.
    /// </summary>
    /// <param name="title">Chart title displayed above the bars.</param>
    /// <param name="items">Ordered label–count pairs to chart.</param>
    /// <param name="barWidth">Maximum bar width in characters (default 40).</param>
    public static string HorizontalBarChart(
        string title,
        IReadOnlyList<(string Label, int Count)> items,
        int barWidth = DefaultBarWidth)
    {
        if (items.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine(title);
        sb.AppendLine(new string('─', title.Length));

        int maxCount = items.Max(i => i.Count);
        int maxLabel = items.Max(i => i.Label.Length);

        // Clamp maxLabel to a reasonable width to avoid very wide output
        maxLabel = Math.Min(maxLabel, 30);

        foreach (var (label, count) in items)
        {
            int barLen = maxCount > 0 ? (int)((double)count / maxCount * barWidth) : 0;
            // Ensure at least 1 block for non-zero values
            if (count > 0 && barLen == 0)
                barLen = 1;

            string truncatedLabel = label.Length > 30 ? label[..27] + "..." : label;
            sb.AppendLine($"  {truncatedLabel.PadRight(maxLabel)}  {new string('█', barLen)} {count:N0}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Renders a sparkline-style representation of hourly request distribution
    /// using Unicode block characters (▁▂▃▄▅▆▇█).
    /// </summary>
    /// <param name="hourlyData">24 hour–count pairs (hours 0–23).</param>
    public static string HourlySparkline(IReadOnlyList<(int Hour, int Count)> hourlyData)
    {
        if (hourlyData.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("Hourly Distribution (sparkline)");
        sb.AppendLine("───────────────────────────────");

        int maxCount = hourlyData.Max(h => h.Count);

        var sparkline = new StringBuilder();
        foreach (var (_, count) in hourlyData)
        {
            if (maxCount == 0 || count == 0)
            {
                sparkline.Append(' ');
            }
            else
            {
                int index = (int)((double)count / maxCount * (SparkBlocks.Length - 1));
                sparkline.Append(SparkBlocks[index]);
            }
        }

        sb.AppendLine($"  {sparkline}");

        // Hour labels row
        sb.Append("  ");
        for (int h = 0; h < 24; h++)
        {
            sb.Append(h % 6 == 0 ? (h / 10).ToString() : " ");
        }
        sb.AppendLine();
        sb.Append("  ");
        for (int h = 0; h < 24; h++)
        {
            sb.Append(h % 6 == 0 ? (h % 10).ToString() : " ");
        }
        sb.AppendLine();

        // Also show peak hour
        var peak = hourlyData.OrderByDescending(h => h.Count).First();
        if (peak.Count > 0)
        {
            sb.AppendLine($"  Peak: {peak.Hour:D2}:00 ({peak.Count:N0} requests)");
        }

        return sb.ToString();
    }
}
