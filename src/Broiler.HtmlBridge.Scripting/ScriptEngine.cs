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

    /// <summary>
    /// Phase 8 item 3: diagnostic for async-drain-limit exhaustion. <see langword="true"/> when a
    /// call to <see cref="DrainAsyncWork"/> ran out its iteration budget
    /// (<see cref="DomBridgeRuntimeLimits.AsyncDrainIterationLimit"/>) while microtasks or timers were
    /// still queued — i.e. the async work did not settle and draining stopped. This makes the former
    /// silent stop observable (also logged as a warning). A fresh <see cref="ScriptEngine"/> per page
    /// means this reflects whether that page's async work exhausted the budget. Stays
    /// <see langword="false"/> when every drain settled normally.
    /// </summary>
    public bool AsyncDrainLimitExhausted { get; private set; }

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
                RunMeasured($"inline-{i}", () => context.Eval(source));
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
    /// the roots run through the engine's own module machinery on a <see cref="BridgeModuleContext"/> after the
    /// <paramref name="deferredScripts"/>; otherwise they are left unrun (the string-rewriting linker fallback
    /// was retired in the Phase 7 tail).
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

            // Event-loop ordering (EL-3): run the synchronous script phases (regular → deferred → modules)
            // with only microtask checkpoints between them, then — after the window load event — drain the
            // timer queue to completion. So a timer scheduled by an early script fires after all script
            // execution (in deadline order), not eagerly between scripts, matching the HTML task model.
            RunPageScripts(context, bridge, moduleContext, scripts, deferredScripts, url, roots,
                _ => MicroTasks.Drain(), DrainAsyncWork, "ScriptEngine.Execute");
            return createResult(bridge);
        }
        finally
        {
            Csp = previousCsp;
        }
    }

    /// <summary>
    /// The single script-execution pipeline shared by the render/typed path (<see cref="ExecuteCore{T}"/>)
    /// and the interactive path (<see cref="ExecuteInteractive(IReadOnlyList{string}, IReadOnlyList{string}, string, string?, IReadOnlyList{ModuleRoot})"/>).
    /// On an already-attached <paramref name="bridge"/>/<paramref name="context"/> it runs, in document order:
    /// the regular <paramref name="scripts"/> (tracking each one's <c>&lt;script&gt;</c> DOM element index for
    /// <c>document.write</c> and applying the <see cref="Profiler"/> when set), the
    /// <paramref name="deferredScripts"/> (end-of-parse for <c>defer</c>), and the authorised engine-driven
    /// module <paramref name="roots"/> (Phase 7 item 6, only when <paramref name="moduleContext"/> is non-null),
    /// then fires the window <c>load</c> event (critical for <c>&lt;body onload&gt;</c> harnesses like Acid3).
    /// Async work is settled in two phases (EL-3 event-loop ordering): <paramref name="interScriptDrain"/> runs
    /// after each eval and drains only microtasks (a microtask checkpoint), so timers are <em>not</em> fired
    /// eagerly between synchronous scripts; <paramref name="finalDrain"/> runs once after the load event. The
    /// render path passes a full timer-draining <c>finalDrain</c> (so timers fire, in deadline order, after all
    /// script execution), while the interactive path drains only microtasks in both and leaves timers for the
    /// session to step. Per-script failures are caught and logged under
    /// <paramref name="logSource"/>; they do not abort the remaining scripts.
    /// </summary>
    private void RunPageScripts(
        JSContext context,
        IDomBridgeRuntime bridge,
        BridgeModuleContext? moduleContext,
        IReadOnlyList<string> scripts,
        IReadOnlyList<string> deferredScripts,
        string? url,
        IReadOnlyList<ModuleRoot> roots,
        Action<IDomBridgeRuntime> interScriptDrain,
        Action<IDomBridgeRuntime> finalDrain,
        string logSource)
    {
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
                RunMeasured($"inline-{i}", () => context.Eval(source));
                interScriptDrain(bridge);
            }
            catch (Exception ex)
            {
                RenderLogger.LogError(LogCategory.JavaScript, logSource, $"Script inline-{i} failed: {ex.Message}", ex);
            }
        }
        bridge.CurrentScriptIndex = -1;

        // Execute deferred scripts after all regular scripts (end-of-parsing for <script defer>).
        for (var i = 0; i < deferredScripts.Count; i++)
        {
            try
            {
                var source = PrepareSource(deferredScripts[i]);
                RunMeasured($"deferred-{i}", () => context.Eval(source));
                interScriptDrain(bridge);
            }
            catch (Exception ex)
            {
                RenderLogger.LogError(LogCategory.JavaScript, logSource, $"Deferred script failed: {ex.Message}", ex);
            }
        }

        // Engine-driven ES modules (Phase 7 item 6): modules are deferred, so run the authorised roots
        // after the classic deferred scripts. Each root executes on the same realm the DOM is attached to,
        // and the engine loads its transitive imports itself (CSP-gated) via BridgeModuleContext's resolution
        // seams — no EsModuleLinker involved. Reached only when EngineModuleSupport.Available.
        if (moduleContext != null)
        {
            foreach (var root in roots)
            {
                try
                {
                    RunMeasured($"module-{root.Key}", () =>
                        moduleContext.RunScriptAsync(root.Source, root.BaseUrl ?? url ?? string.Empty, uniqueModuleID: root.Key)
                            .GetAwaiter().GetResult());
                    interScriptDrain(bridge);
                }
                catch (Exception ex)
                {
                    RenderLogger.LogError(LogCategory.JavaScript, logSource, $"Module root {root.Key} failed: {ex.Message}", ex);
                }
            }
        }

        bridge.FireWindowLoadEvent();
        finalDrain(bridge);
    }

    /// <summary>
    /// Runs <paramref name="work"/> (a single script/module evaluation), timing it through
    /// <see cref="Profiler"/> when a hook is attached and running it directly otherwise. Every script the
    /// engine executes — inline (<c>inline-{i}</c>), deferred (<c>deferred-{i}</c>) and engine-driven module
    /// roots (<c>module-{key}</c>) — funnels through here so the profiling hook, when set, sees a complete
    /// and consistent timeline rather than the inline scripts alone (Phase 8 item 4).
    /// </summary>
    private void RunMeasured(string label, Action work)
    {
        if (Profiler != null)
            Profiler.Measure(label, work);
        else
            work();
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

        // Ownership of the context + bridge transfers to the returned InteractiveSession, which
        // disposes both. If setup throws before the session is built, the catch disposes them so a
        // failed ExecuteInteractive never leaks a JS context or an event loop (Phase 8 item 2). The
        // CSP is restored in the finally on every path.
        JSContext context = moduleContext ?? new JSContext();
        IDomBridgeRuntime? bridge = null;
        try
        {
            RegisterRuntimeExtensions(context);
            bridge = _domBridgeFactory.Create();
            bridge.Csp = Csp;
            bridge.TaskCheckpointCallback = () => MicroTasks.Drain();

            if (!string.IsNullOrEmpty(url))
                bridge.Attach(context, html, url);
            else
                bridge.Attach(context, html);

            // Same pipeline as the render path, but the interactive session drains only microtasks at every
            // point (inter-script and final) and leaves pending timers for the caller to step through one
            // batch at a time.
            RunPageScripts(context, bridge, moduleContext, scripts, deferredScripts, url, roots,
                _ => MicroTasks.Drain(), _ => MicroTasks.Drain(), "ScriptEngine.ExecuteInteractive");

            return new InteractiveSession(context, bridge, MicroTasks);
        }
        catch
        {
            // Failed construction must not leak the private event loop / context.
            (bridge as IDisposable)?.Dispose();
            context.Dispose();
            throw;
        }
        finally
        {
            Csp = previousCsp;
        }
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
                RunMeasured($"inline-{i}", () => context.Eval(source));
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
                return; // settled — the queues drained within the budget
        }

        // Phase 8 item 3: the iteration budget is exhausted while work is still queued (typically a
        // runaway microtask/timer loop, e.g. a setTimeout/queueMicrotask that reschedules itself).
        // Surface this explicitly instead of stopping silently: record it on the engine and log a
        // warning with the pending counts so it is diagnosable rather than an invisible truncation.
        AsyncDrainLimitExhausted = true;
        RenderLogger.LogWarning(LogCategory.JavaScript, "ScriptEngine.DrainAsyncWork",
            $"Async work did not settle within {DomBridgeRuntimeLimits.AsyncDrainIterationLimit} drain iterations; " +
            $"stopping with pending microtasks={MicroTasks.Count}, pendingTimers={bridge.HasPendingTimers}. " +
            "This usually indicates a runaway microtask/timer loop.");
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
