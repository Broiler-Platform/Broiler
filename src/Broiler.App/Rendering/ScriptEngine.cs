using System;
using System.Collections.Generic;
using YantraJS.Core;

namespace Broiler.App.Rendering;

/// <summary>
/// Executes JavaScript using the YantraJS engine.
/// A fresh <see cref="JSContext"/> is created for each call to
/// <see cref="Execute(IReadOnlyList{string})"/> so that scripts from different pages are isolated.
/// </summary>
public sealed class ScriptEngine : IScriptEngine
{

    /// <inheritdoc />
    public bool StrictModeEnabled { get; set; }

    /// <inheritdoc />
    public ContentSecurityPolicy? Csp { get; set; }

    /// <inheritdoc />
    public ScriptProfilingHook? Profiler { get; set; }

    /// <inheritdoc />
    public MicroTaskQueue MicroTasks { get; } = new();

    /// <inheritdoc />
    public bool Execute(IReadOnlyList<string> scripts)
    {
        if (scripts.Count == 0)
            return true;

        using var context = new JSContext();
        RegisterRuntimeExtensions(context);
        var allSucceeded = true;
        for (var i = 0; i < scripts.Count; i++)
        {
            try
            {
                var source = PrepareSource(scripts[i]);
                if (Profiler != null)
                {
                    Profiler.Measure($"inline-{i}", () => context.Eval(source));
                }
                else
                {
                    context.Eval(source);
                }
            }
            catch (Exception ex)
            {
                RenderLogger.LogError(LogCategory.JavaScript, "ScriptEngine.Execute", $"Script inline-{i} failed: {ex.Message}", ex);
                allSucceeded = false;
            }
        }
        MicroTasks.Drain();
        return allSucceeded;
    }

    /// <inheritdoc />
    public bool Execute(IReadOnlyList<string> scripts, string html)
    {
        if (scripts.Count == 0)
            return true;

        using var context = new JSContext();
        RegisterRuntimeExtensions(context);
        var bridge = new DomBridge();
        bridge.Attach(context, html);

        var allSucceeded = true;
        for (var i = 0; i < scripts.Count; i++)
        {
            try
            {
                var source = PrepareSource(scripts[i]);
                if (Profiler != null)
                {
                    Profiler.Measure($"inline-{i}", () => context.Eval(source));
                }
                else
                {
                    context.Eval(source);
                }
            }
            catch (Exception ex)
            {
                RenderLogger.LogError(LogCategory.JavaScript, "ScriptEngine.Execute", $"Script inline-{i} failed: {ex.Message}", ex);
                allSucceeded = false;
            }
        }
        MicroTasks.Drain();
        return allSucceeded;
    }

    /// <inheritdoc />
    public ScriptExecutionResult ExecuteDetailed(IReadOnlyList<string> scripts)
    {
        if (scripts.Count == 0)
            return new ScriptExecutionResult { Success = true };

        using var context = new JSContext();
        RegisterRuntimeExtensions(context);
        var errors = new List<ScriptError>();

        for (var i = 0; i < scripts.Count; i++)
        {
            try
            {
                var source = PrepareSource(scripts[i]);
                if (Profiler != null)
                {
                    Profiler.Measure($"inline-{i}", () => context.Eval(source));
                }
                else
                {
                    context.Eval(source);
                }
            }
            catch (Exception ex)
            {
                RenderLogger.LogError(LogCategory.JavaScript, "ScriptEngine.ExecuteDetailed", $"Script inline-{i} failed: {ex.Message}", ex);
                errors.Add(new ScriptError
                {
                    ScriptIndex = i,
                    Message = ex.Message,
                    StackTrace = ex.StackTrace ?? string.Empty
                });
            }
        }
        MicroTasks.Drain();

        return new ScriptExecutionResult
        {
            Success = errors.Count == 0,
            Errors = errors
        };
    }

    /// <summary>
    /// Optionally prepend <c>"use strict";</c> to the script source.
    /// </summary>
    private string PrepareSource(string script) => StrictModeEnabled ? "\"use strict\";\n" + script : script;

