using System.Reflection;
using System.Xml.Linq;
using Broiler.UI.Toolbar.Standard;

namespace Broiler.UI.Toolbar.Tests;

public sealed class ToolbarArchitectureTests
{
    public static IEnumerable<object[]> RuntimeProjectReferences =>
    [
        [
            "src/Abstractions/Commands/Broiler.UI.Toolbar/Broiler.UI.Toolbar.csproj",
            new[]
            {
                "../../../../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
                "../../../Foundation/Broiler.UI/Broiler.UI.csproj",
            },
        ],
        [
            "src/Implementations/Standard/Commands/Broiler.UI.Toolbar.Standard/Broiler.UI.Toolbar.Standard.csproj",
            new[]
            {
                "../../../../../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
                "../../../../Abstractions/Commands/Broiler.UI.Toolbar/Broiler.UI.Toolbar.csproj",
                "../../../../Foundation/Broiler.UI.Standard/Broiler.UI.Standard.csproj",
            },
        ],
    ];

    [Theory]
    [MemberData(nameof(RuntimeProjectReferences))]
    public void Toolbar_Runtime_Projects_Target_Net10_And_Keep_Per_Control_References(string relativeProjectPath, string[] expectedReferences)
    {
        string projectPath = Path.Combine(FindComponentRoot(), relativeProjectPath.Replace('/', Path.DirectorySeparatorChar));
        XDocument project = XDocument.Load(projectPath);

        Assert.Equal("net10.0", project.Descendants("TargetFramework").Single().Value);
        Assert.Empty(project.Descendants("PackageReference"));

        string[] references = project
            .Descendants("ProjectReference")
            .Select(static reference => ((string?)reference.Attribute("Include"))?.Replace('\\', '/'))
            .Where(static reference => reference is not null)
            .Cast<string>()
            .OrderBy(static reference => reference, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedReferences.OrderBy(static reference => reference, StringComparer.Ordinal), references);
        Assert.DoesNotContain(references, reference => reference.Contains("Windows", StringComparison.Ordinal));
        Assert.DoesNotContain(references, reference => reference.Contains("Direct2D", StringComparison.Ordinal));
    }

    [Fact]
    public void Toolbar_Assembly_Exposes_Abstract_Base_And_Standard_Assembly_One_Primary_Control()
    {
        Assert.True(typeof(UiToolbar).IsAbstract);

        Type[] controls = typeof(StandardToolbar).Assembly
            .GetExportedTypes()
            .Where(static type => typeof(UiElement).IsAssignableFrom(type) && !type.IsAbstract)
            .ToArray();

        Type actual = Assert.Single(controls);
        Assert.Equal(typeof(StandardToolbar), actual);
    }

    [Fact]
    public void Toolbar_Runtime_Assemblies_Do_Not_Expose_Native_Handles_Or_Windows_Types()
    {
        Assembly[] assemblies =
        [
            typeof(UiToolbar).Assembly,
            typeof(StandardToolbar).Assembly,
        ];

        Assert.Empty(assemblies.SelectMany(static assembly => FindForbiddenSurface(assembly.GetExportedTypes())));
    }

    private static string FindComponentRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "Broiler.UI");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(directory.FullName, "Broiler.slnx")))
                return candidate;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Broiler.UI component root not found.");
    }

    private static string[] FindForbiddenSurface(IEnumerable<Type> types)
    {
        var violations = new List<string>();
        foreach (Type type in types)
        {
            foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                if (member.Name.Contains("NativeHandle", StringComparison.Ordinal) ||
                    member.Name.Contains("Hwnd", StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add($"{type.FullName}.{member.Name}: forbidden native handle name");
                }

                foreach (Type memberType in GetMemberTypes(member))
                {
                    if (memberType == typeof(IntPtr) ||
                        memberType.FullName?.StartsWith("System.Windows", StringComparison.Ordinal) == true ||
                        memberType.Namespace?.Contains(".Windows", StringComparison.Ordinal) == true)
                    {
                        violations.Add($"{type.FullName}.{member.Name}: {memberType.FullName}");
                    }
                }
            }
        }

        return violations.OrderBy(static violation => violation, StringComparer.Ordinal).ToArray();
    }

    private static IEnumerable<Type> GetMemberTypes(MemberInfo member) => member switch
    {
        MethodInfo method => [method.ReturnType, .. method.GetParameters().Select(static parameter => parameter.ParameterType)],
        PropertyInfo property => [property.PropertyType],
        FieldInfo field => [field.FieldType],
        EventInfo eventInfo when eventInfo.EventHandlerType is not null => [eventInfo.EventHandlerType],
        _ => [],
    };
}
