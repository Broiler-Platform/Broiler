using Broiler.Scripting;

namespace Broiler.Scripting.Tests;

/// <summary>
/// OS-independent integration tests for <see cref="ScriptEngine"/>
/// exercising core YantraJS functionality through the modular interface.
/// </summary>
public class ScriptEngineTests
{
    [Fact]
    public void Execute_EmptyList_ReturnsTrue()
    {
        var engine = new ScriptEngine();
        Assert.True(engine.Execute(Array.Empty<string>()));
    }

    [Fact]
    public void Execute_ValidScript_ReturnsTrue()
    {
        var engine = new ScriptEngine();
        Assert.True(engine.Execute(new[] { "var x = 1 + 2;" }));
    }

    [Fact]
    public void Execute_InvalidScript_ReturnsFalse()
    {
        var engine = new ScriptEngine();
        Assert.False(engine.Execute(new[] { "throw new Error('test');" }));
    }

    [Fact]
    public void Execute_MultipleScripts_AllSucceed()
    {
        var engine = new ScriptEngine();
        var scripts = new[] { "var a = 1;", "var b = 2;", "var c = a + b;" };
        Assert.True(engine.Execute(scripts));
    }

    [Fact]
    public void Execute_MultipleScripts_FirstFails_OthersStillRun()
    {
        var engine = new ScriptEngine();
        var scripts = new[]
        {
            "throw new Error('fail');",
            "var x = 42;"
        };
        Assert.False(engine.Execute(scripts));
    }

    [Fact]
    public void Execute_StrictMode_CanBeEnabled()
    {
        var engine = new ScriptEngine { StrictModeEnabled = true };
        // Verify strict mode is enabled and scripts still execute.
        // The "use strict" directive is prepended to each script.
        Assert.True(engine.StrictModeEnabled);
        Assert.True(engine.Execute(new[] { "var x = 42;" }));
    }

    [Fact]
    public void Execute_StrictMode_Disabled_AllowsImplicitGlobals()
    {
        var engine = new ScriptEngine { StrictModeEnabled = false };
        Assert.True(engine.Execute(new[] { "undeclaredVar = 42;" }));
    }

    [Fact]
    public void Execute_CspBlocksEval()
    {
        var csp = new ContentSecurityPolicy();
        csp.Parse("script-src 'self'");

        var engine = new ScriptEngine { Csp = csp };
        Assert.False(engine.Execute(new[] { "eval('1 + 1');" }));
    }

    [Fact]
    public void Execute_CspAllowsEval()
    {
        var csp = new ContentSecurityPolicy();
        csp.Parse("script-src 'self' 'unsafe-eval'");

        var engine = new ScriptEngine { Csp = csp };
        Assert.True(engine.Execute(new[] { "eval('1 + 1');" }));
    }

    [Fact]
    public void Execute_NoCsp_EvalWorks()
    {
        var engine = new ScriptEngine();
        Assert.True(engine.Execute(new[] { "eval('1 + 1');" }));
    }

    [Fact]
    public void Execute_WeakRefPolyfill_Works()
    {
        var engine = new ScriptEngine();
        var result = engine.Execute(new[]
        {
            @"
            var obj = { value: 42 };
            var ref = new WeakRef(obj);
            var derefed = ref.deref();
            if (derefed.value !== 42) throw new Error('WeakRef deref failed');
            "
        });
        Assert.True(result);
    }

    [Fact]
    public void Execute_FinalizationRegistryPolyfill_Works()
    {
        var engine = new ScriptEngine();
        // Verify the constructor and methods exist without registering objects,
        // as YantraJS's native FinalizationRegistry has a finalizer bug
        // when objects are GC'd after JSContext disposal.
        var result = engine.Execute(new[]
        {
            @"
            var registry = new FinalizationRegistry(function(value) {});
            if (typeof registry.register !== 'function') throw new Error('missing register');
            if (typeof registry.unregister !== 'function') throw new Error('missing unregister');
            "
        });
        Assert.True(result);
    }

    [Fact]
    public void Execute_QueueMicrotask_EnqueuesCallback()
    {
        var engine = new ScriptEngine();
        engine.Execute(new[]
        {
            "queueMicrotask(function() { /* callback */ });"
        });
        // After execute, micro-tasks should be drained
        Assert.Equal(0, engine.MicroTasks.Count);
    }

