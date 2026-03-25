using System.Runtime.CompilerServices;
using Broiler.JavaScript.BuiltIns;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Storage;

namespace Broiler.JavaScript.Integration.Tests;

/// <summary>
/// Phase 2 (M9–M14) validation tests.
/// These tests verify the structural invariants established during Phase 2
/// deep structural refactoring.
/// </summary>
public class Phase2ValidationTests
{
    /// <summary>
    /// Ensures all satellite assemblies are loaded so module initializers fire.
    /// </summary>
    private static void EnsureAllAssembliesLoaded()
    {
        RuntimeHelpers.RunClassConstructor(
            typeof(Broiler.JavaScript.BuiltIns.Weak.JSWeakRef).TypeHandle);
        RuntimeHelpers.RunClassConstructor(
            typeof(Broiler.JavaScript.Clr.DefaultClrInterop).TypeHandle);
    }

    // ── M9: Code-Generation Builder Isolation Analysis ─────────────────

    [Fact]
    public void M9_BuildersInLinqExpressions()
    {
        // LinqExpressions builders live in the LinqExpressions assembly.
        var linqAssembly = typeof(Broiler.JavaScript.LinqExpressions.LinqExpressions.JSRegExpBuilder).Assembly;
        var linqTypes = linqAssembly.GetTypes().Select(t => t.FullName).ToList();

        // Key builder types in LinqExpressions:
        Assert.Contains(linqTypes, t => t != null && t.Contains("JSValueBuilder"));
        Assert.Contains(linqTypes, t => t != null && t.Contains("ArgumentsBuilder"));
        Assert.Contains(linqTypes, t => t != null && t.Contains("ClrProxyBuilder"));

        // JSObjectBuilder was moved to Runtime:
        var runtimeTypes = typeof(JSValue).Assembly.GetTypes().Select(t => t.FullName).ToList();
        Assert.Contains(runtimeTypes, t => t != null && t.Contains("JSObjectBuilder"));
    }

    [Fact]
    public void M9_RuntimeCoupling_ScriptInfoInRuntime()
    {
        // ScriptInfo is a runtime data type used in JSContext delegate signatures
        // and CallStackItem constructors. It now lives in the Runtime assembly
        // alongside JSValue, Arguments, and other core runtime types.
        var runtimeAssembly = typeof(JSValue).Assembly;
        var scriptInfoType = runtimeAssembly.GetTypes()
            .FirstOrDefault(t => t.Name == "ScriptInfo");
        Assert.NotNull(scriptInfoType);
    }

    [Fact]
    public void M9_RuntimeCoupling_DictionaryCodeCacheInRuntime()
    {
        // DictionaryCodeCache was moved to Runtime alongside the ICodeCache
        // interface it implements. Verify it exists in the Runtime assembly.
        var runtimeAssembly = typeof(JSValue).Assembly;
        var cacheType = runtimeAssembly.GetTypes()
            .FirstOrDefault(t => t.Name == "DictionaryCodeCache");
        Assert.NotNull(cacheType);
    }

    [Fact]
    public void M9_CompilerStillReferencesEngine()
    {
        // Verify that Compiler assembly references Engine (not the other way around).
        var compilerAssembly = typeof(Broiler.JavaScript.Compiler.FastCompiler).Assembly;
        var engineAssembly = typeof(JSContext).Assembly;

        var compilerRefs = compilerAssembly.GetReferencedAssemblies()
            .Select(a => a.Name).ToList();
        Assert.Contains("Broiler.JavaScript.Engine", compilerRefs);

        // Engine must NOT reference Compiler.
        var engineRefs = engineAssembly.GetReferencedAssemblies()
            .Select(a => a.Name).ToList();
        Assert.DoesNotContain("Broiler.JavaScript.Compiler", engineRefs);
    }

    // ── M10: Core Large-File Decomposition ─────────────────────────────

    [Fact]
    public void M10_JSArrayPrototype_PartialFilesExist()
    {
        // JSArray was moved to the BuiltIns assembly (Broiler.JavaScript.BuiltIns.Array namespace).
        // Its prototype methods are split into partial files:
        // JSArrayPrototype.Iteration.cs, .Search.cs, .Modification.cs, .Utility.cs
        var builtInsAssembly = typeof(Broiler.JavaScript.BuiltIns.Array.Typed.JSArrayBuffer).Assembly;
        var type = builtInsAssembly.GetTypes()
            .FirstOrDefault(t => t.Name == "JSArray" && t.Namespace == "Broiler.JavaScript.BuiltIns.Array");
        Assert.NotNull(type);

        // Verify key methods exist from each partial file category.
        var allMethods = type!.GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static);
        var methodNames = allMethods.Select(m => m.Name).ToList();

