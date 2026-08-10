using System;
using System.Collections.Concurrent;
using System.Threading;
using Broiler.HtmlBridge.Logging;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.Globals;
using Broiler.JavaScript.Runtime;

using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Storage;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// One worker: a thread that owns a <see cref="JSContext"/>, runs the worker script in it, and then
/// pumps messages until it is closed or terminated.
/// </summary>
/// <remarks>
/// <para>
/// <b>One thread, one context, for the thread's whole life.</b> That is item #15's rule kept rather
/// than bent: the context is created on the worker thread, every script and every handler runs on
/// that thread, and it is disposed there. Nothing outside ever evaluates in it. The page and the
/// worker meet only at two concurrent queues.
/// </para>
/// <para>
/// <b>Blocking take, not a spin.</b> The pump waits on a <see cref="BlockingCollection{T}"/>, so an
/// idle worker costs nothing; termination completes the collection, which wakes the pump and ends it.
/// </para>
/// </remarks>
internal sealed class JSWorker
{
    private readonly string _name;
    private readonly string _source;
    private readonly IWorkerHost _host;
    private readonly BlockingCollection<JSValue> _inbox = new(new ConcurrentQueue<JSValue>());
    private readonly CancellationTokenSource _cancel = new();
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _started = new();

    private JSObject? _handle;
    private WorkerBinding? _owner;
    private volatile bool _closed;

    public JSWorker(string name, string source, IWorkerHost host)
    {
        _name = name;
        _source = source;
        _host = host;
        _thread = new Thread(Pump) { IsBackground = true, Name = $"broiler-worker:{name}" };
    }

    /// <summary>
    /// Binds the page-side handle and starts the thread. Separate from the constructor because the
    /// handle does not exist until the binding has built it, and the thread must not deliver a
    /// message before there is something to deliver it to.
    /// </summary>
    public void Attach(JSObject handle, WorkerBinding owner)
    {
        _handle = handle;
        _owner = owner;
        _thread.Start();
    }

    /// <summary>Queues an already-detached clone for the worker. Called on the page thread.</summary>
    public void Post(JSValue detached)
    {
        if (_closed || _cancel.IsCancellationRequested)
            return;

        try
        {
            _inbox.Add(detached);
        }
        catch (InvalidOperationException)
        {
            // The inbox was completed by a concurrent Terminate; the message is simply not delivered,
            // which is what terminate() means.
        }
    }

    public void Terminate()
    {
        if (_cancel.IsCancellationRequested)
            return;

        _cancel.Cancel();
        try { _inbox.CompleteAdding(); } catch (ObjectDisposedException) { }

        if (_thread.IsAlive && !_thread.Join(TimeSpan.FromSeconds(5)))
        {
            // A worker stuck in an infinite loop cannot be interrupted safely; say so rather than
            // block the page's teardown forever. The thread is a background thread, so it does not
            // keep the process alive.
            RenderLogger.LogWarning(LogCategory.JavaScript, "JSWorker.Terminate",
                $"Worker '{_name}' did not stop within 5s; abandoning the thread.");
        }
    }

    private void Pump()
    {
        JSContext? context = null;
        try
        {
            context = new JSContext();
            InstallWorkerGlobals(context);

            try
            {
                context.Eval(_source);
            }
            catch (Exception ex)
            {
                RenderLogger.LogError(LogCategory.JavaScript, "JSWorker.Pump",
                    $"Worker '{_name}' script threw: {ex.Message}", ex);
                QueueError($"Worker script error: {ex.Message}");
                return;
            }

            _started.Set();

            foreach (var detached in _inbox.GetConsumingEnumerable(_cancel.Token))
            {
                if (_closed)
                    break;

                DispatchToWorker(context, detached);
            }
        }
        catch (OperationCanceledException)
        {
            // terminate(): the expected way out.
        }
        catch (Exception ex)
        {
            RenderLogger.LogError(LogCategory.JavaScript, "JSWorker.Pump",
                $"Worker '{_name}' failed: {ex.Message}", ex);
        }
        finally
        {
            _started.Set();
            context?.Dispose();
        }
    }

