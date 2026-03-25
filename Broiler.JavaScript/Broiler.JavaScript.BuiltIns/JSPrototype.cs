using Broiler.JavaScript.ExpressionCompiler.Core;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.BuiltIns;


public class JSPrototype : IJSPrototype
{
    public class JSPropertySet
    {
        public SAUint32Map<(JSProperty property, JSPrototype owner)> properties;
        public SAUint32Map<(JSProperty property, JSPrototype owner)> elements;
        public SAUint32Map<(JSProperty property, JSPrototype owner)> symbols;

        public Sequence<KeyString> stringKeys = [];
        public Sequence<uint> uintKeys = [];
    }

    public JSPropertySet propertySet;
    public readonly JSObject @object;
    private bool dirty = true;


    internal JSPrototype(JSObject @object)
    {
        this.@object = @object;
        Build();
    }

    private void Build()
    {
        if (!dirty)
            return;

        var ps = new JSPropertySet();
        lock (this)
        {
            if (!dirty)
                return;

            ps.properties = new SAUint32Map<(JSProperty, JSPrototype)>();
            ps.elements = new SAUint32Map<(JSProperty, JSPrototype)>();
            ps.symbols = new SAUint32Map<(JSProperty, JSPrototype)>();

            Build(ps, this);

            dirty = false;
            propertySet = ps;
        }
    }

    private void Build(JSPropertySet ps, JSPrototype target)
    {
        // first build the base class for correct inheritance...

        var @object = target.@object;

        var @base = @object.prototypeChain;
        if (@base is JSPrototype baseProto && baseProto != this)
            Build(ps, baseProto);

        // if it is registered, remove it first
        @object.PropertyChanged -= @object_PropertyChanged;

        @object.PropertyChanged += @object_PropertyChanged;
        ref var objectProperties = ref @object.GetOwnProperties(false);
        var ve = objectProperties.GetEnumerator(false);
        
        while(ve.MoveNext(out var key, out var value))
            ps.properties.Put(key.Key) = (value.ToNotReadOnly(),target);
        

        ref var objectElements = ref @object.GetElements(false);
        if (!objectElements.IsNull)
        {
            foreach(var e in objectElements.AllValues())
            {
                if (!e.Value.IsEmpty)
                    ps.elements.Put(e.Key) = (e.Value.ToNotReadOnly(), target);
            }
        }

        ref var objectSymbols = ref @object.GetSymbols();
        if(!objectSymbols.IsNull)
        {
            foreach(var e in objectSymbols.AllValues())
            {
                if (!e.Value.IsEmpty)
                    ps.symbols.Put(e.Key) = (e.Value.ToNotReadOnly(), target);
            }
        }
    }

    JSValue IJSPrototype.Object => @object;
    JSProperty IJSPrototype.GetInternalProperty(IJSSymbol symbol) => GetInternalProperty(symbol);
    JSProperty IJSPrototype.GetInternalProperty(in KeyString name) => GetInternalProperty(name);
    JSProperty IJSPrototype.GetInternalProperty(uint name) => GetInternalProperty(name);
    JSFunctionDelegate IJSPrototype.GetMethod(in KeyString key) => GetMethod(key);
    void IJSPrototype.Dirty() => Dirty();
    bool IJSPrototype.TryRemove(uint i, out JSProperty p) => TryRemove(i, out p);

    public void Dirty() => dirty = true;

    private void @object_PropertyChanged(JSObject sender, (uint keyString, uint index, IJSSymbol symbol) index) => dirty = true;

    public JSProperty GetInternalProperty(in KeyString name)
    {
        Build();
        
        var (p, _) = propertySet.properties[name.Key];
        return p;
    }

    public JSProperty GetInternalProperty(uint name)
    {
        Build();
        return propertySet.elements[name].property;
    }

    public JSProperty GetInternalProperty(IJSSymbol symbol)
    {
        Build();
        return propertySet.symbols[symbol.Key].property;
    }

    public JSFunctionDelegate GetMethod(in KeyString key)
    {
        Build();
        var (p, _) = propertySet.properties[key.Key];
        
        if(p.IsValue)
        {
            if (p.get is IJSFunction valueGetter)
                return valueGetter.Delegate;
        }
        
        if (p.IsProperty)
            return (p.get as IJSFunction)?.Delegate;
        
        return null;
    }

    public bool TryRemove(uint i, out JSProperty p)
    {
        if(propertySet.elements.TryGetValue(i, out var ee))
        {
            var @object = ee.owner.@object;
            ref var elements = ref @object.GetElements(false);
            return elements.TryRemove(i, out p);
        }

        p = JSProperty.Empty;
        return false;
    }

    public JSValue this[in KeyString k]
    {
        get
        {
            var p = GetInternalProperty(k);
            return @object.GetValue(p);
        }
    }
}
