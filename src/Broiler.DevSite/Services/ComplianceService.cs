using System.Text.RegularExpressions;

namespace Broiler.DevSite.Services;

public record ChapterChecklist(string FileName, string Title, List<SectionChecklist> Sections, int TotalItems, int CompletedItems)
{
    public double CompletionPercent => TotalItems == 0 ? 0 : Math.Round((double)CompletedItems / TotalItems * 100, 1);
}

public record SectionChecklist(string Heading, List<ChecklistItem> Items);

public record ChecklistItem(string Text, bool IsChecked, string? TestId);

/// <summary>
/// Parses CSS 2.1 compliance checklists from the <c>css2/</c> directory.
/// Register as a singleton via <c>builder.Services.AddSingleton&lt;ComplianceService&gt;()</c>.
/// </summary>
public sealed partial class ComplianceService
{
    private readonly string _css2Directory;
    private readonly Lazy<List<ChapterChecklist>> _checklists;

    public ComplianceService(string contentRootPath)
    {
        _css2Directory = Path.GetFullPath(Path.Combine(contentRootPath, "..", "css2"));
        _checklists = new Lazy<List<ChapterChecklist>>(ParseAllChecklists);
    }

    public List<ChapterChecklist> GetChecklists() => _checklists.Value;

    private List<ChapterChecklist> ParseAllChecklists()
    {
        if (!Directory.Exists(_css2Directory))
            return [];

        var files = Directory.GetFiles(_css2Directory, "*-checklist.md");
        Array.Sort(files, (a, b) => NaturalCompare(Path.GetFileName(a), Path.GetFileName(b)));

        var results = new List<ChapterChecklist>(files.Length);
        foreach (var file in files)
        {
            results.Add(ParseChecklist(file));
        }
        return results;
    }

    private static ChapterChecklist ParseChecklist(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        var fileName = Path.GetFileName(filePath);
        string title = string.Empty;
        var sections = new List<SectionChecklist>();
        List<ChecklistItem>? currentItems = null;

        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(title) && line.StartsWith("# "))
            {
                title = line[2..].Trim();
                continue;
            }

            if (line.StartsWith("## "))
            {
                currentItems = [];
                sections.Add(new SectionChecklist(line[3..].Trim(), currentItems));
                continue;
            }

            if (line.StartsWith("- [x] ") || line.StartsWith("- [ ] "))
            {
                bool isChecked = line.StartsWith("- [x] ");
                string itemText = line[6..].Trim();
                string? testId = null;

                var match = TestIdRegex().Match(itemText);
                if (match.Success)
                {
                    testId = match.Groups[1].Value;
                }

                currentItems ??= [];
                if (sections.Count == 0)
                {
                    sections.Add(new SectionChecklist(string.Empty, currentItems));
                }

                currentItems.Add(new ChecklistItem(itemText, isChecked, testId));
            }
        }

        int total = sections.Sum(s => s.Items.Count);
        int completed = sections.Sum(s => s.Items.Count(i => i.IsChecked));

        return new ChapterChecklist(fileName, title, sections, total, completed);
    }

    private static int NaturalCompare(string a, string b)
    {
        var aNums = NumberExtractor().Matches(a);
        var bNums = NumberExtractor().Matches(b);

        // Extract leading text before first number for grouping (e.g., "appendix" vs "chapter")
        string aPrefix = aNums.Count > 0 ? a[..aNums[0].Index] : a;
        string bPrefix = bNums.Count > 0 ? b[..bNums[0].Index] : b;

        int prefixCmp = string.Compare(aPrefix, bPrefix, StringComparison.OrdinalIgnoreCase);
        if (prefixCmp != 0)
            return prefixCmp;

        // Compare numbers found in the filenames
        int count = Math.Min(aNums.Count, bNums.Count);
        for (int i = 0; i < count; i++)
        {
            int aVal = int.Parse(aNums[i].Value);
            int bVal = int.Parse(bNums[i].Value);
            if (aVal != bVal)
                return aVal.CompareTo(bVal);
        }

        return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"`([A-Za-z0-9_]+)`\s*$")]
    private static partial Regex TestIdRegex();

    [GeneratedRegex(@"\d+")]
    private static partial Regex NumberExtractor();
}
