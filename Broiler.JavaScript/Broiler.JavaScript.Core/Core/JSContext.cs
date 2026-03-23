using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.ComponentModel;
using Broiler.JavaScript.Core.CodeGen;
using Broiler.JavaScript.Core.Core.Clr;
using Broiler.JavaScript.Core.Core.Promise;
using Broiler.JavaScript.Core.Debugger;
using Broiler.JavaScript.Core.Emit;
using Broiler.JavaScript.Core.Core.Primitive;
using Broiler.JavaScript.Core.Core.Function;
using Broiler.JavaScript.Core.Core.Error;
using Broiler.JavaScript.Storage;

namespace Broiler.JavaScript.Core.Core;


public delegate JSValue JSClosureFunctionDelegate(ScriptInfo script, JSVariable[] closures, in Arguments a);
public delegate void ConsoleEvent(JSContext context, string type, in Arguments a);
public delegate void LogEventHandler(JSContext context, JSValue value);
public delegate void ErrorEventHandler(JSContext context, Exception error);

public class EvalEventArgs : EventArgs
{
    public JSContext Context { get; set; }

    public string Script { get; set; }

    public string Location { get; set; }
}

public partial class JSContext : JSObject, IJSContext, IDisposable
{

    private static long contextId = 1;

    public long ID { get; set; } = Interlocked.Increment(ref contextId);

    [ThreadStatic]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static JSContext Current;

    /// <summary>
    /// Gets or sets the debugger attached to this context.
    /// When non-null the runtime notifies the debugger of parsed scripts
    /// and exceptions via the <see cref="IDebugger"/> contract.
    /// </summary>
    public IDebugger Debugger;

    /// <summary>
    /// Gets or sets the built-in object registry used to populate new contexts.
    /// When set before constructing a <see cref="JSContext"/>, the custom
    /// registry will be used instead of the default source-generated one.
    /// Defaults to <see cref="DefaultBuiltInRegistry.Instance"/>.
    /// </summary>
    public static IBuiltInRegistry BuiltInRegistry { get; set; } = DefaultBuiltInRegistry.Instance;

    /// <summary>
    /// Gets or sets the CLR interop provider used to marshal between .NET
    /// objects and JavaScript values.  Custom implementations can override
    /// the default marshalling behaviour.
    /// Defaults to <see cref="FallbackClrInterop.Instance"/>.
    /// </summary>
    public static IClrInterop ClrInterop { get; set; } = FallbackClrInterop.Instance;

    /// <summary>
    /// Factory delegate that provides the default CLR module object.
    /// Set by the Clr assembly during initialization.  Consumed by the
    /// Modules assembly (<c>JSModuleContext</c>) to register the CLR module.
    /// </summary>
    public static Func<JSObject> ClrModuleProvider { get; set; }

    /// <summary>
    /// Available only when Enable Clr Integration is true in JSModuleContext
    /// </summary>
    public ClrMemberNamingConvention ClrMemberNamingConvention { get; set; } = ClrMemberNamingConvention.CamelCase;

    public static JSContext CurrentContext
    {
        get => Current;
        set
        {
            _current.Value = value;
            Current = value;
        }
    }

    private static readonly AsyncLocal<JSContext> _current = new((e) => { Current = e.CurrentValue ?? e.PreviousValue; });

    private TaskCompletionSource<int> _waitTask;
    public Task WaitTask => _waitTask?.Task;

    public CallStackItem Top;

    public static JSFunction NewTarget => Current.Top.NewTarget;

    public static JSObject NewTargetPrototype => Current.Top?.NewTarget?.prototype;

    internal JSFunction CurrentNewTarget;


    public event EventHandler<EvalEventArgs> EvalEvent;

    internal void DispatchEvalEvent(ref string script, ref string location)
    {
        var ee = EvalEvent;

        if (ee == null)
            return;

        var e = new EvalEventArgs { Context = this, Script = script, Location = location };
        EvalEvent.Invoke(this, e);
        script = e.Script;
        location = e.Location;
    }

    public void Dispose() => _current.Value = null;

    public readonly JSObject FunctionPrototype;
    public new readonly JSObject ObjectPrototype;
    public readonly JSFunction Object;
    public event LogEventHandler Log;
    public event ErrorEventHandler Error;
    public event ConsoleEvent ConsoleEvent;

    SAUint32Map<JSVariable> globalVars = new();

