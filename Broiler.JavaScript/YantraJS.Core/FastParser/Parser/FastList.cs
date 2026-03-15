using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace YantraJS.Core.FastParser;

public class FastList<T>: IList<T>, IDisposable
{

    private T[] items = null;
    private int size = 0;
    private readonly FastPool pool;

    public FastList(FastPool pool) => this.pool = pool;

    public FastList(FastPool pool, int size)
    {
        this.pool = pool;
        SetCapacity(size);
    }


    public T this[int index] { get => items[index]; set => items[index] = value; }

    public int Count { get; private set; } = 0;

    public bool IsReadOnly => false;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetCapacity(int capacity)
    {
        if (size >= capacity)
        {
            return;
        }
        T[] release = null;
        if (size > 0)
        {
            release = items;
        }

        size = capacity;
        items = pool.AllocateArray<T>(size);
        size = items.Length;
        if (release!=null)
        {
            Array.Copy(release, 0, items, 0, release.Length);
            pool.ReleaseArray(release);
        }
    }

    public void Add(T item)
    {
        var i = Count++;
        SetCapacity(Count);
        items[i] = item;
    }

    public bool Any() => Count > 0;
    public void Clear()
    {
        if (items != null)
        {
            pool.ReleaseArray(items);

            items = null;
            Count = 0;
            size = 0;
        }
    }

    public T[] Release()
    {
        if(size == Count)
        {
            var r = items;
            items = null;
            size = 0;
            Count = 0;
            return r;
        }
        var copy = new T[Count];
        Array.Copy(items, copy, Count);
        return copy;
    }

    public ArraySpan<T> ToSpan()
    {
        var array = items;
        var length = Count;
        var a = new ArraySpan<T>(array, length);
        items = null;
        Count = 0;
        size = 0;
        return a;
    }

    public bool Contains(T item)
    {
        if (items == null)
            return false;
        foreach(var i in items)
        {
            if (Object.Equals(i, item))
                return true;
        }
        return false;
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        if (items == null)
            return;
        Array.Copy(items, 0, array, arrayIndex, Count);
    }

    public FastEnumerator GetEnumerator() => new(this);

    public ReverseEnumerator GetReverseEnumerator() => new(this);

    public struct ReverseEnumerator(FastList<T> list)
    {
        private readonly T[] items = list.items;
        private int index = list.Count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext(out T item)
        {
            if(--index >= 0)
            {
                item = items[index];
                return true;
            }
            item = default;
            return false;
        }
    }

    public struct FastEnumerator(FastList<T> list) : IEnumerator<T>
    {
        private readonly T[] items = list.items;
        private int index = -1;
        private readonly int length = list.Count;

        public readonly T Current => items[index];

        readonly object IEnumerator.Current => Current;

        public readonly void Dispose()
        {
            
        }

        public bool MoveNext() => ++index < length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext(out T item) {
            if(++index < length) {
                item = items[index];
                return true;
            }
            item = default;
            return false;
        }

        public readonly void Reset()
        {
            
        }
    }

    internal void AddRange(FastList<T> initList)
    {
        var e = initList.GetEnumerator();
        while (e.MoveNext(out var item))
            Add(item);
    }


    internal void AddRange(IEnumerable<T> initList)
    {
        foreach (var exp in initList)
            Add(exp);
    }

    public int IndexOf(T item)
    {
        int i = -1;
        if (items == null)
            return i;

        foreach (var e in items)
        {
            i++;
            if (Object.Equals(e, item))
                return i;
        }
        return -1;
    }

    public void Insert(int index, T item)
    {
        SetCapacity(Count + 1);
        Count++;
        for (int i = Count-1; i > index; i--)
        {
            items[i] = items[i - 1];
        }
        items[index] = item;
    }

    public bool Remove(T item)
    {
        int index = IndexOf(item);
        if (index != -1)
        {
            RemoveAt(index);
            return true;
        }
        return false;
    }

    public void RemoveAt(int index)
    {
        for (int i = index; i < Count-1; i++)
        {
            items[i] = items[i+1];
        }
        items[Count - 1] = default;
        Count--;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    public void Dispose() => Clear();
}
