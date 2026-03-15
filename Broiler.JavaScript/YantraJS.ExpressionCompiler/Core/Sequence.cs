using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace YantraJS.Core;


public class Sequence<T>: IReadOnlyList<T>, IFastEnumerable<T>
{

    public const int DefaultCapacity = 4;

    public static IFastEnumerable<T> Empty = new Sequence<T>();

    // public static implicit operator Sequence<T>(T[] items) => new Sequence<T>(items);


    class Node
    {
        internal T[] Items;
        internal Node Next;
    }

    private Node head;
    private Node tail;
    private T[] tailArray = Array.Empty<T>();
    private int tailCount;

    public T this[int index]
    {
        get {
            if (index >= Count)
                throw new IndexOutOfRangeException();
            var start = head;
            while(start != tail)
            {
                var len = start.Items.Length;
                if (index < len)
                {
                    return start.Items[index];
                }
                index -= len;
                start = start.Next;
            }
            return start.Items[index];
        } set {
            if (index >= Count)
                throw new IndexOutOfRangeException();
            var start = head;
            while (start != tail)
            {
                var len = start.Items.Length;
                if (index < len)
                {
                    start.Items[index] = value;
                    return;
                }
                index -= len;
                start = start.Next;
            }
            start.Items[index] = value;
        }
    }

    public string Description
    {
        get
        {
            var sb = new StringBuilder();
            var en = GetFastEnumerator();
            var isFirst = true;
            while(en.MoveNext(out var item))
            {
                if (!isFirst)
                    sb.Append(',');
                isFirst = false;
                sb.Append(item);
            }
            return sb.ToString();
        }
    }

    public Sequence()
    {
    }

    public Sequence(T[] items)
    {
        if (items.Length > 0)
        {
            tailArray = items;
            tailCount = items.Length;
            var t = new Node
            {
                Items = items,
            };
            Count = items.Length;
            head = t;
            tail = t;
        }
    }

    public Sequence(IEnumerable<T> items) => AddRange(items);

    public Sequence(IFastEnumerable<T> items)
    {
        var all = items.ToArray();
        if (all.Length > 0)
        {
            tailArray = all;
            tailCount = all.Length;
            var t = new Node
            {
                Items = all,
            };
            Count = all.Length;
            head = t;
            tail = t;
        }
    }

    public Sequence(int capacity)
    {
        if (capacity > 0)
        {
            tailArray = new T[capacity];
            head = new Node
            {
                Items = tailArray
            };
            tail = head;
            
        }
    }

    public string Join(string separator = ", ")
    {
        var sb = new StringBuilder();
        var en = new FastSequenceEnumerator(this);
        bool first = true;
        while(en.MoveNext(out var item))
        {
            if (!first)
            {
                sb.Append(separator);
            }
            first = false;
            sb.Append(item);
        }
        return sb.ToString();
    }

    public ref T AddGetRef()
    {
        if (tailCount < tailArray.Length)
        {
            Count++;
            return ref tailArray[tailCount++];
        }

        if (head == null)
        {
            tailArray = new T[DefaultCapacity];
            tailCount = 1;
            var t = new Node
            {
                Items = tailArray,
            };
            head = t;
            tail = t;
        }
        else
        {
            tailArray = new T[Count];
            tailCount = 1;
            var t = new Node
            {
                Items = tailArray,
            };
            tail.Next = t;
            tail = t;
        }
        Count++;
        return ref tailArray[0];
    }

    public void Insert(int i, T item)
    {
        Add(default);
        for (int index = Count - 2 ; index >= i; index--)
        {
            this[index + 1] = this[index];
        }
        this[i] = item;
    }

