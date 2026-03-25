namespace Broiler.JavaScript.Runtime;

/// <summary>
/// A simple integer-key enumerator that yields sequential numbers from 0 to length-1.
/// Used by string values, JSArrayPrototype, and JSTypedArray for key enumeration.
/// </summary>
public struct IntKeyEnumerator(int length) : IElementEnumerator
{
    private int index = -1;

    public bool MoveNext(out bool hasValue, out JSValue value, out uint index)
    {
        if (++this.index < length)
        {
            hasValue = true;
            index = (uint)this.index;
            value = JSValue.CreateNumber(index);
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
            value = JSValue.CreateNumber(index);
            return true;
        }
        value = JSUndefined.Value;
        return false;
    }

    public bool MoveNextOrDefault(out JSValue value, JSValue @default)
    {
        if (++index < length)
        {
            value = JSValue.CreateNumber(index);
            return true;
        }
        value = @default;
        return false;
    }

    public JSValue NextOrDefault(JSValue @default)
    {
        if (++index < length)
        {
            return JSValue.CreateNumber(index);
        }
        return @default;
    }
}
