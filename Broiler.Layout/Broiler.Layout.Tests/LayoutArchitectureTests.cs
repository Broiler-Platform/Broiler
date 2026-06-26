using System.Reflection;
using System.Xml.Linq;

namespace Broiler.Layout.Tests;

/// <summary>
/// Freezes the <c>Broiler.Layout</c> dependency boundary for the layout extraction
/// (see <c>docs/roadmap/broiler-layout-component.md</c> §3, §7). The component may
/// reference only <c>Broiler.CSS</c>, <c>Broiler.CSS.Dom</c>, <c>Broiler.Dom</c>
/// and the BCL; it must not leak the renderer, bridge, JavaScript or graphics
/// backends through its public surface.
/// </summary>
public sealed class LayoutArchitectureTests
{
    [Fact]
    public void Production_Project_References_Only_Css_And_Dom()
    {
        var project = XDocument.Load(FindProjectPath());
        var references = project
            .Descendants("ProjectReference")
            .Select(static element => Path.GetFileNameWithoutExtension((string?)element.Attribute("Include")))
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["Broiler.CSS", "Broiler.CSS.Dom", "Broiler.Dom"], references);
        Assert.Empty(project.Descendants("PackageReference"));
    }

    [Fact]
    public void Public_Surface_Does_Not_Leak_Consumer_Types()
    {
        var assembly = typeof(ILayoutEnvironment).Assembly;
        var forbidden = assembly.GetExportedTypes()
            .SelectMany(GetMemberTypes)
            .Where(static type => type.Namespace is not null)
            .Where(static type =>
                type.Namespace!.StartsWith("Broiler.HtmlBridge", StringComparison.Ordinal) ||
                type.Namespace.StartsWith("Broiler.HTML", StringComparison.Ordinal) ||
                type.Namespace.StartsWith("Broiler.JavaScript", StringComparison.Ordinal) ||
                type.Namespace.StartsWith("Broiler.Graphics", StringComparison.Ordinal))
            .Distinct()
            .ToArray();

        Assert.Empty(forbidden);
    }

    [Fact]
    public void Public_Surface_Does_Not_Expose_Mutable_Collections()
    {
        var assembly = typeof(ILayoutEnvironment).Assembly;
        var mutable = assembly.GetExportedTypes()
            .SelectMany(GetMemberTypes)
            .Where(static type => type.IsGenericType)
            .Select(static type => type.GetGenericTypeDefinition())
            .Where(static definition =>
                definition == typeof(List<>) ||
                definition == typeof(Dictionary<,>) ||
                definition == typeof(HashSet<>))
            .Distinct()
            .ToArray();

        Assert.Empty(mutable);
    }

    private static IEnumerable<Type> GetMemberTypes(Type type)
    {
        yield return type;
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            yield return method.ReturnType;
            foreach (var parameter in method.GetParameters())
                yield return parameter.ParameterType;
        }
        foreach (var property in type.GetProperties())
            yield return property.PropertyType;
    }

    private static string FindProjectPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, "Broiler.Layout", "Broiler.Layout", "Broiler.Layout.csproj");
            if (File.Exists(path))
                return path;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException($"Broiler.Layout.csproj not found walking up from {AppContext.BaseDirectory}");
    }
}
