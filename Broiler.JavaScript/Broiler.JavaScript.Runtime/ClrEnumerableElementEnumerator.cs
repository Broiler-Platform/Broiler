using System.Collections.Generic;

namespace Broiler.JavaScript.Runtime;

public readonly struct ClrEnumerableElementEnumerator(in IEnumerable<JSValue> en) : IElementEnumerator
{
    private readonly IEnumerator<JSValue> en = en.GetEnumerator();

    public readonly bool MoveNext(out bool hasValue, out JSValue value, out uint index)
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

    public readonly bool MoveNext(out JSValue value)
    {
        if (en.MoveNext())
        {
            value = en.Current;
            return true;
        }

        value = null;
        return false;
    }

    public readonly bool MoveNextOrDefault(out JSValue value, JSValue @default)
    {
        if (en.MoveNext())
        {
            value = en.Current;
            return true;
        }

        value = @default;
        return false;
    }

    public readonly JSValue NextOrDefault(JSValue @default)
    {
        if (en.MoveNext())
            return en.Current;

        return @default;
    }
}
