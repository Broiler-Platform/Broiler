using System.Reflection;
using System.Xml.Linq;
using Broiler.UI.ComboBox;
using Broiler.UI.ComboBox.Standard;
using Broiler.UI.ListView;
using Broiler.UI.ListView.Standard;
using Broiler.UI.Menu;
using Broiler.UI.Menu.Standard;
using Broiler.UI.ScrollView;
using Broiler.UI.ScrollView.Standard;
using Broiler.UI.TabView;
using Broiler.UI.TabView.Standard;
using Broiler.UI.Tooltip;
using Broiler.UI.Tooltip.Standard;

namespace Broiler.UI.Phase6.Tests;

public sealed class Phase6ArchitectureTests
{
    public static IEnumerable<object[]> RuntimeProjectReferences =>
    [
        [
            "Broiler.UI.ScrollView/Broiler.UI.ScrollView.csproj",
            new[]
            {
                "../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
                "../Broiler.UI/Broiler.UI.csproj",
            },
        ],
        [
            "Broiler.UI.ScrollView.Standard/Broiler.UI.ScrollView.Standard.csproj",
            new[]
            {
                "../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
                "../Broiler.UI.ScrollView/Broiler.UI.ScrollView.csproj",
                "../Broiler.UI.Standard/Broiler.UI.Standard.csproj",
            },
        ],
        [
            "Broiler.UI.ListView/Broiler.UI.ListView.csproj",
            new[]
            {
                "../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
                "../Broiler.UI/Broiler.UI.csproj",
            },
        ],
        [
            "Broiler.UI.ListView.Standard/Broiler.UI.ListView.Standard.csproj",
            new[]
            {
                "../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
                "../Broiler.UI.ListView/Broiler.UI.ListView.csproj",
                "../Broiler.UI.Standard/Broiler.UI.Standard.csproj",
            },
        ],
        [
            "Broiler.UI.ComboBox/Broiler.UI.ComboBox.csproj",
            new[]
            {
                "../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
                "../Broiler.UI/Broiler.UI.csproj",
            },
        ],
        [
            "Broiler.UI.ComboBox.Standard/Broiler.UI.ComboBox.Standard.csproj",
            new[]
            {
                "../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
                "../Broiler.UI.ComboBox/Broiler.UI.ComboBox.csproj",
                "../Broiler.UI.Standard/Broiler.UI.Standard.csproj",
            },
        ],
        [
            "Broiler.UI.TabView/Broiler.UI.TabView.csproj",
            new[]
            {
                "../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
                "../Broiler.UI/Broiler.UI.csproj",
            },
        ],
        [
            "Broiler.UI.TabView.Standard/Broiler.UI.TabView.Standard.csproj",
            new[]
            {
                "../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
                "../Broiler.UI.Standard/Broiler.UI.Standard.csproj",
                "../Broiler.UI.TabView/Broiler.UI.TabView.csproj",
            },
        ],
        [
            "Broiler.UI.Menu/Broiler.UI.Menu.csproj",
            new[]
            {
                "../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
                "../Broiler.UI/Broiler.UI.csproj",
            },
        ],
        [
            "Broiler.UI.Menu.Standard/Broiler.UI.Menu.Standard.csproj",
            new[]
            {
                "../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
                "../Broiler.UI.Menu/Broiler.UI.Menu.csproj",
                "../Broiler.UI.Standard/Broiler.UI.Standard.csproj",
            },
        ],
        [
            "Broiler.UI.Tooltip/Broiler.UI.Tooltip.csproj",
            new[]
            {
                "../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
                "../Broiler.UI.Window/Broiler.UI.Window.csproj",
            },
        ],
        [
            "Broiler.UI.Tooltip.Standard/Broiler.UI.Tooltip.Standard.csproj",
            new[]
            {
                "../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
                "../Broiler.UI.Standard/Broiler.UI.Standard.csproj",
                "../Broiler.UI.Tooltip/Broiler.UI.Tooltip.csproj",
            },
        ],
    ];

    [Theory]
    [MemberData(nameof(RuntimeProjectReferences))]
    public void Phase6_Runtime_Projects_Target_Net10_And_Keep_Per_Control_References(string relativeProjectPath, string[] expectedReferences)
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
        Assert.DoesNotContain(references, reference => reference.Contains("Broiler.UI.ListView", StringComparison.Ordinal) && relativeProjectPath.StartsWith("Broiler.UI.ComboBox", StringComparison.Ordinal));
        Assert.DoesNotContain(references, reference => reference.Contains("Broiler.UI.ScrollView", StringComparison.Ordinal) && relativeProjectPath.StartsWith("Broiler.UI.ListView", StringComparison.Ordinal));
    }

    [Fact]
    public void Standard_Assemblies_Expose_Exactly_One_Primary_Concrete_Control_Each()
    {
        AssertConcreteControls(typeof(StandardScrollView).Assembly, typeof(StandardScrollView));
        AssertConcreteControls(typeof(StandardListView).Assembly, typeof(StandardListView));
        AssertConcreteControls(typeof(StandardComboBox).Assembly, typeof(StandardComboBox));
        AssertConcreteControls(typeof(StandardTabView).Assembly, typeof(StandardTabView));
        AssertConcreteControls(typeof(StandardMenu).Assembly, typeof(StandardMenu));
        AssertConcreteControls(typeof(StandardTooltip).Assembly, typeof(StandardTooltip));
    }

    [Fact]
    public void Abstraction_Assemblies_Expose_Abstract_Control_Bases()
    {
        Assert.True(typeof(UiScrollView).IsAbstract);
        Assert.True(typeof(UiListView).IsAbstract);
        Assert.True(typeof(UiComboBox).IsAbstract);
        Assert.True(typeof(UiTabView).IsAbstract);
        Assert.True(typeof(UiMenu).IsAbstract);
        Assert.True(typeof(UiTooltip).IsAbstract);
    }

    [Fact]
    public void Menu_Items_Are_Descriptors_Not_Public_Controls()
    {
        Assert.False(typeof(UiElement).IsAssignableFrom(typeof(UiMenuItem)));
        Assert.DoesNotContain(typeof(UiMenu).Assembly.GetExportedTypes(), type => type.Name == "UiMenuItemControl");
    }

    [Fact]
    public void Phase6_Runtime_Assemblies_Do_Not_Expose_Native_Handles_Or_Windows_Types()
    {
        Assembly[] assemblies =
        [
            typeof(UiScrollView).Assembly,
            typeof(StandardScrollView).Assembly,
            typeof(UiListView).Assembly,
            typeof(StandardListView).Assembly,
            typeof(UiComboBox).Assembly,
            typeof(StandardComboBox).Assembly,
            typeof(UiTabView).Assembly,
            typeof(StandardTabView).Assembly,
            typeof(UiMenu).Assembly,
            typeof(StandardMenu).Assembly,
            typeof(UiTooltip).Assembly,
            typeof(StandardTooltip).Assembly,
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