    public void Add(T item)
    {
        if (tailCount < tailArray.Length)
        {
            tailArray[tailCount++] = item;
            Count++;
            return;
        }

        if (head == null)
        {
            tailArray = new T[DefaultCapacity];
            tailArray[0] = item;
            tailCount = 1;
            var t = new Node
            {
                Items = tailArray,
            };
            head = t;
            tail = t;
        }
        else
        {
            tailArray = new T[Count];
            tailArray[0] = item;
            tailCount = 1;
            var t = new Node
            {
                Items = tailArray,
            };
            tail.Next = t;
            tail = t;
        }
        Count++;
    }

    public void AddRange(IEnumerable<T> range)
    {
        foreach(var item in range)
        {
            Add(item);
        }
    }

    public void AddRange(Sequence<T> range)
    {
        var en = range.GetFastEnumerator();
        while (en.MoveNext(out var item))
            Add(item);
    }

    public bool Any() => Count > 0;


    public int Count { get; private set; }

    public T First()
    {
        if(Count > 0)
        {
            return head.Items[0];
        }
        throw new IndexOutOfRangeException();
    }

    public T FirstOrDefault()
    {
        if (Count > 0)
        {
            return head.Items[0];
        }
        return default;
    }

    public T Last()
    {
        if (tailArray != null && tailCount > 0)
        {
            return tailArray[tailCount - 1];
        }
        throw new IndexOutOfRangeException();
    }

    public T LastOrDefault()
    {
        if (tailArray != null && tailCount > 0)
        {
            return tailArray[tailCount - 1];
        }
        return default;
    }

    public T FirstOrDefault(Func<T,bool> predicate)
    {
        var e = new FastSequenceEnumerator(this);
        while(e.MoveNext(out var item))
        {
            if (predicate(item))
                return item;
        }
        return default;
    }

    public T FirstOrDefault<T1>(T1 param, Func<T, T1, bool> predicate)
    {
        var e = new FastSequenceEnumerator(this);
        while (e.MoveNext(out var item))
        {
            if (predicate(item, param))
                return item;
        }
        return default;
    }

    public T[] ToArray()
    {
        if (Count == 0)
        {
            return Array.Empty<T>();
        }
        var items = new T[Count];
        var start = head;
        var last = tail;
        int index = 0;
        while (start != last)
        {
            Array.Copy(start.Items, 0, items, index, start.Items.Length);
            index += start.Items.Length;
            start = start.Next;
        }
        Array.Copy(tailArray, 0, items, index, tailCount);
        return items;
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => new FastSequenceEnumerator(this);

    //IEnumerator<T> IFastEnumerable<T>.GetEnumerator()
    //{
    //    return new FastSequenceEnumerator(this);
    //}

    IEnumerator IEnumerable.GetEnumerator() => GetFastEnumerator();

    public FastSequenceEnumerator GetEnumerator() => new(this);

    public FastSequenceEnumerator GetFastEnumerator() => new(this);


    IFastEnumerator<T> IFastEnumerable<T>.GetFastEnumerator() => new FastSequenceEnumerator(this);

    public struct FastSequenceEnumerator : IFastEnumerator<T>, IEnumerator<T>
    {
        private Node start;
        private readonly int max;
        private int position;
        private int current;

        internal FastSequenceEnumerator(Sequence<T> s)
        {
            start = s.head;
            max = s.Count;
            position = 0;
            current = 0;
            Current = default;
        }

        public T Current { get; private set; }

        readonly object IEnumerator.Current => Current;

        public readonly void Dispose()
        {
            
        }

        public bool MoveNext(out T item, out int index)
        {
            if (current >= max)
            {
                item = default;
                index = default;
                return false;
            }
            Current = start.Items[position++];
            item = Current;
            index = current++;
            if (position == start.Items.Length)
            {
                start = start.Next;
                position = 0;
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext(out T item)
        {
            if(current >= max)
            {
                item = default;
                return false;
            }
            Current = start.Items[position++];
            current++;
            item = Current;
            if (position == start.Items.Length)
            {
                start = start.Next;
                position = 0;
            }
            return true;
        }

        public bool MoveNext() => MoveNext(out var _);

        public void Reset() => throw new NotImplementedException();
    }
}


