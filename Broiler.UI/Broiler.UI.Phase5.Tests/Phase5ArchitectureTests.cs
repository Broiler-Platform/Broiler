using System.Reflection;
using System.Xml.Linq;
using Broiler.Graphics;
using Broiler.UI.CheckBox;
using Broiler.UI.CheckBox.Standard;
using Broiler.UI.ImageView;
using Broiler.UI.ImageView.Standard;
using Broiler.UI.ProgressBar;
using Broiler.UI.ProgressBar.Standard;
using Broiler.UI.RadioButton;
using Broiler.UI.RadioButton.Standard;
using Broiler.UI.Slider;
using Broiler.UI.Slider.Standard;
using Broiler.UI.ToggleButton;
using Broiler.UI.ToggleButton.Standard;

namespace Broiler.UI.Phase5.Tests;

public sealed class Phase5ArchitectureTests
{
    public static IEnumerable<object[]> RuntimeProjectReferences =>
    [
        [
            "Broiler.UI.CheckBox/Broiler.UI.CheckBox.csproj",
            new[]
            {
                "../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
                "../Broiler.UI/Broiler.UI.csproj",
            },
        ],
        [
            "Broiler.UI.CheckBox.Standard/Broiler.UI.CheckBox.Standard.csproj",
            new[]
            {
                "../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
                "../Broiler.UI.CheckBox/Broiler.UI.CheckBox.csproj",
                "../Broiler.UI.Standard/Broiler.UI.Standard.csproj",
            },
        ],
        [
            "Broiler.UI.RadioButton/Broiler.UI.RadioButton.csproj",
            new[]
            {
                "../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
                "../Broiler.UI/Broiler.UI.csproj",
            },
        ],
        [
            "Broiler.UI.RadioButton.Standard/Broiler.UI.RadioButton.Standard.csproj",
            new[]
            {
                "../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
                "../Broiler.UI.RadioButton/Broiler.UI.RadioButton.csproj",
                "../Broiler.UI.Standard/Broiler.UI.Standard.csproj",
            },
        ],
        [
            "Broiler.UI.ToggleButton/Broiler.UI.ToggleButton.csproj",
            new[]
            {
                "../Broiler.UI.Button/Broiler.UI.Button.csproj",
            },
        ],
        [
            "Broiler.UI.ToggleButton.Standard/Broiler.UI.ToggleButton.Standard.csproj",
            new[]
            {
                "../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
                "../Broiler.UI.Standard/Broiler.UI.Standard.csproj",
                "../Broiler.UI.ToggleButton/Broiler.UI.ToggleButton.csproj",
            },
        ],
        [
            "Broiler.UI.Slider/Broiler.UI.Slider.csproj",
            new[]
            {
                "../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
                "../Broiler.UI/Broiler.UI.csproj",
            },
        ],
        [
            "Broiler.UI.Slider.Standard/Broiler.UI.Slider.Standard.csproj",
            new[]
            {
                "../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
                "../Broiler.UI.Slider/Broiler.UI.Slider.csproj",
                "../Broiler.UI.Standard/Broiler.UI.Standard.csproj",
            },
        ],
        [
            "Broiler.UI.ProgressBar/Broiler.UI.ProgressBar.csproj",
            new[]
            {
                "../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
                "../Broiler.UI/Broiler.UI.csproj",
            },
        ],
        [
            "Broiler.UI.ProgressBar.Standard/Broiler.UI.ProgressBar.Standard.csproj",
            new[]
            {
                "../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
                "../Broiler.UI.ProgressBar/Broiler.UI.ProgressBar.csproj",
                "../Broiler.UI.Standard/Broiler.UI.Standard.csproj",
            },
        ],
        [
            "Broiler.UI.ImageView/Broiler.UI.ImageView.csproj",
            new[]
            {
                "../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
                "../Broiler.UI/Broiler.UI.csproj",
            },
        ],
        [
            "Broiler.UI.ImageView.Standard/Broiler.UI.ImageView.Standard.csproj",
            new[]
            {
                "../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
                "../Broiler.UI.ImageView/Broiler.UI.ImageView.csproj",
                "../Broiler.UI.Standard/Broiler.UI.Standard.csproj",
            },
        ],
    ];

    [Theory]
    [MemberData(nameof(RuntimeProjectReferences))]
    public void Phase5_Runtime_Projects_Target_Net10_And_Keep_Per_Control_References(string relativeProjectPath, string[] expectedReferences)
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
        AssertConcreteControls(typeof(StandardCheckBox).Assembly, typeof(StandardCheckBox));
        AssertConcreteControls(typeof(StandardRadioButton).Assembly, typeof(StandardRadioButton));
        AssertConcreteControls(typeof(StandardToggleButton).Assembly, typeof(StandardToggleButton));
        AssertConcreteControls(typeof(StandardSlider).Assembly, typeof(StandardSlider));
        AssertConcreteControls(typeof(StandardProgressBar).Assembly, typeof(StandardProgressBar));
        AssertConcreteControls(typeof(StandardImageView).Assembly, typeof(StandardImageView));
    }

    [Fact]
    public void Abstraction_Assemblies_Expose_Abstract_Control_Bases()
    {
        Assert.True(typeof(UiCheckBox).IsAbstract);
        Assert.True(typeof(UiRadioButton).IsAbstract);
        Assert.True(typeof(UiToggleButton).IsAbstract);
        Assert.True(typeof(UiSlider).IsAbstract);
        Assert.True(typeof(UiProgressBar).IsAbstract);
        Assert.True(typeof(UiImageView).IsAbstract);
    }

    [Fact]
    public void ImageView_Public_Surface_Uses_Only_Graphics_Image_Handles_For_Image_Content()
    {
        PropertyInfo image = typeof(UiImageView).GetProperty(nameof(UiImageView.Image))!;
        Assert.Equal(typeof(BImageHandle), image.PropertyType);

        MemberInfo[] forbiddenMembers = typeof(UiImageView)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(static member =>
                member.Name.Contains("Path", StringComparison.OrdinalIgnoreCase) ||
                member.Name.Contains("File", StringComparison.OrdinalIgnoreCase) ||
                GetMemberTypes(member).Any(static type => type == typeof(byte[]) || type == typeof(Stream)))
            .ToArray();

        Assert.Empty(forbiddenMembers);
    }

    [Fact]
    public void Phase5_Runtime_Assemblies_Do_Not_Expose_Native_Handles_Or_Windows_Types()
    {
        Assembly[] assemblies =
        [
            typeof(UiCheckBox).Assembly,
            typeof(StandardCheckBox).Assembly,
            typeof(UiRadioButton).Assembly,
            typeof(StandardRadioButton).Assembly,
            typeof(UiToggleButton).Assembly,
            typeof(StandardToggleButton).Assembly,
            typeof(UiSlider).Assembly,
            typeof(StandardSlider).Assembly,
            typeof(UiProgressBar).Assembly,
            typeof(StandardProgressBar).Assembly,
            typeof(UiImageView).Assembly,
            typeof(StandardImageView).Assembly,
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
