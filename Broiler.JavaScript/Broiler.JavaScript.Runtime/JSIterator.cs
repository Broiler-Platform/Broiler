using Broiler.JavaScript.Storage;

namespace Broiler.JavaScript.Runtime;

public struct JSIterator(JSValue iterator) : IElementEnumerator
{
    private uint index = 0;

    public bool MoveNext(out bool hasValue, out JSValue value, out uint index)
    {
        value = JSObjectCoreExtensions.InvokeMethodOn(iterator, KeyStrings.next);
        var done = value[KeyStrings.done];
        value = value[KeyStrings.value];
        
        if (done.BooleanValue)
        {
            index = 0;
            hasValue = false;
            return false;
        }
        
        index = this.index++;
        hasValue = true;
        return true;
    }

    public readonly bool MoveNext(out JSValue value)
    {
        value = JSObjectCoreExtensions.InvokeMethodOn(iterator, KeyStrings.next);
        var done = value[KeyStrings.done];
        value = value[KeyStrings.value];
        
        if (done.BooleanValue)
            return false;

        return true;
    }

    public readonly bool MoveNextOrDefault(out JSValue value, JSValue @default)
    {
        value = JSObjectCoreExtensions.InvokeMethodOn(iterator, KeyStrings.next);
        var done = value[KeyStrings.done];

        if (done.BooleanValue)
        {
            value = @default;
            return false;
        }

        value = value[KeyStrings.value];
        return true;
    }

    public readonly JSValue NextOrDefault(JSValue @default)
    {
        var value = JSObjectCoreExtensions.InvokeMethodOn(iterator, KeyStrings.next);
        var done = value[KeyStrings.done];

        if (done.BooleanValue)
            return @default;

        return value[KeyStrings.value];
    }
}
