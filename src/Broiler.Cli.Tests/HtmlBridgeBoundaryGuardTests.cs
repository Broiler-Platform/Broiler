using System.Reflection;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;

namespace Broiler.Cli.Tests;

public class HtmlBridgeBoundaryGuardTests
{
    private static readonly string[] AllowedCoreJavaScriptDependencies =
    [
        "Broiler.JavaScript.Engine",
    ];

    // RF-BRIDGE-1c Phase F4 removed the DomElement facade, so DomBridge.DocumentElement /
    // Elements now expose canonical Broiler.Dom.DomElement — no longer an engine-internal
    // compatibility leak. Only the JSContext-bearing script-attach entry points remain.
    private static readonly string[] AllowedDomBridgeEngineInternalLeaks =
    [
        "Method:Attach(JSContext, String)",
        "Method:Attach(JSContext, String, String)",
        "Method:RegisterNamedElementGlobals(JSContext)",
    ];

    [Fact]
    public void IScriptEngine_Surface_Does_Not_Expose_Engine_Internal_Types()
    {
        // Phase 8 item 1 split IScriptEngine into segregated capability interfaces
        // (IScriptExecutor / IInteractiveScriptEngine / IScriptProfiling / IScriptEventLoop) that it now
        // inherits. For interfaces, GetMembers does not flatten inherited members, so the guard must span
        // IScriptEngine and every capability interface it aggregates to stay meaningful.
        var surfaceTypes = new[] { typeof(IScriptEngine) }
            .Concat(typeof(IScriptEngine).GetInterfaces());

        var exposedMembers = surfaceTypes
            .SelectMany(static t => t.GetMembers(BindingFlags.Public | BindingFlags.Instance))
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
    public void Core_And_Dom_Bridge_Assemblies_Are_Split_As_Expected()
    {
        // RF-BRIDGE-1c Phase F4: the DomElement facade that used to anchor "owned by Core"
        // is gone. The narrow runtime contract (IDomBridgeRuntime) stays in Core; the
        // DomBridge implementation stays in the Dom assembly.
        Assert.Equal("Broiler.HtmlBridge.Core", typeof(Broiler.HtmlBridge.Dom.IDomBridgeRuntime).Assembly.GetName().Name);
        Assert.Equal("Broiler.HtmlBridge.Dom", typeof(DomBridge).Assembly.GetName().Name);
    }

    [Fact]
    public void Core_JavaScript_Dependencies_Are_Frozen_For_Dom_Extraction()
    {
        var javaScriptDependencies = typeof(Broiler.HtmlBridge.Dom.IDomBridgeRuntime).Assembly
            .GetReferencedAssemblies()
            .Select(static reference => reference.Name)
            .Where(static name => name?.StartsWith("Broiler.JavaScript.", StringComparison.Ordinal) == true)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(AllowedCoreJavaScriptDependencies, javaScriptDependencies);
    }

    [Fact]
    public void DomBridge_Runtime_State_Uses_Typed_Groups_Without_A_String_Property_Bag()
    {
        var bridgeAssembly = typeof(DomBridge).Assembly;

        // Phase 2/4 de-globalization (2026-07-17): the former process-static, catch-all per-element
        // runtime-state composite (`ElementRuntimeState`, itself the successor to an even earlier
        // string->object `ElementRuntimeProperties` bag) has been split into one per-bridge typed
        // *RuntimeState DTO per concern in the internal Broiler.HtmlBridge.Dom.Runtime namespace, so no
        // single element-runtime-state type — and no property bag — remains. (The last remnant was
        // renamed `ElementRuntimeState` -> `InlineStyleRuntimeState` once it held only the inline-style
        // concern.) Both the monolithic composite and the old string bag must be gone.
        Assert.Null(bridgeAssembly.GetType("Broiler.HtmlBridge.Dom.Runtime.ElementRuntimeState"));
        Assert.Null(bridgeAssembly.GetType("Broiler.HtmlBridge.Dom.Runtime.ElementRuntimeProperties"));

        // The per-concern typed groups that replaced it live in that Runtime namespace; the inline-style
        // group (the renamed remnant) must be among them.
        var typedGroups = bridgeAssembly.GetTypes()
            .Where(static type =>
                type.Namespace == "Broiler.HtmlBridge.Dom.Runtime" &&
                type.Name.EndsWith("RuntimeState", StringComparison.Ordinal))
            .ToArray();

        Assert.Contains(typedGroups, static type => type.Name == "InlineStyleRuntimeState");

        // None of the typed groups is a string->object property bag. Typed value maps (e.g. the CSS
        // property dictionaries keyed by property name — Dictionary<string, string> / <string, JSValue>)
        // are legitimate; only a catch-all Dictionary<string, object> is the forbidden bag shape.
        foreach (var group in typedGroups)
        {
            var memberTypes = group
                .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Select(static property => property.PropertyType)
                .Concat(group
                    .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Select(static field => field.FieldType));

            Assert.DoesNotContain(
                memberTypes,
                static memberType =>
                    memberType.IsGenericType &&
                    memberType.GetGenericTypeDefinition() == typeof(Dictionary<,>) &&
                    memberType.GetGenericArguments() is [var keyType, var valueType] &&
                    keyType == typeof(string) &&
                    valueType == typeof(object));
        }
    }

    [Fact]
    public void Phase_Three_Bridge_Owns_Canonical_Document_Without_A_Flat_Element_List()
    {
        Assert.Equal(typeof(Broiler.Dom.DomDocument), typeof(DomBridge).GetProperty(nameof(DomBridge.Document))?.PropertyType);
        Assert.True(typeof(Broiler.Dom.DomNode).IsAssignableFrom(typeof(Broiler.Dom.DomElement)));
        Assert.DoesNotContain(
            typeof(DomBridge).GetFields(BindingFlags.NonPublic | BindingFlags.Instance),
            static field =>
                field.FieldType.IsGenericType &&
                field.FieldType.GetGenericTypeDefinition() == typeof(List<>) &&
                field.FieldType.GetGenericArguments()[0] == typeof(Broiler.Dom.DomElement));
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

    [Fact]
    public void Range_Ownership_Splits_Canonical_Boundaries_From_Renderer_Geometry()
    {
        Assert.Equal("Broiler.Dom", typeof(Broiler.Dom.DomRange).Assembly.GetName().Name);
        Assert.NotNull(typeof(DomBridge).GetMethod(
            "GetClientRectsForRange",
            BindingFlags.NonPublic | BindingFlags.Instance));
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
        // RF-BRIDGE-1c Phase F4: the DomElement facade is gone, so the only engine-internal
        // types the bridge can leak are the JavaScript engine's own. Canonical Broiler.Dom
        // element/document types on the public surface are the intended contract, not a leak.
        if (type.Namespace?.StartsWith("Broiler.JavaScript.", StringComparison.Ordinal) == true)
            return true;

        if (type.IsArray)
            return HasEngineInternalType(type.GetElementType()!);

        return type.IsGenericType && type.GetGenericArguments().Any(HasEngineInternalType);
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
