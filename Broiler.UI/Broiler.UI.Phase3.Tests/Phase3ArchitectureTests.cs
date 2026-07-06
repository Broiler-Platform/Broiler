using System.Reflection;
using System.Xml.Linq;
using Broiler.UI.Label;
using Broiler.UI.Label.Standard;
using Broiler.UI.Panel;
using Broiler.UI.Panel.Standard;
using Broiler.UI.Window;
using Broiler.UI.Window.Standard;

namespace Broiler.UI.Phase3.Tests;

public sealed class Phase3ArchitectureTests
{
    public static IEnumerable<object[]> RuntimeProjectReferences =>
    [
        [
            "Broiler.UI.Window/Broiler.UI.Window.csproj",
            new[]
            {
                "../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
                "../Broiler.UI/Broiler.UI.csproj",
            },
        ],
        [
            "Broiler.UI.Window.Standard/Broiler.UI.Window.Standard.csproj",
            new[]
            {
                "../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
                "../Broiler.UI.Standard/Broiler.UI.Standard.csproj",
                "../Broiler.UI.Window/Broiler.UI.Window.csproj",
            },
        ],
        [
            "Broiler.UI.Panel/Broiler.UI.Panel.csproj",
            new[]
            {
                "../Broiler.UI/Broiler.UI.csproj",
            },
        ],
        [
            "Broiler.UI.Panel.Standard/Broiler.UI.Panel.Standard.csproj",
            new[]
            {
                "../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
                "../Broiler.UI.Panel/Broiler.UI.Panel.csproj",
                "../Broiler.UI.Standard/Broiler.UI.Standard.csproj",
            },
        ],
        [
            "Broiler.UI.Label/Broiler.UI.Label.csproj",
            new[]
            {
                "../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
                "../Broiler.UI/Broiler.UI.csproj",
            },
        ],
        [
            "Broiler.UI.Label.Standard/Broiler.UI.Label.Standard.csproj",
            new[]
            {
                "../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
                "../Broiler.UI.Label/Broiler.UI.Label.csproj",
                "../Broiler.UI.Standard/Broiler.UI.Standard.csproj",
            },
        ],
    ];

    [Theory]
    [MemberData(nameof(RuntimeProjectReferences))]
    public void Phase3_Runtime_Projects_Target_Net10_And_Keep_Allowed_References(string relativeProjectPath, string[] expectedReferences)
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
    public void Standard_Assemblies_Expose_Exactly_One_Primary_Concrete_Control_Each()
    {
        AssertConcreteControls(typeof(StandardWindow).Assembly, typeof(StandardWindow));
        AssertConcreteControls(typeof(StandardPanel).Assembly, typeof(StandardPanel));
        AssertConcreteControls(typeof(StandardLabel).Assembly, typeof(StandardLabel));
    }

    [Fact]
    public void Abstraction_Assemblies_Expose_Abstract_Control_Bases()
    {
        Assert.True(typeof(UiWindow).IsAbstract);
        Assert.True(typeof(UiPanel).IsAbstract);
        Assert.True(typeof(UiLabel).IsAbstract);
    }

    [Fact]
    public void Phase3_Runtime_Assemblies_Do_Not_Expose_Native_Handles_Or_Windows_Types()
    {
        Assembly[] assemblies =
        [
            typeof(UiWindow).Assembly,
            typeof(StandardWindow).Assembly,
            typeof(UiPanel).Assembly,
            typeof(StandardPanel).Assembly,
            typeof(UiLabel).Assembly,
            typeof(StandardLabel).Assembly,
        ];

        Assert.Empty(assemblies.SelectMany(static assembly => FindForbiddenSurface(assembly.GetExportedTypes())));
    }

    private static void AssertConcreteControls(Assembly assembly, Type expected)
    {
        Type[] controls = assembly
            .GetExportedTypes()
            .Where(static type => typeof(UiElement).IsAssignableFrom(type) && !type.IsAbstract)
            .ToArray();

        Type actual = Assert.Single(controls);
        Assert.Equal(expected, actual);
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
