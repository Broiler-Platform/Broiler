using System.Collections.Generic;

namespace Broiler.JavaScript.Runtime;

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
