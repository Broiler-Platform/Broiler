using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.Runtime;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// The executable form of the gate the multithreading roadmap puts in front of item #18 (Web
/// Workers): can two <see cref="JSContext"/> instances run JavaScript on two threads at the same
/// time without observing each other?
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists before any worker code does.</b> The roadmap's <c>Broiler.JS</c> section says
/// workers are "a feature, scoped and scheduled as one", and gates them on the P0-c static-state
/// audit: "anything genuinely global (interned strings, shapes/hidden classes, the code cache) needs
/// an explicit thread-safety contract before a second context runs concurrently." Item #18's master
/// table row asserts the other half — "per-context state exists … so isolation is feasible". Both
/// are claims about code, and this phase has repeatedly found that the claim in a row is not the
/// operative fact. An audit by reading cannot settle a data race; running the thing can, and a
/// standing test is worth more than a one-off reading because it keeps answering.
/// </para>
/// <para>
/// <b>What it deliberately does not do.</b> It does not run <em>one</em> context on two threads —
/// that is not what a worker is and item #15 made the event loop single-threaded on purpose. Each
/// thread owns a context for its whole life, which is exactly the shape item #18 proposes
/// (one <c>JSContext</c> per worker thread, messages between them), so a failure here is a failure
/// of that design and not of a misuse of the engine.
/// </para>
/// <para>
/// <b>Each case names the shared state it is aimed at</b>, because "run some JS on threads" passing
/// proves very little. The engine's genuinely process-wide state is the interned key strings
/// (property names), the shapes/hidden classes objects share, the built-in registry's static
/// constructors, and the code cache — so the cases below are built to collide on each in turn.
/// </para>
/// <para>
/// <b>The code cache case is newly load-bearing.</b> Phase 4 §8/§9 made the bridge and the WPT
/// runner evaluate their constant sources through the process-shared
/// <see cref="DictionaryCodeCache.Current"/>, which is safe today because those hosts render on one
/// thread per process. Under item #18 a second thread would reach the same cache concurrently, so
/// that change is now part of what this gate has to cover — it is the newest shared-state user in
/// the tree, not the oldest.
/// </para>
/// </remarks>
public sealed class JSContextIsolationTests
{
    private const int Threads = 4;
    private const int Iterations = 25;

    /// <summary>
    /// Runs <paramref name="body"/> on <see cref="Threads"/> real threads, each with its own
    /// <see cref="JSContext"/>, and rethrows the first failure with its thread index attached.
    /// </summary>
    /// <remarks>
    /// Real <see cref="Thread"/>s rather than <c>Task.Run</c>: the engine keeps its current context
    /// in <c>[ThreadStatic]</c> state, so a pool thread that is handed a different work item
    /// mid-test would be a different experiment from the one item #18 needs answered.
    /// </remarks>
    private static void OnEachThread(Action<int, JSContext> body)
    {
        var failures = new ConcurrentBag<Exception>();
        var ready = new Barrier(Threads);
        var threads = new Thread[Threads];

        // Overlap evidence. A concurrency test whose threads happen to run one after another passes
        // for the wrong reason and is worth nothing; `peak` records the largest number of threads
        // that were inside the body at the same moment, and the test asserts it was more than one.
        var inFlight = 0;
        var peak = 0;

        for (var t = 0; t < Threads; t++)
        {
            var index = t;
            threads[t] = new Thread(() =>
            {
                try
                {
                    using var context = new JSContext();
                    // Start the measured work together, so the threads actually overlap rather than
                    // finishing one after another and proving nothing about concurrency.
                    ready.SignalAndWait();

                    var now = Interlocked.Increment(ref inFlight);
                    int seen;
                    while (now > (seen = Volatile.Read(ref peak)))
                        Interlocked.CompareExchange(ref peak, now, seen);
                    try
                    {
                        body(index, context);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref inFlight);
                    }
                }
                catch (Exception ex)
                {
                    failures.Add(new InvalidOperationException($"thread {index}: {ex.Message}", ex));
                    // A thrower must not strand the others on the barrier.
                    try { ready.RemoveParticipant(); } catch { /* already past it */ }
                }
            });
            threads[t].IsBackground = true;
        }

        foreach (var thread in threads)
            thread.Start();
        foreach (var thread in threads)
            Assert.True(thread.Join(TimeSpan.FromMinutes(2)), "a worker thread did not finish");

        if (!failures.IsEmpty)
            throw failures.First();

