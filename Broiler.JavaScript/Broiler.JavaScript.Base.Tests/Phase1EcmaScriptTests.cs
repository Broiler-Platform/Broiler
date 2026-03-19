using Broiler.JavaScript.Core.Core;

namespace Broiler.JavaScript.Core.Tests;

/// <summary>
/// ECMAScript conformance tests for Phase 1 features:
/// RegExp.escape, Promise.try, Error.isError, Object.groupBy,
/// Map.groupBy, Date constructor day default fix, Array destructuring
/// elision, and Set methods.
/// </summary>
public class Phase1EcmaScriptTests : IDisposable
{
    private readonly JSContext _context;
    private readonly SynchronizationContext _previousSyncCtx;

    public Phase1EcmaScriptTests()
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

    // ---------------------------------------------------------------
    // RegExp.escape
    // ---------------------------------------------------------------

    [Fact]
    public void RegExp_Escape_IsFunction()
    {
        var result = _context.Eval("typeof RegExp.escape");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void RegExp_Escape_EscapesDot()
    {
        var result = _context.Eval("RegExp.escape('a.b')");
        Assert.Equal(@"a\.b", result.ToString());
    }

    [Fact]
    public void RegExp_Escape_EscapesSyntaxCharacters()
    {
        var result = _context.Eval(@"RegExp.escape('hello (world) [test]')");
        Assert.Equal(@"hello \(world\) \[test\]", result.ToString());
    }

    [Fact]
    public void RegExp_Escape_EscapesAllSpecialChars()
    {
        // Test each special character individually to avoid escaping confusion
        var result = _context.Eval("RegExp.escape('^$.*+?()[]{}|/')");
        Assert.Equal(@"\^\$\.\*\+\?\(\)\[\]\{\}\|\/", result.ToString());
    }

    [Fact]
    public void RegExp_Escape_PlainStringUnchanged()
    {
        var result = _context.Eval("RegExp.escape('hello')");
        Assert.Equal("hello", result.ToString());
    }

    [Fact]
    public void RegExp_Escape_EmptyString()
    {
        var result = _context.Eval("RegExp.escape('')");
        Assert.Equal("", result.ToString());
    }

    [Fact]
    public void RegExp_Escape_WorksInRegExp()
    {
        var result = _context.Eval(@"
            var str = 'price: $100.00';
            var escaped = RegExp.escape('$100.00');
            new RegExp(escaped).test(str)
        ");
        Assert.True(result.BooleanValue);
    }

    // ---------------------------------------------------------------
    // Promise.try
    // ---------------------------------------------------------------

    [Fact]
    public void Promise_Try_IsFunction()
    {
        var result = _context.Eval("typeof Promise.try");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Promise_Try_ReturnsPromise()
    {
        var result = _context.Eval("Promise.try(function() { return 42; }) instanceof Promise");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void Promise_Try_ThrowReturnsRejectedPromise()
    {
        var result = _context.Eval(@"
            var p = Promise.try(function() { throw new Error('fail'); });
            p instanceof Promise
        ");
        Assert.True(result.BooleanValue);
    }

    // ---------------------------------------------------------------
    // Error.isError
    // ---------------------------------------------------------------

    [Fact]
    public void Error_IsError_IsFunction()
    {
        var result = _context.Eval("typeof Error.isError");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Error_IsError_WithError()
    {
        var result = _context.Eval("Error.isError(new Error('test'))");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void Error_IsError_WithTypeError()
    {
        var result = _context.Eval("Error.isError(new TypeError('test'))");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void Error_IsError_WithRangeError()
    {
        var result = _context.Eval("Error.isError(new RangeError('test'))");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void Error_IsError_WithNonError()
    {
        var result = _context.Eval("Error.isError({})");
        Assert.False(result.BooleanValue);
    }

    [Fact]
    public void Error_IsError_WithString()
    {
        var result = _context.Eval("Error.isError('error')");
        Assert.False(result.BooleanValue);
    }

    [Fact]
    public void Error_IsError_WithNull()
    {
        var result = _context.Eval("Error.isError(null)");
        Assert.False(result.BooleanValue);
    }

    [Fact]
    public void Error_IsError_WithUndefined()
    {
        var result = _context.Eval("Error.isError(undefined)");
        Assert.False(result.BooleanValue);
    }

    // ---------------------------------------------------------------
    // Object.groupBy
    // ---------------------------------------------------------------

    [Fact]
    public void Object_GroupBy_IsFunction()
    {
        var result = _context.Eval("typeof Object.groupBy");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Object_GroupBy_BasicGrouping()
    {
        var result = _context.Eval(@"
            var items = [1, 2, 3, 4, 5, 6];
            var grouped = Object.groupBy(items, function(item) {
                return item % 2 === 0 ? 'even' : 'odd';
            });
            grouped.odd.length
        ");
        Assert.Equal(3d, result.DoubleValue);
    }

    [Fact]
    public void Object_GroupBy_ReturnsObject()
    {
        var result = _context.Eval(@"
            var items = [1, 2, 3, 4, 5, 6];
            var grouped = Object.groupBy(items, function(item) {
                return item % 2 === 0 ? 'even' : 'odd';
            });
            typeof grouped
        ");
        Assert.Equal("object", result.ToString());
    }

    [Fact]
    public void Object_GroupBy_GroupValues()
    {
        var result = _context.Eval(@"
            var items = [1, 2, 3, 4, 5, 6];
            var grouped = Object.groupBy(items, function(item) {
                return item % 2 === 0 ? 'even' : 'odd';
            });
            grouped.even.length
        ");
        Assert.Equal(3d, result.DoubleValue);
    }

    [Fact]
    public void Object_GroupBy_EmptyArray()
    {
        var result = _context.Eval(@"
            var grouped = Object.groupBy([], function(item) { return 'a'; });
            Object.keys(grouped).length
        ");
        Assert.Equal(0d, result.DoubleValue);
    }

    // ---------------------------------------------------------------
    // Map.groupBy
    // ---------------------------------------------------------------

    [Fact]
    public void Map_GroupBy_IsFunction()
    {
        var result = _context.Eval("typeof Map.groupBy");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Map_GroupBy_ReturnsMap()
    {
        var result = _context.Eval(@"
            var items = [1, 2, 3, 4, 5, 6];
            var grouped = Map.groupBy(items, function(item) {
                return item % 2 === 0 ? 'even' : 'odd';
            });
            grouped instanceof Map
        ");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void Map_GroupBy_GroupSize()
    {
        var result = _context.Eval(@"
            var items = [1, 2, 3, 4, 5, 6];
            var grouped = Map.groupBy(items, function(item) {
                return item % 2 === 0 ? 'even' : 'odd';
            });
            grouped.size
        ");
        Assert.Equal(2d, result.DoubleValue);
    }

    // ---------------------------------------------------------------
    // Date constructor day default fix (0 → 1)
    // ---------------------------------------------------------------

    [Fact]
    public void Date_Constructor_YearMonth_DayDefaultsTo1()
    {
        // new Date(2020, 0) should create Jan 1 2020 (day defaults to 1)
        var result = _context.Eval("new Date(2020, 0).getDate()");
        Assert.Equal(1d, result.DoubleValue);
    }

    [Fact]
    public void Date_Constructor_YearMonth_CorrectMonth()
    {
        // new Date(2020, 0) should be January
        var result = _context.Eval("new Date(2020, 0).getMonth()");
        Assert.Equal(0d, result.DoubleValue);
    }

    [Fact]
    public void Date_Constructor_YearMonth_CorrectYear()
    {
        var result = _context.Eval("new Date(2020, 0).getFullYear()");
        Assert.Equal(2020d, result.DoubleValue);
    }

    [Fact]
    public void Date_UTC_YearMonth_DayDefaultsTo1()
    {
        // Date.UTC(2020, 0) should be Jan 1 2020 UTC
        var result = _context.Eval("new Date(Date.UTC(2020, 0)).getUTCDate()");
        Assert.Equal(1d, result.DoubleValue);
    }

    // ---------------------------------------------------------------
    // Array destructuring elision
    // ---------------------------------------------------------------

    [Fact]
    public void Array_Destructuring_Elision_SkipsSecond()
    {
        var result = _context.Eval(@"
            var arr = [1, 2, 3];
            var [a, , c] = arr;
            a + c
        ");
        Assert.Equal(4d, result.DoubleValue); // 1 + 3
    }

    [Fact]
    public void Array_Destructuring_Elision_FirstHole()
    {
        var result = _context.Eval(@"
            var arr = [1, 2, 3];
            var [, b, c] = arr;
            b + c
        ");
        Assert.Equal(5d, result.DoubleValue); // 2 + 3
    }

    [Fact]
    public void Array_Destructuring_Elision_MultipleHoles()
    {
        var result = _context.Eval(@"
            var arr = [1, 2, 3, 4, 5];
            var [a, , , , e] = arr;
            a + e
        ");
        Assert.Equal(6d, result.DoubleValue); // 1 + 5
    }

    // ---------------------------------------------------------------
    // Set methods
    // ---------------------------------------------------------------

    [Fact]
    public void Set_Union_IsFunction()
    {
        var result = _context.Eval("typeof Set.prototype.union");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Set_Union_CombinesSets()
    {
        var result = _context.Eval(@"
            var a = new Set([1, 2, 3]);
            var b = new Set([3, 4, 5]);
            var u = a.union(b);
            u.size
        ");
        Assert.Equal(5d, result.DoubleValue);
    }

    [Fact]
    public void Set_Intersection_IsFunction()
    {
        var result = _context.Eval("typeof Set.prototype.intersection");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Set_Intersection_CommonElements()
    {
        var result = _context.Eval(@"
            var a = new Set([1, 2, 3]);
            var b = new Set([2, 3, 4]);
            var i = a.intersection(b);
            i.size
        ");
        Assert.Equal(2d, result.DoubleValue);
    }

    [Fact]
    public void Set_Difference_IsFunction()
    {
        var result = _context.Eval("typeof Set.prototype.difference");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Set_Difference_ExcludesOther()
    {
        var result = _context.Eval(@"
            var a = new Set([1, 2, 3]);
            var b = new Set([2, 3, 4]);
            var d = a.difference(b);
            d.size
        ");
        Assert.Equal(1d, result.DoubleValue);
    }

    [Fact]
    public void Set_SymmetricDifference_IsFunction()
    {
        var result = _context.Eval("typeof Set.prototype.symmetricDifference");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Set_SymmetricDifference_ExclusiveElements()
    {
        var result = _context.Eval(@"
            var a = new Set([1, 2, 3]);
            var b = new Set([2, 3, 4]);
            var sd = a.symmetricDifference(b);
            sd.size
        ");
        Assert.Equal(2d, result.DoubleValue);
    }

    [Fact]
    public void Set_IsSubsetOf_IsFunction()
    {
        var result = _context.Eval("typeof Set.prototype.isSubsetOf");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Set_IsSubsetOf_True()
    {
        var result = _context.Eval(@"
            var a = new Set([1, 2]);
            var b = new Set([1, 2, 3]);
            a.isSubsetOf(b)
        ");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void Set_IsSubsetOf_False()
    {
        var result = _context.Eval(@"
            var a = new Set([1, 2, 4]);
            var b = new Set([1, 2, 3]);
            a.isSubsetOf(b)
        ");
        Assert.False(result.BooleanValue);
    }

    [Fact]
    public void Set_IsSupersetOf_IsFunction()
    {
        var result = _context.Eval("typeof Set.prototype.isSupersetOf");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Set_IsSupersetOf_True()
    {
        var result = _context.Eval(@"
            var a = new Set([1, 2, 3]);
            var b = new Set([1, 2]);
            a.isSupersetOf(b)
        ");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void Set_IsSupersetOf_False()
    {
        var result = _context.Eval(@"
            var a = new Set([1, 2]);
            var b = new Set([1, 2, 3]);
            a.isSupersetOf(b)
        ");
        Assert.False(result.BooleanValue);
    }

    [Fact]
    public void Set_IsDisjointFrom_IsFunction()
    {
        var result = _context.Eval("typeof Set.prototype.isDisjointFrom");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Set_IsDisjointFrom_True()
    {
        var result = _context.Eval(@"
            var a = new Set([1, 2]);
            var b = new Set([3, 4]);
            a.isDisjointFrom(b)
        ");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void Set_IsDisjointFrom_False()
    {
        var result = _context.Eval(@"
            var a = new Set([1, 2, 3]);
            var b = new Set([3, 4, 5]);
            a.isDisjointFrom(b)
        ");
        Assert.False(result.BooleanValue);
    }

    [Fact]
    public void Set_Union_ReturnsNewSet()
    {
        var result = _context.Eval(@"
            var a = new Set([1, 2]);
            var b = new Set([3, 4]);
            var u = a.union(b);
            u instanceof Set
        ");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void Set_Intersection_ReturnsNewSet()
    {
        var result = _context.Eval(@"
            var a = new Set([1, 2, 3]);
            var b = new Set([2, 3, 4]);
            a.intersection(b) instanceof Set
        ");
        Assert.True(result.BooleanValue);
    }
}
