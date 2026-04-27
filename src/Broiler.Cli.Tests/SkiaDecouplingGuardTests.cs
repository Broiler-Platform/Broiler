using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Reflection;
using Broiler.HTML.Image;

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

    private static readonly string[] AllowedHighLevelSkiaCompatibilityMembers =
    [
        "HtmlRender.RenderToFile(String, Int32, Int32, String, SKEncodedImageFormat, Int32, SKColor, CssData, EventHandler<HtmlStylesheetLoadEventArgs>, EventHandler<HtmlImageLoadEventArgs>, String) -> Void",
        "HtmlRender.RenderToImage(String, Int32, Int32, SKColor, CssData, EventHandler<HtmlStylesheetLoadEventArgs>, EventHandler<HtmlImageLoadEventArgs>, String) -> SKBitmap",
        "HtmlRender.RenderToImageAutoSized(String, Int32, Int32, SKColor, CssData, EventHandler<HtmlStylesheetLoadEventArgs>, EventHandler<HtmlImageLoadEventArgs>) -> SKBitmap",
        "HtmlRender.RenderToPng(String, Int32, Int32, SKColor, CssData, EventHandler<HtmlStylesheetLoadEventArgs>, EventHandler<HtmlImageLoadEventArgs>) -> Byte[]",
        "PixelDiffResult.DiffImage -> SKBitmap",
        "PixelDiffRunner.Compare(SKBitmap, SKBitmap, DeterministicRenderConfig) -> PixelDiffResult",
    ];

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
                var inBlockComment = false;
                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();

                    if (inBlockComment)
                    {
                        if (line.Contains("*/", StringComparison.Ordinal))
                            inBlockComment = false;

                        continue;
                    }

                    if (line.StartsWith("/*", StringComparison.Ordinal))
                    {
                        if (!line.Contains("*/", StringComparison.Ordinal))
                            inBlockComment = true;

                        continue;
                    }

                    if (line.StartsWith("//", StringComparison.Ordinal) ||
                        line.StartsWith("///", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (SkiaTokenPattern.IsMatch(line))
                        violations.Add($"{relativePath}:{i + 1}: {line}");
                }
            }
        }

        Assert.True(violations.Count == 0,
            "Non-image production source should stay Skia-free.\n" + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void HighLevel_Rendering_Skia_Compatibility_Surface_Is_Frozen()
    {
        var members = GetHighLevelSkiaCompatibilityMembers();

        var actual = members
            .Select(DescribeMember)
            .OrderBy(static x => x, StringComparer.Ordinal)
            .ToArray();

        var expected = AllowedHighLevelSkiaCompatibilityMembers
            .OrderBy(static x => x, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);

        foreach (var member in members)
        {
            var attribute = member.GetCustomAttribute<EditorBrowsableAttribute>();
            Assert.NotNull(attribute);
            Assert.Equal(EditorBrowsableState.Never, attribute!.State);
        }
    }

    private static MemberInfo[] GetHighLevelSkiaCompatibilityMembers() =>
    [
        .. typeof(HtmlRender).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(HasSkiaExposure)
            .Cast<MemberInfo>(),
        .. typeof(PixelDiffRunner).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(HasSkiaExposure)
            .Cast<MemberInfo>(),
        .. typeof(PixelDiffResult).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(HasSkiaExposure)
            .Cast<MemberInfo>(),
    ];

    private static bool HasSkiaExposure(MethodInfo method) =>
        IsSkiaType(method.ReturnType) || method.GetParameters().Any(parameter => IsSkiaType(parameter.ParameterType));

    private static bool HasSkiaExposure(PropertyInfo property) => IsSkiaType(property.PropertyType);

    private static bool IsSkiaType(Type type)
    {
        if (type.Namespace == "SkiaSharp")
            return true;

        if (type.IsArray)
            return IsSkiaType(type.GetElementType()!);

        if (!type.IsGenericType)
            return false;

        return type.GetGenericArguments().Any(IsSkiaType);
    }

    private static string DescribeMember(MemberInfo member) => member switch
    {
        MethodInfo method => $"{method.DeclaringType!.Name}.{method.Name}({string.Join(", ", method.GetParameters().Select(parameter => DescribeType(parameter.ParameterType)))}) -> {DescribeType(method.ReturnType)}",
        PropertyInfo property => $"{property.DeclaringType!.Name}.{property.Name} -> {DescribeType(property.PropertyType)}",
        _ => member.Name,
    };

    private static string DescribeType(Type type)
    {
        if (type.IsArray)
            return $"{DescribeType(type.GetElementType()!)}[]";

        if (!type.IsGenericType)
            return type.Name;

        var name = type.Name;
        var tickIndex = name.IndexOf('`');
        if (tickIndex >= 0)
            name = name[..tickIndex];

        return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(DescribeType))}>";
    }
}
