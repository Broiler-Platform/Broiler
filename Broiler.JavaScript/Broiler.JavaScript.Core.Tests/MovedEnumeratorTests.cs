using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.Core.Tests;

/// <summary>
/// Tests for types moved from Broiler.JavaScript.Core to Broiler.JavaScript.Runtime:
/// ListElementEnumerator, ClrObjectEnumerator, ClrObjectEnumerable,
/// EnumerableElementEnumerable, JavaScriptObject.
/// </summary>
public class MovedEnumeratorTests
{
    [Fact]
    public void ListElementEnumerator_EnumeratesElements_ViaJSArray()
    {
        using var ctx = new JSContext();
        // Test element enumeration through the JS array API,
        // which exercises the full enumeration pipeline.
        var result = ctx.Eval("[10, 20, 30]");
        var en = result.GetElementEnumerator();
        var values = new List<double>();

        while (en.MoveNext(out var value))
        {
            values.Add(value.DoubleValue);
        }

        Assert.Equal(new double[] { 10, 20, 30 }, values.ToArray());
    }

    [Fact]
    public void ListElementEnumerator_CanBeConstructed()
    {
        using var ctx = new JSContext();
        var list = new List<JSValue> { new JSNumber(42) };
        // Verify struct can be constructed without error
        var enumerator = new ListElementEnumerator(list.GetEnumerator());
        Assert.IsType<ListElementEnumerator>(enumerator);
    }

    [Fact]
    public void ListElementEnumerator_Empty_ReturnsFalse()
    {
        using var ctx = new JSContext();
        var list = new List<JSValue>();
        IElementEnumerator enumerator = new ListElementEnumerator(list.GetEnumerator());

        Assert.False(enumerator.MoveNext(out var value));
        Assert.Null(value);
    }

    [Fact]
    public void ListElementEnumerator_NextOrDefault_ReturnsDefault_WhenEmpty()
    {
        using var ctx = new JSContext();
        var list = new List<JSValue>();
        IElementEnumerator enumerator = new ListElementEnumerator(list.GetEnumerator());
        var defaultVal = new JSNumber(99);

        var result = enumerator.NextOrDefault(defaultVal);
        Assert.Equal(99.0, result.DoubleValue);
    }

    [Fact]
    public void ListElementEnumerator_MoveNextOrDefault_ReturnsDefault_WhenExhausted()
    {
        using var ctx = new JSContext();
        var list = new List<JSValue>();
        IElementEnumerator enumerator = new ListElementEnumerator(list.GetEnumerator());
        var defaultVal = new JSNumber(77);

        var moved = enumerator.MoveNextOrDefault(out var value, defaultVal);
        Assert.False(moved);
        Assert.Equal(77.0, value.DoubleValue);
    }

    [Fact]
    public void EnumerableElementEnumerable_WrapsClrEnumerator()
    {
        using var ctx = new JSContext();
        var clrList = new List<object> { 10, 20, 30 };
        var enumerator = new EnumerableElementEnumerable(clrList.GetEnumerator());

        var values = new List<double>();
        while (enumerator.MoveNext(out var value))
        {
            values.Add(value.DoubleValue);
        }

        Assert.Equal(new double[] { 10, 20, 30 }, values.ToArray());
    }

    [Fact]
    public void EnumerableElementEnumerable_Empty_ReturnsUndefined()
    {
        using var ctx = new JSContext();
        var clrList = new List<object>();
        var enumerator = new EnumerableElementEnumerable(clrList.GetEnumerator());

        var moved = enumerator.MoveNext(out var value);
        Assert.False(moved);
        Assert.True(value.IsUndefined);
    }

    [Fact]
    public void EnumerableElementEnumerable_MoveNextWithIndex_TracksIndex()
    {
        using var ctx = new JSContext();
        var clrList = new List<object> { 5, 15 };
        var enumerator = new EnumerableElementEnumerable(clrList.GetEnumerator());

        enumerator.MoveNext(out var hasValue1, out var value1, out var index1);
        Assert.True(hasValue1);
        Assert.Equal(0u, index1);
        Assert.Equal(5.0, value1.DoubleValue);

        enumerator.MoveNext(out var hasValue2, out var value2, out var index2);
        Assert.True(hasValue2);
        Assert.Equal(1u, index2);
        Assert.Equal(15.0, value2.DoubleValue);
    }

    [Fact]
    public void ClrObjectEnumerable_CanBeCreated()
    {
        using var ctx = new JSContext();
        // Verify that ClrObjectEnumerable<T> can be instantiated with a JSValue
        // (used for converting JS iterables to CLR IEnumerable<T>)
        var result = ctx.Eval("[1, 2, 3]");
        var enumerable = new ClrObjectEnumerable<object>(result);
        // Verify the struct was created successfully by checking we can get an enumerator
        var enumerator = enumerable.GetEnumerator();
        Assert.NotNull(enumerator);
    }

    [Fact]
    public void JavaScriptObject_TypeForwarding_TypeResolvesCorrectly()
    {
        // Verify that JavaScriptObject type resolves correctly from Runtime assembly
        var type = typeof(JavaScriptObject);
        Assert.NotNull(type);
        Assert.Equal("Broiler.JavaScript.Runtime", type.Assembly.GetName().Name);
        Assert.Equal("Broiler.JavaScript.Runtime", type.Namespace);
    }

    [Fact]
    public void ListElementEnumerator_TypeResolvesFromRuntimeAssembly()
    {
        var type = typeof(ListElementEnumerator);
        Assert.NotNull(type);
        Assert.Equal("Broiler.JavaScript.Runtime", type.Assembly.GetName().Name);
        Assert.Equal("Broiler.JavaScript.Runtime", type.Namespace);
    }

    [Fact]
    public void EnumerableElementEnumerable_TypeResolvesFromRuntimeAssembly()
    {
        var type = typeof(EnumerableElementEnumerable);
        Assert.NotNull(type);
        Assert.Equal("Broiler.JavaScript.Runtime", type.Assembly.GetName().Name);
        Assert.Equal("Broiler.JavaScript.Runtime", type.Namespace);
    }

    [Fact]
    public void ClrObjectEnumerator_TypeResolvesFromRuntimeAssembly()
    {
        var type = typeof(ClrObjectEnumerator<>);
        Assert.NotNull(type);
        Assert.Equal("Broiler.JavaScript.Runtime", type.Assembly.GetName().Name);
    }

    [Fact]
    public void ClrObjectEnumerable_TypeResolvesFromRuntimeAssembly()
    {
        var type = typeof(ClrObjectEnumerable<>);
        Assert.NotNull(type);
        Assert.Equal("Broiler.JavaScript.Runtime", type.Assembly.GetName().Name);
    }
}