    internal JSValue Register(JSVariable variable)
    {
        var v = variable.Value;
        var oldV = this[variable.Name];

        if (oldV != v)
        {
            // avoid IsReadOnly error
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

    public JSContext(SynchronizationContext synchronizationContext = null)
    {
        this.synchronizationContext = synchronizationContext ?? SynchronizationContext.Current;

        Current = this;
        _current.Value = this;

        ref var ownProperties = ref GetOwnProperties();

        this[Names.Symbol] = JSSymbol.CreateClass(this, false);
        var func = JSFunction.CreateClass(this, false);
        this[Names.Function] = func;
        FunctionPrototype = func.prototype;
        Object = CreateClass(this, false);
        this[Names.Object] = Object;
        ObjectPrototype = Object.prototype;
        ObjectPrototype.BasePrototypeObject = null;

        func.BasePrototypeObject = Object;
        FunctionPrototype.BasePrototypeObject = ObjectPrototype;

        BuiltInRegistry.Register(this);

        this[KeyStrings.debug] = new JSFunction(Debug);

    }

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


    static readonly ConcurrentUInt32Map<JSFunction> cache = ConcurrentUInt32Map<JSFunction>.Create();
    internal readonly SynchronizationContext synchronizationContext;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSException NewTypeError(string message, [CallerMemberName] string function = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int line = 0) =>
        new JSTypeError(new Arguments(JSUndefined.Value, new JSString(message)), function: function, filePath: filePath, line: line).Exception;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSException NewSyntaxError(string message, [CallerMemberName] string function = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int line = 0) =>
        new JSSyntaxError(new Arguments(JSUndefined.Value, new JSString(message)), function: function, filePath: filePath, line: line).Exception;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSException NewURIError(string message, [CallerMemberName] string function = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int line = 0) =>
        new JSURIError(new Arguments(JSUndefined.Value, new JSString(message)), function: function, filePath: filePath, line: line).Exception;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSException NewRangeError(string message, [CallerMemberName] string function = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int line = 0) =>
        new JSRangeError(new Arguments(JSUndefined.Value, new JSString(message)), function: function, filePath: filePath, line: line).Exception;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSException NewError(string message, [CallerMemberName] string function = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int line = 0) =>
        new JSError(new Arguments(JSUndefined.Value, new JSString(message)), function: function, filePath: filePath, line: line).Exception;

    partial void OnError(Exception ex);

    internal void ReportError(Exception ex)
    {
        OnError(ex);
        Error?.Invoke(this, ex);
    }

    public void ReportLog(JSValue f) => Log?.Invoke(this, f);

    private static long nextTimeout = 1;
    private static long nextInterval = 1;

    internal long PostTimeout(int delay, JSFunction f, in Arguments a)
    {
        var ctx = synchronizationContext ?? throw NewTypeError($"Synchronization context must be present to set timeout");
        var key = Interlocked.Increment(ref nextTimeout);
        JSValue[] args = JSArguments.Empty;

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
    internal long SetInterval(int delay, JSFunction f, in Arguments a)
    {
        var ctx = synchronizationContext ?? throw NewTypeError($"Synchronization context must be present to set timeout");
        var key = Interlocked.Increment(ref nextInterval);
        JSValue[] args = JSArguments.Empty;

        if (a.Length > 2)
        {
            args = new JSValue[a.Length - 2];
            for (int i = 2; i < a.Length; i++)
                args[i - 2] = a.GetAt(i);
        }

        var timer = new Timer((_) =>
        {
            ctx.Post(f, (x) =>
            {
                try
                {
                    x.InvokeFunction(new Arguments(JSUndefined.Value, args));
                }
                catch (Exception ex)
                {
                    ReportError(ex);
                }
                ClearInterval(key);
            });
        }, f, delay, Timeout.Infinite);

        timers.AddOrUpdate(key, timer, (a1, a2) => a2);
        lock (this)
        {
            _waitTask = _waitTask ?? new TaskCompletionSource<int>();
        }
        return key;

    }

    public ICodeCache CodeCache { get; set; } = DictionaryCodeCache.Current;

    internal ConcurrentDictionary<long, JSPromise> PendingPromises = new();

    /// <summary>
    /// Quickly evaluates the code, does not wait for promises and timeouts/intervals.
    /// </summary>
    /// <param name="code"></param>
    /// <param name="codeFilePath"></param>
    /// <returns></returns>
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
    /// <param name="code"></param>
    /// <param name="codeFilePath"></param>
    /// <returns></returns>
    public async Task<JSValue> ExecuteAsync(string code, string codeFilePath = null)
    {
        var r = CoreScript.Evaluate(code, codeFilePath, codeCache: CodeCache);
        var wt = WaitTask;
        if (wt != null)
            await wt;

        if (r is JSPromise promise)
            return await promise.Task;

        if (r is not JSObject @object)
            return r;

        var then = @object[KeyStrings.then];
        if (!then.IsFunction)
            return r;

        promise = new JSPromise((resolve, reject) =>
        {
            var resolveF = new JSFunction((in Arguments a) =>
            {
                var a1 = a.Get1();
                resolve(a1);
                return a1;
            });

            var rejectF = new JSFunction((in Arguments a) =>
            {
                var a1 = a.Get1();
                reject(a1);
                return a1;
            });

            var a = new Arguments(@object, resolveF, rejectF);
            then.InvokeFunction(a);
        });

        return await promise.Task;
    }


    /// <summary>
    /// Evaluates the given code, waits for the promise and also 
    /// waits synchronously (by running and AsyncPump) for timeouts/intervals to finish
    /// </summary>
    /// <param name="code"></param>
    /// <param name="codeFilePath"></param>
    /// <returns></returns>
    public JSValue Execute(string code, string codeFilePath = null) => AsyncPump.Run(() => ExecuteAsync(code, codeFilePath));
}
