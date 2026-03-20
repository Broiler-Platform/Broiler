using Broiler.JavaScript.Storage;

namespace Broiler.JavaScript.Storage.Tests;

/// <summary>
/// Tests for <see cref="VirtualMemory{T}"/> and <see cref="VirtualArray"/>.
/// </summary>
public class VirtualMemoryTests
{
    [Fact]
    public void NewVirtualMemory_IsEmpty()
    {
        var memory = new VirtualMemory<int>();
        Assert.True(memory.IsEmpty);
        Assert.Equal(0, memory.Count);
    }

    [Fact]
    public void Allocate_ReturnsNonEmptyArray()
    {
        var memory = new VirtualMemory<int>();
        var array = memory.Allocate(4);

        Assert.False(array.IsEmpty);
        Assert.Equal(4, array.Length);
        Assert.Equal(0, array.Offset);
    }

    [Fact]
    public void Allocate_MultipleBlocks_NonOverlapping()
    {
        var memory = new VirtualMemory<int>();
        var a1 = memory.Allocate(4);
        var a2 = memory.Allocate(8);

        Assert.Equal(0, a1.Offset);
        Assert.Equal(4, a1.Length);
        Assert.Equal(4, a2.Offset);
        Assert.Equal(8, a2.Length);
    }

    [Fact]
    public void Indexer_ReadWrite()
    {
        var memory = new VirtualMemory<int>();
        var array = memory.Allocate(3);

        memory[array, 0] = 10;
        memory[array, 1] = 20;
        memory[array, 2] = 30;

        Assert.Equal(10, memory[array, 0]);
        Assert.Equal(20, memory[array, 1]);
        Assert.Equal(30, memory[array, 2]);
    }

    [Fact]
    public void GetAt_ReturnsCorrectValue()
    {
        var memory = new VirtualMemory<string>();
        var array = memory.Allocate(2);

        memory[array, 0] = "hello";
        memory[array, 1] = "world";

        Assert.Equal("hello", memory.GetAt(0));
        Assert.Equal("world", memory.GetAt(1));
    }

    [Fact]
    public void SetCapacity_ExpandsMemory()
    {
        var memory = new VirtualMemory<int>();
        memory.SetCapacity(100);

        Assert.False(memory.IsEmpty);
        Assert.True(memory.Count >= 100);
    }

    [Fact]
    public void SetCapacity_ZeroOrNegative_NoOp()
    {
        var memory = new VirtualMemory<int>();
        memory.SetCapacity(0);
        Assert.True(memory.IsEmpty);

        memory.SetCapacity(-1);
        Assert.True(memory.IsEmpty);
    }

    [Fact]
    public void Allocate_LargeBlock_GrowsAutomatically()
    {
        var memory = new VirtualMemory<int>();
        var array = memory.Allocate(1000);

        Assert.Equal(1000, array.Length);
        Assert.False(memory.IsEmpty);
        Assert.True(memory.Count >= 1000);
    }

    [Fact]
    public void VirtualArray_EmptyWhenZeroLength()
    {
        var array = new VirtualArray(0, 0);
        Assert.True(array.IsEmpty);
    }

    [Fact]
    public void VirtualArray_NotEmptyWhenNonZeroLength()
    {
        var array = new VirtualArray(5, 3);
        Assert.False(array.IsEmpty);
        Assert.Equal(5, array.Offset);
        Assert.Equal(3, array.Length);
    }
}
