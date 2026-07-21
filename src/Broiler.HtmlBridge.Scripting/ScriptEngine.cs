using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.HtmlBridge.Dom;
using Broiler.HtmlBridge.Logging;
using Broiler.HtmlBridge.Scripting;

namespace Broiler.HtmlBridge;

/// <summary>
/// Executes JavaScript using the YantraJS engine.
/// A fresh <see cref="JSContext"/> is created for each call to
/// <see cref="Execute(IReadOnlyList{string})"/> so that scripts from different pages are isolated.
/// </summary>
public sealed partial class ScriptEngine : ITypedScriptEngine
{
    private readonly IDomBridgeRuntimeFactory _domBridgeFactory;

    public ScriptEngine()
        : this(new DomBridgeFactory())
    {
    }

    public ScriptEngine(IDomBridgeRuntimeFactory domBridgeFactory)
    {
        _domBridgeFactory = domBridgeFactory ?? throw new ArgumentNullException(nameof(domBridgeFactory));
    }

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

                MicroTasks.Drain();
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
    public string? Execute(IReadOnlyList<string> scripts, string html)
    {
        return Execute(scripts, html, url: null);
    }

    /// <inheritdoc />
    public string? Execute(IReadOnlyList<string> scripts, string html, string? url)
    {
        return Execute(scripts, Array.Empty<string>(), html, url);
    }

    /// <inheritdoc />
    public string? Execute(IReadOnlyList<string> scripts, IReadOnlyList<string> deferredScripts, string html, string? url)
        => Execute(scripts, deferredScripts, html, url, moduleRoots: null);

    /// <summary>
    /// As <see cref="Execute(IReadOnlyList{string}, IReadOnlyList{string}, string, string?)"/>, with the
    /// document's authorised ES-module roots. When the engine binds imports (<see cref="EngineModuleSupport"/>),
    /// the roots run through the engine's own module machinery on a <see cref="BridgeModuleContext"/>; the
    /// caller must then NOT have pre-appended the linked <see cref="ScriptExtractionResult.ModuleScripts"/> to
    /// <paramref name="deferredScripts"/>. When the engine does not, the roots are ignored and the linked
    /// strings in <paramref name="deferredScripts"/> run as before.
    /// </summary>
    public string? Execute(IReadOnlyList<string> scripts, IReadOnlyList<string> deferredScripts, string html, string? url, IReadOnlyList<ModuleRoot>? moduleRoots)
        => ExecuteCore(scripts, deferredScripts, html, url, moduleRoots, static bridge => bridge.SerializeToHtml());

    /// <summary>
    /// Executes scripts against the canonical DOM and returns that same
    /// document for direct renderer consumption, avoiding serialization and
    /// reparsing between script execution and layout.
    /// </summary>
    public Broiler.Dom.DomDocument? ExecuteToDocument(
        IReadOnlyList<string> scripts,
        IReadOnlyList<string> deferredScripts,
        string html,
        string? url)
        => ExecuteToDocument(scripts, deferredScripts, html, url, moduleRoots: null);

    /// <summary>
    /// As <see cref="ExecuteToDocument(IReadOnlyList{string}, IReadOnlyList{string}, string, string?)"/>,
    /// with the document's authorised ES-module roots for the engine-driven module path.
    /// </summary>
    public Broiler.Dom.DomDocument? ExecuteToDocument(
        IReadOnlyList<string> scripts,
        IReadOnlyList<string> deferredScripts,
        string html,
        string? url,
        IReadOnlyList<ModuleRoot>? moduleRoots)
        => ExecuteCore(scripts, deferredScripts, html, url, moduleRoots, static bridge => bridge.GetRenderDocument());

