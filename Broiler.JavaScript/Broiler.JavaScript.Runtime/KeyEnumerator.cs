using Broiler.JavaScript.Storage;

namespace Broiler.JavaScript.Runtime;

public class PropertyEnumerator
{
    readonly JSObject target;
    readonly bool showEnumerableOnly;
    readonly bool inherited;
    private PropertyEnumerator parent;
    PropertyValueEnumerator properties;

    public PropertyEnumerator(JSObject jSObject, bool showEnumerableOnly, bool inherited)
    {
        target = jSObject;
        ref var op = ref jSObject.GetOwnProperties(false);
        properties = !op.IsEmpty ? new PropertyValueEnumerator(jSObject, showEnumerableOnly) : new PropertyValueEnumerator();
        this.showEnumerableOnly = showEnumerableOnly;
        this.inherited = inherited;
        parent = null;
    }

    public bool MoveNextProperty(out KeyString key, out JSProperty value)
    {
        if (properties.target != null)
        {
            if (properties.MoveNextProperty(out value, out key))
                return true;

            properties.target = null;

            if (inherited)
            {
                var @base = (target.prototypeChain as IJSPrototype)?.Object as JSObject;
                if (@base != null && @base != target)
                    parent = new PropertyEnumerator(@base, showEnumerableOnly, inherited);
            }
        }

        if (parent != null)
        {
            if (parent.MoveNextProperty(out key, out value))
                return true;

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
                return true;

            properties.target = null;
            
            if (inherited)
            {
                var @base = (target.prototypeChain as IJSPrototype)?.Object as JSObject;
                if (@base != null && @base != target)
                    parent = new PropertyEnumerator(@base, showEnumerableOnly, inherited);
            }
        }

        if (parent != null)
        {
            if (parent.MoveNext(out key, out value))
                return true;

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
    PropertyValueEnumerator properties = new PropertyValueEnumerator(jSObject, showEnumerableOnly);

    public bool MoveNext(out bool hasValue, out JSValue value, out uint index)
    {
        if (elements != null)
        {
            if (elements.MoveNext(out var hasValueout, out var _, out var ui))
            {
                value = JSValue.CreateString(ui.ToString());
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
                value = JSObjectCoreExtensions.KeyStringToJSValue(key);
                hasValue = true;
                index = 0;
                return true;
            }

            properties.target = null;

            if (inherited)
            {
                var @base = (jSObject.prototypeChain as IJSPrototype)?.Object as JSObject;
                if (@base != null && @base != jSObject)
                    parent = new KeyEnumerator(@base, showEnumerableOnly, inherited);
            }
        }

        if (parent != null)
        {
            if (parent.MoveNext(out hasValue, out value, out index))
                return true;

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
                value = JSValue.CreateString(ui.ToString());
                return true;
            }

            elements = null;
        }
        
        if (properties.target != null)
        {
            if (properties.MoveNext(out var key))
            {
                value = JSObjectCoreExtensions.KeyStringToJSValue(key);
                return true;
            }

            properties.target = null;

            if (inherited)
            {
                var @base = (jSObject.prototypeChain as IJSPrototype)?.Object as JSObject;
                if (@base != null && @base != jSObject)
                    parent = new KeyEnumerator(@base, showEnumerableOnly, inherited);
            }
        }

        if (parent != null)
        {
            if (parent.MoveNext(out value))
                return true;

            parent = null;
        }

        value = JSValue.UndefinedValue;
        return false;
    }

    public bool MoveNextOrDefault(out JSValue value, JSValue @default)
    {
        if (elements != null)
        {
            if (elements.MoveNext(out _, out _, out var ui))
            {
                value = JSValue.CreateString(ui.ToString());
                return true;
            }

            elements = null;
        }

        if (properties.target != null)
        {
            if (properties.MoveNext(out var key))
            {
                value = JSObjectCoreExtensions.KeyStringToJSValue(key);
                return true;
            }

            properties.target = null;

            if (inherited)
            {
                var @base = (jSObject.prototypeChain as IJSPrototype)?.Object as JSObject;
                if (@base != null && @base != jSObject)
                    parent = new KeyEnumerator(@base, showEnumerableOnly, inherited);
            }
        }

        if (parent != null)
        {
            if (parent.MoveNext(out value))
                return true;

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
                return JSValue.CreateString(ui.ToString());

            elements = null;
        }

        if (properties.target != null)
        {
            if (properties.MoveNext(out var key))
                return JSObjectCoreExtensions.KeyStringToJSValue(key);

            properties.target = null;

            if (inherited)
            {
                var @base = (jSObject.prototypeChain as IJSPrototype)?.Object as JSObject;
                if (@base != null && @base != jSObject)
                    parent = new KeyEnumerator(@base, showEnumerableOnly, inherited);
            }
        }

        if (parent != null)
        {
            if (parent.MoveNext(out var value))
                return value;

            parent = null;
        }

        return @default;
    }
}
