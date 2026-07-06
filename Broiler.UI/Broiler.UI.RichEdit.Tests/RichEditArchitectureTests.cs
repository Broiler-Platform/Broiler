using System.Reflection;
using System.Xml.Linq;
using Broiler.UI;

namespace Broiler.UI.RichEdit.Tests;

public sealed class RichEditArchitectureTests
{
    private static readonly string[] ExpectedReferences =
    [
        "../../Broiler.Documents/Broiler.Documents.Model/Broiler.Documents.Model.csproj",
        "../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
        "../Broiler.UI/Broiler.UI.csproj",
    ];

    [Fact]
    public void RichEdit_Project_Targets_Net10_And_References_Only_DocumentsModel_Ui_And_Graphics()
    {
        XDocument project = XDocument.Load(RichEditProjectPath());

        Assert.Equal("net10.0", project.Descendants("TargetFramework").Single().Value);
        Assert.Empty(project.Descendants("PackageReference"));
        Assert.Equal(ExpectedReferences, ProjectReferences(project));
    }

    [Fact]
    public void RichEdit_Project_Does_Not_Reference_Dom_Windows_Or_Backends()
    {
        string[] references = ProjectReferences(XDocument.Load(RichEditProjectPath()));

        Assert.DoesNotContain(references, r => r.Contains("Dom", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, r => r.Contains("Windows", StringComparison.Ordinal));
        Assert.DoesNotContain(references, r => r.Contains("Direct2D", StringComparison.Ordinal));
    }

    [Fact]
    public void UiRichEdit_Is_The_Only_Control_And_Is_Abstract()
    {
        Type[] controls = typeof(UiRichEdit).Assembly
            .GetExportedTypes()
            .Where(static type => type.IsSubclassOf(typeof(UiElement)))
            .ToArray();

        Assert.Equal(new[] { typeof(UiRichEdit) }, controls);
        Assert.True(typeof(UiRichEdit).IsAbstract);
        Assert.Equal(typeof(UiElement), typeof(UiRichEdit).BaseType);
    }

    [Fact]
    public void Public_Surface_Exposes_No_Native_Handles_Or_Windows_Types()
    {
        Assert.Empty(FindForbiddenSurface(typeof(UiRichEdit).Assembly.GetExportedTypes()));
    }

    private static string RichEditProjectPath() =>
        Path.Combine(FindComponentRoot(), "Broiler.UI.RichEdit", "Broiler.UI.RichEdit.csproj");

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
                        memberType.Namespace?.Contains(".Windows", StringComparison.Ordinal) == true ||
                        memberType.Namespace?.Contains("Dom", StringComparison.Ordinal) == true)
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
