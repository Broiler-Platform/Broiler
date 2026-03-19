using Broiler.JavaScript.Core.Core;

namespace Broiler.JavaScript.Core.Tests;

/// <summary>
/// ECMAScript conformance tests for Phase 2 features:
/// Iterator helpers (§2.1), Iterator.concat (§4.8),
/// ArrayBuffer.transfer (§2.9), and Array.fromAsync (§4.5).
/// </summary>
public class Phase2EcmaScriptTests : IDisposable
{
    private readonly JSContext _context;
    private readonly SynchronizationContext _previousSyncCtx;

    public Phase2EcmaScriptTests()
    {
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

    // ===============================================================
    // §2.1  Iterator helpers — existence checks
    // ===============================================================

    [Fact]
    public void Iterator_IsFunction()
    {
        var result = _context.Eval("typeof Iterator");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Iterator_From_IsFunction()
    {
        var result = _context.Eval("typeof Iterator.from");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Iterator_Prototype_Map_IsFunction()
    {
        var result = _context.Eval("typeof Iterator.prototype.map");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Iterator_Prototype_Filter_IsFunction()
    {
        var result = _context.Eval("typeof Iterator.prototype.filter");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Iterator_Prototype_Take_IsFunction()
    {
        var result = _context.Eval("typeof Iterator.prototype.take");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Iterator_Prototype_Drop_IsFunction()
    {
        var result = _context.Eval("typeof Iterator.prototype.drop");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Iterator_Prototype_FlatMap_IsFunction()
    {
        var result = _context.Eval("typeof Iterator.prototype.flatMap");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Iterator_Prototype_Reduce_IsFunction()
    {
        var result = _context.Eval("typeof Iterator.prototype.reduce");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Iterator_Prototype_ToArray_IsFunction()
    {
        var result = _context.Eval("typeof Iterator.prototype.toArray");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Iterator_Prototype_ForEach_IsFunction()
    {
        var result = _context.Eval("typeof Iterator.prototype.forEach");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Iterator_Prototype_Some_IsFunction()
    {
        var result = _context.Eval("typeof Iterator.prototype.some");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Iterator_Prototype_Every_IsFunction()
    {
        var result = _context.Eval("typeof Iterator.prototype.every");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Iterator_Prototype_Find_IsFunction()
    {
        var result = _context.Eval("typeof Iterator.prototype.find");
        Assert.Equal("function", result.ToString());
    }

    // ===============================================================
    // §2.1.2  Iterator.from
    // ===============================================================

    [Fact]
    public void Iterator_From_WrapsArray()
    {
        var result = _context.Eval(
            "Iterator.from([1,2,3]).toArray().join(',')");
        Assert.Equal("1,2,3", result.ToString());
    }

    [Fact]
    public void Iterator_From_WrapsGenerator()
    {
        var result = _context.Eval(@"
            function* gen() { yield 10; yield 20; }
            Iterator.from(gen()).toArray().join(',')");
        Assert.Equal("10,20", result.ToString());
    }

    // ===============================================================
    // §2.1.3  Iterator.prototype.map
    // ===============================================================

    [Fact]
    public void Iterator_Map_TransformsValues()
    {
        var result = _context.Eval(
            "Iterator.from([1,2,3]).map(x => x * 2).toArray().join(',')");
        Assert.Equal("2,4,6", result.ToString());
    }

    [Fact]
    public void Iterator_Map_IsLazy()
    {
        // map should not eagerly consume the source
        var result = _context.Eval(@"
            let count = 0;
            function* gen() { while(true) { count++; yield count; } }
            const it = Iterator.from(gen()).map(x => x * 10);
            const first = it.next();
            first.value");
        Assert.Equal(10.0, result.DoubleValue);
    }

    // ===============================================================
    // §2.1.4  Iterator.prototype.filter
    // ===============================================================

    [Fact]
    public void Iterator_Filter_SelectsMatchingValues()
    {
        var result = _context.Eval(
            "Iterator.from([1,2,3,4,5]).filter(x => x % 2 === 0).toArray().join(',')");
        Assert.Equal("2,4", result.ToString());
    }

    // ===============================================================
    // §2.1.5  Iterator.prototype.take
    // ===============================================================

    [Fact]
    public void Iterator_Take_LimitsCount()
    {
        var result = _context.Eval(
            "Iterator.from([1,2,3,4,5]).take(3).toArray().join(',')");
        Assert.Equal("1,2,3", result.ToString());
    }

    [Fact]
    public void Iterator_Take_Zero_ReturnsEmpty()
    {
        var result = _context.Eval(
            "Iterator.from([1,2,3]).take(0).toArray().length");
        Assert.Equal(0.0, result.DoubleValue);
    }

    // ===============================================================
    // §2.1.6  Iterator.prototype.drop
    // ===============================================================

    [Fact]
    public void Iterator_Drop_SkipsElements()
    {
        var result = _context.Eval(
            "Iterator.from([1,2,3,4,5]).drop(2).toArray().join(',')");
        Assert.Equal("3,4,5", result.ToString());
    }

    // ===============================================================
    // §2.1.7  Iterator.prototype.flatMap
    // ===============================================================

    [Fact]
    public void Iterator_FlatMap_FlattensResults()
    {
        var result = _context.Eval(
            "Iterator.from([1,2,3]).flatMap(x => [x, x * 10]).toArray().join(',')");
        Assert.Equal("1,10,2,20,3,30", result.ToString());
    }

    // ===============================================================
    // §2.1.8  Iterator.prototype.reduce
    // ===============================================================

    [Fact]
    public void Iterator_Reduce_WithInitialValue()
    {
        var result = _context.Eval(
            "Iterator.from([1,2,3]).reduce((acc, x) => acc + x, 0)");
        Assert.Equal(6.0, result.DoubleValue);
    }

    [Fact]
    public void Iterator_Reduce_WithoutInitialValue()
    {
        var result = _context.Eval(
            "Iterator.from([1,2,3]).reduce((acc, x) => acc + x)");
        Assert.Equal(6.0, result.DoubleValue);
    }

    // ===============================================================
    // §2.1.9  Iterator.prototype.toArray
    // ===============================================================

    [Fact]
    public void Iterator_ToArray_ReturnsArray()
    {
        var result = _context.Eval(
            "Array.isArray(Iterator.from([1,2]).toArray())");
        Assert.Equal(true, result.BooleanValue);
    }

    // ===============================================================
    // §2.1.10  Iterator.prototype.forEach
    // ===============================================================

    [Fact]
    public void Iterator_ForEach_VisitsAllElements()
    {
        var result = _context.Eval(@"
            let sum = 0;
            Iterator.from([1,2,3]).forEach(x => sum += x);
            sum");
        Assert.Equal(6.0, result.DoubleValue);
    }

    // ===============================================================
    // §2.1.11  Iterator.prototype.some
    // ===============================================================

    [Fact]
    public void Iterator_Some_ReturnsTrue()
    {
        var result = _context.Eval(
            "Iterator.from([1,2,3]).some(x => x > 2)");
        Assert.Equal(true, result.BooleanValue);
    }

    [Fact]
    public void Iterator_Some_ReturnsFalse()
    {
        var result = _context.Eval(
            "Iterator.from([1,2,3]).some(x => x > 5)");
        Assert.Equal(false, result.BooleanValue);
    }

    // ===============================================================
    // §2.1.12  Iterator.prototype.every
    // ===============================================================

    [Fact]
    public void Iterator_Every_ReturnsTrue()
    {
        var result = _context.Eval(
            "Iterator.from([1,2,3]).every(x => x > 0)");
        Assert.Equal(true, result.BooleanValue);
    }

    [Fact]
    public void Iterator_Every_ReturnsFalse()
    {
        var result = _context.Eval(
            "Iterator.from([1,2,3]).every(x => x > 2)");
        Assert.Equal(false, result.BooleanValue);
    }

    // ===============================================================
    // §2.1.13  Iterator.prototype.find
    // ===============================================================

    [Fact]
    public void Iterator_Find_ReturnsMatch()
    {
        var result = _context.Eval(
            "Iterator.from([1,2,3]).find(x => x > 1)");
        Assert.Equal(2.0, result.DoubleValue);
    }

    [Fact]
    public void Iterator_Find_ReturnsUndefined_WhenNoMatch()
    {
        var result = _context.Eval(
            "Iterator.from([1,2,3]).find(x => x > 5)");
        Assert.True(result.IsUndefined);
    }

    // ===============================================================
    // §2.1  Chaining
    // ===============================================================

    [Fact]
    public void Iterator_Chain_MapFilterReduce()
    {
        var result = _context.Eval(@"
            Iterator.from([1,2,3,4,5])
                .map(x => x * 2)
                .filter(x => x > 4)
                .reduce((a, b) => a + b, 0)");
        Assert.Equal(24.0, result.DoubleValue);
    }

    [Fact]
    public void Iterator_Chain_DropTakeToArray()
    {
        var result = _context.Eval(
            "Iterator.from([1,2,3,4,5]).drop(1).take(3).toArray().join(',')");
        Assert.Equal("2,3,4", result.ToString());
    }

    // ===============================================================
    // §4.8  Iterator.concat
    // ===============================================================

    [Fact]
    public void Iterator_Concat_IsFunction()
    {
        var result = _context.Eval("typeof Iterator.concat");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Iterator_Concat_CombinesIterables()
    {
        var result = _context.Eval(
            "Iterator.concat([1,2], [3,4], [5]).toArray().join(',')");
        Assert.Equal("1,2,3,4,5", result.ToString());
    }

    [Fact]
    public void Iterator_Concat_Empty()
    {
        var result = _context.Eval(
            "Iterator.concat().toArray().length");
        Assert.Equal(0.0, result.DoubleValue);
    }

    // ===============================================================
    // §2.9  ArrayBuffer.transfer
    // ===============================================================

    [Fact]
    public void ArrayBuffer_ByteLength_ReturnsLength()
    {
        var result = _context.Eval(
            "new ArrayBuffer(8).byteLength");
        Assert.Equal(8.0, result.DoubleValue);
    }

    [Fact]
    public void ArrayBuffer_Detached_IsFalseInitially()
    {
        var result = _context.Eval(
            "new ArrayBuffer(8).detached");
        Assert.Equal(false, result.BooleanValue);
    }

    [Fact]
    public void ArrayBuffer_Transfer_ReturnsNewBuffer()
    {
        var result = _context.Eval(@"
            const buf = new ArrayBuffer(8);
            const buf2 = buf.transfer();
            buf2.byteLength");
        Assert.Equal(8.0, result.DoubleValue);
    }

    [Fact]
    public void ArrayBuffer_Transfer_DetachesSource()
    {
        var result = _context.Eval(@"
            const buf = new ArrayBuffer(8);
            buf.transfer();
            buf.detached");
        Assert.Equal(true, result.BooleanValue);
    }

    [Fact]
    public void ArrayBuffer_Transfer_WithNewLength()
    {
        var result = _context.Eval(@"
            const buf = new ArrayBuffer(8);
            const buf2 = buf.transfer(16);
            buf2.byteLength");
        Assert.Equal(16.0, result.DoubleValue);
    }

    [Fact]
    public void ArrayBuffer_TransferToFixedLength_IsFunction()
    {
        var result = _context.Eval(
            "typeof ArrayBuffer.prototype.transferToFixedLength");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void ArrayBuffer_Slice_ReturnsSubset()
    {
        var result = _context.Eval(@"
            const buf = new ArrayBuffer(10);
            const view = new Uint8Array(buf);
            for (let i = 0; i < 10; i++) view[i] = i;
            const sliced = buf.slice(2, 5);
            const view2 = new Uint8Array(sliced);
            '' + view2[0] + ',' + view2[1] + ',' + view2[2]");
        Assert.Equal("2,3,4", result.ToString());
    }

    // ===============================================================
    // §4.5  Array.fromAsync
    // ===============================================================

    [Fact]
    public void Array_FromAsync_IsFunction()
    {
        var result = _context.Eval("typeof Array.fromAsync");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Array_FromAsync_ReturnsPromise()
    {
        var result = _context.Eval(@"
            const p = Array.fromAsync([1,2,3]);
            p instanceof Promise");
        Assert.Equal(true, result.BooleanValue);
    }

    [Fact]
    public void Array_FromAsync_WithMapFunction()
    {
        // Array.fromAsync returns a promise; test that it's a promise
        var result = _context.Eval(@"
            const p = Array.fromAsync([1,2,3], x => x * 2);
            p instanceof Promise");
        Assert.Equal(true, result.BooleanValue);
    }

    // ===============================================================
    // §2.1.14  Generators inherit Iterator.prototype
    // ===============================================================

    [Fact]
    public void Generator_Has_Map_FromIteratorPrototype()
    {
        var result = _context.Eval(@"
            function* gen() { yield 1; yield 2; yield 3; }
            const g = gen();
            typeof g.map");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Generator_Map_Works()
    {
        var result = _context.Eval(@"
            function* gen() { yield 1; yield 2; yield 3; }
            gen().map(x => x * 10).toArray().join(',')");
        Assert.Equal("10,20,30", result.ToString());
    }

    [Fact]
    public void Generator_Filter_Works()
    {
        var result = _context.Eval(@"
            function* gen() { yield 1; yield 2; yield 3; yield 4; }
            gen().filter(x => x % 2 === 0).toArray().join(',')");
        Assert.Equal("2,4", result.ToString());
    }

    [Fact]
    public void Generator_Reduce_Works()
    {
        var result = _context.Eval(@"
            function* gen() { yield 1; yield 2; yield 3; }
            gen().reduce((a, b) => a + b, 0)");
        Assert.Equal(6.0, result.DoubleValue);
    }
}
