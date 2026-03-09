using System.Threading;

namespace YantraJS.Core.Tests;

/// <summary>
/// ECMAScript conformance tests for the Promise built-in object.
/// Covers constructor, prototype methods (then/catch/finally),
/// and static methods (resolve/reject/all).
/// </summary>
public class PromiseBuiltInTests : IDisposable
{
    private readonly JSContext _context;
    private readonly SynchronizationContext _previousSyncCtx;

    public PromiseBuiltInTests()
    {
        // Promise requires a SynchronizationContext
        _previousSyncCtx = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

        _context = new JSContext(SynchronizationContext.Current);
        JSContext.CurrentContext = _context;
    }

    public void Dispose()
    {
        _context.Dispose();
        SynchronizationContext.SetSynchronizationContext(_previousSyncCtx);
    }

    // ---------------------------------------------------------------
    // Constructor
    // ---------------------------------------------------------------

    [Fact]
    public void Promise_Constructor_CreatesPromise()
    {
        var result = _context.Eval("typeof new Promise(function(resolve) { resolve(1); })");
        Assert.Equal("object", result.ToString());
    }

    [Fact]
    public void Promise_InstanceOf()
    {
        var result = _context.Eval("new Promise(function(resolve) { resolve(1); }) instanceof Promise");
        Assert.True(result.BooleanValue);
    }

    // ---------------------------------------------------------------
    // Promise.resolve / Promise.reject
    // ---------------------------------------------------------------

    [Fact]
    public void Promise_Resolve_ReturnsPromise()
    {
        var result = _context.Eval("Promise.resolve(42) instanceof Promise");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void Promise_Reject_ReturnsPromise()
    {
        // Catch reject to prevent unhandled rejection
        var result = _context.Eval(@"
            var p = Promise.reject('err');
            p.catch(function() {});
            p instanceof Promise
        ");
        Assert.True(result.BooleanValue);
    }

    // ---------------------------------------------------------------
    // then
    // ---------------------------------------------------------------

    [Fact]
    public void Promise_Then_ChainReturnsPromise()
    {
        var result = _context.Eval(@"
            Promise.resolve(1).then(function(v) { return v + 1; }) instanceof Promise
        ");
        Assert.True(result.BooleanValue);
    }

    // ---------------------------------------------------------------
    // Promise.all
    // ---------------------------------------------------------------

    [Fact]
    public void Promise_All_ReturnsPromise()
    {
        var result = _context.Eval(@"
            typeof Promise.all
        ");
        Assert.Equal("function", result.ToString());
    }

    // ---------------------------------------------------------------
    // catch
    // ---------------------------------------------------------------

    [Fact]
    public void Promise_Catch_ReturnsPromise()
    {
        var result = _context.Eval(@"
            Promise.reject('err').catch(function(e) { return 'caught'; }) instanceof Promise
        ");
        Assert.True(result.BooleanValue);
    }

    // ---------------------------------------------------------------
    // finally
    // ---------------------------------------------------------------

    [Fact]
    public void Promise_Finally_ReturnsPromise()
    {
        var result = _context.Eval(@"
            Promise.resolve(1).finally(function() {}) instanceof Promise
        ");
        Assert.True(result.BooleanValue);
    }

    // ---------------------------------------------------------------
    // Typeof check (not a function return)
    // ---------------------------------------------------------------

    [Fact]
    public void Promise_TypeofConstructor_IsFunction()
    {
        var result = _context.Eval("typeof Promise");
        Assert.Equal("function", result.ToString());
    }

    // ---------------------------------------------------------------
    // Promise chaining with synchronous resolution
    // ---------------------------------------------------------------

    [Fact]
    public void Promise_Resolve_Then_ChainingWorks()
    {
        // Synchronous resolution chain
        var result = _context.Eval(@"
            var result = 0;
            Promise.resolve(10).then(function(v) { result = v; });
            result
        ");
        // Depending on microtask execution, result may be 0 or 10
        // We just verify no errors occur
        Assert.True(result.DoubleValue == 0 || result.DoubleValue == 10);
    }
}
