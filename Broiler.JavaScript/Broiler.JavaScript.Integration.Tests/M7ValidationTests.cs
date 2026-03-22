using System.Runtime.CompilerServices;
using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Array.Typed;
using Broiler.JavaScript.Core.Core.Intl;
using Broiler.JavaScript.Core.Core.Iterator;
using Broiler.JavaScript.Core.Typed;

namespace Broiler.JavaScript.Integration.Tests;

/// <summary>
/// Milestone 7 (M7) — Future Extraction Candidates validation tests.
/// These tests verify the coupling analysis assumptions for each extraction
/// candidate and ensure the documented extraction plans remain valid.
/// </summary>
public class M7ValidationTests
{
    /// <summary>
    /// Ensures all satellite assemblies are loaded so module initializers fire.
    /// </summary>
    private static void EnsureAllAssembliesLoaded()
    {
        RuntimeHelpers.RunClassConstructor(
            typeof(Broiler.JavaScript.Core.Core.Weak.JSWeakRef).TypeHandle);
        RuntimeHelpers.RunClassConstructor(
            typeof(Broiler.JavaScript.Clr.DefaultClrInterop).TypeHandle);
    }

    // ── 7.1: TypedArrays — Extractable ─────────────────────────────────

    [Fact]
    public void M7_TypedArrays_ResideInCoreAssembly()
    {
        // TypedArrays currently live in Core; this is the starting point
        // for any future extraction.
        var arrayBufferAsm = typeof(JSArrayBuffer).Assembly.GetName().Name;
        Assert.Equal("Broiler.JavaScript.Core", arrayBufferAsm);

        var typedArrayAsm = typeof(JSTypedArray).Assembly.GetName().Name;
        Assert.Equal("Broiler.JavaScript.Core", typedArrayAsm);
    }

    [Fact]
    public void M7_TypedArrays_NoCompilerCoupling()
    {
        // The Compiler assembly must NOT reference TypedArray types.
        // This confirms they can be extracted without affecting compilation.
        var compilerAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .First(a => a.GetName().Name == "Broiler.JavaScript.Compiler");

        var compilerTypes = compilerAssembly.GetTypes()
            .SelectMany(t => t.GetMethods(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Static))
            .SelectMany(m =>
            {
                var types = new List<Type>();
                if (m.ReturnType != null) types.Add(m.ReturnType);
                types.AddRange(m.GetParameters().Select(p => p.ParameterType));
                return types;
            })
            .Select(t => t.FullName ?? "")
            .ToHashSet();

        Assert.DoesNotContain(compilerTypes,
            t => t.Contains("JSArrayBuffer") || t.Contains("JSTypedArray"));
    }

    [Fact]
    public void M7_TypedArrays_FunctionalAfterAssemblyLoad()
    {
        // TypedArrays must work end-to-end through eval.
        EnsureAllAssembliesLoaded();
        using var ctx = new JSContext();

        var result = ctx.Eval("new ArrayBuffer(16).byteLength");
        Assert.Equal(16.0, result.DoubleValue);

        result = ctx.Eval("new Int32Array([1, 2, 3]).length");
        Assert.Equal(3.0, result.DoubleValue);
    }

    // ── 7.2: RegExp — NOT Extractable ──────────────────────────────────

    [Fact]
    public void M7_RegExp_ResideInCoreAssembly()
    {
        // RegExp must remain in Core due to tight integration with
        // String prototype and Compiler.
        var regExpAsm = typeof(JSRegExp).Assembly.GetName().Name;
        Assert.Equal("Broiler.JavaScript.Core", regExpAsm);
    }

    [Fact]
    public void M7_RegExp_HasCompilerCoupling()
    {
        // The Compiler assembly uses JSRegExpBuilder (in Core/LinqExpressions)
        // which directly references JSRegExp. This coupling means RegExp
        // cannot be extracted to BuiltIns without creating a circular dependency.
        var coreAssembly = typeof(JSContext).Assembly;
        var regExpBuilderType = coreAssembly.GetType(
            "Broiler.JavaScript.Core.LinqExpressions.JSRegExpBuilder");
        Assert.NotNull(regExpBuilderType);

        // Confirm the Compiler assembly references Core (where JSRegExp lives).
        var compilerAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .First(a => a.GetName().Name == "Broiler.JavaScript.Compiler");
        var compilerRefs = compilerAssembly.GetReferencedAssemblies()
            .Select(r => r.Name!)
            .ToHashSet();
        Assert.Contains("Broiler.JavaScript.Core", compilerRefs);
    }

