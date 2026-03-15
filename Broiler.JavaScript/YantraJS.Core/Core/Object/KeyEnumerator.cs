namespace YantraJS.Core.Enumerators;

public class PropertyEnumerator
{
    readonly JSObject target;
    readonly bool showEnumerableOnly;
    readonly bool inherited;
    private PropertyEnumerator parent;
    PropertySequence.ValueEnumerator properties;

    public PropertyEnumerator(JSObject jSObject, bool showEnumerableOnly, bool inherited)
    {
        target = jSObject;
        ref var op = ref jSObject.GetOwnProperties(false);
        properties = !op.IsEmpty
            ? new PropertySequence.ValueEnumerator(jSObject, showEnumerableOnly)
            : new PropertySequence.ValueEnumerator();
        this.showEnumerableOnly = showEnumerableOnly;
        this.inherited = inherited;
        parent = null;
    }

    public bool MoveNextProperty(out KeyString key, out JSProperty value)
    {
        if (properties.target != null)
        {
            if (properties.MoveNextProperty(out value, out key))
            {
                return true;
            }
            properties.target = null;
            if (inherited)
            {
                var @base = target.prototypeChain?.@object;
                if (@base != null
                    && @base != target)
                {
                    parent = new PropertyEnumerator(@base, showEnumerableOnly, inherited);
                }
            }
        }
        if (parent != null)
        {
            if (parent.MoveNextProperty(out key, out value))
            {
                return true;
            }
            parent = null;
        }
        key = KeyString.Empty;
        value = default;
        return false;
    }

    public bool MoveNext(out KeyString key, out JSValue value)
    {
        if (properties.target != null)
        {
            if (properties.MoveNext(out value, out key))
            {
                return true;
            }
            properties.target = null;
            if (inherited)
            {
                var @base = target.prototypeChain?.@object;
                if (@base != null 
                    && @base != target)
                {
                    parent = new PropertyEnumerator(@base, showEnumerableOnly, inherited);
                }
            }
        }
        if (parent != null)
        {
            if (parent.MoveNext(out key, out value))
            {
                return true;
            }
            parent = null;
        }
        key = KeyString.Empty;
        value = null;
        return false;
    }
}

public class KeyEnumerator(JSObject jSObject, bool showEnumerableOnly, bool inherited) : IElementEnumerator
{
    private KeyEnumerator parent = null;
    IElementEnumerator elements = jSObject.GetElementEnumerator();
    PropertySequence.ValueEnumerator properties = new PropertySequence.ValueEnumerator(jSObject, showEnumerableOnly);

    public bool MoveNext(out bool hasValue, out JSValue value, out uint index)
    {
        if (elements != null)
        {
            if (elements.MoveNext(out var hasValueout, out var _, out var ui))
            {
                value = new JSString(ui.ToString());
                hasValue = hasValueout;
                index = ui;
                return true;
            }
            elements = null;
        }
        if (properties.target != null)
        {
            if (properties.MoveNext(out var key))
            {
                value = key.ToJSValue();
                hasValue = true;
                index = 0;
                return true;
            }
            properties.target = null;
            if (inherited)
            {
                var @base = jSObject.prototypeChain?.@object;
                if (@base != null && @base != jSObject)
                {
                    parent = new KeyEnumerator(@base, showEnumerableOnly, inherited);
                }
            }
        }
        if (parent != null)
        {
            if (parent.MoveNext(out hasValue, out value, out index))
            {
                return true;
            }
            parent = null;
        }
        hasValue = false;
        value = null;
        index = 0;
        return false;
    }

    public bool MoveNext(out JSValue value)
    {
        if (elements != null)
        {
            if (elements.MoveNext(out var hasValueout, out var _, out var ui))
            {
                value = new JSString(ui.ToString());
                return true;
            }
            elements = null;
        }
        if (properties.target != null)
        {
            if (properties.MoveNext(out var key))
            {
                value = key.ToJSValue();
                return true;
            }
            properties.target = null;
            if (inherited)
            {
                var @base = jSObject.prototypeChain?.@object;
                if (@base != null && @base != jSObject)
                {
                    parent = new KeyEnumerator(@base, showEnumerableOnly, inherited);
                }
            }
        }
        if (parent != null)
        {
            if (parent.MoveNext(out value))
            {
                return true;
            }
            parent = null;
        }
        value = JSUndefined.Value;
        return false;
    }

    public bool MoveNextOrDefault(out JSValue value, JSValue @default)
    {
        if (elements != null)
        {
            if (elements.MoveNext(out var hasValueout, out var _, out var ui))
            {
                value = new JSString(ui.ToString());
                return true;
            }
            elements = null;
        }
        if (properties.target != null)
        {
            if (properties.MoveNext(out var key))
            {
                value = key.ToJSValue();
                return true;
            }
            properties.target = null;
            if (inherited)
            {
                var @base = jSObject.prototypeChain?.@object;
                if (@base != null && @base != jSObject)
                {
                    parent = new KeyEnumerator(@base, showEnumerableOnly, inherited);
                }
            }
        }
        if (parent != null)
        {
            if (parent.MoveNext(out value))
            {
                return true;
            }
            parent = null;
        }
        value = @default;
        return false;
    }

    public JSValue NextOrDefault(JSValue @default)
    {
        if (elements != null)
        {
            if (elements.MoveNext(out var hasValueout, out var _, out var ui))
            {
                return new JSString(ui.ToString());
            }
            elements = null;
        }
        if (properties.target != null)
        {
            if (properties.MoveNext(out var key))
            {
                return key.ToJSValue();
            }
            properties.target = null;
            if (inherited)
            {
                var @base = jSObject.prototypeChain?.@object;
                if (@base != null && @base != jSObject)
                {
                    parent = new KeyEnumerator(@base, showEnumerableOnly, inherited);
                }
            }
        }
        if (parent != null)
        {
            if (parent.MoveNext(out var value))
            {
                return value;
            }
            parent = null;
        }
        return @default;
    }
}
