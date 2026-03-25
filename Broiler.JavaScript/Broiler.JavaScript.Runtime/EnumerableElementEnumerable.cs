using System.Collections;

namespace Broiler.JavaScript.Runtime;

public struct EnumerableElementEnumerable(IEnumerator en) : IElementEnumerator
{
    uint index = uint.MaxValue;

    public bool MoveNext(out bool hasValue, out JSValue value, out uint index)
    {
        if (en.MoveNext())
        {
            value = JSValue.MarshalObject(en.Current);
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
            value = JSValue.MarshalObject(en.Current);
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
            value = JSValue.MarshalObject(en.Current);
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
            return JSValue.MarshalObject(en.Current);
        }

        return @default;
    }
}