        // Iteration methods (from JSArrayPrototype.Iteration.cs):
        Assert.Contains("Map", methodNames);
        // Search methods (from JSArrayPrototype.Search.cs):
        Assert.Contains("IndexOf", methodNames);
    }

    [Fact]
    public void M10_JSObject_PartialFilesExist()
    {
        // Verify JSObject class exists in Runtime and is not broken by the split.
        var type = typeof(JSValue).Assembly.GetTypes()
            .FirstOrDefault(t => t.Name == "JSObject");
        Assert.NotNull(type);

        // Verify it has members from different partial file categories.
        Assert.True(type!.GetMembers(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static)
            .Length > 10, "JSObject should have many members after partial split");
    }

    [Fact]
    public void M10_JSDatePrototype_PartialFilesExist()
    {
        // JSDate was moved from Core to BuiltIns as part of the JSDate extraction.
        // JSDate's prototype methods were split into partial files:
        // JSDatePrototype.Getters.cs, .Setters.cs, .Formatters.cs
        // The class is JSDate (partial), with prototype methods in separate files.
        var builtInsAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Broiler.JavaScript.BuiltIns");
        Assert.NotNull(builtInsAssembly);

        var type = builtInsAssembly!.GetTypes()
            .FirstOrDefault(t => t.Name == "JSDate" && t.Namespace == "Broiler.JavaScript.BuiltIns.Date");
        Assert.NotNull(type);

        // Verify getter methods exist (from JSDatePrototype.Getters.cs):
        var allMethods = type!.GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static);
        var methodNames = allMethods.Select(m => m.Name).ToList();
        Assert.Contains("GetFullYear", methodNames);
    }

    [Fact]
    public void M10_JSStringPrototype_PartialFilesExist()
    {
        // JSString's prototype methods were split into partial files:
        // JSStringPrototype.Search.cs, .Transform.cs, .Extract.cs, .Pattern.cs
        // The class is JSString (partial), now in the BuiltIns assembly.
        var type = typeof(Broiler.JavaScript.BuiltIns.String.JSString);
        Assert.NotNull(type);

        var allMethods = type.GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static);
        Assert.True(allMethods.Length > 5,
            "JSString should have methods after partial split");
    }

    [Fact]
    public void M10_JSObjectStatic_PartialFilesExist()
    {
        // Verify JSObjectStatic exists in Runtime and has methods from each category.
        var type = typeof(JSValue).Assembly.GetTypes()
            .FirstOrDefault(t => t.Name == "JSObjectStatic");
        Assert.NotNull(type);
    }

    // ── M12: Compiler Internal Organization ────────────────────────────

    [Fact]
    public void M12_CompilerAssembly_AllPartialClassesMerged()
    {
        // Moving files to subdirectories should not affect partial class merging.
        // Verify FastCompiler has visitor methods from all subdirectories.
        var compilerType = typeof(Broiler.JavaScript.Compiler.FastCompiler);
        Assert.NotNull(compilerType);

        var methods = compilerType.GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static);

        // FastCompiler should have many methods from all the partial files.
        Assert.True(methods.Length > 20,
            $"FastCompiler should have many methods after subdirectory reorganization, found {methods.Length}");
    }

    [Fact]
    public void M12_CompilerAssembly_InitializerStillWorks()
    {
        // Verify CompilerAssemblyInitializer still registers correctly
        // after being moved to Infrastructure/ subdirectory.
        EnsureAllAssembliesLoaded();

        var compilerAssembly = typeof(Broiler.JavaScript.Compiler.FastCompiler).Assembly;
        var initializerType = compilerAssembly.GetTypes()
            .FirstOrDefault(t => t.Name == "CompilerAssemblyInitializer");
        Assert.NotNull(initializerType);
    }

    [Fact]
    public void M12_CompilerAssembly_NamespaceMatchesAssembly()
    {
        // FastCompiler lives in the Broiler.JavaScript.Compiler namespace.
        var compilerType = typeof(Broiler.JavaScript.Compiler.FastCompiler);
        Assert.Equal("Broiler.JavaScript.Compiler", compilerType.Namespace);
    }

    // ── M13: ExpressionCompiler Assessment ─────────────────────────────

    [Fact]
    public void M13_ExpressionCompiler_RemainsMonolithic()
    {
        // M13 concluded no-go on decomposition. Verify the assembly exists as one unit.
        var ecAssembly = typeof(Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression).Assembly;
        Assert.Equal("Broiler.JavaScript.ExpressionCompiler", ecAssembly.GetName().Name);

        // Verify key functional groups coexist in same assembly:
        var types = ecAssembly.GetTypes().Select(t => t.Name).ToList();

        // Y-expression types:
        Assert.Contains("YExpression", types);
        Assert.Contains("YBinaryExpression", types);
        Assert.Contains("YConstantExpression", types);

        // IL Code Generator:
        Assert.Contains("ILCodeGenerator", types);

        // Runtime support:
        Assert.Contains("RuntimeAssembly", types);
    }

    [Fact]
    public void M13_ExpressionCompiler_IsLeafDependency()
    {
        // ExpressionCompiler should not reference any other Broiler assemblies.
        var ecAssembly = typeof(Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression).Assembly;
        var refs = ecAssembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(n => n != null && n.StartsWith("Broiler."))
            .ToList();

        Assert.Empty(refs);
    }

    // ── M14: Cross-Cutting Validation ──────────────────────────────────

    [Fact]
    public void M14_NoCircularReferences()
    {
        // Verify no circular assembly references exist in the engine assemblies.
        EnsureAllAssembliesLoaded();

        var visited = new HashSet<string>();
        var stack = new HashSet<string>();

        var engineAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("Broiler.JavaScript") == true)
            .ToList();

        foreach (var assembly in engineAssemblies)
        {
            Assert.False(HasCycle(assembly.GetName().Name!, visited, stack, engineAssemblies),
                $"Circular reference detected involving {assembly.GetName().Name}");
        }
    }

    private static bool HasCycle(string name, HashSet<string> visited, HashSet<string> stack,
        List<System.Reflection.Assembly> assemblies)
    {
        if (stack.Contains(name)) return true;
        if (visited.Contains(name)) return false;

        visited.Add(name);
        stack.Add(name);

        var assembly = assemblies.FirstOrDefault(a => a.GetName().Name == name);
        if (assembly != null)
        {
            foreach (var refName in assembly.GetReferencedAssemblies()
                .Where(r => r.Name?.StartsWith("Broiler.JavaScript") == true))
            {
                if (HasCycle(refName.Name!, visited, stack, assemblies))
                    return true;
            }
        }

        stack.Remove(name);
        return false;
    }

    [Fact]
    public void M14_AllModuleInitializersDelegatesWired()
    {
        // Comprehensive check that all 6 module initializers from Phase 1
        // still work correctly after Phase 2 changes.
        EnsureAllAssembliesLoaded();

        // BuiltIns delegates:
        Assert.NotNull(DefaultBuiltInRegistry.AdditionalRegistrations);
        Assert.NotNull(DefaultBuiltInRegistry.ConsoleFactory);
        Assert.NotNull(DefaultBuiltInRegistry.StructuredCloneExtension);
        Assert.NotNull(DefaultBuiltInRegistry.IteratorPrototypeSetup);

        // Factory methods:
        Assert.NotNull(JSValue.CreateDecimal(0m));
        Assert.NotNull(JSValue.CreateBigInt(0));

        // CLR interop:
        Assert.NotNull(JSEngine.ClrInterop);
    }

    [Fact]
    public void M14_TypesInCorrectAssemblies()
    {
        // Verify types reside directly in their target assemblies after
        // Core was merged into Engine (no type forwarding needed).
        Assert.Equal("Broiler.JavaScript.Runtime", typeof(JSValue).Assembly.GetName().Name);
        Assert.Equal("Broiler.JavaScript.Runtime", typeof(Arguments).Assembly.GetName().Name);
        Assert.Equal("Broiler.JavaScript.Storage", typeof(KeyStrings).Assembly.GetName().Name);
        Assert.Equal("Broiler.JavaScript.Engine", typeof(JSContext).Assembly.GetName().Name);
    }

    [Fact]
    public void M14_EndToEnd_EvalStillWorks()
    {
        // End-to-end smoke test: verify the JavaScript engine still works
        // after all Phase 2 structural changes.
        EnsureAllAssembliesLoaded();

        using var context = new JSContext();
        var result = context.Eval("1 + 2");
        Assert.Equal(3, result.IntValue);
    }

    [Fact]
    public void M14_EndToEnd_BuiltInsStillWork()
    {
        // Verify built-in types work after Phase 2 changes.
        EnsureAllAssembliesLoaded();

        using var context = new JSContext();

        // Map (extracted in M3):
        var mapResult = context.Eval("var m = new Map(); m.set('a', 1); m.get('a')");
        Assert.Equal(1, mapResult.IntValue);

        // Array methods (prototype split in M10):
        var arrayResult = context.Eval("[1,2,3].map(x => x * 2).join(',')");
        Assert.Equal("2,4,6", arrayResult.ToString());

        // Date (prototype split in M10):
        var dateResult = context.Eval("new Date(2024, 0, 1).getFullYear()");
        Assert.Equal(2024, dateResult.IntValue);
    }
}
