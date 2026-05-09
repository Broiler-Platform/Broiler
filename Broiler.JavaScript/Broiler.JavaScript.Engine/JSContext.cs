using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine.Core;

namespace Broiler.JavaScript.Engine;

public delegate void ConsoleEvent(JSContext context, string type, in Arguments a);
public delegate void LogEventHandler(JSContext context, JSValue value);
public delegate void ErrorEventHandler(JSContext context, Exception error);

public class EvalEventArgs : EventArgs
{
    public JSContext Context { get; set; }
    public string Script { get; set; }
    public string Location { get; set; }
}

public class JSContext : JSObject, IJSExecutionContext, IDisposable
{
    private static long contextId = 1;

    public long ID { get; set; } = Interlocked.Increment(ref contextId);

    /// <summary>
    /// Gets or sets the debugger attached to this context.
    /// </summary>
    public IDebugger Debugger;

    /// <summary>
    /// Available only when Enable Clr Integration is true in JSModuleContext
    /// </summary>
    public ClrMemberNamingConvention ClrMemberNamingConvention { get; set; } = ClrMemberNamingConvention.CamelCase;

    private TaskCompletionSource<int> _waitTask;
    public Task WaitTask => _waitTask?.Task;

    public CallStackItem Top { get; set; }

    public JSValue CurrentNewTarget { get; set; }

    public event EventHandler<EvalEventArgs> EvalEvent;

    public void DispatchEvalEvent(ref string script, ref string location)
    {
        var ee = EvalEvent;
        if (ee == null)
            return;

        var e = new EvalEventArgs { Context = this, Script = script, Location = location };
        EvalEvent.Invoke(this, e);
        script = e.Script;
        location = e.Location;
    }

    public void Dispose() => JSEngine.ClearAsyncLocal();

    public JSObject FunctionPrototype { get; private set; }
    public new JSObject ObjectPrototype { get; private set; }
    public JSValue Object { get; private set; }
    public event LogEventHandler Log;
    public event ErrorEventHandler Error;
    public event ConsoleEvent ConsoleEvent;
    public JavaScriptFeatureFlags ExperimentalFeatures { get; }

    SAUint32Map<JSVariable> globalVars = new();

    public JSValue Register(JSVariable variable)
    {
        var v = variable.Value;
        var oldV = this[variable.Name];

        if (oldV != v)
        {
            this[variable.Name] = v;
        }

        KeyString name = variable.Name;
        globalVars.Put(name.Key) = variable;
        return v;
    }

    public override JSValue this[KeyString name]
    {
        get => base[name];
        set
        {
            base[name] = value;
            if (globalVars.TryGetValue(name.Key, out var jsv))
                jsv.Value = value;
        }
    }

    internal void FillStackTrace(StringBuilder sb) { }

    public JSContext(
        SynchronizationContext synchronizationContext = null,
        JavaScriptFeatureFlags experimentalFeatures = JavaScriptFeatureFlags.None)
    {
        JSEngine.EnsureBuiltInsAssemblyLoaded();

        this.synchronizationContext = synchronizationContext ?? SynchronizationContext.Current;
        ExperimentalFeatures = experimentalFeatures;

        JSEngine.CurrentContext = this;

        ref var ownProperties = ref GetOwnProperties();

        KeyString functionKey = "Function";
        KeyString objectKey = "Object";

        var func = JSEngine.CreateFunctionClass(this, false);
        this[functionKey] = func;
        FunctionPrototype = ((IJSFunction)func).Prototype as JSObject;
        Object = JSEngine.CreateObjectClass(this, false);
        this[objectKey] = Object;
        ObjectPrototype = ((IJSFunction)Object).Prototype as JSObject;
        ObjectPrototype.BasePrototypeObject = null;

        func.BasePrototypeObject = Object;
        FunctionPrototype.BasePrototypeObject = ObjectPrototype;

        if (JSEngine.BuiltInRegistry != null)
        {
            JSEngine.BuiltInRegistry.Register(this);
        }
        else
        {
            JSEngine.CoreClassRegistrations?.Invoke(this);
        }

        this[KeyStrings.debug] = JSValue.CreateFunction(Debug);
    }

    public bool HasExperimentalFeature(JavaScriptFeatureFlags feature)
        => (ExperimentalFeatures & feature) == feature;

    internal void FireConsoleEvent(string type, in Arguments a) => ConsoleEvent?.Invoke(this, type, in a);

    private JSValue Debug(in Arguments a)
    {
        System.Diagnostics.Debug.WriteLine(a.Get1().ToString());
        return JSUndefined.Value;
    }

    internal readonly ConcurrentDictionary<long, Timer> timeouts = new();
    internal readonly ConcurrentDictionary<long, Timer> timers = new();

    internal void ClearTimeout(long n)
    {
        if (timeouts.TryRemove(n, out var timer))
        {
            try
            {
                timer.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Broiler.JavaScript] ClearTimeout dispose error: {ex.Message}");
            }
        }