    /// <summary>
    /// Register Milestone 4 runtime extensions on the JS context:
    /// <c>queueMicrotask</c>, CSP-gated <c>eval</c>, and polyfills for
    /// ES2023+ built-ins not natively provided by YantraJS.
    /// </summary>
    private void RegisterRuntimeExtensions(JSContext context)
    {
        // queueMicrotask(fn)
        context["queueMicrotask"] = new JSFunction((in Arguments a) =>
        {
            if (a.Length > 0 && a[0] is JSFunction fn)
            {
                MicroTasks.Enqueue(() =>
                {
                    try { fn.InvokeFunction(new Arguments(JSUndefined.Value)); }
                    catch (Exception ex) { RenderLogger.LogError(LogCategory.JavaScript, "ScriptEngine.queueMicrotask", $"Callback error: {ex.Message}", ex); }
                });
            }
            return JSUndefined.Value;
        }, "queueMicrotask", 1);

        // CSP-gated eval wrapper
        if (Csp != null && !Csp.AllowsEval)
        {
            context["eval"] = new JSFunction((in Arguments _) =>
            {
                throw new InvalidOperationException(
                    "Refused to evaluate a string as JavaScript because 'unsafe-eval' is not an allowed source in the Content Security Policy.");
            }, "eval", 1);
        }

        // WeakRef polyfill (YantraJS may not expose this natively)
        RegisterWeakRefPolyfill(context);

        // FinalizationRegistry polyfill
        RegisterFinalizationRegistryPolyfill(context);
    }

    /// <summary>
    /// Register a minimal <c>WeakRef</c> constructor.  Because .NET's GC
    /// model differs from V8/SpiderMonkey, the implementation uses
    /// <see cref="WeakReference{T}"/> under the hood.
    /// </summary>
    private static void RegisterWeakRefPolyfill(JSContext context)
    {
        // Only install if not already present
        try
        {
            var existing = context.Eval("typeof WeakRef");
            if (existing is JSString s && s.ToString() != "undefined")
                return;
        }
        catch (Exception ex) { RenderLogger.LogDebug(LogCategory.JavaScript, "ScriptEngine.WeakRefPolyfill", $"WeakRef not present, installing polyfill: {ex.Message}"); }

        var weakRefCtor = new JSFunction((in Arguments args) =>
        {
            if (args.Length == 0)
                throw new InvalidOperationException("WeakRef requires a target object.");

            var target = args[0];
            var weakRef = new WeakReference<JSValue>(target);

            var instance = new JSObject();
            instance.FastAddValue((KeyString)"deref", new JSFunction((in Arguments _) =>
            {
                return weakRef.TryGetTarget(out var t) ? t : JSUndefined.Value;
            }, "deref", 0), JSPropertyAttributes.EnumerableConfigurableValue);

            return instance;
        }, "WeakRef", 1);

        context["WeakRef"] = weakRefCtor;
    }

    /// <summary>
    /// Register a minimal <c>FinalizationRegistry</c> constructor.
    /// Since .NET GC timing is non-deterministic, the cleanup callback
    /// is exposed but invocation depends on GC scheduling.
    /// </summary>
    private static void RegisterFinalizationRegistryPolyfill(JSContext context)
    {
        try
        {
            var existing = context.Eval("typeof FinalizationRegistry");
            if (existing is JSString s && s.ToString() != "undefined")
                return;
        }
        catch (Exception ex) { RenderLogger.LogDebug(LogCategory.JavaScript, "ScriptEngine.FinalizationRegistryPolyfill", $"FinalizationRegistry not present, installing polyfill: {ex.Message}"); }

        var registryCtor = new JSFunction((in Arguments args) =>
        {
            // The callback is stored but invocation depends on .NET GC
            var callback = args.Length > 0 ? args[0] as JSFunction : null;

            var instance = new JSObject();

            // register(target, heldValue [, unregisterToken])
            instance.FastAddValue((KeyString)"register", new JSFunction((in Arguments regArgs) =>
            {
                // No-op in this polyfill; real cleanup requires GC integration
                return JSUndefined.Value;
            }, "register", 3), JSPropertyAttributes.EnumerableConfigurableValue);

            // unregister(unregisterToken)
            instance.FastAddValue((KeyString)"unregister", new JSFunction((in Arguments unregArgs) =>
            {
                return JSBoolean.False;
            }, "unregister", 1), JSPropertyAttributes.EnumerableConfigurableValue);

            return instance;
        }, "FinalizationRegistry", 1);

        context["FinalizationRegistry"] = registryCtor;
    }
}
