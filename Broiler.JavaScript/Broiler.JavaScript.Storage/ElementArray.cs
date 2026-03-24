using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Broiler.JavaScript.Ast.Misc;

namespace Broiler.JavaScript.Storage;


public struct ElementArray
{
    private SAUint32Map<JSProperty> Storage;

    public uint Length { get; private set; }

    public void Put(uint index, IPropertyAccessor getter, IPropertyAccessor setter, JSPropertyAttributes attributes = JSPropertyAttributes.EnumerableConfigurableProperty) => Put(index) = JSProperty.Property(getter, setter, attributes);

    public void Put(uint index, IPropertyValue value, JSPropertyAttributes attributes = JSPropertyAttributes.EnumerableConfigurableValue) => Put(index) = JSProperty.Property(value, attributes);

    public ref JSProperty Put(uint index)
    {
        if(index >= Length)
            Length = index + 1;

        return ref Storage.Put(index);
    }

    public ref JSProperty Get(uint index) => ref Storage.GetRefOrDefault(index, ref JSProperty.Empty);

    public JSProperty this[uint index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ref var p = ref Storage.GetRefOrDefault(index, ref JSProperty.Empty);
            return p;
        }
    }

    public bool IsNull => Storage.IsNull;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(uint key, out JSProperty value) => Storage.TryGetValue(key, out value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRemove(uint key, out JSProperty value) => Storage.TryRemove(key, out value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool RemoveAt(uint key) => Storage.RemoveAt(key);
    public IEnumerable<(uint Key, JSProperty Value)> AllValues()
    {
        for (uint i = 0; i < Length; i++)
        {
            if (Storage.TryGetValue(i, out var v))
            {
                yield return (i, v);
            }
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasKey(uint key) => Storage.HasKey(key);

    public void Resize(uint size)
    {
        if (Length <= size)
        {
            Storage.Resize((int)size);
        }
    }

    public void QuickSort(Comparison<IPropertyValue> comparer, uint start, uint end)
    {
        if (end - start < 30)
        {
            // Insertion sort is faster than quick sort for small arrays.
            InsertionSort(comparer, start, end);
            return;
        }

        // Choose a random pivot.
        uint pivotIndex = start + (uint)(Random.Shared.NextDouble() * (end - start));

        // Get the pivot value.
        var pivotValue = this[pivotIndex];

        // Send the pivot to the back.
        Swap(pivotIndex, end);

        // Sweep all the low values to the front of the array and the high values to the back
        // of the array.  This version of quicksort never gets into an infinite loop even if
        // the comparer function is not consistent.
        uint newPivotIndex = start;
        for (uint i = start; i < end; i++)
        {
            if (comparer(this[i].value, pivotValue.value) <= 0)
            {
                Swap(i, newPivotIndex);
                newPivotIndex++;
            }
        }

        // Swap the pivot back to where it belongs.
        Swap(end, newPivotIndex);

        // Quick sort the array to the left of the pivot.
        if (newPivotIndex > start)
            QuickSort(comparer, start, newPivotIndex - 1);

        // Quick sort the array to the right of the pivot.
        if (newPivotIndex < end)
            QuickSort(comparer, newPivotIndex + 1, end);
    }

    /// <summary>
    /// Sorts an array using the insertion sort algorithm.
    /// </summary>
    /// <param name="comparer"> A comparison function. </param>
    /// <param name="start"> The first index in the range. </param>
    /// <param name="end"> The last index in the range. </param>
    private void InsertionSort(Comparison<IPropertyValue> comparer, uint start, uint end)
    {
        for (uint i = start + 1; i <= end; i++)
        {
            var value = this[i];
            uint j;
            for (j = i - 1; j > start && comparer(this[j].value, value.value) > 0; j--)
                Put(j + 1) = this[j];

            // Normally the for loop above would continue until j < start but since we are
            // using uint it doesn't work when start == 0.  Therefore the for loop stops one
            // short of start then the extra loop iteration runs below.
            if (j == start && comparer(this[j].value, value.value) > 0)
            {
                Put(j + 1) = this[j];
                j--;
            }

            Put(j + 1) = value;
        }
    }

    /// <summary>
    /// Swaps the elements at two locations in the array.
    /// </summary>
    /// <param name="index1"> The location of the first element. </param>
    /// <param name="index2"> The location of the second element. </param>
    private void Swap(uint index1, uint index2)
    {
        var temp = this[index1];
        Put(index1) = this[index2];
        Put(index2) = temp;
    }
}
