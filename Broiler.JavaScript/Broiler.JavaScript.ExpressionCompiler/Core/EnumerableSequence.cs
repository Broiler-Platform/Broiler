using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace YantraJS.Core;

public struct EnumerableSequence<T>(IEnumerable<T> enumerable) : IFastEnumerable<T>
{
    public readonly T this[int index] => enumerable.ElementAt(index);

    public readonly int Count => enumerable.Count();

    public readonly T First() => enumerable.First();

    public readonly T FirstOrDefault() => enumerable.FirstOrDefault();

    public readonly IEnumerator<T> GetEnumerator() => enumerable.GetEnumerator();

    public readonly EnumerableEnumerator GetFastEnumerator() => new(enumerable.GetEnumerator());

    readonly IFastEnumerator<T> IFastEnumerable<T>.GetFastEnumerator() => new EnumerableEnumerator(enumerable.GetEnumerator());

    public readonly T Last() => enumerable.Last();

    public readonly T LastOrDefault() => enumerable.LastOrDefault();

    readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public readonly bool Any() => enumerable.Any();

    public readonly T[] ToArray() => enumerable.ToArray();

    public struct EnumerableEnumerator(IEnumerator<T> en) : IFastEnumerator<T>, IEnumerator<T>
    {
        private int index = 0;

        public readonly T Current => en.Current;

        readonly object IEnumerator.Current => en.Current;

        public readonly void Dispose()
        {
            
        }

        public readonly bool MoveNext(out T item)
        {
            if (en.MoveNext())
            {
                item = en.Current;
                return true;
            }
            item = default;
            return false;
        }

        public bool MoveNext(out T item, out int index)
        {
            if (en.MoveNext())
            {
                item = en.Current;
                index = this.index++;
                return true;
            }
            item = default;
            index = default;
            return false;

        }

        public readonly bool MoveNext() => en.MoveNext();

        public readonly void Reset()
        {
            
        }
    }
}