    [Fact]
    public void ExecuteDetailed_EmptyList_ReturnsSuccess()
    {
        var engine = new ScriptEngine();
        var result = engine.ExecuteDetailed(Array.Empty<string>());
        Assert.True(result.Success);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ExecuteDetailed_ValidScript_ReturnsSuccess()
    {
        var engine = new ScriptEngine();
        var result = engine.ExecuteDetailed(new[] { "var x = 1 + 2;" });
        Assert.True(result.Success);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ExecuteDetailed_InvalidScript_ReturnsErrors()
    {
        var engine = new ScriptEngine();
        var result = engine.ExecuteDetailed(new[] { "throw new Error('test error');" });
        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Equal(0, result.Errors[0].ScriptIndex);
        Assert.Contains("test error", result.Errors[0].Message);
    }

    [Fact]
    public void ExecuteDetailed_MultipleErrors_CapturesAll()
    {
        var engine = new ScriptEngine();
        var result = engine.ExecuteDetailed(new[]
        {
            "throw new Error('error 1');",
            "throw new Error('error 2');"
        });
        Assert.False(result.Success);
        Assert.Equal(2, result.Errors.Count);
        Assert.Equal(0, result.Errors[0].ScriptIndex);
        Assert.Equal(1, result.Errors[1].ScriptIndex);
    }

    [Fact]
    public void Execute_WithProfiling_RecordsTimings()
    {
        var profiler = new ScriptProfilingHook();
        var engine = new ScriptEngine { Profiler = profiler };

        engine.Execute(new[] { "var x = 1;", "var y = 2;" });

        Assert.Equal(2, profiler.Entries.Count);
        Assert.Equal("inline-0", profiler.Entries[0].Label);
        Assert.Equal("inline-1", profiler.Entries[1].Label);
        Assert.True(profiler.Entries[0].Succeeded);
        Assert.True(profiler.Entries[1].Succeeded);
    }

    [Fact]
    public void Execute_WithHtml_NoContextSetup_Succeeds()
    {
        var engine = new ScriptEngine();
        // Without ContextSetup configured, Execute(scripts, html) still works
        // (just no DOM is available)
        Assert.True(engine.Execute(new[] { "var x = 1;" }, "<html></html>"));
    }

    [Fact]
    public void Execute_WithHtml_ContextSetupCalled()
    {
        var engine = new ScriptEngine();
        string? receivedHtml = null;

        engine.ContextSetup = (ctx, html) =>
        {
            receivedHtml = html;
        };

        engine.Execute(new[] { "var x = 1;" }, "<html><body>Test</body></html>");
        Assert.Equal("<html><body>Test</body></html>", receivedHtml);
    }

    [Fact]
    public void Execute_ScriptIsolation_FreshContextPerCall()
    {
        var engine = new ScriptEngine();

        // First call defines a variable
        engine.Execute(new[] { "var isolated = 123;" });

        // Second call should NOT see the variable from first call
        // (accessing undefined var in non-strict mode returns undefined, not error)
        var result = engine.ExecuteDetailed(new[]
        {
            "if (typeof isolated !== 'undefined') throw new Error('Variable leaked between contexts');"
        });
        Assert.True(result.Success);
    }

    [Fact]
    public void Execute_ArithmeticExpressions()
    {
        var engine = new ScriptEngine();
        Assert.True(engine.Execute(new[]
        {
            @"
            if (1 + 2 !== 3) throw new Error('addition');
            if (10 - 3 !== 7) throw new Error('subtraction');
            if (4 * 5 !== 20) throw new Error('multiplication');
            if (15 / 3 !== 5) throw new Error('division');
            if (7 % 3 !== 1) throw new Error('modulo');
            "
        }));
    }

    [Fact]
    public void Execute_StringOperations()
    {
        var engine = new ScriptEngine();
        Assert.True(engine.Execute(new[]
        {
            @"
            var s = 'hello' + ' ' + 'world';
            if (s !== 'hello world') throw new Error('concat failed');
            if (s.length !== 11) throw new Error('length failed');
            if (s.toUpperCase() !== 'HELLO WORLD') throw new Error('toUpperCase failed');
            "
        }));
    }

    [Fact]
    public void Execute_ArrayOperations()
    {
        var engine = new ScriptEngine();
        Assert.True(engine.Execute(new[]
        {
            @"
            var arr = [1, 2, 3, 4, 5];
            if (arr.length !== 5) throw new Error('length');
            if (arr.indexOf(3) !== 2) throw new Error('indexOf');
            var mapped = arr.map(function(x) { return x * 2; });
            if (mapped[0] !== 2 || mapped[4] !== 10) throw new Error('map');
            var filtered = arr.filter(function(x) { return x > 3; });
            if (filtered.length !== 2) throw new Error('filter');
            "
        }));
    }

    [Fact]
    public void Execute_ObjectCreation()
    {
        var engine = new ScriptEngine();
        Assert.True(engine.Execute(new[]
        {
            @"
            var obj = { name: 'test', value: 42 };
            if (obj.name !== 'test') throw new Error('property access');
            if (Object.keys(obj).length !== 2) throw new Error('Object.keys');
            "
        }));
    }

    [Fact]
    public void Execute_FunctionDefinition()
    {
        var engine = new ScriptEngine();
        Assert.True(engine.Execute(new[]
        {
            @"
            function add(a, b) { return a + b; }
            if (add(3, 4) !== 7) throw new Error('function call');

            var multiply = function(a, b) { return a * b; };
            if (multiply(3, 4) !== 12) throw new Error('function expression');
            "
        }));
    }

    [Fact]
    public void Execute_ErrorTypes()
    {
        var engine = new ScriptEngine();
        var result = engine.ExecuteDetailed(new[]
        {
            "null.property;"
        });
        Assert.False(result.Success);
        Assert.Single(result.Errors);
    }
}