    /// <summary>
    /// Materializes the page's detached clone into the worker's realm and calls its handler. Runs on
    /// the worker thread, with the worker's context current, which is what makes the resulting
    /// objects the worker's own.
    /// </summary>
    private void DispatchToWorker(JSContext context, JSValue detached)
    {
        try
        {
            var data = JSGlobalStatic.StructuredClone(new Arguments(JSUndefined.Value, detached));

            var evt = new JSObject();
            evt.FastAddValue((KeyString)"type", new JSString("message"), JSPropertyAttributes.EnumerableConfigurableValue);
            evt.FastAddValue((KeyString)"data", data, JSPropertyAttributes.EnumerableConfigurableValue);

            if (context["onmessage"] is JSFunction handler)
                handler.InvokeFunction(new Arguments(JSUndefined.Value, evt));

            if (context["__broilerWorkerListeners"] is JSArray listeners)
            {
                foreach (var (_, listener) in listeners.GetArrayElements(withHoles: false))
                {
                    if (listener is JSFunction fn)
                        fn.InvokeFunction(new Arguments(JSUndefined.Value, evt));
                }
            }
        }
        catch (Exception ex)
        {
            RenderLogger.LogError(LogCategory.JavaScript, "JSWorker.DispatchToWorker",
                $"Worker '{_name}' message handler threw: {ex.Message}", ex);
            QueueError($"Worker message handler error: {ex.Message}");
        }
    }

    /// <summary>
    /// The worker global. Small on purpose — see <see cref="WorkerBinding"/>'s remarks for what is
    /// deliberately absent rather than half-built.
    /// </summary>
    private void InstallWorkerGlobals(JSContext context)
    {
        context["self"] = context.Eval("this");
        context["onmessage"] = JSNull.Value;
        context["__broilerWorkerListeners"] = new JSArray();

        context["postMessage"] = new JSFunction((in a) =>
        {
            var payload = a.Length > 0 ? a[0] : JSUndefined.Value;

            // Cloned here, on the worker thread in the worker's realm, into a graph no script holds.
            // The page clones it again on its own thread; see WorkerBinding's remarks.
            JSValue detached;
            try
            {
                detached = JSGlobalStatic.StructuredClone(new Arguments(JSUndefined.Value, payload));
            }
            catch (JSException ex)
            {
                RenderLogger.LogWarning(LogCategory.JavaScript, "JSWorker.postMessage",
                    $"Worker '{_name}' posted a value that could not be cloned: {ex.Message}", ex);
                return JSUndefined.Value;
            }

            var handle = _handle;
            var owner = _owner;
            if (handle is null || owner is null || _cancel.IsCancellationRequested)
                return JSUndefined.Value;

            // Onto the page's event loop, not into it: the queue is concurrent, and the page's own
            // drain is what will run this.
            _host.QueueFrameAction(() => owner.DeliverToPage(handle, detached));
            return JSUndefined.Value;
        }, "postMessage", 1);

        context["addEventListener"] = new JSFunction((in a) =>
        {
            if (a.Length >= 2 && a[1] is JSFunction fn &&
                string.Equals(a[0]?.ToString(), "message", StringComparison.Ordinal) &&
                context["__broilerWorkerListeners"] is JSArray listeners)
            {
                listeners.AddArrayItem(fn);
            }

            return JSUndefined.Value;
        }, "addEventListener", 2);

        context["close"] = new JSFunction((in _) =>
        {
            _closed = true;
            try { _inbox.CompleteAdding(); } catch (ObjectDisposedException) { }
            return JSUndefined.Value;
        }, "close", 0);

        // A worker without console is a worker nobody can debug; routed to the same logger the rest
        // of the bridge uses rather than to the page's console object, which belongs to another realm.
        var console = new JSObject();
        foreach (var level in new[] { "log", "info", "warn", "error", "debug" })
        {
            var captured = level;
            console.FastAddValue(
                (KeyString)captured,
                new JSFunction((in a) =>
                {
                    var text = a.Length > 0 ? a[0]?.ToString() ?? string.Empty : string.Empty;
                    RenderLogger.LogDebug(LogCategory.JavaScript, $"worker:{_name}", $"[{captured}] {text}");
                    return JSUndefined.Value;
                }, captured, 1),
                JSPropertyAttributes.EnumerableConfigurableValue);
        }

        context["console"] = console;
    }

    private void QueueError(string message)
    {
        var handle = _handle;
        var owner = _owner;
        if (handle is null || owner is null)
            return;

        _host.QueueFrameAction(() => owner.FireErrorEvent(handle, message));
    }
}
