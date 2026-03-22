using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Primitive;

namespace Broiler.JavaScript.Core.Typed;

/// <summary>
/// A simple integer-key enumerator that yields sequential numbers from 0 to length-1.
/// Used by JSString, JSArrayPrototype, and JSTypedArray for key enumeration.
/// </summary>
public struct KeyEnumerator(int length) : IElementEnumerator
{
    private int index = -1;

    public bool MoveNext(out bool hasValue, out JSValue value, out uint index)
    {
        if (++this.index < length)
        {
            hasValue = true;
            index = (uint)this.index;
            value = new JSNumber(index);
            return true;
        }
        hasValue = false;
        index = 0;
        value = JSUndefined.Value;
        return false;
    }

    public bool MoveNext(out JSValue value)
    {
        if (++index < length)
        {
            value = new JSNumber(index);
            return true;
        }
        value = JSUndefined.Value;
        return false;
    }

    public bool MoveNextOrDefault(out JSValue value, JSValue @default)
    {
        if (++index < length)
        {
            value = new JSNumber(index);
            return true;
        }
        value = @default;
        return false;
    }

    public JSValue NextOrDefault(JSValue @default)
    {
        if (++index < length)
        {
            return new JSNumber(index);
        }
        return @default;
    }
}
