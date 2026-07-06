using System.Reflection;
using System.Xml.Linq;

namespace Broiler.UI.RichEdit.Standard.Tests;

public sealed class StandardRichEditArchitectureTests
{
    private static readonly string[] ExpectedReferences =
    [
        "../../Broiler.Documents/Broiler.Documents.Model/Broiler.Documents.Model.csproj",
        "../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
        "../Broiler.UI.RichEdit/Broiler.UI.RichEdit.csproj",
        "../Broiler.UI.Standard/Broiler.UI.Standard.csproj",
    ];

    [Fact]
    public void Standard_Project_Targets_Net10_And_References_Only_Approved_Assemblies()
    {
        XDocument project = XDocument.Load(StandardProjectPath());

        Assert.Equal("net10.0", project.Descendants("TargetFramework").Single().Value);
        Assert.Empty(project.Descendants("PackageReference"));
        Assert.Equal(ExpectedReferences, ProjectReferences(project));
    }

    [Fact]
    public void Standard_Project_Does_Not_Reference_Dom_Windows_Or_Backends()
    {
        string[] references = ProjectReferences(XDocument.Load(StandardProjectPath()));

        Assert.DoesNotContain(references, r => r.Contains("Dom", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, r => r.Contains("Windows", StringComparison.Ordinal));
        Assert.DoesNotContain(references, r => r.Contains("Direct2D", StringComparison.Ordinal));
    }

    [Fact]
    public void StandardRichEdit_Is_The_Only_Concrete_Control_And_Is_Sealed()
    {
        Type[] controls = typeof(StandardRichEdit).Assembly
            .GetExportedTypes()
            .Where(static type => typeof(UiElement).IsAssignableFrom(type) && !type.IsAbstract)
            .ToArray();

        Assert.Equal(new[] { typeof(StandardRichEdit) }, controls);
        Assert.True(typeof(StandardRichEdit).IsSealed);
        Assert.Equal(typeof(UiRichEdit), typeof(StandardRichEdit).BaseType);
    }

    [Fact]
    public void Public_Surface_Exposes_No_Native_Handles_Or_Windows_Types()
    {
        Assert.Empty(FindForbiddenSurface(typeof(StandardRichEdit).Assembly.GetExportedTypes()));
    }

    private static string StandardProjectPath() =>
        Path.Combine(FindComponentRoot(), "Broiler.UI.RichEdit.Standard", "Broiler.UI.RichEdit.Standard.csproj");

    private static string[] ProjectReferences(XDocument project) =>
        project
            .Descendants("ProjectReference")
            .Select(static reference => ((string?)reference.Attribute("Include"))?.Replace('\\', '/'))
            .Where(static reference => reference is not null)
            .Cast<string>()
            .OrderBy(static reference => reference, StringComparer.Ordinal)
            .ToArray();

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
}
