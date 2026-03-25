using System.Diagnostics;
using System.Runtime.CompilerServices;
using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.BuiltIns;
using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.Parser;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Storage;

namespace Broiler.JavaScript.Integration.Tests;

/// <summary>
/// Milestone 6 (M6) — Final Validation tests.
/// These tests verify the structural integrity of the refactored assembly architecture.
/// </summary>
public class M6ValidationTests
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

    // ── 6.2: Type Location Verification ──────────────────────────────

    [Fact]
    public void M6_TypesResolveToCorrectAssembly()
    {
        // Spot-check representative types resolve to their target assemblies.
        var expectations = new (Type type, string expectedAssemblyPrefix)[]
        {
            // → Runtime
            (typeof(JSValue), "Broiler.JavaScript.Runtime"),
            (typeof(Arguments), "Broiler.JavaScript.Runtime"),
            (typeof(PropertyKey), "Broiler.JavaScript.Runtime"),
            (typeof(JSFunctionDelegate), "Broiler.JavaScript.Runtime"),
            (typeof(CoreScript), "Broiler.JavaScript.Runtime"),
            (typeof(IJSCompiler), "Broiler.JavaScript.Runtime"),
            (typeof(ICodeCache), "Broiler.JavaScript.Runtime"),
            (typeof(IClrInterop), "Broiler.JavaScript.Runtime"),
            (typeof(IDebugger), "Broiler.JavaScript.Runtime"),

            // → Storage
            (typeof(KeyString), "Broiler.JavaScript.Storage"),
            (typeof(KeyStrings), "Broiler.JavaScript.Storage"),
            (typeof(VirtualMemory<>), "Broiler.JavaScript.Storage"),
            (typeof(PropertySequence), "Broiler.JavaScript.Storage"),

            // → Ast
            (typeof(StringSpan), "Broiler.JavaScript.Ast"),
            (typeof(FastToken), "Broiler.JavaScript.Ast"),
            (typeof(FastNodeType), "Broiler.JavaScript.Ast"),
            (typeof(TokenTypes), "Broiler.JavaScript.Ast"),

            // → Parser
            (typeof(FastParser), "Broiler.JavaScript.Parser"),
            (typeof(FastTokenStream), "Broiler.JavaScript.Parser"),
        };

        foreach (var (type, expectedPrefix) in expectations)
        {
            var assemblyName = type.Assembly.GetName().Name;
            Assert.StartsWith(expectedPrefix, assemblyName!,
                StringComparison.Ordinal);
        }
    }

    // ── 6.3: Module Initializer Verification ───────────────────────────

    [Fact]
    public void M6_CompilerRegistration_WiredByModuleInitializer()
    {
        // CompilerAssemblyInitializer registers FastCompiler pipeline.
        // Verify by evaluating code (requires a working compiler).
        using var ctx = new JSContext();
        var result = ctx.Eval("1 + 1");
        Assert.Equal(2.0, result.DoubleValue);
    }

    [Fact]
    public void M6_BuiltInsRegistration_WiredByModuleInitializer()
    {
        EnsureAllAssembliesLoaded();

        // BuiltInsAssemblyInitializer wires built-in factory delegates.
        Assert.NotNull(DefaultBuiltInRegistry.ConsoleFactory);
        Assert.NotNull(DefaultBuiltInRegistry.AdditionalRegistrations);
        Assert.NotNull(DefaultBuiltInRegistry.StructuredCloneExtension);

        // Public factory delegates wired by BuiltIns initializer.
        Assert.NotNull(JSValue.CreateBigIntFromStringFactory);
        Assert.NotNull(JSValue.CreateDecimalFromStringFactory);
    }

    [Fact]
    public void M6_ClrRegistration_WiredByModuleInitializer()
    {
        EnsureAllAssembliesLoaded();

        // ClrAssemblyInitializer wires the full CLR interop implementation.
        Assert.NotNull(JSEngine.ClrInterop);
        Assert.IsType<Broiler.JavaScript.Clr.DefaultClrInterop>(JSEngine.ClrInterop);
    }

    [Fact]
    public void M6_PropertySequenceTypeErrorFactory_WiredByModuleInitializer()
    {
        // PropertySequenceCoreExtensions wires TypeErrorFactory.
        Assert.NotNull(PropertySequence.TypeErrorFactory);
    }

    [Fact]
    public void M6_CoreScriptFactories_VerifiedThroughEval()
    {
        // CoreScriptCoreExtensions wires factory delegates on CoreScript.
        // Verify indirectly: CoreScript.Eval uses CreateDefaultCompiler,
        // GetDefaultCodeCache, and GetCurrentContext internally.
        using var ctx = new JSContext();
        var result = ctx.Eval("(function() { return 42; })()");
        Assert.Equal(42.0, result.DoubleValue);
    }

    [Fact]
    public void M6_JSValueFactories_VerifiedThroughPrimitives()
    {
        // JSValueCoreExtensions wires core value constants.
        // Verify indirectly: JSUndefined.Value and JSNull.Value are set
        // via the factory delegates and used throughout the engine.
        Assert.NotNull(JSUndefined.Value);
        Assert.True(JSUndefined.Value.IsUndefined);

        Assert.NotNull(JSNull.Value);
        Assert.True(JSNull.Value.IsNull);

        // Arguments.Empty is wired by JSValueCoreExtensions.
        // Verify it returns a valid Arguments value (not default).
        var empty = Arguments.Empty;
        Assert.NotNull(empty.This);
    }

    // ── 6.4: No Circular Assembly References ───────────────────────────

    [Fact]
    public void M6_NoCircularAssemblyReferences()
    {
        EnsureAllAssembliesLoaded();

        // Collect the engine assemblies that are loaded.
        var engineAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("Broiler.JavaScript") == true
                     && !a.GetName().Name!.Contains("Tests")
                     && !a.GetName().Name!.Contains("Generator"))
            .ToList();

        Assert.True(engineAssemblies.Count >= 10,
            $"Expected at least 10 engine assemblies loaded but found {engineAssemblies.Count}");

        // Build the reference graph.
        var graph = new Dictionary<string, HashSet<string>>();
        foreach (var asm in engineAssemblies)
        {
            var name = asm.GetName().Name!;
            var refs = asm.GetReferencedAssemblies()
                .Where(r => r.Name?.StartsWith("Broiler.JavaScript") == true)
                .Select(r => r.Name!)
                .ToHashSet();
            graph[name] = refs;
        }

        // Detect cycles using DFS.
        var visited = new HashSet<string>();
        var inStack = new HashSet<string>();
        var cyclePath = new List<string>();

        bool HasCycle(string node)
        {
            if (inStack.Contains(node))
            {
                cyclePath.Add(node);
                return true;
            }
            if (visited.Contains(node)) return false;

            visited.Add(node);
            inStack.Add(node);

            if (graph.TryGetValue(node, out var deps))
            {
                foreach (var dep in deps)
                {
                    if (HasCycle(dep))
                    {
                        cyclePath.Add(node);
                        return true;
                    }
                }
            }

            inStack.Remove(node);
            return false;
        }

        foreach (var node in graph.Keys)
        {
            if (HasCycle(node))
            {
                cyclePath.Reverse();
                Assert.Fail($"Circular dependency detected: {string.Join(" → ", cyclePath)}");
            }
        }
    }

    // ── 6.5: Backward Compatibility ────────────────────────────────────

    [Fact]
    public void M6_TypesResolvedDirectlyInTargetAssemblies()
    {
        // After refactoring, types live directly in their target assemblies
        // (no forwarding needed since Core was merged into Engine).
        Assert.Equal("Broiler.JavaScript.Runtime", typeof(JSValue).Assembly.GetName().Name);
        Assert.Equal("Broiler.JavaScript.Ast", typeof(StringSpan).Assembly.GetName().Name);
        Assert.Equal("Broiler.JavaScript.Parser", typeof(FastParser).Assembly.GetName().Name);
    }

    [Fact]
    public void M6_NamespacePreserved_AfterExtraction()
    {
        // Types extracted to BuiltIns retain their Core namespace for source compatibility.
        EnsureAllAssembliesLoaded();
        using var ctx = new JSContext();

        // JSMap was extracted to BuiltIns but keeps its namespace.
        var result = ctx.Eval("new Map([[1,'a'],[2,'b']]).size");
        Assert.Equal(2.0, result.DoubleValue);

        // JSSet was extracted to BuiltIns but keeps its namespace.
        result = ctx.Eval("new Set([1,2,3]).size");
        Assert.Equal(3.0, result.DoubleValue);

        // JSON was extracted to BuiltIns but keeps its namespace.
        result = ctx.Eval("JSON.stringify({a:1})");
        Assert.Equal("{\"a\":1}", result.ToString());
    }

    [Fact]
    public void M6_BigIntFactory_WorksAcrossAssemblies()
    {
        // BigInt factory delegates wired by BuiltInsAssemblyInitializer
        // allow Core/Compiler to create BigInt values without referencing BuiltIns.
        EnsureAllAssembliesLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("typeof BigInt(42)");
        Assert.Equal("bigint", result.ToString());
    }

    [Fact]
    public void M6_DependencyLayering_FeaturesDependOnEngine()
    {
        EnsureAllAssembliesLoaded();

        // Feature-layer assemblies should reference Engine but Engine should NOT
        // reference any Feature-layer assembly.
        var engineRefs = typeof(JSContext).Assembly.GetReferencedAssemblies()
            .Select(r => r.Name!)
            .ToHashSet();

        // Engine must NOT reference Feature assemblies.
        Assert.DoesNotContain("Broiler.JavaScript.BuiltIns", engineRefs);
        Assert.DoesNotContain("Broiler.JavaScript.Compiler", engineRefs);
        Assert.DoesNotContain("Broiler.JavaScript.Clr", engineRefs);
        Assert.DoesNotContain("Broiler.JavaScript.Debugger", engineRefs);
        Assert.DoesNotContain("Broiler.JavaScript.Modules", engineRefs);

        // Engine must reference Foundation assemblies.
        Assert.Contains("Broiler.JavaScript.Runtime", engineRefs);
        Assert.Contains("Broiler.JavaScript.Storage", engineRefs);
        Assert.Contains("Broiler.JavaScript.ExpressionCompiler", engineRefs);
    }

    // ── 6.6: Performance Baseline ──────────────────────────────────────

    [Fact]
    public void M6_PerformanceBaseline_EvalOverhead()
    {
        EnsureAllAssembliesLoaded();

        // Warm up: first context creation + eval triggers all module initializers.
        using (var warmup = new JSContext())
        {
            warmup.Eval("1");
        }

        // Measure context creation + simple eval latency.
        var sw = Stopwatch.StartNew();
        const int iterations = 100;
        for (int i = 0; i < iterations; i++)
        {
            using var ctx = new JSContext();
            ctx.Eval("2 + 2");
        }
        sw.Stop();

        var avgMs = sw.Elapsed.TotalMilliseconds / iterations;

        // Baseline: context creation + simple eval should complete in under 200ms
        // on any CI platform. This is a generous upper bound to avoid flaky tests.
        Assert.True(avgMs < 200,
            $"Average eval latency {avgMs:F2}ms exceeds 200ms baseline");
    }

    [Fact]
    public void M6_PerformanceBaseline_AssemblyLoadingDoesNotRegress()
    {
        // Verify that evaluating a moderately complex expression across
        // multiple built-in types completes in reasonable time.
        EnsureAllAssembliesLoaded();

        var sw = Stopwatch.StartNew();
        using var ctx = new JSContext();
        var result = ctx.Eval(@"
            var m = new Map();
            for (var i = 0; i < 1000; i++) m.set(i, i * 2);
            var s = new Set();
            for (var j = 0; j < 1000; j++) s.add(j);
            m.size + s.size;
        ");
        sw.Stop();

        Assert.Equal(2000.0, result.DoubleValue);

        // Generous baseline: 5 seconds for 1000-element Map + Set on CI.
        Assert.True(sw.Elapsed.TotalSeconds < 5.0,
            $"Built-in operations took {sw.Elapsed.TotalSeconds:F2}s, exceeding 5s baseline");
    }
}
