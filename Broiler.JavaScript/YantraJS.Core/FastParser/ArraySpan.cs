using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace YantraJS.Core.FastParser;

public static class ArraySpanHelper
{

    public static ArraySpan<T> ToArraySpan<T>(this IList<T> items) {
        var a = items.ToArray();
        return new ArraySpan<T>(a, a.Length);
    }

    public static ArraySpan<T> ToArraySpan<T>(this T[] items) => new(items, items.Length);

    public static ArraySpan<T> ToArraySpan<T>(this T[] items, int length) => new(items, length);

}

public readonly struct ArraySpan<T>(T[] items, int length)
    : IEnumerable<T>
{
    public readonly int Length = length;

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Length;
    }

    public static ArraySpan<T> Empty;

    public ref T this[int index] {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get =>ref items[index];
    }

    public string Join(string separator = ", ")
    {
        var sb = new StringBuilder();
        for (int i = 0; i < Length; i++)
        {
            ref var item = ref this[i];
            if(i>0)
            {
                sb.Append(separator);
            }
            sb.Append(item);
        }
        return sb.ToString();
    }

    public static implicit operator ArraySpan<T>(FastList<T> source) => source?.ToSpan() ?? Empty;

    public static ArraySpan<T> From(params T[] items) => new(items, items.Length);

    public Enumerator GetEnumerator() => new(items, Length);

    IEnumerator IEnumerable.GetEnumerator() => new Enumerator(items, Length);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => new Enumerator(items, Length);

    public T FirstOrDefault()
    {
        if (Length == 0)
            return default;
        return items[0];
    }

    public T LastOrDefault()
    {
        if (Length == 0)
            return default;
        return items[Length-1];
    }


    public bool Any() => Length > 0;

    public T[] ToArray()
    {
        if (Length == items.Length)
            return items;
        var copy = new T[Length];
        Array.Copy(items, copy, Length);
        return copy;
    }

    public struct Enumerator(T[] items, int length) : IEnumerator<T>
    {
        private int index = -1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext(out T item)
        {
            if(++index < length )
            {
                item = items[index];
                return true;
            }
            item = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext(out T item, out int i) {
            if (++index < length) {
                i = index;
                item = items[index];
                return true;
            }
            i = -1;
            item = default;
            return false;
        }

        public readonly T Current => items[index];

        readonly object IEnumerator.Current => items[index];

        public readonly void Dispose()
        {
            
        }

        public bool MoveNext() => ++index < length;

        public void Reset() => index = -1;
    }

    internal void Copy(T[] copy, int start)
    {
        if (Length == 0)
            return;
        Array.Copy(items, 0, copy, start, Length);
    }
}
