using Broiler.JavaScript.Core.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.Core.Enumerators;

public readonly struct ListElementEnumerator(List<JSValue>.Enumerator en) : IElementEnumerator
{
    public bool MoveNext(out bool hasValue, out JSValue value, out uint index)
    {
        if (en.MoveNext())
        {
            hasValue = true;
            index = 0;
            value = en.Current;
            return true;
        }

        hasValue = false;
        index = 0;
        value = null;
        return false;
    }

    public bool MoveNext(out JSValue value)
    {
        if (en.MoveNext())
        {
            value = en.Current;
            return true;
        }

        value = null;
        return false;
    }

    public bool MoveNextOrDefault(out JSValue value, JSValue @default)
    {
        if (en.MoveNext())
        {
            value = en.Current;
            return true;
        }

        value = @default;
        return false;
    }

    public JSValue NextOrDefault(JSValue @default)
    {
        if (en.MoveNext())
            return en.Current;

        return @default;
    }
}

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
                Current = (T)v;

            throw JSEngine.NewTypeError($"Failed to convert {c} to type {typeof(T).Name}");
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

public struct EnumerableElementEnumerable(IEnumerator en) : IElementEnumerator
{
    uint index = uint.MaxValue;

    public bool MoveNext(out bool hasValue, out JSValue value, out uint index)
    {
        if (en.MoveNext())
        {
            value = JSEngine.ClrInterop.Marshal(en.Current);
            this.index = this.index == uint.MaxValue ? 0 : this.index + 1;
            index = this.index;
            hasValue = true;
            return true;
        }

        value = JSUndefined.Value;
        index = this.index;
        hasValue = false;
        return false;
    }

    public bool MoveNext(out JSValue value)
    {
        if (en.MoveNext())
        {
            value = JSEngine.ClrInterop.Marshal(en.Current);
            index = index == uint.MaxValue ? 0 : index + 1;
            return true;
        }

        value = JSUndefined.Value;
        return false;
    }

    public bool MoveNextOrDefault(out JSValue value, JSValue @default)
    {
        if (en.MoveNext())
        {
            value = JSEngine.ClrInterop.Marshal(en.Current);
            index = index == uint.MaxValue ? 0 : index + 1;
            return true;
        }

        value = @default;
        return false;
    }

    public JSValue NextOrDefault(JSValue @default)
    {
        if (en.MoveNext())
        {
            index = index == uint.MaxValue ? 0 : index + 1;
            return JSEngine.ClrInterop.Marshal(en.Current);
        }

        return @default;
    }
}
