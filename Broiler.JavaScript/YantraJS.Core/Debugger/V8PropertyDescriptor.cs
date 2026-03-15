using System;
using YantraJS.Core.Core;
using YantraJS.Extensions;

namespace YantraJS.Core.Debugger;

public class V8PropertyDescriptor
{
    public V8PropertyDescriptor(JSPrototype prototypeChain)
    {
        Name = "__proto__";
        Enumerable = false;
        Configurable = true;
        IsOwn = true;
        Writable = true;
        Value = new V8RemoteObject(prototypeChain.@object);
    }

    public V8PropertyDescriptor(string name, JSValue v, in JSProperty p, bool isOwn = false)
    {
        Name = name;
        Writable = !p.IsReadOnly;
        Configurable = p.IsConfigurable;
        Enumerable = p.IsEnumerable;
        IsOwn = isOwn;
        if (!p.IsProperty)
        {
            try {
                Value = new V8RemoteObject(v.GetValue(p));
            } catch (Exception ex)
            {
                Value = new V8RemoteObject(ex.ToString());
                WasThrown = true;
            }
            return;
        }
        if (p.get != null)
            Get = new V8RemoteObject(p.get);
        if (p.set != null)
            Set = new V8RemoteObject(p.set);
    }

    public string Name { get; set; }
    public bool Writable { get; set; }
    public bool Configurable { get; set; }
    public bool Enumerable { get; set; }
    public bool IsOwn { get; set; }
    public V8RemoteObject Value { get; set; }
    public bool WasThrown { get; set; }
    public V8RemoteObject Get { get; set; }
    public V8RemoteObject Set { get; set; }
}
