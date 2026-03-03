using Broiler.App.Rendering;

namespace Broiler.App.Tests;

/// <summary>
/// Tests for Milestone 4 JavaScript Runtime Hardening features:
/// micro-task queue, module extraction, strict mode, CSP, profiling,
/// error handling, and ES2023+ built-in polyfills.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Engine", "Broiler")]
public class Milestone4RuntimeTests
{
    // ── MicroTaskQueue ──────────────────────────────────────────────

    [Fact]
    public void MicroTaskQueue_Drain_ExecutesEnqueuedTasks()
    {
        var queue = new MicroTaskQueue();
        var executed = false;
        queue.Enqueue(() => executed = true);

        queue.Drain();
        Assert.True(executed);
    }

    [Fact]
    public void MicroTaskQueue_Drain_ExecutesInFifoOrder()
    {
        var queue = new MicroTaskQueue();
        var order = new List<int>();
        queue.Enqueue(() => order.Add(1));
        queue.Enqueue(() => order.Add(2));
        queue.Enqueue(() => order.Add(3));

        queue.Drain();
        Assert.Equal(new[] { 1, 2, 3 }, order);
    }

    [Fact]
    public void MicroTaskQueue_Drain_TasksEnqueuedDuringDrainAreProcessed()
    {
        var queue = new MicroTaskQueue();
        var executed = false;
        queue.Enqueue(() => queue.Enqueue(() => executed = true));

        queue.Drain();
        Assert.True(executed);
    }

    [Fact]
    public void MicroTaskQueue_Drain_CapturesExceptionsAndContinues()
    {
        var queue = new MicroTaskQueue();
        var secondRan = false;
        queue.Enqueue(() => throw new InvalidOperationException("boom"));
        queue.Enqueue(() => secondRan = true);

        var errors = queue.Drain();
        Assert.Single(errors);
        Assert.True(secondRan);
    }

    [Fact]
    public void MicroTaskQueue_EmptyDrain_ReturnsNoErrors()
    {
        var queue = new MicroTaskQueue();
        var errors = queue.Drain();
        Assert.Empty(errors);
    }

    // ── ContentSecurityPolicy ───────────────────────────────────────

    [Fact]
    public void Csp_Default_AllowsEval()
    {
        var csp = new ContentSecurityPolicy();
        Assert.True(csp.AllowsEval);
    }

    [Fact]
    public void Csp_ScriptSrcSelf_DisallowsEval()
    {
        var csp = new ContentSecurityPolicy();
        csp.Parse("script-src 'self'");

        Assert.False(csp.AllowsEval);
    }

    [Fact]
    public void Csp_ScriptSrcUnsafeEval_AllowsEval()
    {
        var csp = new ContentSecurityPolicy();
        csp.Parse("script-src 'self' 'unsafe-eval'");

        Assert.True(csp.AllowsEval);
    }

    [Fact]
    public void Csp_StrictDynamic_IsDetected()
    {
        var csp = new ContentSecurityPolicy();
        csp.Parse("script-src 'strict-dynamic'");

        Assert.True(csp.StrictDynamic);
    }

    [Fact]
    public void Csp_EnforceEval_ThrowsWhenEvalDisallowed()
    {
        var csp = new ContentSecurityPolicy();
        csp.Parse("script-src 'self'");

        Assert.Throws<InvalidOperationException>(() => csp.EnforceEval());
    }

    [Fact]
    public void Csp_EnforceEval_DoesNotThrowWhenAllowed()
    {
        var csp = new ContentSecurityPolicy();
        csp.Parse("script-src 'self' 'unsafe-eval'");

        var ex = Record.Exception(() => csp.EnforceEval());
        Assert.Null(ex);
    }

    [Fact]
    public void Csp_MultipleDirectives_OnlyScriptSrcAffectsEval()
    {
        var csp = new ContentSecurityPolicy();
        csp.Parse("default-src 'self'; script-src 'none'");

        Assert.False(csp.AllowsEval);
    }

    // ── ScriptProfilingHook ─────────────────────────────────────────

    [Fact]
    public void Profiler_Measure_RecordsEntry()
    {
        var profiler = new ScriptProfilingHook();
        profiler.Measure("test-script", () => { });

        Assert.Single(profiler.Entries);
        Assert.Equal("test-script", profiler.Entries[0].Label);
        Assert.True(profiler.Entries[0].Succeeded);
    }

    [Fact]
    public void Profiler_Measure_RecordsFailure()
    {
        var profiler = new ScriptProfilingHook();

        Assert.Throws<InvalidOperationException>(() =>
            profiler.Measure("failing", () => throw new InvalidOperationException("boom")));

        Assert.Single(profiler.Entries);
        Assert.False(profiler.Entries[0].Succeeded);
    }

    [Fact]
    public void Profiler_Measure_RecordsElapsedTime()
    {
        var profiler = new ScriptProfilingHook();
        profiler.Measure("timed", () => System.Threading.Thread.Sleep(10));

        Assert.True(profiler.Entries[0].Elapsed.TotalMilliseconds >= 5);
    }

    [Fact]
    public void Profiler_Clear_RemovesEntries()
    {
        var profiler = new ScriptProfilingHook();
        profiler.Measure("a", () => { });
        profiler.Clear();

        Assert.Empty(profiler.Entries);
    }

    // ── ScriptExecutionResult ───────────────────────────────────────

