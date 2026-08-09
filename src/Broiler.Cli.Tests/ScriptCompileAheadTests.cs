using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.Runtime;

namespace Broiler.Cli.Tests;

/// <summary>
/// The exit gate for multithreading roadmap item #16: a document's serialized output is identical
/// whether its scripts were compiled inline by the eval loop or ahead of it on a thread budget.
/// </summary>
/// <remarks>
/// <para>
/// <b>Budget 1 is the oracle, and it is the unmodified path rather than a one-wide version of the
/// new one</b> — <see cref="ScriptCompileAhead.Start"/> returns <c>null</c> there and no worker is
/// created, so the comparison is against the code as it was, not against this code configured
/// narrowly.
/// </para>
/// <para>
/// <b>The documents are chosen for what makes a script's compile depend on something other than
/// its own source</b>, because that is the only way a compile performed on a worker — with no
/// ambient context, possibly while the previous script is still executing — could differ from one
/// performed inline:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Cross-script bindings.</b> Later scripts read <c>var</c>/<c>function</c>/<c>class</c>
/// declarations from earlier ones. Compilation must not resolve them early: the binding does not
/// exist when the worker runs.
/// </description></item>
/// <item><description>
/// <b>A syntax error among valid sources.</b> The failure path is the one the compile-ahead is
/// forbidden to own — it must be reported by the consuming <c>Eval</c>, in document order, with the
/// same net effect on the page (the bad script does nothing, the ones around it still run).
/// </description></item>
/// <item><description>
/// <b><c>eval()</c> inside a script.</b> The compiler reads direct-eval state from the ambient
/// context, and that state is not part of the cache key. A direct eval's <em>body</em> is never
/// handed to the compile-ahead; this asserts that a script <em>containing</em> one is still safe,
/// which is the case the restriction has to survive.
/// </description></item>
/// <item><description>
/// <b>Duplicate sources.</b> Two identical scripts collapse onto one cache entry, so one of them
/// consumes an entry a worker created and the other consumes one the first <c>Eval</c> did.
/// </description></item>
/// <item><description>
/// <b><c>document.write</c> and <c>document.currentScript</c>.</b> Both depend on <em>which</em>
/// script is running, which is the property a reordering would break first.
/// </description></item>
/// <item><description>
/// <b>Deferred scripts.</b> They are queued for compilation together with the regular ones but run
/// in a separate later pass, so their entries are the ones most likely to be stale if the key were
/// wrong.
/// </description></item>
/// <item><description>
/// <b>Generators, async functions and classes.</b> The three compilation shapes item #15 touched,
/// so the three whose codegen is most worth running off-thread at all.
/// </description></item>
/// </list>
/// <para>
/// <see cref="Compile_Ahead_Actually_Ran_For_Every_Document"/> is the guard that keeps every
/// equality assertion above from passing vacuously: a document that fell below
/// <see cref="ScriptCompileAhead.MinimumScriptsToOverlap"/> would compare "no worker" against "no
/// worker" and prove nothing.
/// </para>
/// </remarks>
[Collection("ScriptCompileAhead")]
public sealed class ScriptCompileAheadTests : IDisposable
{
    private readonly int _budget = ScriptCompileAhead.MaxDegreeOfParallelism;
    private readonly bool _diagnostics = ScriptCompileAhead.CollectDiagnostics;

    public void Dispose()
    {
        ScriptCompileAhead.MaxDegreeOfParallelism = _budget;
        ScriptCompileAhead.CollectDiagnostics = _diagnostics;
    }