        Assert.True(
            Volatile.Read(ref peak) > 1,
            $"the threads never overlapped (peak concurrency {peak}), so this run proves nothing " +
            "about isolation — the case needs more work per thread, not a passing grade");
    }

    /// <summary>
    /// Globals do not leak between contexts, which is the property the whole feature rests on: a
    /// worker that could see the page's globals is not a worker.
    /// </summary>
    [Fact]
    public void Globals_do_not_leak_between_concurrent_contexts()
    {
        OnEachThread((index, context) =>
        {
            for (var i = 0; i < Iterations; i++)
            {
                context.Eval(FormattableString.Invariant($"var mine = {index * 1000 + i};"));
                var seen = context.Eval("mine").DoubleValue;
                Assert.Equal(index * 1000 + i, seen);
                Assert.True(
                    context.Eval("typeof theirs === 'undefined'").BooleanValue,
                    "a global defined on another thread's context was visible");
            }

            context.Eval(FormattableString.Invariant($"var theirs_{index} = 1;"));
        });
    }

    /// <summary>
    /// Aimed at the interned key strings and the shape/hidden-class tables: every thread builds
    /// objects with the <em>same</em> property names, which is what forces them onto the same
    /// interned keys and the same shape transitions.
    /// </summary>
    [Fact]
    public void Shared_property_names_and_shapes_stay_correct_across_threads()
    {
        OnEachThread((index, context) =>
        {
            for (var i = 0; i < Iterations; i++)
            {
                var value = index * 100 + i;
                var result = context.Eval(FormattableString.Invariant($@"
                    (function () {{
                        var o = {{ alpha: {value}, beta: 'b{index}', gamma: null }};
                        o.delta = o.alpha * 2;
                        delete o.gamma;
                        var keys = Object.keys(o).join(',');
                        return keys + '|' + o.alpha + '|' + o.beta + '|' + o.delta;
                    }})()"));

                Assert.Equal(
                    FormattableString.Invariant($"alpha,beta,delta|{value}|b{index}|{value * 2}"),
                    result.ToString());
            }
        });
    }

    /// <summary>
    /// Aimed at the compiler and the per-context code cache: every thread compiles the
    /// <em>identical</em> source, so any cross-context reuse of compiled code is exercised at
    /// maximum contention.
    /// </summary>
    [Fact]
    public void Identical_sources_compile_and_run_correctly_on_all_threads()
    {
        const string Source = @"
            (function () {
                function fib(n) { return n < 2 ? n : fib(n - 1) + fib(n - 2); }
                var acc = [];
                for (var i = 0; i < 12; i++) acc.push(fib(i));
                return acc.join(',');
            })()";

        OnEachThread((_, context) =>
        {
            for (var i = 0; i < Iterations; i++)
                Assert.Equal("0,1,1,2,3,5,8,13,21,34,55,89", context.Eval(Source).ToString());
        });
    }

    /// <summary>
    /// The same, through <see cref="DictionaryCodeCache.Current"/> — the process-shared cache Phase
    /// 4 §8/§9 put on the bridge's and the runner's constant sources. Today only one thread per
    /// process reaches it; item #18 would put several there at once, so this is the case that says
    /// whether that change is worker-ready or is a worker blocker of its own making.
    /// </summary>
    [Fact]
    public void Process_shared_code_cache_is_safe_under_concurrent_contexts()
    {
        const string Source = @"
            (function () {
                var out = '';
                for (var i = 0; i < 8; i++) out += (i * i) + ':';
                return out;
            })()";

        OnEachThread((_, context) =>
        {
            var previous = context.CodeCache;
            context.CodeCache = DictionaryCodeCache.Current;
            try
            {
                for (var i = 0; i < Iterations; i++)
                    Assert.Equal("0:1:4:9:16:25:36:49:", context.Eval(Source).ToString());
            }
            finally
            {
                context.CodeCache = previous;
            }
        });
    }

    /// <summary>
    /// Aimed at the built-in registry's static initialization: several threads reach the built-ins
    /// for the first time at once, which is the window in which a static constructor is still
    /// running.
    /// </summary>
    [Fact]
    public void Builtins_behave_identically_on_every_thread()
    {
        OnEachThread((_, context) =>
        {
            for (var i = 0; i < Iterations; i++)
            {
                Assert.Equal("[1,2,3]", context.Eval("JSON.stringify([1,2,3])").ToString());
                Assert.Equal("3", context.Eval("String(Math.max(1,2,3))").ToString());
                Assert.Equal("abc", context.Eval("['a','b','c'].join('')").ToString());
                Assert.Equal("2", context.Eval("String(Object.keys({x:1,y:2}).length)").ToString());
                Assert.Equal(
                    "caught:boom",
                    context.Eval("(function(){ try { throw new Error('boom'); } catch (e) { return 'caught:' + e.message; } })()").ToString());
            }
        });
    }
}
