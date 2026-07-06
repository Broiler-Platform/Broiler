using System.Reflection;
using System.Xml.Linq;
using Broiler.UI.Dialog;
using Broiler.UI.Dialog.Standard;
using Broiler.UI.Edit.Standard;

namespace Broiler.UI.Phase7.Tests;

public sealed class Phase7ArchitectureTests
{
    public static IEnumerable<object[]> RuntimeProjectReferences =>
    [
        [
            "Broiler.UI.Dialog/Broiler.UI.Dialog.csproj",
            new[]
            {
                "../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
                "../Broiler.UI.Window/Broiler.UI.Window.csproj",
            },
        ],
        [
            "Broiler.UI.Dialog.Standard/Broiler.UI.Dialog.Standard.csproj",
            new[]
            {
                "../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
                "../Broiler.UI.Dialog/Broiler.UI.Dialog.csproj",
                "../Broiler.UI.Standard/Broiler.UI.Standard.csproj",
            },
        ],
    ];

    [Theory]
    [MemberData(nameof(RuntimeProjectReferences))]
    public void Phase7_Runtime_Projects_Target_Net10_And_Keep_Per_Control_References(string relativeProjectPath, string[] expectedReferences)
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
    public void Dialog_Assembly_Exposes_Abstract_Base_And_Standard_Assembly_One_Primary_Control()
    {
        Assert.True(typeof(UiDialog).IsAbstract);

        Type[] controls = typeof(StandardDialog).Assembly
            .GetExportedTypes()
            .Where(static type => typeof(UiElement).IsAssignableFrom(type) && !type.IsAbstract)
            .ToArray();

        Type actual = Assert.Single(controls);
        Assert.Equal(typeof(StandardDialog), actual);
    }

    [Fact]
    public void Phase7_Host_Ports_Are_Small_Neutral_And_Core_Owned()
    {
        Type[] expected =
        [
            typeof(IUiTextInputHost),
            typeof(IUiCursorHost),
            typeof(IUiDragDropHost),
            typeof(IUiAccessibilityHost),
            typeof(IUiSystemSettingsHost),
        ];

        Assert.All(expected, type =>
        {
            Assert.Equal(typeof(UiElement).Assembly, type.Assembly);
            Assert.True(type.IsInterface);
            Assert.InRange(type.GetMethods().Length + type.GetProperties().Length, 1, 3);
        });
    }

    [Fact]
    public void Phase7_Runtime_Assemblies_Do_Not_Expose_Native_Handles_Windows_Types_Or_Uia_Types()
    {
        Assembly[] assemblies =
        [
            typeof(UiElement).Assembly,
            typeof(UiDialog).Assembly,
            typeof(StandardDialog).Assembly,
            typeof(StandardEdit).Assembly,
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
                    member.Name.Contains("Hwnd", StringComparison.OrdinalIgnoreCase) ||
                    member.Name.Contains("AutomationPeer", StringComparison.Ordinal) ||
                    member.Name.Contains("Uia", StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add($"{type.FullName}.{member.Name}: forbidden native/UIA surface name");
                }

                foreach (Type memberType in GetMemberTypes(member))
                {
                    string? fullName = memberType.FullName;
                    if (memberType == typeof(IntPtr) ||
                        fullName?.StartsWith("System.Windows", StringComparison.Ordinal) == true ||
                        memberType.Namespace?.Contains(".Windows", StringComparison.Ordinal) == true ||
                        fullName?.Contains("Automation", StringComparison.Ordinal) == true ||
                        fullName?.Contains("UIAutomation", StringComparison.Ordinal) == true)
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