    /// <summary>Budgets crossed against every document. 1 is the oracle; 8 oversubscribes 4 cores.</summary>
    public static TheoryData<string, int> DocumentsAndBudgets()
    {
        var data = new TheoryData<string, int>();
        foreach (var name in Documents.Keys)
        {
            foreach (var budget in new[] { 1, 2, 3, 4, 8 })
                data.Add(name, budget);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(DocumentsAndBudgets))]
    public void Output_Is_Identical_At_Every_Budget(string document, int budget)
    {
        var html = Documents[document];

        var expected = RunAt(1, html);
        var actual = RunAt(budget, html);

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// The guard. Without it every equality above could be comparing two runs that both did nothing
    /// — a document with one script, or a budget that never took effect.
    /// </summary>
    [Fact]
    public void Compile_Ahead_Actually_Ran_For_Every_Document()
    {
        foreach (var (name, html) in Documents)
        {
            ScriptCompileAhead.ResetDiagnostics();
            ScriptCompileAhead.CollectDiagnostics = true;
            try
            {
                RunAt(4, html);
            }
            finally
            {
                ScriptCompileAhead.CollectDiagnostics = false;
            }

            var diagnostics = ScriptCompileAhead.Diagnostics;
            Assert.True(
                diagnostics.DocumentsCompiledAhead >= 1,
                $"'{name}' never reached the compile-ahead: it has fewer than " +
                $"{ScriptCompileAhead.MinimumScriptsToOverlap} script sources, so its equality " +
                "assertions compare two unmodified runs and prove nothing.");
            Assert.True(
                diagnostics.ScriptsQueued >= ScriptCompileAhead.MinimumScriptsToOverlap,
                $"'{name}' queued {diagnostics.ScriptsQueued} sources.");
        }
    }

    /// <summary>
    /// The failure path, stated directly rather than only through the equality above: a source that
    /// cannot compile is not cached as a failure, so the consume site still raises it. Counted on
    /// the worker and observable, because "the compile-ahead silently succeeded on a broken script"
    /// and "the compile-ahead never ran" look the same from the output.
    /// </summary>
    [Fact]
    public void A_Source_That_Cannot_Compile_Is_Left_To_The_Consume_Site()
    {
        ScriptCompileAhead.ResetDiagnostics();
        ScriptCompileAhead.CollectDiagnostics = true;
        try
        {
            var output = RunAt(4, Documents["syntax-error"]);

            var diagnostics = ScriptCompileAhead.Diagnostics;
            Assert.True(diagnostics.ScriptsFailed >= 1, "the broken source compiled on a worker");
            Assert.True(diagnostics.ScriptsCompiled >= 1, "no source compiled on a worker");

            // The scripts either side of the broken one still ran, and the broken one still did
            // nothing — which is what the unmodified path does with it. Asserted on the serialized
            // attribute (double-quoted) rather than on the value alone: every script's source text
            // is serialized back into the document, so a bare substring would find the broken
            // script's own body and pass no matter what ran.
            Assert.Contains("data-a=\"before-ok\"", output);
            Assert.Contains("data-b=\"after-ok\"", output);
            Assert.DoesNotContain("data-c=\"never-runs\"", output);
        }
        finally
        {
            ScriptCompileAhead.CollectDiagnostics = false;
        }
    }

    /// <summary>
    /// A budget of 1 does not start a worker at all — asserted on the return value rather than on
    /// the process-wide counters, which another test class running a capture beside this one can
    /// legitimately move. This is what makes the oracle above the pre-change path rather than a
    /// narrow configuration of the new one.
    /// </summary>
    [Fact]
    public void A_Budget_Of_One_Starts_No_Worker()
    {
        using var context = new JSContext();

        ScriptCompileAhead.MaxDegreeOfParallelism = 1;
        Assert.Null(ScriptCompileAhead.Start(context, ["var a = 1;", "var b = 2;", "var c = 3;"]));

        ScriptCompileAhead.MaxDegreeOfParallelism = 4;
        using var started = ScriptCompileAhead.Start(context, ["var a = 1;", "var b = 2;"]);
        Assert.NotNull(started);
    }

    /// <summary>
    /// One source is one compile plus a handoff: the consuming <c>Eval</c> is the very next thing
    /// the host does with it, so there is nothing left to overlap it with.
    /// </summary>
    [Fact]
    public void A_Single_Source_Starts_No_Worker()
    {
        using var context = new JSContext();
        ScriptCompileAhead.MaxDegreeOfParallelism = 4;

        Assert.Null(ScriptCompileAhead.Start(context, ["var only = 1;"]));
        Assert.Null(ScriptCompileAhead.Start(context, []));
    }

    /// <summary>
    /// The claim that makes the whole item worth anything: a source the worker compiled is one the
    /// consuming <c>Eval</c> <em>finds</em>, rather than one it compiles again under a different
    /// key. Asserted on the cache's own hit/miss counters, because a wrong key is invisible in the
    /// output — it costs a second compile and nothing else, which is exactly item #17's lesson
    /// about a prefetch filed where nobody looks.
    /// </summary>
    [Fact]
    public void A_Compiled_Ahead_Source_Is_A_Cache_Hit_At_The_Consume_Site()
    {
        using var context = new JSContext();
        var cache = Assert.IsType<DictionaryCodeCache>(context.CodeCache);

        var sources = new[]
        {
            "var compileAheadA = 1 + 2 + 3;",
            "var compileAheadB = (function () { return compileAheadA * 2; })();",
            "var compileAheadC = [1, 2, 3].map(function (v) { return v + compileAheadB; }).join('-');",
        };

        ScriptCompileAhead.MaxDegreeOfParallelism = 4;
        using (var ahead = ScriptCompileAhead.Start(context, sources))
        {
            Assert.NotNull(ahead);
            ahead.Join();
        }

        var before = cache.Metrics;
        foreach (var source in sources)
            context.Eval(source);
        var after = cache.Metrics;

        // Every one of the three was already in the cache: three more hits, no more compilations.
        Assert.Equal(before.Hits + sources.Length, after.Hits);
        Assert.Equal(before.Compilations, after.Compilations);
    }

    private static string RunAt(int budget, string html)
    {
        ScriptCompileAhead.MaxDegreeOfParallelism = budget;
        return CaptureService.ExecuteScriptsWithDom(html, "file:///compile-ahead.html");
    }

    private static readonly Dictionary<string, string> Documents = new()
    {
        // Later scripts read declarations from earlier ones: a compile that resolved bindings
        // eagerly would fail on the worker, where none of them exist yet.
        ["cross-script-bindings"] = """
            <html><body><div id="out"></div>
            <script>
              var counter = 0;
              function bump(by) { counter += by; return counter; }
            </script>
            <script>
              class Doubler { constructor(n) { this.n = n; } get value() { return this.n * 2; } }
              bump(3);
            </script>
            <script>
              bump(new Doubler(4).value);
              document.getElementById('out').textContent = 'counter=' + counter;
            </script>
            </body></html>
            """,

        // document.write and currentScript both depend on which script is running.
        ["ordered-writes"] = """
            <html><body>
            <script>document.write('<p>one</p>');</script>
            <script>document.write('<p>two</p>');</script>
            <script>document.write('<p>three</p>');</script>
            <script>
              var marker = document.createElement('span');
              marker.id = 'current';
              marker.textContent = document.currentScript ? 'has-current-script' : 'no-current-script';
              document.body.appendChild(marker);
            </script>
            </body></html>
            """,

        // The compile-ahead must not own the failure: the broken source is reported by the eval
        // loop, and the scripts around it are unaffected.
        ["syntax-error"] = """
            <html><body><div id="out"></div>
            <script>document.getElementById('out').setAttribute('data-a', 'before-ok');</script>
            <script>
              document.getElementById('out').setAttribute('data-c', 'never-runs');
              function ( { this is not javascript ]]] ;
            </script>
            <script>document.getElementById('out').setAttribute('data-b', 'after-ok');</script>
            </body></html>
            """,

        // A script containing a direct eval. Its body is never handed to the compile-ahead; the
        // enclosing script is, and the ambient direct-eval state it reads must still be "none".
        ["direct-eval"] = """
            <html><body><div id="out"></div>
            <script>
              var seed = 6;
              var viaEval = eval('seed * 7');
            </script>
            <script>
              function evalInScope() { var local = 2; return eval('local + seed'); }
              document.getElementById('out').textContent = viaEval + ':' + evalInScope();
            </script>
            </body></html>
            """,

        // Two identical sources collapse onto one cache entry, so one Eval consumes an entry a
        // worker made and the other consumes one the first Eval made.
        ["duplicate-sources"] = """
            <html><body><div id="out"></div>
            <script>window.hits = (window.hits || 0) + 1;</script>
            <script>window.hits = (window.hits || 0) + 1;</script>
            <script>window.hits = (window.hits || 0) + 1;</script>
            <script>document.getElementById('out').textContent = 'hits=' + window.hits;</script>
            </body></html>
            """,

        // Deferred sources are queued with the regular ones but consumed by a later pass.
        ["deferred"] = """
            <html><body><div id="out"></div>
            <script>window.order = [];</script>
            <script defer>window.order.push('deferred-a');</script>
            <script>window.order.push('regular');</script>
            <script defer>window.order.push('deferred-b');</script>
            <script defer>
              document.getElementById('out').textContent = window.order.join(',');
            </script>
            </body></html>
            """,

        // The three compilation shapes item #15 touched, and the ones whose codegen is heaviest.
        ["generators-async-classes"] = """
            <html><body><div id="out"></div>
            <script>
              function* range(n) { for (var i = 0; i < n; i++) yield i; }
              window.sum = 0;
              for (const v of range(5)) window.sum += v;
            </script>
            <script>
              async function later(v) { return v * 2; }
              window.pending = later(window.sum);
            </script>
            <script>
              class Base { label() { return 'base'; } }
              class Derived extends Base { label() { return super.label() + '+derived'; } }
              window.pending.then(function (doubled) {
                document.getElementById('out').textContent =
                  new Derived().label() + ':' + window.sum + ':' + doubled;
              });
            </script>
            </body></html>
            """,

        // Strict mode is a compilation-affecting directive, and it is per source: the worker must
        // not carry one script's strictness into another's compile.
        ["mixed-strictness"] = """
            <html><body><div id="out"></div>
            <script>"use strict"; var strictResult = (function () { return this; })() === undefined;</script>
            <script>var sloppyResult = (function () { return this; })() !== undefined;</script>
            <script>
              document.getElementById('out').textContent = strictResult + ':' + sloppyResult;
            </script>
            </body></html>
            """,
    };
}
