using System;
using System.Collections;
using System.Collections.Generic;

namespace Broiler.JavaScript.Runtime;

public struct ClrObjectEnumerator<T>(IElementEnumerator en) : IEnumerator<T>
{
    public T Current { get; private set; } = default;

    readonly object IEnumerator.Current => Current;

    public void Dispose()
    {
        // No-op: struct enumerator has no unmanaged resources to release.
    }

    public bool MoveNext()
    {
        if (en.MoveNext(out var c))
        {
            if (c.ConvertTo(typeof(T), out var v))
            {
                Current = (T)v;
                return true;
            }

            throw JSValue.NewTypeError($"Failed to convert {c} to type {typeof(T).Name}");
        }
        return false;
    }

    public void Reset() => throw new NotSupportedException();
}

public readonly struct ClrObjectEnumerable<T>(JSValue value) : IEnumerable<T>
{
    public IEnumerator<T> GetEnumerator() => new ClrObjectEnumerator<T>(value.GetElementEnumerator());
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
