using System;
using System.Collections;
using System.Collections.Generic;

namespace YantraJS.Core;

public struct SingleElementSequence<T>(T item) : IFastEnumerable<T>
{
    public readonly T this[int index] => index == 0 ? item : throw new IndexOutOfRangeException();

    public readonly int Count => 1;

    public readonly T First() => item;

    public readonly T FirstOrDefault() => item;

    readonly IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetFastEnumerator();

    public readonly IEnumerator<T> GetEnumerator() => new SingleSequenceEnumerator(item);

    public readonly SingleSequenceEnumerator GetFastEnumerator() => new(item);

    readonly IFastEnumerator<T> IFastEnumerable<T>.GetFastEnumerator() => GetFastEnumerator();

    public readonly T Last() => item;

    public readonly T LastOrDefault() => item;

    readonly IEnumerator IEnumerable.GetEnumerator() => GetFastEnumerator();

    public readonly bool Any() => true;

    public readonly T[] ToArray() => [item];

    public struct SingleSequenceEnumerator(T item) : IFastEnumerator<T>, IEnumerator<T>
    {
        private readonly T item = item;
        private bool done = false;

        public readonly T Current => item;

        readonly object IEnumerator.Current => item;

        public readonly void Dispose()
        {
            
        }

        public bool MoveNext(out T item)
        {
            if (done)
            {
                item = default;
                return false;
            }
            done = true;
            item = this.item;
            return true;
        }

        public bool MoveNext(out T item, out int index)
        {
            index = 0;
            if (done)
            {
                item = default;
                return false;
            }
            done = true;
            item = this.item;
            return true;
        }

        public bool MoveNext()
        {
            if (done)
            {
                return false;
            }
            done = true;
            return true;
        }

        public void Reset() => throw new NotImplementedException();
    }
}


