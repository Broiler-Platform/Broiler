using System.Text.RegularExpressions;
using System.Reflection;
using System.Xml.Linq;
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

    private static readonly string CliTestsDirectory = Path.Combine(RepoRoot, "src", "Broiler.Cli.Tests");

    private static readonly HashSet<string> AllowedCliTestFilesWithSkiaReferences =
    [
        "GraphicsAbstractionTests.cs",
        "SkiaDecouplingGuardTests.cs",
    ];

    private static readonly Regex SkiaTokenPattern = new(
        @"using\s+SkiaSharp\s*;|SkiaSharp\.|\bSK[A-Z][A-Za-z0-9_]*\b",
        RegexOptions.Compiled);

    private static readonly string ImageProjectRelativePath = Path.Combine(
        "Broiler.HTML", "Source", "Broiler.HTML.Image", "Broiler.HTML.Image.csproj");

    private static readonly string ImageCompatProjectRelativePath = Path.Combine(
        "Broiler.HTML", "Source", "Broiler.HTML.Image.Compat", "Broiler.HTML.Image.Compat.csproj");

    private static readonly string[] AllowedSkiaPackageReferences =
    [
        "SkiaSharp",
        "SkiaSharp.NativeAssets.Linux",
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
    public void HighLevel_Rendering_Surface_Does_Not_Expose_SkiaSharp()
    {
        var members = GetHighLevelSkiaCompatibilityMembers();

        Assert.True(
            members.Length == 0,
            "High-level rendering surface should stay Skia-free.\n" +
            string.Join(Environment.NewLine, members.Select(DescribeMember)));
    }

    [Fact]
    public void NonAbstraction_Cli_Tests_Do_Not_Reference_SkiaSharp()
    {
        var violations = FindSkiaSourceViolations(
            CliTestsDirectory,
            static relativePath => !AllowedCliTestFilesWithSkiaReferences.Contains(Path.GetFileName(relativePath)));

        Assert.True(
            violations.Count == 0,
            "Only the explicit Skia seam tests should reference SkiaSharp inside Broiler.Cli.Tests.\n" +
            string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void No_Project_Carries_Skia_Package_References()
    {
        var actual = Directory.EnumerateFiles(RepoRoot, "*.csproj", SearchOption.AllDirectories)
            .Select(path => new
            {
                RelativePath = Path.GetRelativePath(RepoRoot, path),
                Packages = XDocument.Load(path)
                    .Descendants()
                    .Where(element => element.Name.LocalName == "PackageReference")
                    .Select(element => element.Attribute("Include")?.Value)
                    .Where(static value => !string.IsNullOrWhiteSpace(value))
                    .Select(static value => value!)
                    .Where(AllowedSkiaPackageReferences.Contains)
                    .OrderBy(static value => value, StringComparer.Ordinal)
                    .ToArray(),
            })
            .Where(project => project.Packages.Length > 0)
            .OrderBy(project => project.RelativePath, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(actual);
    }

    [Fact]
    public void Image_Compat_Project_Does_Not_Carry_SkiaSharp_Packages()
    {
        var projectPath = Path.Combine(RepoRoot, ImageCompatProjectRelativePath);
        var packages = XDocument.Load(projectPath)
            .Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Select(element => new
            {
                Include = element.Attribute("Include")?.Value,
                Version = element.Attribute("Version")?.Value,
            })
            .Where(static package => !string.IsNullOrWhiteSpace(package.Include))
            .ToDictionary(static package => package.Include!, static package => package.Version, StringComparer.Ordinal);

        Assert.DoesNotContain("SkiaSharp", packages.Keys);
        Assert.DoesNotContain("SkiaSharp.NativeAssets.Linux", packages.Keys);
    }

    [Fact]
    public void Image_Color_Parsing_Does_Not_Call_SkiaSharp_Color_Parser()
    {
        var adapterPath = Path.Combine(
            RepoRoot,
            "Broiler.HTML",
            "Source",
            "Broiler.HTML.Image.Compat",
            "Adapters",
            "StubImageAdapter.cs");

        var source = File.ReadAllText(adapterPath);

        Assert.DoesNotContain("SKColor.TryParse", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Image_System_Font_Enumeration_Does_Not_Call_Skia_Font_Manager()
    {
        var adapterPath = Path.Combine(
            RepoRoot,
            "Broiler.HTML",
            "Source",
            "Broiler.HTML.Image.Compat",
            "Adapters",
            "StubImageAdapter.cs");

        var source = File.ReadAllText(adapterPath);

        Assert.DoesNotContain("_typefaceResolver.GetSystemFontFamilies()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SKFontManager.Default.FontFamilies", source, StringComparison.Ordinal);
    }

    private static MemberInfo[] GetHighLevelSkiaCompatibilityMembers() =>
    [
        .. typeof(HtmlContainer).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => !method.IsSpecialName)
            .Where(HasSkiaExposure)
            .Cast<MemberInfo>(),
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

    private static List<string> FindSkiaSourceViolations(string rootDirectory, Func<string, bool> includeFile)
    {
        var violations = new List<string>();
        Assert.True(Directory.Exists(rootDirectory), $"Source directory not found: {rootDirectory}");

        foreach (var path in Directory.EnumerateFiles(rootDirectory, "*.cs", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(RepoRoot, path);
            if (relativePath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || relativePath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || !includeFile(relativePath))
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

                if (line.StartsWith("//", StringComparison.Ordinal)
                    || line.StartsWith("///", StringComparison.Ordinal))
                {
                    continue;
                }

                if (SkiaTokenPattern.IsMatch(line))
                    violations.Add($"{relativePath}:{i + 1}: {line}");
            }
        }

        return violations;
    }

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