        if (timers.IsEmpty && timeouts.IsEmpty)
            _waitTask.TrySetResult(1);
    }

    internal void ClearInterval(long n)
    {
        if (timers.TryRemove(n, out var timer))
        {
            try
            {
                timer.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Broiler.JavaScript] ClearInterval dispose error: {ex.Message}");
            }
        }

        if (timers.IsEmpty && timeouts.IsEmpty)
            _waitTask.TrySetResult(1);
    }

    static readonly ConcurrentUInt32Map<JSValue> cache = ConcurrentUInt32Map<JSValue>.Create();
    internal readonly SynchronizationContext synchronizationContext;

    private static long nextTimeout = 1;
    private static long nextInterval = 1;

    internal void ReportError(Exception ex)
    {
        Error?.Invoke(this, ex);
    }

    public void ReportLog(JSValue f) => Log?.Invoke(this, f);

    internal long PostTimeout(int delay, JSValue f, in Arguments a)
    {
        var ctx = synchronizationContext ?? throw JSEngine.NewTypeError($"Synchronization context must be present to set timeout");
        var key = Interlocked.Increment(ref nextTimeout);
        JSValue[] args = System.Array.Empty<JSValue>();

        if (a.Length > 2)
        {
            args = new JSValue[a.Length - 2];
            for (int i = 2; i < a.Length; i++)
                args[i - 2] = a.GetAt(i);
        }

        var timer = new Timer((_) =>
        {
            ctx.Post((x) =>
            {
                var f = x as JSValue;
                try
                {
                    f.InvokeFunction(new Arguments(JSUndefined.Value, args));
                }
                catch (Exception ex)
                {
                    ReportError(ex);
                }
                ClearTimeout(key);
            }, f);
        }, f, delay, Timeout.Infinite);

        timeouts.AddOrUpdate(key, timer, (a1, a2) => a2);
        lock (this)
        {
            _waitTask = _waitTask ?? new TaskCompletionSource<int>();
        }

        return key;
    }

    internal long SetInterval(int delay, JSValue f, in Arguments a)
    {
        var ctx = synchronizationContext ?? throw JSEngine.NewTypeError($"Synchronization context must be present to set timeout");
        var key = Interlocked.Increment(ref nextInterval);
        JSValue[] args = System.Array.Empty<JSValue>();

        if (a.Length > 2)
        {
            args = new JSValue[a.Length - 2];
            for (int i = 2; i < a.Length; i++)
                args[i - 2] = a.GetAt(i);
        }

        var timer = new Timer((_) =>
        {
            ctx.Post((x) =>
            {
                var f = x as JSValue;
                try
                {
                    f.InvokeFunction(new Arguments(JSUndefined.Value, args));
                }
                catch (Exception ex)
                {
                    ReportError(ex);
                }
                ClearInterval(key);
            }, f);
        }, f, delay, Timeout.Infinite);

        timers.AddOrUpdate(key, timer, (a1, a2) => a2);
        lock (this)
        {
            _waitTask = _waitTask ?? new TaskCompletionSource<int>();
        }
        return key;
    }

    public ICodeCache CodeCache { get; set; } = DictionaryCodeCache.Current;

    internal ConcurrentDictionary<long, JSValue> PendingPromises = new();

    /// <summary>
    /// Quickly evaluates the code, does not wait for promises and timeouts/intervals.
    /// </summary>
    public JSValue Eval(string code, string codeFilePath = null, JSValue @this = null)
    {
        @this ??= this;
        if (Debugger == null)
        {
            var fx = CoreScript.Compile(code, codeFilePath, codeCache: CodeCache);
            return fx(new Arguments(@this));
        }

        try
        {
            var f = CoreScript.Compile(code, codeFilePath, codeCache: CodeCache);
            Debugger.ScriptParsed(ID, code, codeFilePath);
            return f(new Arguments(@this));
        }
        catch (Exception ex)
        {
            ReportError(ex);
            throw;
        }
    }

    /// <summary>
    /// Evaluates the given code, waits for the promise and returns task that
    /// completes till all timeouts/intervals are completed.
    /// </summary>
    public async Task<JSValue> ExecuteAsync(string code, string codeFilePath = null)
    {
        var r = CoreScript.Evaluate(code, codeFilePath, codeCache: CodeCache);
        var wt = WaitTask;
        if (wt != null)
            await wt;

        if (r is IJSPromise promise)
            return await promise.Task;

        if (r is not JSObject @object)
            return r;

        var then = @object[KeyStrings.then];
        if (!then.IsFunction)
            return r;

        var promiseObj = JSEngine.CreatePromiseFromDelegate((resolve, reject) =>
        {
            var resolveF = JSValue.CreateFunction((in Arguments a) =>
            {
                var a1 = a.Get1();
                resolve(a1);
                return a1;
            });

            var rejectF = JSValue.CreateFunction((in Arguments a) =>
            {
                var a1 = a.Get1();
                reject(a1);
                return a1;
            });

            var a = new Arguments(@object, resolveF, rejectF);
            then.InvokeFunction(a);
        });

        return await promiseObj.Task;
    }

    /// <summary>
    /// Evaluates the given code, waits for the promise and also
    /// waits synchronously (by running and AsyncPump) for timeouts/intervals to finish
    /// </summary>
    public JSValue Execute(string code, string codeFilePath = null) => AsyncPump.Run(() => ExecuteAsync(code, codeFilePath));
}