    [Fact]
    public void ExecuteDetailed_ValidScripts_ReturnsSuccess()
    {
        var engine = new ScriptEngine();
        var result = engine.ExecuteDetailed(["var x = 1 + 2;"]);

        Assert.True(result.Success);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ExecuteDetailed_InvalidScript_ReturnsErrors()
    {
        var engine = new ScriptEngine();
        var result = engine.ExecuteDetailed(["throw new Error('boom');"]);

        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Equal(0, result.Errors[0].ScriptIndex);
        Assert.Contains("boom", result.Errors[0].Message);
    }

    [Fact]
    public void ExecuteDetailed_ErrorIncludesStackTrace()
    {
        var engine = new ScriptEngine();
        var result = engine.ExecuteDetailed(["throw new Error('trace-test');"]);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors[0].StackTrace);
    }

    [Fact]
    public void ExecuteDetailed_EmptyList_ReturnsSuccess()
    {
        var engine = new ScriptEngine();
        var result = engine.ExecuteDetailed(Array.Empty<string>());

        Assert.True(result.Success);
    }

    // ── Strict Mode ─────────────────────────────────────────────────

    [Fact]
    public void StrictMode_Enabled_PrependStrictDirective()
    {
        var engine = new ScriptEngine();
        engine.StrictModeEnabled = true;

        // In strict mode, assigning to an undeclared variable should throw.
        var result = engine.Execute(["undeclaredVar = 42;"]);
        Assert.False(result);
    }

    [Fact]
    public void StrictMode_Disabled_AllowsImplicitGlobals()
    {
        var engine = new ScriptEngine();
        engine.StrictModeEnabled = false;

        // Without strict mode, this should succeed (implicitly creates global).
        var result = engine.Execute(["undeclaredVar = 42;"]);
        Assert.True(result);
    }

    // ── CSP eval() gating ───────────────────────────────────────────

    [Fact]
    public void Csp_EvalBlocked_EngineRejectsEval()
    {
        var engine = new ScriptEngine();
        var csp = new ContentSecurityPolicy();
        csp.Parse("script-src 'self'");
        engine.Csp = csp;

        // eval() should be replaced by a throwing function
        var result = engine.Execute(["eval('1 + 1');"]);
        Assert.False(result);
    }

    [Fact]
    public void Csp_EvalAllowed_EnginePermitsEval()
    {
        var engine = new ScriptEngine();
        var csp = new ContentSecurityPolicy();
        csp.Parse("script-src 'self' 'unsafe-eval'");
        engine.Csp = csp;

        var result = engine.Execute(["var x = eval('1 + 1');"]);
        Assert.True(result);
    }

    // ── Profiling integration ───────────────────────────────────────

    [Fact]
    public void Profiler_WithEngine_RecordsEntries()
    {
        var engine = new ScriptEngine();
        var profiler = new ScriptProfilingHook();
        engine.Profiler = profiler;

        engine.Execute(["var a = 1;", "var b = 2;"]);
        Assert.Equal(2, profiler.Entries.Count);
    }

    // ── MicroTaskQueue integration ──────────────────────────────────

    [Fact]
    public void QueueMicrotask_IsAvailableInContext()
    {
        var engine = new ScriptEngine();
        // queueMicrotask should be defined and callable
        var result = engine.Execute(["queueMicrotask(function() {});"]);
        Assert.True(result);
    }

    // ── Module extraction ───────────────────────────────────────────

    [Fact]
    public void ExtractModules_ReturnsModuleScripts()
    {
        var extractor = new ScriptExtractor();
        var html = @"<html><body>
            <script type=""module"">import { foo } from './foo.js';</script>
            <script>var x = 1;</script>
        </body></html>";

        var modules = extractor.ExtractModules(html);
        Assert.Single(modules);
        Assert.Contains("import", modules[0]);
    }

    [Fact]
    public void ExtractModules_IgnoresClassicScripts()
    {
        var extractor = new ScriptExtractor();
        var html = @"<html><body><script>var x = 1;</script></body></html>";

        var modules = extractor.ExtractModules(html);
        Assert.Empty(modules);
    }

    [Fact]
    public void Extract_IgnoresModuleScripts()
    {
        var extractor = new ScriptExtractor();
        var html = @"<html><body>
            <script type=""module"">export default 42;</script>
            <script>var x = 1;</script>
        </body></html>";

        var classics = extractor.Extract(html);
        Assert.Single(classics);
        Assert.Equal("var x = 1;", classics[0]);
    }

    [Fact]
    public void ExtractModules_ExternalModuleScript_IsIgnored()
    {
        var extractor = new ScriptExtractor();
        var html = @"<html><body><script type=""module"" src=""app.js""></script></body></html>";

        var modules = extractor.ExtractModules(html);
        Assert.Empty(modules);
    }

    // ── WeakRef polyfill ────────────────────────────────────────────

    [Fact]
    public void WeakRef_IsAvailableInContext()
    {
        var engine = new ScriptEngine();
        var result = engine.Execute(["var ref = new WeakRef({}); ref.deref();"]);
        Assert.True(result);
    }

    // ── FinalizationRegistry polyfill ───────────────────────────────

    [Fact]
    public void FinalizationRegistry_IsAvailableInContext()
    {
        var engine = new ScriptEngine();
        var result = engine.Execute([
            "var registry = new FinalizationRegistry(function(v) {}); registry.register({}, 'held');"
        ]);
        Assert.True(result);
    }
}
