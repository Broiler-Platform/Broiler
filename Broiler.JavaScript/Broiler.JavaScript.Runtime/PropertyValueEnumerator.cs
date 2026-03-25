using Broiler.JavaScript.Storage;

namespace Broiler.JavaScript.Runtime;

/// <summary>
/// Enumerator for iterating over property values in a <see cref="PropertySequence"/>.
/// This type was extracted from <see cref="PropertySequence"/> because it depends on
/// <see cref="JSObject"/> and <see cref="JSValue"/> (Core runtime types), while
/// <see cref="PropertySequence"/> itself lives in the Storage assembly.
/// </summary>
public struct PropertyValueEnumerator
{
    public JSObject target;
    private SAUint32Map<JSObjectProperty> map;
    private uint start;
    readonly bool showEnumerableOnly;

    public PropertyValueEnumerator(JSObject target, bool showEnumerableOnly)
    {
        this.showEnumerableOnly = showEnumerableOnly;
        this.target = target;
        ref var properties = ref target.GetOwnProperties();
        map = properties.GetMap();
        start = properties.Head;
    }

    public bool MoveNext(out KeyString key)
    {
        while (start > 0)
        {
            ref var objP = ref map.GetRefOrDefault(start, ref JSObjectProperty.Empty);
            ref var p = ref objP.Property;

            if (p.IsEmpty)
            {
                start = objP.Next;
                continue;
            }

            if (showEnumerableOnly && !p.IsEnumerable)
            {
                start = objP.Next;
                continue;
            }

            key = KeyStrings.GetName(start);
            start = objP.Next;
            return true;
        }

        key = KeyString.Empty;
        return false;
    }

    public bool MoveNext(out JSValue value, out KeyString key)
    {
        while (start > 0)
        {
            ref var objP = ref map.GetRefOrDefault(start, ref JSObjectProperty.Empty);
            ref var p = ref objP.Property;

            if (p.IsEmpty)
            {
                start = objP.Next;
                continue;
            }

            if (showEnumerableOnly && !p.IsEnumerable)
            {
                start = objP.Next;
                continue;
            }

            value = target.GetValue(in p);
            key = KeyStrings.GetName(start);
            start = objP.Next;
            return true;
        }

        value = null;
        key = KeyString.Empty;
        return false;
    }

    public bool MoveNextProperty(out JSProperty value, out KeyString key)
    {
        while (start > 0)
        {
            ref var objP = ref map.GetRefOrDefault(start, ref JSObjectProperty.Empty);
            ref var p = ref objP.Property;

            if (p.IsEmpty)
            {
                start = objP.Next;
                continue;
            }

            if (showEnumerableOnly && !p.IsEnumerable)
            {
                start = objP.Next;
                continue;
            }

            value = p;
            key = KeyStrings.GetName(start);
            start = objP.Next;
            return true;
        }

        value = JSProperty.Empty;
        key = KeyString.Empty;
        return false;
    }
}
