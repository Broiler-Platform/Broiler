using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using Broiler.Graphics;

namespace Broiler.UI.Phase0.Tests;

public sealed class Phase0ArchitectureTests
{
    private static readonly string[] ExpectedControls =
    [
        "Window",
        "Panel",
        "Label",
        "Button",
        "Edit",
        "CheckBox",
        "RadioButton",
        "ToggleButton",
        "Slider",
        "ProgressBar",
        "ScrollView",
        "ListView",
        "ComboBox",
        "TabView",
        "Menu",
        "ImageView",
        "Dialog",
        "Tooltip",
    ];

    private static readonly string[] ExpectedAdrs =
    [
        "0001-ui-root-and-per-type-assembly-rule.md",
        "0002-logical-versus-native-windows.md",
        "0003-graphics-submission-boundary.md",
        "0004-input-and-text-service-boundary.md",
        "0005-ui-context-and-reentrancy.md",
        "0006-layout-protocol.md",
        "0007-implementation-factories.md",
        "0008-accessibility-semantic-bridge.md",
        "0009-edit-text-model.md",
        "0010-theme-and-visual-state-model.md",
        "0011-compatibility-removal.md",
        "0012-package-repository-topology.md",
    ];

    [Fact]
    public void Boundary_Manifest_Freezes_Approved_Control_Matrix_And_Owners()
    {
        using JsonDocument document = LoadBoundaryManifest();
        JsonElement root = document.RootElement;

        Assert.Equal("0", root.GetProperty("phase").GetString());
        Assert.Equal("approved-for-phase-1", root.GetProperty("status").GetString());
        Assert.Equal("Broiler.UI", root.GetProperty("componentRoot").GetString());

        string[] firstSlice = ReadStringArray(root.GetProperty("firstVerticalSlice"));
        Assert.Equal(["Window", "Panel", "Label", "Button", "Edit"], firstSlice);

        string[] controls = ReadStringArray(root.GetProperty("controlPairs"));
        Assert.Equal(ExpectedControls, controls);

        JsonElement owners = root.GetProperty("owners");
        foreach (string owner in new[]
        {
            "windowHosting",
            "inputTranslation",
            "focus",
            "accessibility",
            "clipboard",
            "cursor",
            "rendering",
            "nativeControls",
        })
        {
            Assert.True(owners.TryGetProperty(owner, out _), $"Missing owner '{owner}'.");
        }
    }

    [Fact]
    public void Runtime_Reference_Graph_Allows_Only_Platform_Neutral_Graphics_For_Graphics()
    {
        using JsonDocument document = LoadBoundaryManifest();
        JsonElement runtimeAssemblies = document.RootElement.GetProperty("runtimeAssemblies");

        foreach (JsonElement assembly in runtimeAssemblies.EnumerateArray())
        {
            string name = assembly.GetProperty("name").GetString()!;
            string[] references = ReadStringArray(assembly.GetProperty("allowedBroilerReferences"));

            Assert.DoesNotContain(references, reference => reference.Contains(".Windows", StringComparison.Ordinal));
            Assert.DoesNotContain(references, reference => reference.Contains("Direct2D", StringComparison.Ordinal));

            if (name is "Broiler.UI" or "Broiler.UI.Standard")
                Assert.Contains("Broiler.Graphics", references);
        }
    }