    private T? ExecuteCore<T>(
        IReadOnlyList<string> scripts,
        IReadOnlyList<string> deferredScripts,
        string html,
        string? url,
        IReadOnlyList<ModuleRoot>? moduleRoots,
        Func<IDomBridgeRuntime, T> createResult)
        where T : class
    {
        var roots = moduleRoots ?? [];
        if (scripts.Count == 0 && deferredScripts.Count == 0 && roots.Count == 0)
            return null;

        var previousCsp = Csp;
        Csp = ContentSecurityPolicy.FromHtml(html) ?? previousCsp;

        // Drive the engine's own module machinery only when it actually binds imports (patches 0010/0011);
        // otherwise the page runs on a plain JSContext and modules come in as linked strings via the linker.
        var useEngineModules = roots.Count > 0 && EngineModuleSupport.Available;
        var moduleContext = useEngineModules ? new BridgeModuleContext(Csp, url) : null;

        try
        {
            using JSContext context = moduleContext ?? new JSContext();
            RegisterRuntimeExtensions(context);
            var bridge = _domBridgeFactory.Create();
            bridge.Csp = Csp;
            bridge.TaskCheckpointCallback = () => MicroTasks.Drain();

            if (!string.IsNullOrEmpty(url))
                bridge.Attach(context, html, url);
            else
                bridge.Attach(context, html);

            // Track the corresponding <script> DOM element index so that
            // document.write() can insert content at the correct position.
            var scriptElements = new List<int>();
            for (int idx = 0; idx < bridge.Elements.Count; idx++)
            {
                if (string.Equals(bridge.Elements[idx].TagName, "script", StringComparison.OrdinalIgnoreCase))
                    scriptElements.Add(idx);
            }

            for (var i = 0; i < scripts.Count; i++)
            {
                if (i < scriptElements.Count)
                    bridge.CurrentScriptIndex = scriptElements[i];
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

                    DrainAsyncWork(bridge);
                }
                catch (Exception ex)
                {
                    RenderLogger.LogError(LogCategory.JavaScript, "ScriptEngine.Execute", $"Script inline-{i} failed: {ex.Message}", ex);
                }
            }
            bridge.CurrentScriptIndex = -1;

            // Execute deferred scripts after all regular scripts
            // (simulates end-of-parsing for <script defer> tags).
            foreach (var script in deferredScripts)
            {
                try
                {
                    var source = PrepareSource(script);
                    context.Eval(source);
                    DrainAsyncWork(bridge);
                }
                catch (Exception ex)
                {
                    RenderLogger.LogError(LogCategory.JavaScript, "ScriptEngine.Execute", $"Deferred script failed: {ex.Message}", ex);
                }
            }

            // Engine-driven ES modules (Phase 7 item 6): modules are deferred, so run the authorised roots
            // after the classic deferred scripts. Each root executes on the same realm the DOM is attached
            // to, and the engine loads its transitive imports itself (CSP-gated) via BridgeModuleContext's
            // resolution seams — no EsModuleLinker involved. Reached only when EngineModuleSupport.Available.
            if (moduleContext != null)
            {
                foreach (var root in roots)
                {
                    try
                    {
                        moduleContext.RunScriptAsync(root.Source, root.BaseUrl ?? url ?? string.Empty, uniqueModuleID: root.Key)
                            .GetAwaiter().GetResult();
                        DrainAsyncWork(bridge);
                    }
                    catch (Exception ex)
                    {
                        RenderLogger.LogError(LogCategory.JavaScript, "ScriptEngine.Execute", $"Module root {root.Key} failed: {ex.Message}", ex);
                    }
                }
            }

            // Fire body onload event after all scripts have executed
            // (simulates end-of-parsing / window load in browsers).
            // This is critical for test harnesses like Acid3 that bootstrap
            // the test runner via <body onload="update()">.
            bridge.FireWindowLoadEvent();
            DrainAsyncWork(bridge);
            return createResult(bridge);
        }
        finally
        {
            Csp = previousCsp;
        }
    }

    /// <inheritdoc />
    public InteractiveSession? ExecuteInteractive(IReadOnlyList<string> scripts, IReadOnlyList<string> deferredScripts, string html, string? url)
        => ExecuteInteractive(scripts, deferredScripts, html, url, moduleRoots: null);

    /// <summary>
    /// As <see cref="ExecuteInteractive(IReadOnlyList{string}, IReadOnlyList{string}, string, string?)"/>,
    /// with the document's authorised ES-module roots. When the engine binds imports the roots run through
    /// the engine's module machinery on a <see cref="BridgeModuleContext"/> (whose lifetime transfers to the
    /// returned session); otherwise they are ignored and the linked strings in <paramref name="deferredScripts"/>
    /// run as before. Modules are deferred, so they run eagerly here after the deferred scripts.
    /// </summary>
    public InteractiveSession? ExecuteInteractive(IReadOnlyList<string> scripts, IReadOnlyList<string> deferredScripts, string html, string? url, IReadOnlyList<ModuleRoot>? moduleRoots)
    {
        var roots = moduleRoots ?? [];
        if (scripts.Count == 0 && deferredScripts.Count == 0 && roots.Count == 0)
            return null;

        var previousCsp = Csp;
        Csp = ContentSecurityPolicy.FromHtml(html) ?? previousCsp;

        var useEngineModules = roots.Count > 0 && EngineModuleSupport.Available;
        var moduleContext = useEngineModules ? new BridgeModuleContext(Csp, url) : null;

        // The JSContext is NOT disposed here – ownership transfers to the
        // InteractiveSession which will dispose it when the caller is done.
        JSContext context = moduleContext ?? new JSContext();
        RegisterRuntimeExtensions(context);
        var bridge = _domBridgeFactory.Create();
        bridge.Csp = Csp;
        bridge.TaskCheckpointCallback = () => MicroTasks.Drain();

        if (!string.IsNullOrEmpty(url))
            bridge.Attach(context, html, url);
        else
            bridge.Attach(context, html);

        // Track the corresponding <script> DOM element index so that
        // document.write() can insert content at the correct position.
        var scriptElements = new List<int>();
        for (int idx = 0; idx < bridge.Elements.Count; idx++)
        {
            if (string.Equals(bridge.Elements[idx].TagName, "script", StringComparison.OrdinalIgnoreCase))
                scriptElements.Add(idx);
        }

        for (var i = 0; i < scripts.Count; i++)
        {
            if (i < scriptElements.Count)
                bridge.CurrentScriptIndex = scriptElements[i];
            try
            {
                var source = PrepareSource(scripts[i]);
                context.Eval(source);
                MicroTasks.Drain();
            }
            catch (Exception ex)
            {
                RenderLogger.LogError(LogCategory.JavaScript, "ScriptEngine.ExecuteInteractive", $"Script inline-{i} failed: {ex.Message}", ex);
            }
        }
        bridge.CurrentScriptIndex = -1;

        foreach (var script in deferredScripts)
        {
            try
            {
                var source = PrepareSource(script);
                context.Eval(source);
                MicroTasks.Drain();
            }
            catch (Exception ex)
            {
                RenderLogger.LogError(LogCategory.JavaScript, "ScriptEngine.ExecuteInteractive", $"Deferred script failed: {ex.Message}", ex);
            }
        }

        // Engine-driven ES modules (see ExecuteCore): run the authorised roots on the DOM-attached realm.
        if (moduleContext != null)
        {
            foreach (var root in roots)
            {
                try
                {
                    moduleContext.RunScriptAsync(root.Source, root.BaseUrl ?? url ?? string.Empty, uniqueModuleID: root.Key)
                        .GetAwaiter().GetResult();
                    MicroTasks.Drain();
                }
                catch (Exception ex)
                {
                    RenderLogger.LogError(LogCategory.JavaScript, "ScriptEngine.ExecuteInteractive", $"Module root {root.Key} failed: {ex.Message}", ex);
                }
            }
        }

        bridge.FireWindowLoadEvent();
        MicroTasks.Drain();

        Csp = previousCsp;
        return new InteractiveSession(context, bridge, MicroTasks);
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

                MicroTasks.Drain();
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
    /// Drain queued microtasks and timer tasks until the bridge-backed execution
    /// environment settles, matching the checkpointing used by the WPT harness.
    /// </summary>
    private void DrainAsyncWork(IDomBridgeRuntime bridge)
    {
        for (var iteration = 0; iteration < DomBridgeRuntimeLimits.AsyncDrainIterationLimit; iteration++)
        {
            var hadWork = false;

            if (MicroTasks.Count > 0)
            {
                MicroTasks.Drain();
                hadWork = true;
            }

            if (bridge.HasPendingTimers)
            {
                bridge.FlushTimerStep();
                hadWork = true;
            }

            if (!hadWork)
                break;
        }
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
        context["queueMicrotask"] = new JSFunction((in Arguments a) => JsScriptEngineQueueMicrotask001Core(in a), "queueMicrotask", 1);

        // CSP-gated eval wrapper
        if (Csp != null && !Csp.AllowsEval)
        {
            context["eval"] = new JSFunction((in Arguments _) => JsScriptEngineEval002Core(in _), "eval", 1);
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

        var weakRefCtor = new JSFunction((in Arguments args) => JsScriptEngineWeakRef004Core(in args), "WeakRef", 1);

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

        var registryCtor = new JSFunction((in Arguments args) => JsScriptEngineFinalizationRegistry007Core(in args), "FinalizationRegistry", 1);

        context["FinalizationRegistry"] = registryCtor;
    }
}
