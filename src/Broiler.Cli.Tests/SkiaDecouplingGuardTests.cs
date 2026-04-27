using System.Text.RegularExpressions;

namespace Broiler.Cli.Tests;

public class SkiaDecouplingGuardTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", ".."));

    private static readonly string[] ProductionDirectories =
    [
        Path.Combine(RepoRoot, "src", "Broiler.Cli"),
        Path.Combine(RepoRoot, "src", "Broiler.DevSite"),
        Path.Combine(RepoRoot, "src", "Broiler.Wpt"),
        Path.Combine(RepoRoot, "Broiler.HTML", "Source", "Broiler.HTML.WPF"),
    ];

    private static readonly Regex SkiaTokenPattern = new(
        @"using\s+SkiaSharp\s*;|SkiaSharp\.|\bSK[A-Z][A-Za-z0-9_]*\b",
        RegexOptions.Compiled);

    [Fact]
    public void NonImage_Production_Source_Does_Not_Reference_SkiaSharp()
    {
        var violations = new List<string>();

        foreach (var directory in ProductionDirectories)
        {
            Assert.True(Directory.Exists(directory), $"Production directory not found: {directory}");

            foreach (var path in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(RepoRoot, path);
                if (relativePath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                    relativePath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }

                var lines = File.ReadAllLines(path);
                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    if (line.StartsWith("//", StringComparison.Ordinal) ||
                        line.StartsWith("///", StringComparison.Ordinal) ||
                        line.StartsWith("*", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (SkiaTokenPattern.IsMatch(line))
                        violations.Add($"{relativePath}:{i + 1}: {lines[i].Trim()}");
                }
            }
        }

        Assert.True(violations.Count == 0,
            "Non-image production source should stay Skia-free.\n" + string.Join(Environment.NewLine, violations));
    }
}
