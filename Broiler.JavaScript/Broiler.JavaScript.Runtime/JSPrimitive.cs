using Broiler.JavaScript.Storage;

namespace Broiler.JavaScript.Runtime;

/// <summary>
/// JSPrimitive class does not hold prototype, prototype is only resolved from
/// current context when requested first time
/// 
/// Boolean, Number, Integer are derived from JSPrimitive
/// </summary>
public abstract class JSPrimitive: JSValue
{
    internal protected void ResolvePrototype() { 
        if (prototypeChain == null)
        {
            BasePrototypeObject = GetPrototype();
        }
    }

    protected abstract JSValue GetPrototype();

    protected JSPrimitive() : base(null)
    {

    }

    protected JSPrimitive(JSValue prototype): base(prototype)
    {

    }

    public override JSValue this[IJSSymbol symbol] {
        get
        {
            ResolvePrototype();
            if (prototypeChain == null)
                return UndefinedValue;
            var px = prototypeChain.GetInternalProperty(symbol);
            if (px.IsEmpty)
            {
                // throw JSEngine.Current.NewTypeError($"{name} property not found on {this.GetType().Name}:{this}");
                return UndefinedValue;
            }
            return this.GetValue(px);
        }
        set => base[symbol] = value;
    }

    public override JSValue this[KeyString name]
    {
        get
        {
            ResolvePrototype();
            if (prototypeChain == null)
                return UndefinedValue;
            var px = prototypeChain.GetInternalProperty(name);
            if (px.IsEmpty)
            {
                // throw JSEngine.Current.NewTypeError($"{name} property not found on {this.GetType().Name}:{this}");
                return UndefinedValue;
            }
            return this.GetValue(px);
        }
        set
        {
            // throw new NotSupportedException();
        }
    }

    public override IElementEnumerator GetAllKeys(bool showEnumerableOnly = true, bool inherited = true)
    {
        ResolvePrototype();
        return base.GetAllKeys(showEnumerableOnly, inherited);
    }

    internal override JSFunctionDelegate GetMethod(in KeyString key)
    {
        if(prototypeChain == null)
        {
            BasePrototypeObject = GetPrototype();
        }
        return prototypeChain?.GetMethod(key);
    }

    public override JSValue GetPrototypeOf()
    {
        ResolvePrototype();
        return base.GetPrototypeOf();
    }
}
