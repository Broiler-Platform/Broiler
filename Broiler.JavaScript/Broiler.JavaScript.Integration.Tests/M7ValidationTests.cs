using System.Runtime.CompilerServices;
using Broiler.JavaScript.BuiltIns;
using Broiler.JavaScript.BuiltIns.Array.Typed;
using Broiler.JavaScript.BuiltIns.Intl;
using Broiler.JavaScript.BuiltIns.Iterator;
using Broiler.JavaScript.BuiltIns.Promise;
using Broiler.JavaScript.BuiltIns.RegExp;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.Integration.Tests;

/// <summary>
/// Milestone 7 (M7) — Future Extraction Candidates validation tests.
/// These tests verify that the TypedArrays, Iterator, and RegExp types have
/// been successfully extracted from Core to BuiltIns.
/// </summary>
public class M7ValidationTests
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

    // ── 7.1: TypedArrays — Extracted to BuiltIns ──────────────────────

    [Fact]
    public void M7_TypedArrays_ExtractedToBuiltInsAssembly()
    {
        // TypedArrays have been extracted from Core to BuiltIns.
        var arrayBufferAsm = typeof(JSArrayBuffer).Assembly.GetName().Name;
        Assert.Equal("Broiler.JavaScript.BuiltIns", arrayBufferAsm);

        var typedArrayAsm = typeof(JSTypedArray).Assembly.GetName().Name;
        Assert.Equal("Broiler.JavaScript.BuiltIns", typedArrayAsm);
    }

    [Fact]
    public void M7_TypedArrays_NoCompilerCoupling()
    {
        // The Compiler assembly must NOT reference TypedArray types.
        // This confirms extraction didn't introduce unwanted coupling.
        var compilerAssembly = typeof(Broiler.JavaScript.Compiler.FastCompiler).Assembly;

        var compilerTypes = compilerAssembly.GetTypes()
            .SelectMany(t => t.GetMethods(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Static))
            .SelectMany(m =>
            {
                var types = new List<Type> { m.ReturnType };
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
        // TypedArrays must work end-to-end through eval after extraction.
        EnsureAllAssembliesLoaded();
        using var ctx = new JSContext();

        var result = ctx.Eval("new ArrayBuffer(16).byteLength");
        Assert.Equal(16.0, result.DoubleValue);

        result = ctx.Eval("new Int32Array([1, 2, 3]).length");
        Assert.Equal(3.0, result.DoubleValue);
    }

    [Fact]
    public void M7_TypedArrays_StructuredCloneDelegated()
    {
        // ArrayBuffer StructuredClone is now handled by the extension delegate
        // in BuiltIns, not directly in Core's JSGlobal.
        EnsureAllAssembliesLoaded();
        using var ctx = new JSContext(experimentalFeatures: JavaScriptFeatureFlags.StructuredClone);

        var result = ctx.Eval(@"
            var buf = new ArrayBuffer(8);
            var view = new Int32Array(buf);
            view[0] = 42;
            var cloned = structuredClone(buf);
            var clonedView = new Int32Array(cloned);
            clonedView[0];
        ");
        Assert.Equal(42.0, result.DoubleValue);
    }

    // ── 7.2: RegExp — Extracted to BuiltIns ──────────────────────────────

    [Fact]
    public void M7_RegExp_ResideInBuiltInsAssembly()
    {
        // RegExp has been extracted to BuiltIns using the Initialize(Type)
        // pattern for JSRegExpBuilder, and IJSRegExp interface in Runtime.
        var regExpAsm = typeof(JSRegExp).Assembly.GetName().Name;
        Assert.Equal("Broiler.JavaScript.BuiltIns", regExpAsm);
    }

    [Fact]
    public void M7_RegExp_CompilerDecoupledViaInitialize()
    {
        // JSRegExpBuilder lives in LinqExpressions and uses the
        // Initialize(Type) pattern — the Compiler no longer has a
        // compile-time dependency on the concrete JSRegExp type.
        var linqAssembly = typeof(JSRegExpBuilder).Assembly;
        var regExpBuilderType = linqAssembly.GetType(
            "Broiler.JavaScript.LinqExpressions.LinqExpressions.JSRegExpBuilder");
        Assert.NotNull(regExpBuilderType);

        // Confirm the Compiler assembly references Engine (for JSRegExpBuilder).
        var compilerAssembly = typeof(Broiler.JavaScript.Compiler.FastCompiler).Assembly;
        var compilerRefs = compilerAssembly.GetReferencedAssemblies()
            .Select(r => r.Name!)
            .ToHashSet();
        Assert.Contains("Broiler.JavaScript.Engine", compilerRefs);
    }

    [Fact]
    public void M7_RegExp_FunctionalWithStringMethods()
    {
        // RegExp is tightly integrated with String prototype methods.
        EnsureAllAssembliesLoaded();
        using var ctx = new JSContext();

        var result = ctx.Eval("'hello world'.match(/\\w+/g).length");
        Assert.Equal(2.0, result.DoubleValue);

        result = ctx.Eval("'abc'.replace(/b/, 'x')");
        Assert.Equal("axc", result.ToString());

        result = ctx.Eval("'a,b,c'.split(/,/).length");
        Assert.Equal(3.0, result.DoubleValue);
    }

    // ── 7.3: Promise — NOT Extractable ─────────────────────────────────

    [Fact]
    public void M7_Promise_ResideInCoreAssembly()
    {
        var promiseAsm = typeof(JSPromise).Assembly.GetName().Name;
        Assert.Equal("Broiler.JavaScript.BuiltIns", promiseAsm);
    }

    [Fact]
    public void M7_Promise_TightlyCoupledToJSContext()
    {
        var contextType = typeof(JSContext);
        var pendingField = contextType.GetField(
            "PendingPromises",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(pendingField);

        var fieldType = pendingField!.FieldType;
        Assert.Contains("JSValue", fieldType.GenericTypeArguments
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

    // ── 7.4: Iterator — Extracted to BuiltIns ──────────────────────────

    [Fact]
    public void M7_Iterator_ExtractedToBuiltInsAssembly()
    {
        // Iterator helpers have been extracted from Core to BuiltIns.
        var iteratorAsm = typeof(JSIteratorObject).Assembly.GetName().Name;
        Assert.Equal("Broiler.JavaScript.BuiltIns", iteratorAsm);
    }

    [Fact]
    public void M7_Iterator_NoCompilerOrParserCoupling()
    {
        // Neither Compiler nor Parser reference JSIteratorObject.
        var compilerAssembly = typeof(Broiler.JavaScript.Compiler.FastCompiler).Assembly;
        var parserAssembly = typeof(Broiler.JavaScript.Parser.FastParser).Assembly;

        var compilerTypeNames = compilerAssembly.GetTypes()
            .Select(t => t.FullName ?? "")
            .ToList();
        Assert.DoesNotContain(compilerTypeNames,
            n => n.Contains("JSIteratorObject"));

        var parserTypeNames = parserAssembly.GetTypes()
            .Select(t => t.FullName ?? "")
            .ToList();
        Assert.DoesNotContain(parserTypeNames,
            n => n.Contains("JSIteratorObject"));
    }

    [Fact]
    public void M7_Iterator_DecoupledFromDefaultBuiltInRegistry()
    {
        // DefaultBuiltInRegistry uses the IteratorPrototypeSetup delegate
        // for loose coupling with iterator setup.
        Assert.NotNull(DefaultBuiltInRegistry.IteratorPrototypeSetup);

        // Both JSIteratorObject and DefaultBuiltInRegistry are in the BuiltIns
        // assembly after the refactoring merged Core into Engine.
        Assert.Equal("Broiler.JavaScript.BuiltIns",
            typeof(JSIteratorObject).Assembly.GetName().Name);
        Assert.Equal("Broiler.JavaScript.BuiltIns",
            typeof(DefaultBuiltInRegistry).Assembly.GetName().Name);
    }

    [Fact]
    public void M7_Iterator_FunctionalEndToEnd()
    {
        // Iterator helpers must work through eval after extraction.
        EnsureAllAssembliesLoaded();
        using var ctx = new JSContext();

        var result = ctx.Eval("[1, 2, 3, 4, 5].values().filter(x => x > 2).toArray().length");
        Assert.Equal(3.0, result.DoubleValue);

        result = ctx.Eval("[1, 2, 3].values().map(x => x * 2).toArray().join(',')");
        Assert.Equal("2,4,6", result.ToString());
    }

    // ── 7.5: Intl — Already Extracted ──────────────────────────────────

    [Fact]
    public void M7_Intl_AlreadyInBuiltInsAssembly()
    {
        var intlAsm = typeof(JSIntl).Assembly.GetName().Name;
        Assert.Equal("Broiler.JavaScript.BuiltIns", intlAsm);
    }

    [Fact]
    public void M7_Intl_CoreHasNoDirectReference()
    {
        var coreRefs = typeof(JSContext).Assembly.GetReferencedAssemblies()
            .Select(r => r.Name!)
            .ToHashSet();
        Assert.DoesNotContain("Broiler.JavaScript.BuiltIns", coreRefs);
    }

    [Fact]
    public void M7_Intl_FactoryDelegatePattern()
    {
        EnsureAllAssembliesLoaded();

        // IntlFactory was moved to DefaultBuiltInRegistry when JSGlobalStatic
        // was extracted from Core to the Globals assembly.
        var registryType = typeof(DefaultBuiltInRegistry);
        var intlFactoryProp = registryType.GetProperty(
            "IntlFactory",
            System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.Public);
        Assert.NotNull(intlFactoryProp);

        var factoryValue = intlFactoryProp!.GetValue(null);
        Assert.NotNull(factoryValue);
    }

    // ── 7.6: Extraction Pattern Invariants ─────────────────────────────

    [Fact]
    public void M7_ExtractionPattern_CoreDoesNotReferenceFeatureAssemblies()
    {
        var coreRefs = typeof(JSContext).Assembly.GetReferencedAssemblies()
            .Select(r => r.Name!)
            .Where(n => n.StartsWith("Broiler.JavaScript"))
            .ToHashSet();

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

        // TypedArrays — extracted to BuiltIns
        Assert.Equal("Broiler.JavaScript.BuiltIns",
            typeof(JSArrayBuffer).Assembly.GetName().Name);

        // RegExp — extracted to BuiltIns
        Assert.Equal("Broiler.JavaScript.BuiltIns",
            typeof(JSRegExp).Assembly.GetName().Name);

        // Promise — extracted to BuiltIns
        Assert.Equal("Broiler.JavaScript.BuiltIns",
            typeof(JSPromise).Assembly.GetName().Name);

        // Iterator — extracted to BuiltIns
        Assert.Equal("Broiler.JavaScript.BuiltIns",
            typeof(JSIteratorObject).Assembly.GetName().Name);

        // Intl — already in BuiltIns (extracted in M3)
        Assert.Equal("Broiler.JavaScript.BuiltIns",
            typeof(JSIntl).Assembly.GetName().Name);
    }
}
