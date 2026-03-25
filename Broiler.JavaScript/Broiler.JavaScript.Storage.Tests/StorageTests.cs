using Broiler.JavaScript.Ast;
using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Storage;

namespace Broiler.JavaScript.Storage.Tests;

public class StorageTests
{
    [Fact]
    public void KeyStrings_GetOrCreate_ReturnsSameKeyForSameString()
    {
        var span1 = new StringSpan("testProp");
        var span2 = new StringSpan("testProp");
        var key1 = KeyStrings.GetOrCreate(span1);
        var key2 = KeyStrings.GetOrCreate(span2);
        Assert.Equal(key1.Key, key2.Key);
    }

    [Fact]
    public void KeyStrings_GetOrCreate_ReturnsDifferentKeysForDifferentStrings()
    {
        var key1 = KeyStrings.GetOrCreate(new StringSpan("alpha"));
        var key2 = KeyStrings.GetOrCreate(new StringSpan("beta"));
        Assert.NotEqual(key1.Key, key2.Key);
    }

    [Fact]
    public void VirtualMemory_Allocate_ReturnsNonEmptyArray()
    {
        var vm = new VirtualMemory<int>();
        var arr = vm.Allocate(5);
        Assert.False(arr.IsEmpty);
        Assert.Equal(5, arr.Length);
    }

    [Fact]
    public void VirtualMemory_Count_GrowsWithAllocations()
    {
        var vm = new VirtualMemory<int>();
        Assert.Equal(0, vm.Count);
        vm.Allocate(3);
        Assert.True(vm.Count >= 3);
        var countAfterFirst = vm.Count;
        vm.Allocate(2);
        Assert.True(vm.Count >= countAfterFirst);
    }

    [Fact]
    public void JSPropertyAttributes_FlagCombinations()
    {
        var attrs = JSPropertyAttributes.Value | JSPropertyAttributes.Enumerable | JSPropertyAttributes.Configurable;
        Assert.True(attrs.HasFlag(JSPropertyAttributes.Value));
        Assert.True(attrs.HasFlag(JSPropertyAttributes.Enumerable));
        Assert.True(attrs.HasFlag(JSPropertyAttributes.Configurable));
        Assert.False(attrs.HasFlag(JSPropertyAttributes.Readonly));
    }
}