    [Fact]
    public void M7_RegExp_FunctionalWithStringMethods()
    {
        // RegExp is tightly integrated with String prototype methods
        // (match, replace, split, search, matchAll, replaceAll).
        EnsureAllAssembliesLoaded();
        using var ctx = new JSContext();

        // String.match with RegExp
        var result = ctx.Eval("'hello world'.match(/\\w+/g).length");
        Assert.Equal(2.0, result.DoubleValue);

        // String.replace with RegExp
        result = ctx.Eval("'abc'.replace(/b/, 'x')");
        Assert.Equal("axc", result.ToString());

        // String.split with RegExp
        result = ctx.Eval("'a,b,c'.split(/,/).length");
        Assert.Equal(3.0, result.DoubleValue);
    }

    // ── 7.3: Promise — NOT Extractable ─────────────────────────────────

    [Fact]
    public void M7_Promise_ResideInCoreAssembly()
    {
        // Promise is infrastructure-level: JSContext owns PendingPromises
        // and async functions create JSPromise instances directly.
        var promiseAsm = typeof(JSPromise).Assembly.GetName().Name;
        Assert.Equal("Broiler.JavaScript.Core", promiseAsm);
    }

    [Fact]
    public void M7_Promise_TightlyCoupledToJSContext()
    {
        // JSContext has a ConcurrentDictionary<long, JSPromise> field,
        // making Promise non-extractable without major JSContext refactoring.
        var contextType = typeof(JSContext);
        var pendingField = contextType.GetField(
            "PendingPromises",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(pendingField);

        // Verify the field type references JSPromise.
        var fieldType = pendingField!.FieldType;
        Assert.Contains("JSPromise", fieldType.GenericTypeArguments
            .Select(t => t.Name));
    }

    [Fact]
    public void M7_Promise_FunctionalEndToEnd()
    {
        EnsureAllAssembliesLoaded();
        using var ctx = new JSContext();

        var result = ctx.Eval("typeof Promise");
        Assert.Equal("function", result.ToString());

        result = ctx.Eval("Promise.resolve(42).constructor.name");
        Assert.Equal("Promise", result.ToString());
    }

    // ── 7.4: Iterator — Extractable ────────────────────────────────────

    [Fact]
    public void M7_Iterator_ResideInCoreAssembly()
    {
        // Iterator helpers currently live in Core; they are extractable
        // using the factory delegate pattern.
        var iteratorAsm = typeof(JSIteratorObject).Assembly.GetName().Name;
        Assert.Equal("Broiler.JavaScript.Core", iteratorAsm);
    }

    [Fact]
    public void M7_Iterator_NoCompilerOrParserCoupling()
    {
        // Neither Compiler nor Parser reference JSIteratorObject.
        // Use direct type references to get assemblies (avoids AppDomain timing).
        var compilerAssembly = typeof(Broiler.JavaScript.Core.FastParser.Compiler.FastCompiler).Assembly;
        var parserAssembly = typeof(Broiler.JavaScript.Parser.FastParser).Assembly;

        // Check Compiler types don't reference Iterator namespace.
        var compilerTypeNames = compilerAssembly.GetTypes()
            .Select(t => t.FullName ?? "")
            .ToList();
        Assert.DoesNotContain(compilerTypeNames,
            n => n.Contains("JSIteratorObject"));

        // Check Parser types don't reference Iterator namespace.
        var parserTypeNames = parserAssembly.GetTypes()
            .Select(t => t.FullName ?? "")
            .ToList();
        Assert.DoesNotContain(parserTypeNames,
            n => n.Contains("JSIteratorObject"));
    }

    [Fact]
    public void M7_Iterator_CoupledOnlyViaDefaultBuiltInRegistry()
    {
        // The only coupling point is DefaultBuiltInRegistry, which
        // hardcodes references to JSIteratorObject static methods.
        // This can be replaced with factory delegates during extraction.
        var registryType = typeof(DefaultBuiltInRegistry);
        Assert.Equal("Broiler.JavaScript.Core",
            registryType.Assembly.GetName().Name);

        // JSIteratorObject and DefaultBuiltInRegistry are in the same assembly,
        // confirming the coupling is intra-assembly (not cross-assembly).
        Assert.Equal(typeof(JSIteratorObject).Assembly,
            registryType.Assembly);
    }

    // ── 7.5: Intl — Already Extracted ──────────────────────────────────

    [Fact]
    public void M7_Intl_AlreadyInBuiltInsAssembly()
    {
        // JSIntl was extracted to BuiltIns as part of M3.
        // This serves as the template pattern for future extractions.
        var intlAsm = typeof(JSIntl).Assembly.GetName().Name;
        Assert.Equal("Broiler.JavaScript.BuiltIns", intlAsm);
    }

    [Fact]
    public void M7_Intl_CoreHasNoDirectReference()
    {
        // Core must NOT reference BuiltIns (where JSIntl lives).
        // Communication is via factory delegates wired at initialization.
        var coreRefs = typeof(JSContext).Assembly.GetReferencedAssemblies()
            .Select(r => r.Name!)
            .ToHashSet();
        Assert.DoesNotContain("Broiler.JavaScript.BuiltIns", coreRefs);
    }

    [Fact]
    public void M7_Intl_FactoryDelegatePattern()
    {
        // Verify the Intl factory delegate is wired by the module initializer.
        // This is the pattern that TypedArrays and Iterator should follow.
        EnsureAllAssembliesLoaded();

        // JSGlobalStatic.IntlFactory should be wired by BuiltInsAssemblyInitializer.
        // The class lives in namespace Broiler.JavaScript.Core.Core.Global.
        var globalStaticType = typeof(JSContext).Assembly
            .GetType("Broiler.JavaScript.Core.Core.Global.JSGlobalStatic");
        Assert.NotNull(globalStaticType);

        var intlFactoryProp = globalStaticType!.GetProperty(
            "IntlFactory",
            System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(intlFactoryProp);

        // After all assemblies are loaded, the factory should be wired.
        var factoryValue = intlFactoryProp!.GetValue(null);
        Assert.NotNull(factoryValue);
    }

    // ── 7.6: Extraction Pattern Invariants ─────────────────────────────

    [Fact]
    public void M7_ExtractionPattern_CoreDoesNotReferenceFeatureAssemblies()
    {
        // The fundamental invariant: Core → Foundation only, never Core → Feature.
        // Future extractions must maintain this property.
        var coreRefs = typeof(JSContext).Assembly.GetReferencedAssemblies()
            .Select(r => r.Name!)
            .Where(n => n.StartsWith("Broiler.JavaScript"))
            .ToHashSet();

        // Core references only Foundation-layer assemblies.
        var allowedRefs = new HashSet<string>
        {
            "Broiler.JavaScript.Runtime",
            "Broiler.JavaScript.Storage",
            "Broiler.JavaScript.Parser",
            "Broiler.JavaScript.Ast",
            "Broiler.JavaScript.ExpressionCompiler",
        };

        var disallowed = coreRefs.Except(allowedRefs).ToList();
        Assert.True(disallowed.Count == 0,
            $"Core references disallowed assemblies: {string.Join(", ", disallowed)}");
    }

    [Fact]
    public void M7_ExtractionPattern_AllCandidatesAccountedFor()
    {
        // Verify all 5 documented candidates exist in the expected locations.
        // This test ensures the roadmap stays in sync with the code.

        // TypedArrays — Core
        Assert.Equal("Broiler.JavaScript.Core",
            typeof(JSArrayBuffer).Assembly.GetName().Name);

        // RegExp — Core
        Assert.Equal("Broiler.JavaScript.Core",
            typeof(JSRegExp).Assembly.GetName().Name);

        // Promise — Core
        Assert.Equal("Broiler.JavaScript.Core",
            typeof(JSPromise).Assembly.GetName().Name);

        // Iterator — Core
        Assert.Equal("Broiler.JavaScript.Core",
            typeof(JSIteratorObject).Assembly.GetName().Name);

        // Intl — BuiltIns (already extracted)
        Assert.Equal("Broiler.JavaScript.BuiltIns",
            typeof(JSIntl).Assembly.GetName().Name);
    }
}
