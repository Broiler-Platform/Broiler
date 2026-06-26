using System.Reflection;
using Broiler.HtmlBridge;

namespace Broiler.Cli.Tests;

public class HtmlBridgeBoundaryGuardTests
{
    private static readonly string[] AllowedCoreJavaScriptDependencies =
    [
        "Broiler.JavaScript.Engine",
    ];

    private static readonly string[] AllowedDomBridgeEngineInternalLeaks =
    [
        "Method:Attach(JSContext, String)",
        "Method:Attach(JSContext, String, String)",
        "Method:RegisterNamedElementGlobals(JSContext)",
        "Property:DocumentElement",
        "Property:Elements",
    ];

    [Fact]
    public void IScriptEngine_Surface_Does_Not_Expose_Engine_Internal_Types()
    {
        var exposedMembers = typeof(IScriptEngine)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Where(static member => member.MemberType is MemberTypes.Method or MemberTypes.Property)
            .Where(ExposesEngineInternalType)
            .Select(DescribeMember)
            .OrderBy(static member => member, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(exposedMembers);
    }

    [Fact]
    public void DomBridge_Engine_Internal_Leaks_Are_Frozen_To_The_Known_Compatibility_Surface()
    {
        var exposedMembers = typeof(DomBridge)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(static member => member.DeclaringType == typeof(DomBridge))
            .Where(static member => member.MemberType is MemberTypes.Method or MemberTypes.Property)
            .Where(ExposesEngineInternalType)
            .Select(DescribeMember)
            .OrderBy(static member => member, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(AllowedDomBridgeEngineInternalLeaks, exposedMembers);
    }

    [Fact]
    public void DomElement_Remains_Owned_By_Core_During_Phase_Zero()
    {
        Assert.Equal("Broiler.HtmlBridge.Core", typeof(DomElement).Assembly.GetName().Name);
        Assert.Equal("Broiler.HtmlBridge.Dom", typeof(DomBridge).Assembly.GetName().Name);
    }

    [Fact]
    public void Core_JavaScript_Dependencies_Are_Frozen_For_Dom_Extraction()
    {
        var javaScriptDependencies = typeof(DomElement).Assembly
            .GetReferencedAssemblies()
            .Select(static reference => reference.Name)
            .Where(static name => name?.StartsWith("Broiler.JavaScript.", StringComparison.Ordinal) == true)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(AllowedCoreJavaScriptDependencies, javaScriptDependencies);
    }

    [Fact]
    public void DomElement_Does_Not_Expose_JavaScript_State()
    {
        var properties = typeof(DomElement)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(static property => ContainsJavaScriptType(property.PropertyType, []))
            .Select(static property => property.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(properties);
    }

    [Fact]
    public void DomBridge_Runtime_State_Uses_Typed_Groups_Without_A_String_Property_Bag()
    {
        var bridgeAssembly = typeof(DomBridge).Assembly;
        var runtimeStateType = bridgeAssembly.GetType("Broiler.HtmlBridge.ElementRuntimeState");

        Assert.NotNull(runtimeStateType);
        Assert.Null(bridgeAssembly.GetType("Broiler.HtmlBridge.ElementRuntimeProperties"));
        Assert.DoesNotContain(
            runtimeStateType!.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            static property =>
                property.PropertyType.IsGenericType &&
                property.PropertyType.GetGenericTypeDefinition() == typeof(Dictionary<,>) &&
                property.PropertyType.GetGenericArguments() is [var keyType, var valueType] &&
                keyType == typeof(string) &&
                valueType == typeof(object));
    }

    [Fact]
    public void Phase_Three_Bridge_Owns_Canonical_Document_Without_A_Flat_Element_List()
    {
        Assert.Equal(typeof(Broiler.Dom.DomDocument), typeof(DomBridge).GetProperty(nameof(DomBridge.Document))?.PropertyType);
        Assert.True(typeof(Broiler.Dom.DomNode).IsAssignableFrom(typeof(DomElement)));
        Assert.DoesNotContain(
            typeof(DomBridge).GetFields(BindingFlags.NonPublic | BindingFlags.Instance),
            static field =>
                field.FieldType.IsGenericType &&
                field.FieldType.GetGenericTypeDefinition() == typeof(List<>) &&
                field.FieldType.GetGenericArguments()[0] == typeof(DomElement));
    }

    [Fact]
    public void Future_Dom_Kernel_Project_Must_Remain_Dependency_Free()
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(repositoryRoot, "src", "Broiler.Dom", "Broiler.Dom.csproj");
        if (!File.Exists(projectPath))
            return;

        var projectText = File.ReadAllText(projectPath);

        Assert.DoesNotContain("<ProjectReference", projectText, StringComparison.Ordinal);
        Assert.DoesNotContain("<PackageReference", projectText, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase_Four_Html_Contracts_Are_Owned_By_The_Companion_Assembly()
    {
        Assert.Equal("Broiler.Dom.Html", typeof(Broiler.Dom.Html.HtmlTokenizer).Assembly.GetName().Name);
        Assert.Equal("Broiler.Dom.Html", typeof(Broiler.Dom.Html.HtmlDocumentParser).Assembly.GetName().Name);
        Assert.Equal("Broiler.Dom.Html", typeof(Broiler.Dom.Html.HtmlSerializer).Assembly.GetName().Name);

        var legacyRendererAssembly = typeof(Broiler.HTML.Dom.Parse.HtmlParser).Assembly;
        Assert.Null(legacyRendererAssembly.GetType("Broiler.HTML.Dom.Parse.HtmlTokenizer"));
    }

    [Fact]
    public void Dom_Html_Depends_Only_On_The_Canonical_Dom_Component()
    {
        var broilerDependencies = typeof(Broiler.Dom.Html.HtmlDocumentParser).Assembly
            .GetReferencedAssemblies()
            .Select(static reference => reference.Name)
            .Where(static name => name?.StartsWith("Broiler.", StringComparison.Ordinal) == true)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["Broiler.Dom"], broilerDependencies);
    }

    [Fact]
    public void Phase_Five_Renderer_Exposes_A_Canonical_Document_Entry_Point()
    {
        var setDocument = typeof(Broiler.HTML.Image.HtmlContainer).GetMethod(
            "SetDocument",
            [typeof(Broiler.Dom.DomDocument), typeof(Broiler.HTML.Core.CssData), typeof(string)]);

        Assert.NotNull(setDocument);
        Assert.True(typeof(ITypedScriptEngine).IsAssignableFrom(typeof(ScriptEngine)));
        Assert.Equal(
            typeof(Broiler.Dom.DomDocument),
            typeof(ITypedScriptEngine).GetMethod(nameof(ITypedScriptEngine.ExecuteToDocument))?.ReturnType);
    }

    [Fact]
    public void Phase_Six_Traversal_State_Is_Owned_By_The_Canonical_Dom_Component()
    {
        Assert.Equal("Broiler.Dom", typeof(Broiler.Dom.DomTreeWalker).Assembly.GetName().Name);
        Assert.Equal("Broiler.Dom", typeof(Broiler.Dom.DomNodeIterator).Assembly.GetName().Name);
        Assert.Equal("Broiler.Dom", typeof(Broiler.Dom.DomRange).Assembly.GetName().Name);

        Assert.DoesNotContain(
            typeof(DomBridge).GetFields(BindingFlags.NonPublic | BindingFlags.Instance),
            static field =>
                field.FieldType.Name.Contains("TreeWalker", StringComparison.Ordinal) ||
                field.FieldType.ToString().Contains("IteratorState", StringComparison.Ordinal));
    }

    private static bool ExposesEngineInternalType(MemberInfo member) => member switch
    {
        MethodInfo method when !method.IsSpecialName =>
            HasEngineInternalType(method.ReturnType) || method.GetParameters().Any(parameter => HasEngineInternalType(parameter.ParameterType)),
        PropertyInfo property =>
            HasEngineInternalType(property.PropertyType),
        _ => false,
    };

    private static bool HasEngineInternalType(Type type)
    {
        if (type == typeof(DomElement))
            return true;

        if (type.Namespace?.StartsWith("Broiler.JavaScript.", StringComparison.Ordinal) == true)
            return true;

        if (type.IsArray)
            return HasEngineInternalType(type.GetElementType()!);

        return type.IsGenericType && type.GetGenericArguments().Any(HasEngineInternalType);
    }

    private static bool ContainsJavaScriptType(Type type, HashSet<Type> visited)
    {
        if (type == typeof(DomElement))
            return false;

        if (!visited.Add(type))
            return false;

        if (type.Namespace?.StartsWith("Broiler.JavaScript.", StringComparison.Ordinal) == true)
            return true;

        if (type.IsArray)
            return ContainsJavaScriptType(type.GetElementType()!, visited);

        if (type.IsGenericType &&
            type.GetGenericArguments().Any(argument => ContainsJavaScriptType(argument, visited)))
        {
            return true;
        }

        if (type.Assembly != typeof(DomElement).Assembly)
            return false;

        return type
            .GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Any(member => member switch
            {
                PropertyInfo property => ContainsJavaScriptType(property.PropertyType, visited),
                FieldInfo field => ContainsJavaScriptType(field.FieldType, visited),
                _ => false,
            });
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Broiler.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Broiler.slnx from the test output directory.");
    }

    private static string DescribeMember(MemberInfo member) => member switch
    {
        MethodInfo method => $"Method:{method.Name}({string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.Name))})",
        PropertyInfo property => $"Property:{property.Name}",
        _ => member.Name,
    };
}
