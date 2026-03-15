namespace YantraJS.Core.Tests;

/// <summary>
/// ECMAScript conformance tests for the Array built-in object.
/// Covers constructor, prototype methods, and static methods.
/// </summary>
public class ArrayBuiltInTests : IDisposable
{
    private readonly JSContext _context;

    public ArrayBuiltInTests()
    {
        _context = new JSContext();
        JSContext.CurrentContext = _context;
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    // ---------------------------------------------------------------
    // Constructor
    // ---------------------------------------------------------------

    [Fact]
    public void Array_Constructor_NoArgs_CreatesEmpty()
    {
        var result = _context.Eval("new Array().length");
        Assert.Equal(0d, result.DoubleValue);
    }

    [Fact]
    public void Array_Constructor_SingleNumeric_SetsLength()
    {
        var result = _context.Eval("new Array(5).length");
        Assert.Equal(5d, result.DoubleValue);
    }

    [Fact]
    public void Array_Constructor_MultipleArgs_CreatesWithElements()
    {
        var result = _context.Eval("var a = new Array(1, 2, 3); a.length");
        Assert.Equal(3d, result.DoubleValue);
    }

    [Fact]
    public void Array_Literal_CreatesWithElements()
    {
        var result = _context.Eval("[10, 20, 30][1]");
        Assert.Equal(20d, result.DoubleValue);
    }

    // ---------------------------------------------------------------
    // Prototype methods
    // ---------------------------------------------------------------

    [Fact]
    public void Array_Push_AddsElement()
    {
        var result = _context.Eval("var a = [1]; a.push(2); a.length");
        Assert.Equal(2d, result.DoubleValue);
    }

    [Fact]
    public void Array_Push_ReturnsNewLength()
    {
        var result = _context.Eval("var a = [1]; a.push(2, 3)");
        Assert.Equal(3d, result.DoubleValue);
    }

    [Fact]
    public void Array_Pop_RemovesLast()
    {
        var result = _context.Eval("var a = [1, 2, 3]; a.pop()");
        Assert.Equal(3d, result.DoubleValue);
    }

    [Fact]
    public void Array_Pop_ReducesLength()
    {
        var result = _context.Eval("var a = [1, 2, 3]; a.pop(); a.length");
        Assert.Equal(2d, result.DoubleValue);
    }

    [Fact]
    public void Array_Shift_RemovesFirst()
    {
        var result = _context.Eval("[10, 20, 30].shift()");
        Assert.Equal(10d, result.DoubleValue);
    }

    [Fact]
    public void Array_Unshift_PrependsElements()
    {
        var result = _context.Eval("var a = [3]; a.unshift(1, 2); a[0]");
        Assert.Equal(1d, result.DoubleValue);
    }

    [Fact]
    public void Array_IndexOf_FindsElement()
    {
        var result = _context.Eval("[10, 20, 30].indexOf(20)");
        Assert.Equal(1d, result.DoubleValue);
    }

    [Fact]
    public void Array_IndexOf_ReturnsMinusOneWhenNotFound()
    {
        var result = _context.Eval("[10, 20, 30].indexOf(99)");
        Assert.Equal(-1d, result.DoubleValue);
    }

    [Fact]
    public void Array_Includes_ReturnsTrue()
    {
        var result = _context.Eval("[1, 2, 3].includes(2)");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void Array_Includes_ReturnsFalse()
    {
        var result = _context.Eval("[1, 2, 3].includes(5)");
        Assert.False(result.BooleanValue);
    }

    [Fact]
    public void Array_Join_DefaultSeparator()
    {
        var result = _context.Eval("[1, 2, 3].join()");
        Assert.Equal("1,2,3", result.ToString());
    }

    [Fact]
    public void Array_Join_CustomSeparator()
    {
        var result = _context.Eval("[1, 2, 3].join('-')");
        Assert.Equal("1-2-3", result.ToString());
    }

    [Fact]
    public void Array_Reverse_ReversesInPlace()
    {
        var result = _context.Eval("var a = [1, 2, 3]; a.reverse(); a[0]");
        Assert.Equal(3d, result.DoubleValue);
    }

    [Fact]
    public void Array_Slice_ReturnsSubarray()
    {
        var result = _context.Eval("[10, 20, 30, 40].slice(1, 3).length");
        Assert.Equal(2d, result.DoubleValue);
    }

    [Fact]
    public void Array_Slice_ValuesCorrect()
    {
        var result = _context.Eval("[10, 20, 30, 40].slice(1, 3)[0]");
        Assert.Equal(20d, result.DoubleValue);
    }

    [Fact]
    public void Array_Splice_RemovesElements()
    {
        var result = _context.Eval("var a = [1, 2, 3, 4]; a.splice(1, 2); a.length");
        Assert.Equal(2d, result.DoubleValue);
    }

    [Fact]
    public void Array_Concat_MergesArrays()
    {
        var result = _context.Eval("[1, 2].concat([3, 4]).length");
        Assert.Equal(4d, result.DoubleValue);
    }

    // ---------------------------------------------------------------
    // Higher-order methods
    // ---------------------------------------------------------------

    [Fact]
    public void Array_Map_TransformsElements()
    {
        var result = _context.Eval("[1, 2, 3].map(x => x * 10)[2]");
        Assert.Equal(30d, result.DoubleValue);
    }

    [Fact]
    public void Array_Filter_SelectsElements()
    {
        var result = _context.Eval("[1, 2, 3, 4, 5].filter(x => x > 3).length");
        Assert.Equal(2d, result.DoubleValue);
    }

    [Fact]
    public void Array_Reduce_AccumulatesValue()
    {
        var result = _context.Eval("[1, 2, 3, 4].reduce((acc, x) => acc + x, 0)");
        Assert.Equal(10d, result.DoubleValue);
    }

    [Fact]
    public void Array_Every_AllPass()
    {
        var result = _context.Eval("[2, 4, 6].every(x => x % 2 === 0)");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void Array_Every_SomeFail()
    {
        var result = _context.Eval("[2, 3, 6].every(x => x % 2 === 0)");
        Assert.False(result.BooleanValue);
    }

    [Fact]
    public void Array_Some_AtLeastOnePass()
    {
        var result = _context.Eval("[1, 3, 4].some(x => x % 2 === 0)");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void Array_Some_NonePass()
    {
        var result = _context.Eval("[1, 3, 5].some(x => x % 2 === 0)");
        Assert.False(result.BooleanValue);
    }

    [Fact]
    public void Array_Find_ReturnsFirstMatch()
    {
        var result = _context.Eval("[1, 2, 3, 4].find(x => x > 2)");
        Assert.Equal(3d, result.DoubleValue);
    }

    [Fact]
    public void Array_FindIndex_ReturnsIndex()
    {
        var result = _context.Eval("[10, 20, 30].findIndex(x => x === 20)");
        Assert.Equal(1d, result.DoubleValue);
    }

    [Fact]
    public void Array_ForEach_IteratesAll()
    {
        var result = _context.Eval(@"
            var sum = 0;
            [1, 2, 3].forEach(x => { sum += x; });
            sum
        ");
        Assert.Equal(6d, result.DoubleValue);
    }

    [Fact]
    public void Array_Sort_SortsInPlace()
    {
        var result = _context.Eval("[3, 1, 2].sort()[0]");
        Assert.Equal(1d, result.DoubleValue);
    }

    [Fact]
    public void Array_Sort_WithComparator()
    {
        var result = _context.Eval("[3, 1, 2].sort((a, b) => b - a)[0]");
        Assert.Equal(3d, result.DoubleValue);
    }

    [Fact]
    public void Array_Flat_FlattensOneLevel()
    {
        var result = _context.Eval("[1, [2, 3], [4]].flat().length");
        Assert.Equal(4d, result.DoubleValue);
    }

    [Fact]
    public void Array_FlatMap_MapsAndFlattens()
    {
        var result = _context.Eval("[1, 2, 3].flatMap(x => [x, x * 2]).length");
        Assert.Equal(6d, result.DoubleValue);
    }

    // ---------------------------------------------------------------
    // Static methods
    // ---------------------------------------------------------------

    [Fact]
    public void Array_IsArray_TrueForArrays()
    {
        var result = _context.Eval("Array.isArray([1, 2])");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void Array_IsArray_FalseForNonArrays()
    {
        var result = _context.Eval("Array.isArray({})");
        Assert.False(result.BooleanValue);
    }

    [Fact]
    public void Array_From_CreatesFromIterable()
    {
        var result = _context.Eval("Array.from('abc').length");
        Assert.Equal(3d, result.DoubleValue);
    }

    [Fact]
    public void Array_Of_CreatesFromArguments()
    {
        var result = _context.Eval("Array.of(1, 2, 3).length");
        Assert.Equal(3d, result.DoubleValue);
    }

    // ---------------------------------------------------------------
    // Iteration
    // ---------------------------------------------------------------

    [Fact]
    public void Array_ForOf_Iterates()
    {
        var result = _context.Eval(@"
            var sum = 0;
            for (var x of [10, 20, 30]) { sum += x; }
            sum
        ");
        Assert.Equal(60d, result.DoubleValue);
    }

    [Fact]
    public void Array_SpreadIntoNew()
    {
        var result = _context.Eval("[...[1, 2], ...[3, 4]].length");
        Assert.Equal(4d, result.DoubleValue);
    }

    [Fact]
    public void Array_Destructuring()
    {
        var result = _context.Eval("var [a, b, c] = [10, 20, 30]; a + c");
        Assert.Equal(40d, result.DoubleValue);
    }

    // ---------------------------------------------------------------
    // toString / valueOf
    // ---------------------------------------------------------------

    [Fact]
    public void Array_ToString_JoinsWithComma()
    {
        var result = _context.Eval("[1, 2, 3].toString()");
        Assert.Equal("1,2,3", result.ToString());
    }

    // ---------------------------------------------------------------
    // LastIndexOf
    // ---------------------------------------------------------------

    [Fact]
    public void Array_LastIndexOf_FindsLast()
    {
        var result = _context.Eval("[1, 2, 3, 2, 1].lastIndexOf(2)");
        Assert.Equal(3d, result.DoubleValue);
    }

    // ---------------------------------------------------------------
    // ReduceRight
    // ---------------------------------------------------------------

    [Fact]
    public void Array_ReduceRight_AccumulatesRightToLeft()
    {
        var result = _context.Eval("[1, 2, 3].reduceRight((acc, x) => acc + x, 0)");
        Assert.Equal(6d, result.DoubleValue);
    }

    // ---------------------------------------------------------------
    // Keys / Values / Entries
    // ---------------------------------------------------------------

    [Fact]
    public void Array_Keys_ReturnsIterator()
    {
        var result = _context.Eval("Array.from([10, 20, 30].keys()).length");
        Assert.Equal(3d, result.DoubleValue);
    }

    [Fact]
    public void Array_Values_ReturnsIterator()
    {
        var result = _context.Eval("Array.from([10, 20, 30].values())[1]");
        Assert.Equal(20d, result.DoubleValue);
    }

    // ---------------------------------------------------------------
    // Fill / CopyWithin
    // ---------------------------------------------------------------

    [Fact]
    public void Array_Fill_FillsEntireArray()
    {
        var result = _context.Eval("[1, 2, 3].fill(0)[1]");
        Assert.Equal(0d, result.DoubleValue);
    }

    [Fact]
    public void Array_CopyWithin_CopiesWithinArray()
    {
        var result = _context.Eval("[1, 2, 3, 4, 5].copyWithin(0, 3)[0]");
        Assert.Equal(4d, result.DoubleValue);
    }
}