    [Fact]
    public void Required_Adrs_Are_Present_Approved_And_Listed_In_Manifest()
    {
        string componentRoot = FindComponentRoot();
        string adrRoot = Path.Combine(componentRoot, "docs", "adr");

        using JsonDocument document = LoadBoundaryManifest();
        string[] manifestAdrs = ReadStringArray(document.RootElement.GetProperty("requiredAdrs"));
        Assert.Equal(ExpectedAdrs, manifestAdrs);

        foreach (string adr in ExpectedAdrs)
        {
            string path = Path.Combine(adrRoot, adr);
            Assert.True(File.Exists(path), $"Missing ADR {adr}.");

            string text = File.ReadAllText(path);
            Assert.Contains("**Status:** Approved", text, StringComparison.Ordinal);
            Assert.Contains("## Decision", text, StringComparison.Ordinal);
            Assert.Contains("## Consequences", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Browser_Baseline_Artifact_Exists()
    {
        string repoRoot = FindRepoRoot();

        using JsonDocument document = LoadBoundaryManifest();
        string relative = document.RootElement.GetProperty("browserBaseline").GetString()!;
        string path = Path.GetFullPath(Path.Combine(Path.Combine(repoRoot, "Broiler.UI"), relative));

        Assert.True(File.Exists(path), $"Missing baseline artifact {path}.");
        Assert.True(new FileInfo(path).Length > 0, "Baseline artifact is empty.");
    }

    [Fact]
    public void Phase0_Test_Project_References_Only_Graphics_Core_Project()
    {
        string componentRoot = FindComponentRoot();
        string projectPath = Path.Combine(componentRoot, "Broiler.UI.Phase0.Tests", "Broiler.UI.Phase0.Tests.csproj");
        XDocument project = XDocument.Load(projectPath);

        string[] projectReferences = project
            .Descendants("ProjectReference")
            .Select(static element => ((string?)element.Attribute("Include"))?.Replace('\\', '/'))
            .Where(static include => include is not null)
            .Cast<string>()
            .OrderBy(static include => include, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj"], projectReferences);
        Assert.DoesNotContain(projectReferences, reference => reference.Contains("Windows", StringComparison.Ordinal));
        Assert.DoesNotContain(projectReferences, reference => reference.Contains("Direct2D", StringComparison.Ordinal));
    }

    [Fact]
    public void Graphics_Current_Ui_Shaped_Surface_Is_Frozen_For_Migration()
    {
        Assert.Equal(typeof(IntPtr), typeof(BWindow).GetProperty(nameof(BWindow.NativeHandle))!.PropertyType);
        Assert.Equal(typeof(IntPtr), typeof(BControl).GetProperty(nameof(BControl.NativeHandle))!.PropertyType);

        AssertPublicInstanceMethod(typeof(BWindow), nameof(BWindow.CreateEditControl), typeof(BControlOptions));
        AssertPublicInstanceMethod(typeof(BWindow), nameof(BWindow.CreateButtonControl), typeof(BControlOptions));
        AssertPublicInstanceMethod(typeof(BWindow), nameof(BWindow.CreateLabelControl), typeof(BControlOptions));
        AssertPublicInstanceMethod(typeof(BWindow), nameof(BWindow.StartAnimationTimer), typeof(double));
        AssertPublicInstanceMethod(typeof(BWindow), nameof(BWindow.StopAnimationTimer));

        Assert.True(typeof(BEditControl).GetEvent(nameof(BEditControl.TextChanged)) is not null);
        Assert.True(typeof(BEditControl).GetEvent(nameof(BEditControl.Submitted)) is not null);
        Assert.True(typeof(BButtonControl).GetEvent(nameof(BButtonControl.Clicked)) is not null);

        Assert.Equal(typeof(BRect), typeof(BControlOptions).GetProperty(nameof(BControlOptions.Bounds))!.PropertyType);
        Assert.Equal(typeof(string), typeof(BControlOptions).GetProperty(nameof(BControlOptions.Text))!.PropertyType);
        Assert.Equal(typeof(bool), typeof(BControlOptions).GetProperty(nameof(BControlOptions.Enabled))!.PropertyType);
        Assert.Equal(typeof(bool), typeof(BControlOptions).GetProperty(nameof(BControlOptions.Visible))!.PropertyType);
    }

    [Fact]
    public void Current_App_Control_Consumer_Inventory_Remains_Focused()
    {
        string repoRoot = FindRepoRoot();
        string srcRoot = Path.Combine(repoRoot, "src");

        string[] consumers = Directory
            .EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(ContainsGraphicsControlType)
            .Select(path => Path.GetRelativePath(repoRoot, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["src/Broiler.App.Graphics/BrowserWindow.cs"], consumers);
    }

    private static void AssertPublicInstanceMethod(Type type, string name, params Type[] parameterTypes)
    {
        MethodInfo? method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance, parameterTypes);
        Assert.True(method is not null, $"Expected public instance method {type.Name}.{name}.");
    }

    private static bool ContainsGraphicsControlType(string path)
    {
        string text = File.ReadAllText(path);
        return text.Contains("BControl", StringComparison.Ordinal) ||
               text.Contains("BButtonControl", StringComparison.Ordinal) ||
               text.Contains("BEditControl", StringComparison.Ordinal) ||
               text.Contains("BLabelControl", StringComparison.Ordinal) ||
               text.Contains("BControlOptions", StringComparison.Ordinal);
    }

    private static JsonDocument LoadBoundaryManifest()
    {
        string manifestPath = Path.Combine(FindComponentRoot(), "docs", "phase0", "phase0-boundary.json");
        return JsonDocument.Parse(File.ReadAllText(manifestPath));
    }

    private static string[] ReadStringArray(JsonElement array) =>
        array.EnumerateArray()
            .Select(static element => element.GetString()!)
            .ToArray();

    private static string FindComponentRoot() => Path.Combine(FindRepoRoot(), "Broiler.UI");

    private static string FindRepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Broiler.slnx")) &&
                Directory.Exists(Path.Combine(directory.FullName, "Broiler.UI")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"Repository root not found from {AppContext.BaseDirectory}.");
    }
}
