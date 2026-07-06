using System.Xml.Linq;
using System.Reflection;
using Broiler.UI;
using Broiler.UI.Standard;

namespace Broiler.UI.Standard.Tests;

public sealed class StandardArchitectureTests
{
    private static readonly string[] ExpectedReferences =
    [
        "../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
        "../../Broiler.Input/Broiler.Input.Keyboard/Broiler.Input.Keyboard.csproj",
        "../../Broiler.Input/Broiler.Input.Mouse/Broiler.Input.Mouse.csproj",
        "../../Broiler.Input/Broiler.Input.Pen/Broiler.Input.Pen.csproj",
        "../../Broiler.Input/Broiler.Input.Text/Broiler.Input.Text.csproj",
        "../../Broiler.Input/Broiler.Input.Touch/Broiler.Input.Touch.csproj",
        "../Broiler.UI/Broiler.UI.csproj",
    ];

    [Fact]
    public void Standard_Project_Targets_Net10_And_References_Only_Ui_Graphics_And_Input_Abstractions()
    {
        string projectPath = Path.Combine(FindComponentRoot(), "Broiler.UI.Standard", "Broiler.UI.Standard.csproj");
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

        Assert.Equal(ExpectedReferences, references);
        Assert.DoesNotContain(references, reference => reference.Contains("Windows", StringComparison.Ordinal));
        Assert.DoesNotContain(references, reference => reference.Contains("Direct2D", StringComparison.Ordinal));
    }

    [Fact]
    public void Standard_Assembly_Contains_No_Public_Concrete_Controls()
    {
        Type[] publicUiElements = typeof(StandardUiSessionBuilder).Assembly
            .GetExportedTypes()
            .Where(static type => typeof(UiElement).IsAssignableFrom(type))
            .ToArray();

        Assert.Empty(publicUiElements);
    }

    [Fact]
    public void Standard_Assembly_Does_Not_Expose_Native_Handles_Or_Windows_Types()
    {
        Assert.Empty(FindForbiddenSurface(typeof(StandardUiSessionBuilder).Assembly.GetExportedTypes()));
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
