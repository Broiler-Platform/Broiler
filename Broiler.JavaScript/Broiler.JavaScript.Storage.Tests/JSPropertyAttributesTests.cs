using Broiler.JavaScript.Storage;

namespace Broiler.JavaScript.Storage.Tests;

/// <summary>
/// Tests for <see cref="JSPropertyAttributes"/> flags enum, which was
/// extracted from Core into the Storage assembly.
/// </summary>
public class JSPropertyAttributesTests
{
    [Fact]
    public void Empty_IsZero()
    {
        Assert.Equal(0, (byte)JSPropertyAttributes.Empty);
    }

    [Fact]
    public void Value_IsSingleBit()
    {
        Assert.Equal(1, (byte)JSPropertyAttributes.Value);
    }

    [Fact]
    public void Property_IsSingleBit()
    {
        Assert.Equal(2, (byte)JSPropertyAttributes.Property);
    }

    [Fact]
    public void Configurable_IsSingleBit()
    {
        Assert.Equal(8, (byte)JSPropertyAttributes.Configurable);
    }

    [Fact]
    public void Enumerable_IsSingleBit()
    {
        Assert.Equal(16, (byte)JSPropertyAttributes.Enumerable);
    }

    [Fact]
    public void Readonly_IsSingleBit()
    {
        Assert.Equal(32, (byte)JSPropertyAttributes.Readonly);
    }

    [Theory]
    [InlineData(JSPropertyAttributes.EnumerableConfigurableValue,
                JSPropertyAttributes.Value | JSPropertyAttributes.Enumerable | JSPropertyAttributes.Configurable)]
    [InlineData(JSPropertyAttributes.EnumerableConfigurableReadonlyValue,
                JSPropertyAttributes.Value | JSPropertyAttributes.Enumerable | JSPropertyAttributes.Configurable | JSPropertyAttributes.Readonly)]
    [InlineData(JSPropertyAttributes.ConfigurableValue,
                JSPropertyAttributes.Value | JSPropertyAttributes.Configurable)]
    [InlineData(JSPropertyAttributes.ConfigurableReadonlyValue,
                JSPropertyAttributes.Value | JSPropertyAttributes.Configurable | JSPropertyAttributes.Readonly)]
    [InlineData(JSPropertyAttributes.EnumerableConfigurableProperty,
                JSPropertyAttributes.Property | JSPropertyAttributes.Enumerable | JSPropertyAttributes.Configurable)]
    [InlineData(JSPropertyAttributes.ConfigurableProperty,
                JSPropertyAttributes.Property | JSPropertyAttributes.Configurable)]
    [InlineData(JSPropertyAttributes.ReadonlyValue,
                JSPropertyAttributes.Readonly | JSPropertyAttributes.Value)]
    [InlineData(JSPropertyAttributes.ReadonlyProperty,
                JSPropertyAttributes.Readonly | JSPropertyAttributes.Property)]
    [InlineData(JSPropertyAttributes.EnumerableReadonlyValue,
                JSPropertyAttributes.Enumerable | JSPropertyAttributes.Readonly | JSPropertyAttributes.Value)]
    [InlineData(JSPropertyAttributes.EnumerableReadonlyProperty,
                JSPropertyAttributes.Enumerable | JSPropertyAttributes.Readonly | JSPropertyAttributes.Property)]
    public void ShortcutCombinations_MatchExpected(JSPropertyAttributes shortcut, JSPropertyAttributes expanded)
    {
        Assert.Equal(expanded, shortcut);
    }

    [Fact]
    public void HasFlag_Value_InCombination()
    {
        var attrs = JSPropertyAttributes.EnumerableConfigurableValue;

        Assert.True(attrs.HasFlag(JSPropertyAttributes.Value));
        Assert.True(attrs.HasFlag(JSPropertyAttributes.Enumerable));
        Assert.True(attrs.HasFlag(JSPropertyAttributes.Configurable));
        Assert.False(attrs.HasFlag(JSPropertyAttributes.Readonly));
        Assert.False(attrs.HasFlag(JSPropertyAttributes.Property));
    }

    [Fact]
    public void BitwiseAnd_CanTestReadonly()
    {
        var readonlyValue = JSPropertyAttributes.ReadonlyValue;
        Assert.True((readonlyValue & JSPropertyAttributes.Readonly) > 0);
        Assert.True((readonlyValue & JSPropertyAttributes.Value) > 0);
        Assert.False((readonlyValue & JSPropertyAttributes.Configurable) > 0);
    }

    [Fact]
    public void BitwiseNot_CanRemoveReadonly()
    {
        var original = JSPropertyAttributes.EnumerableConfigurableReadonlyValue;
        var cleared = original & (~JSPropertyAttributes.Readonly);

        Assert.Equal(JSPropertyAttributes.EnumerableConfigurableValue, cleared);
    }

    [Fact]
    public void Empty_IsDefault()
    {
        JSPropertyAttributes attrs = default;
        Assert.Equal(JSPropertyAttributes.Empty, attrs);
    }
}
