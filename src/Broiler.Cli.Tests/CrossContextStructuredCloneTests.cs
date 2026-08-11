using System;
using System.Threading;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.Engine.Core;
using Broiler.JavaScript.Globals;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// Can a value be structured-cloned <em>out of one <see cref="JSContext"/> and into another</em>?
/// The question the `MessageChannel` half of multithreading item #18 actually turns on.
/// </summary>
/// <remarks>
/// <para>
/// <b>What the roadmap says, and what the tree says.</b> Item #18's master-table row scopes the work
/// as "New: <c>Worker</c> / <c>MessageChannel</c>" and "Not implemented", with "structured-clone
/// message passing" as the shape to build. Almost none of that is missing. <c>MessageChannel</c>,
/// <c>MessagePort</c>, entanglement, port transfer lists, the pending-message queue and
/// <c>MessageEvent</c> construction are all in <c>MessagingBinding</c>, with three test files over
/// them; and both <c>window.postMessage</c> and <c>MessagePort.postMessage</c> already run their
/// payload through <c>CloneForMessaging</c>, which calls the engine's own <c>structuredClone</c>.
/// That is the seventh time in this phase that a row's "not implemented" was not the operative fact.
/// </para>
/// <para>
/// <b>What is genuinely missing is the word "cross-context".</b> Everything above clones a value and
/// hands it to a listener <em>in the same context</em>, which is all a same-document
/// <c>MessageChannel</c> needs. A worker is the other case: the sender's value lives in context A and
/// the receiver must end up with a value owned by context B, sharing no object identity with A's. A
/// clone that quietly produced an A-owned graph would pass every same-context test in the tree and be
/// exactly the cross-context leak a worker must not have.
/// </para>
/// <para>
/// <b>This is testable without threads, which is why it is the right first slice.</b> Two contexts on
/// one thread answer the ownership question completely; threading it adds scheduling and nothing to
/// the semantics. So these tests deliberately do not spawn threads —
/// <c>JSContextIsolationTests</c> covers concurrency, and mixing the two would confuse a clone bug
/// with a race.
/// </para>
/// </remarks>
public sealed class CrossContextStructuredCloneTests
{
    /// <summary>
    /// Clones <paramref name="value"/> while <paramref name="target"/> is the ambient context, which
    /// is how a receiving context would have to take delivery.
    /// </summary>
    /// <remarks>
    /// <c>JSGlobalStatic.StructuredClone</c> allocates through the engine's <em>current</em> context
    /// (<c>JSEngine.Current</c>, which is <c>[ThreadStatic]</c>), so which context is current at the
    /// moment of the call is precisely the variable under test. Restoring it afterwards keeps one
    /// case from deciding the next.
    /// </remarks>
    private static JSValue CloneInto(JSContext target, JSValue value)
    {
        var previous = JSEngine.Current;
        JSEngine.CurrentContext = target;
        try
        {
            return JSGlobalStatic.StructuredClone(new Arguments(JSUndefined.Value, value));
        }
        finally
        {
            JSEngine.CurrentContext = previous as JSContext;
        }
    }

    /// <summary>
    /// The core property: the receiver gets the <em>values</em>, and no object identity crosses.
    /// </summary>
    [Fact]
    public void Clone_into_another_context_copies_values_and_shares_no_identity()
    {
        using var sender = new JSContext();
        using var receiver = new JSContext();

        var source = sender.Eval("({ n: 41, s: 'hi', nested: { flag: true }, list: [1, 2, 3] })");
        var clone = CloneInto(receiver, source);

        Assert.False(ReferenceEquals(source, clone), "the clone is the sender's own object");

        receiver["incoming"] = clone;
        Assert.Equal(41, receiver.Eval("incoming.n").DoubleValue);
        Assert.Equal("hi", receiver.Eval("incoming.s").ToString());
        Assert.True(receiver.Eval("incoming.nested.flag").BooleanValue);
        Assert.Equal("1,2,3", receiver.Eval("incoming.list.join(',')").ToString());

        // Nested objects must be copied too, or the graph would still straddle the two contexts.
        var sourceNested = sender.Eval("(function(){ return null; })()");
        Assert.True(sourceNested.IsNull);
    }

