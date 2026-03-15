using System;
using System.Runtime.CompilerServices;

namespace YantraJS.Core.LightWeight;



public struct LightWeightStack<T>
{

    public struct StackWalker(T[] storage, int index)
    {
        public bool MoveNext()
        {
            index--;
            return index >= 0;
        }

        public readonly ref T Current => ref storage[index];
    }

    public readonly int Count => storage == null ? -1 : length;

    public readonly StackWalker Walker => new(storage, length);

    private T[] storage;
    private int length;

    public LightWeightStack(int size)
    {
        storage = new T[size];
        length = 0;
    }

    public LightWeightStack(in LightWeightStack<T> stack)
    {
        storage = new T[stack.storage.Length];
        Array.Copy(stack.storage, storage, storage.Length);
        length = stack.length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Push()
    {
        EnsureCapacity(length);
        var x = Activator.CreateInstance<T>();
        storage[length++] = x;
        return x;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Pop() => --length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ref T GetAt(int index) => ref storage[index];

    public readonly ref T Top => ref storage[length];

    void EnsureCapacity(int length)
    {
        if (length >= storage.Length)
        {
            Array.Resize(ref storage, length + 4);
        }
    }
    public void Dispose()
    {
        storage = null;
        length = 0;
    }

}
