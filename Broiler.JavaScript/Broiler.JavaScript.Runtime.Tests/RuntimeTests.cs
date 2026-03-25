using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Storage;

namespace Broiler.JavaScript.Runtime.Tests;

public class RuntimeTests
{
    [Fact]
    public void JSUndefined_IsUndefined()
    {
        var undef = JSUndefined.Value;
        Assert.True(undef.IsUndefined);
        Assert.False(undef.IsNull);
    }

    [Fact]
    public void JSNull_IsNull()
    {
        var n = JSNull.Value;
        Assert.True(n.IsNull);
        Assert.False(n.IsUndefined);
    }

    [Fact]
    public void PropertyKey_FromInt_IsUInt()
    {
        PropertyKey key = 42;
        Assert.True(key.IsUInt);
        Assert.Equal(42u, key.Index);
    }

    [Fact]
    public void PropertyKey_FromString_IsKeyString()
    {
        var ks = KeyStrings.GetOrCreate(new StringSpan("prop"));
        PropertyKey key = ks;
        Assert.False(key.IsUInt);
        Assert.False(key.IsSymbol);
    }

    [Fact]
    public void GetValue_UintKey_NonFunctionGetter_ReturnsUndefined()
    {
        // Arrange: create a JSObject and add an accessor property (getter/setter descriptor)
        // at uint key 0 where the getter slot contains a JSString (not IJSFunction).
        var obj = new JSObject();
        var nonFunctionGetter = new JSString("not a function");
        obj.FastAddProperty(
            0u,
            nonFunctionGetter,
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // Act: should not throw InvalidCastException
        var result = obj.GetValue(0u, obj);

        // Assert: should return undefined since getter is not callable
        Assert.True(result.IsUndefined);
    }

    [Fact]
    public void GetValue_KeyString_NonFunctionGetter_ReturnsUndefined()
    {
        // Arrange: create a JSObject and add an accessor property (getter/setter descriptor)
        // at a KeyString key where the getter slot contains a JSString (not IJSFunction).
        var obj = new JSObject();
        var nonFunctionGetter = new JSString("not a function");
        var key = KeyStrings.GetOrCreate(new StringSpan("testProp"));
        obj.FastAddProperty(
            key,
            nonFunctionGetter,
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // Act: should not throw InvalidCastException (uses the KeyString indexer)
        var result = obj[key];

        // Assert: should return undefined since getter is not callable
        Assert.True(result.IsUndefined);
    }

    [Fact]
    public void SetValue_UintKey_NonFunctionSetter_DoesNotThrow()
    {
        // Arrange: create a JSObject and add an accessor property (getter/setter descriptor)
        // at uint key 0 where the setter slot contains a JSString (not IJSFunction).
        var obj = new JSObject();
        var nonFunctionSetter = new JSString("not a function");
        obj.FastAddProperty(
            0u,
            null,
            nonFunctionSetter,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // Act & Assert: should not throw InvalidCastException
        var result = obj.SetValue(0u, JSUndefined.Value, obj);

        // Setter is not a function, so SetValue should return false
        Assert.False(result);
    }
}