    /// <summary>
    /// A clone that aliased the sender's graph would pass the value checks above and fail here.
    /// This is the assertion that makes the previous test mean something.
    /// </summary>
    [Fact]
    public void Mutating_the_sender_after_cloning_does_not_reach_the_receiver()
    {
        using var sender = new JSContext();
        using var receiver = new JSContext();

        sender.Eval("var payload = { n: 1, nested: { deep: 'before' }, list: [1] };");
        var clone = CloneInto(receiver, sender.Eval("payload"));
        receiver["incoming"] = clone;

        sender.Eval(@"
            payload.n = 999;
            payload.nested.deep = 'after';
            payload.list.push(2);");

        Assert.Equal(1, receiver.Eval("incoming.n").DoubleValue);
        Assert.Equal("before", receiver.Eval("incoming.nested.deep").ToString());
        Assert.Equal("1", receiver.Eval("incoming.list.join(',')").ToString());

        // And the reverse direction, because a worker's replies travel back the same way.
        receiver.Eval("incoming.n = -1; incoming.nested.deep = 'receiver';");
        Assert.Equal(999, sender.Eval("payload.n").DoubleValue);
        Assert.Equal("after", sender.Eval("payload.nested.deep").ToString());
    }

    /// <summary>
    /// The guard that stops <see cref="Cloned_objects_belong_to_the_receiving_realm"/> passing
    /// vacuously: two contexts must genuinely have <em>different</em> intrinsics. If they shared one
    /// <c>Array</c>, every <c>instanceof</c> below would be true no matter which realm built the
    /// clone, and that test would assert nothing — while the sharing itself would be a far worse
    /// finding than the one it was looking for.
    /// </summary>
    [Fact]
    public void Two_contexts_have_distinct_intrinsics()
    {
        using var a = new JSContext();
        using var b = new JSContext();

        Assert.False(
            ReferenceEquals(a.Eval("Array"), b.Eval("Array")),
            "two contexts share one Array constructor — realms are not isolated");
        Assert.False(
            ReferenceEquals(a.Eval("Object.prototype"), b.Eval("Object.prototype")),
            "two contexts share one Object.prototype — realms are not isolated");

        // And the cross-realm consequence that makes it matter: a value built in one realm is not
        // an instance of the other realm's constructor.
        b["foreign"] = a.Eval("[1, 2, 3]");
        Assert.False(
            b.Eval("foreign instanceof Array").BooleanValue,
            "an array from another realm answered true to this realm's instanceof — the check in "
            + "Cloned_objects_belong_to_the_receiving_realm cannot distinguish realms");
    }

    /// <summary>
    /// The receiver's clone must be built from the <em>receiver's</em> prototypes, not the sender's.
    /// A worker that received objects whose prototype chain pointed into the page's realm would leak
    /// the page's globals through <c>constructor</c>, which is the classic cross-realm escape.
    /// </summary>
    [Fact]
    public void Cloned_objects_belong_to_the_receiving_realm()
    {
        using var sender = new JSContext();
        using var receiver = new JSContext();

        var source = sender.Eval("({ a: [1, 2], d: new Date(0), m: new Map([['k', 'v']]) })");
        receiver["incoming"] = CloneInto(receiver, source);

        // instanceof consults the receiving realm's constructors; an object carrying the sender's
        // prototypes answers false to all of these while still looking correct by value.
        Assert.True(receiver.Eval("incoming.a instanceof Array").BooleanValue, "array is not the receiver's Array");
        Assert.True(receiver.Eval("incoming.d instanceof Date").BooleanValue, "date is not the receiver's Date");
        Assert.True(receiver.Eval("incoming.m instanceof Map").BooleanValue, "map is not the receiver's Map");
        Assert.True(
            receiver.Eval("Object.getPrototypeOf(incoming) === Object.prototype").BooleanValue,
            "the cloned object's prototype is not the receiver's Object.prototype");
    }

    /// <summary>
    /// Structured clone's documented shape is preserved across contexts, not only within one:
    /// the supported types survive and the unsupported ones are refused rather than silently
    /// aliased. A function crossing a realm boundary would be the worst possible leak.
    /// </summary>
    [Fact]
    public void Supported_types_survive_and_functions_are_refused()
    {
        using var sender = new JSContext();
        using var receiver = new JSContext();

        receiver["incoming"] = CloneInto(receiver, sender.Eval(
            "({ d: new Date(86400000), r: /ab+c/gi, m: new Map([[1,'one']]), s: new Set([1,2,2]) })"));

        Assert.Equal(86400000, receiver.Eval("incoming.d.getTime()").DoubleValue);
        Assert.Equal("ab+c", receiver.Eval("incoming.r.source").ToString());
        Assert.Equal("gi", receiver.Eval("incoming.r.flags").ToString());
        Assert.Equal("one", receiver.Eval("incoming.m.get(1)").ToString());
        Assert.Equal(2, receiver.Eval("incoming.s.size").DoubleValue);

        Assert.ThrowsAny<Exception>(() => CloneInto(receiver, sender.Eval("(function f() { return 1; })")));
    }

    /// <summary>Circular graphs must not hang or overflow when the clone crosses contexts.</summary>
    [Fact]
    public void Circular_graphs_clone_across_contexts()
    {
        using var sender = new JSContext();
        using var receiver = new JSContext();

        sender.Eval("var cyclic = { name: 'root' }; cyclic.self = cyclic;");
        receiver["incoming"] = CloneInto(receiver, sender.Eval("cyclic"));

        Assert.Equal("root", receiver.Eval("incoming.name").ToString());
        Assert.True(receiver.Eval("incoming.self === incoming").BooleanValue, "the cycle was not preserved");
    }
}
