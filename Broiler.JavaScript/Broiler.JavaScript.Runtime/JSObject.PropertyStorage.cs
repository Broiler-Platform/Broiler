using Broiler.JavaScript.Storage;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
namespace Broiler.JavaScript.Runtime;

public partial class JSObject
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref PropertySequence GetOwnProperties(bool create = true) => ref ownProperties;

    public override JSValue GetOwnPropertyDescriptor(JSValue name)
    {
        var key = name.ToKey(false);

        switch (key.Type)
        {
            case KeyType.String:
                if (ownProperties.TryGetValue(key.KeyString.Key, out var p))
                    return JSObjectCoreExtensions.PropertyToJSValue(in p);
                return JSValue.UndefinedValue;

            case KeyType.UInt:
                if (elements.TryGetValue(key.Index, out var p1))
                    return JSObjectCoreExtensions.PropertyToJSValue(in p1);
                return JSValue.UndefinedValue;

            case KeyType.Symbol:
                if (symbols.TryGetValue(key.Symbol.Key, out var p3))
                    return JSObjectCoreExtensions.PropertyToJSValue(in p3);
                return JSValue.UndefinedValue;
        }

        return JSValue.UndefinedValue;
    }

    public override JSValue GetOwnProperty(in KeyString name)
    {
        ref var p = ref ownProperties.GetValue(name.Key);
        return this.GetValue(p);
    }

    public override JSValue GetOwnProperty(IJSSymbol name)
    {
        ref var p = ref symbols.GetRefOrDefault(name.Key, ref JSProperty.Empty);
        return this.GetValue(p);
    }

    public override JSValue GetOwnProperty(uint name)
    {
        ref var p = ref elements.Get(name);
        return this.GetValue(p);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref ElementArray GetElements(bool create = true) => ref elements;
    public ref SAUint32Map<JSProperty> GetSymbols() => ref symbols;

    internal void AllocateElements(uint size)
    {
        size = size > 1024 ? 1024 : size;
        elements.Resize(size);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref ElementArray CreateElements(uint size = 4) => ref elements;
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FastAddValue(uint index, JSValue value, JSPropertyAttributes attributes) => elements.Put(index, value, attributes);

    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FastAddProperty(uint index, JSValue getter, JSValue setter, JSPropertyAttributes attributes) => elements.Put(index) = new JSProperty(index, getter, setter, getter, attributes);

    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FastAddValue(KeyString key, JSValue value, JSPropertyAttributes attributes)
    {
        ref var pr = ref GetOwnProperties(true);
        pr.Put(key.Key) = new JSProperty(key.Key, value, attributes);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FastAddProperty(KeyString key, JSValue getter, JSValue setter, JSPropertyAttributes attributes)
    {
        ref var pr = ref GetOwnProperties(true);
        pr.Put(key.Key) = new JSProperty(key, getter, setter, attributes);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FastAddValue(IJSSymbol key, JSValue value, JSPropertyAttributes attributes)
    {
        ref var pr = ref GetSymbols();
        pr.Put(key.Key) = new JSProperty(key.Key, value, attributes);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FastAddProperty(IJSSymbol key, JSValue getter, JSValue setter, JSPropertyAttributes attributes)
    {
        ref var pr = ref GetSymbols();
        pr.Put(key.Key) = new JSProperty(key.Key, getter, setter, getter, attributes);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FastAddValue(JSValue key, JSValue value, JSPropertyAttributes attributes)
    {
        var k = key.ToKey(true);
        switch (k.Type)
        {
            case KeyType.String:
                FastAddValue(k.KeyString, value, attributes);
                return;

            case KeyType.UInt:
                FastAddValue(k.Index, value, attributes);
                return;

            default:
                FastAddValue(k.Symbol, value, attributes);
                return;
        }
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FastAddProperty(JSValue key, JSValue getter, JSValue setter, JSPropertyAttributes attributes)
    {
        var k = key.ToKey(true);
        switch (k.Type)
        {
            case KeyType.String:
                FastAddProperty(k.KeyString, getter, setter, attributes);
                return;

            case KeyType.UInt:
                FastAddProperty(k.Index, getter, setter, attributes);
                return;

            default:
                FastAddProperty(k.Symbol, getter, setter, attributes);
                return;
        }
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public void FastAddRange(JSValue value)
    {
        if (value is not JSObject target)
            return;

        var pe = target.ownProperties.GetEnumerator();
        while (pe.MoveNext(out var key, out var val) && !val.IsEmpty)
            ownProperties.Put(key.Key) = val.IsValue ? JSProperty.Property(val.value) : JSProperty.Property(target.GetValue(val));

        var en = target.elements.Length;
        for (uint i = 0; i < en; i++)
        {
            if (target.elements.TryGetValue(i, out var p) && !p.IsEmpty)
                elements.Put(i) = p.IsValue ? JSProperty.Property(p.value) : JSProperty.Property(target.GetValue(p));
        }

        foreach (var symbol in target.symbols.All)
        {
            var key = symbol.Key;
            var sv = symbol.Value;

            if (sv.IsEmpty)
                continue;

            symbols.Put(key) = sv.IsValue ? JSProperty.Property(sv.value) : JSProperty.Property(target.GetValue(sv));
        }
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public JSObject Merge(JSValue value)
    {
        if (value is not JSObject target)
            return this;

        var pe = new PropertyEnumerator(target, true, false);
        while (pe.MoveNext(out var key, out var val))
            this[key] = val;

        var en = new ElementEnumerator(target);
        while (en.MoveNext(out var hasValue, out var val, out var index))
        {
            if (hasValue)
                this[index] = val;
        }

        return this;
    }
    public override JSValue this[KeyString name]
    {
        get => GetValue(name, this);
        set => SetValue(name, value, null, true);
    }

    internal protected override bool SetValue(KeyString name, JSValue value, JSValue receiver, bool throwError = true)
    {
        var start = this;
        var p = GetInternalProperty(name, true);
        if (p.IsProperty)
        {
            if (p.set is IJSFunction setter)
            {
                setter.Delegate(new Arguments(receiver ?? this, value));
                return true;
            }

            if (throwError)
                throw NewTypeError($"Cannot modify property {name} of {this} which has only a getter");

            return false;
        }

        if (p.IsReadOnly)
        {
            if (throwError)
            {
                // Only in Strict Mode ..
                throw NewTypeError($"Cannot modify property {name} of {this}");
            }

            return false;
        }

        if (IsFrozen())
        {
            if (throwError)
                throw NewTypeError($"Cannot modify property {name} of {this}");

            return false;
        }

        if (p.IsEmpty && !IsExtensible())
        {
            if (throwError)
                throw NewTypeError($"Cannot add property {name} to {this}");

            return false;
        }

        ref var ownProperties = ref GetOwnProperties();
        ownProperties.Put(name, value, !p.IsEmpty ? p.Attributes : JSPropertyAttributes.EnumerableConfigurableValue);
        PropertyChanged?.Invoke(this, (name.Key, uint.MaxValue, null));
        return true;
    }

    public override JSValue this[uint name]
    {
        get => GetValue(name, this);
        set => SetValue(name, value, this, true);
    }

    public override bool SetValue(uint name, JSValue value, JSValue receiver, bool throwError = true)
    {
        var p = GetInternalProperty(name);
        if (p.IsProperty)
        {
            if (p.set is IJSFunction setter)
            {
                setter.Delegate(new Arguments(receiver ?? this, value));
                return true;
            }

            return false;
        }

        if (IsFrozen())
        {
            if (throwError)
                throw NewTypeError($"Cannot modify property {name} of {this}");

            return false;
        }

        ref var elements = ref CreateElements();
        elements.Put(name, value);
        PropertyChanged?.Invoke(this, (uint.MaxValue, name, null));
        return true;
    }

    public override JSValue this[IJSSymbol name]
    {
        get => GetValue(name, this);
        set => SetValue(name, value, null, true);
    }

    internal protected override bool SetValue(IJSSymbol name, JSValue value, JSValue receiver, bool throwError = true)
    {
        if (name.Key == JSValue.SymbolIterator.Key)
            HasIterator = true;

        var p = GetInternalProperty(name);
        if (p.IsProperty)
        {
            if (p.set is IJSFunction setter)
            {
                setter.Delegate(new Arguments(receiver ?? this, value));
                return true;
            }

            return false;
        }

        if (IsFrozen())
        {
            if (throwError)
                throw NewTypeError($"Cannot modify property {name} of {this}");

            return false;
        }

        symbols.Put(name.Key) = JSProperty.Property(value);
        PropertyChanged?.Invoke(this, (uint.MaxValue, uint.MaxValue, name));
        return true;
    }

    internal protected override JSValue GetValue(IJSSymbol key, JSValue receiver, bool throwError = true)
    {
        ref var p = ref symbols.GetRefOrDefault(key.Key, ref JSProperty.Empty);
        if (!p.IsEmpty)
            return (receiver ?? this).GetValue(p);

        return base.GetValue(key, receiver, throwError);
    }

    internal protected override JSValue GetValue(KeyString key, JSValue receiver, bool throwError = true)
    {
        ref var p = ref ownProperties.GetValue(key.Key);
        if (!p.IsEmpty)
            return (receiver ?? this).GetValue(p);

        return base.GetValue(key, receiver, throwError);
    }

    public override JSValue GetValue(uint key, JSValue receiver, bool throwError = true)
    {
        ref var p = ref elements.Get(key);
        if (!p.IsEmpty)
        {
            if (p.IsValue)
                return (JSValue)p.value;

            if (p.get is IJSFunction getter)
                return getter.InvokeFunction(new Arguments(receiver ?? this));

            return JSValue.UndefinedValue;
        }

        return base.GetValue(key, receiver, throwError);
    }

    public virtual JSValue DefineProperty(JSValue key, JSObject propertyDescription)
    {
        var k = key.ToKey();
        return k.Type switch
        {
            KeyType.Empty => JSValue.BooleanFalse,
            KeyType.UInt => DefineProperty(k.Index, propertyDescription),
            KeyType.String => DefineProperty(k.KeyString, propertyDescription),
            KeyType.Symbol => DefineProperty(k.Symbol, propertyDescription),
            _ => JSValue.BooleanFalse,
        };
    }

    public JSValue DefineProperty(IJSSymbol name, JSObject pd)
    {
        var key = name.Key;
        var old = symbols[key];
        if (!old.IsEmpty)
        {
            if (!old.IsConfigurable)
                throw new UnauthorizedAccessException();
        }

        symbols.Put(key) = pd.ToProperty(key);
        PropertyChanged?.Invoke(this, (uint.MaxValue, uint.MaxValue, name));
        return JSValue.UndefinedValue;
    }

    public JSValue DefineProperty(uint key, JSObject pd)
    {
        ref var elements = ref GetElements(true);
        var old = elements[key];
        if (!old.IsEmpty)
        {
            if (!old.IsConfigurable)
                throw new UnauthorizedAccessException();
        }

        elements.Put(key) = pd.ToProperty(key);
        this.UpdateArrayLengthIfNeeded(key);

        PropertyChanged?.Invoke(this, (uint.MaxValue, key, null));
        return JSValue.UndefinedValue;
    }

    public JSValue DefineProperty(in KeyString name, JSObject pd)
    {
        var key = name.Key;
        ref var ownProperties = ref GetOwnProperties();
        ref var old = ref ownProperties.GetValue(name.Key);

        if (!old.IsEmpty)
        {
            if (!old.IsConfigurable)
            {
                throw new UnauthorizedAccessException();
            }
        }
        // p.key = name;
        ownProperties.Put(key) = pd.ToProperty(key);
        PropertyChanged?.Invoke(this, (name.Key, uint.MaxValue, null));
        return JSValue.UndefinedValue;
    }
    public override IElementEnumerator GetAllKeys(bool showEnumerableOnly = true, bool inherited = true) => new KeyEnumerator(this, showEnumerableOnly, inherited);//var elements = this.elements;//if (elements != null)//{//    foreach (var (Key, Value) in elements.AllValues)//    {//        if (showEnumerableOnly)//        {//            if (!Value.IsEnumerable)//                continue;//        }//        yield return new JSNumber(Key);//    }//}//var ownProperties = this.ownProperties;//if (ownProperties != null)//{//    var en = new PropertySequence.Enumerator(ownProperties);//    while(en.MoveNext())//    {//        var p = en.Current;//        if (showEnumerableOnly)//        {//            if (!p.IsEnumerable)//                continue;//        }//        yield return p.ToJSValue();//    }//}//if (inherited)//{//    var @base = this.prototypeChain;//    if (@base != this && @base != null)//    {//        foreach (var i in @base.GetAllKeys(showEnumerableOnly))//            yield return i;//    }//}

    internal JSProperty ToProperty(uint key)
    {
        JSValue pget = null;
        JSValue pset = null;
        JSValue pvalue = null;
        var value = this[KeyStrings.value];
        var get = this[KeyStrings.get];
        var set = this[KeyStrings.set];
        var pt = JSPropertyAttributes.Empty;

        if (this[KeyStrings.configurable].BooleanValue)
            pt |= JSPropertyAttributes.Configurable;

        if (this[KeyStrings.enumerable].BooleanValue)
            pt |= JSPropertyAttributes.Enumerable;

        if (!this[KeyStrings.writable].BooleanValue)
            pt |= JSPropertyAttributes.Readonly;

        if (get is IJSFunction)
        {
            pt |= JSPropertyAttributes.Property;
            pget = get;
        }

        if (set is IJSFunction)
        {
            pt |= JSPropertyAttributes.Property;
            pset = set;
        }

        if (pget == null && pset == null)
        {
            pt |= JSPropertyAttributes.Value;
            pvalue = value;
        }

        var pAttributes = pt;
        return new JSProperty(key, pget, pset, pvalue, pAttributes);
    }

    public override JSValue Delete(in KeyString key)
    {
        if (IsSealedOrFrozen())
            throw NewTypeError($"Cannot delete property {key} of {this}");

        if (ownProperties.RemoveAt(key.Key))
        {
            PropertyChanged?.Invoke(this, (key.Key, uint.MaxValue, null));
            return JSValue.BooleanTrue;
        }

        return JSValue.BooleanTrue;
    }

    public override JSValue Delete(uint key)
    {
        if (IsSealedOrFrozen())
            throw NewTypeError($"Cannot delete property {key} of {this}");

        ref var element = ref elements.Get(key);

        if (elements.RemoveAt(key))
        {
            PropertyChanged?.Invoke(this, (uint.MaxValue, key, null));
            return JSValue.BooleanTrue;
        }

        return JSValue.BooleanTrue;
    }

    public override JSValue Delete(IJSSymbol symbol)
    {
        if (IsSealedOrFrozen())
            throw NewTypeError($"Cannot delete property {symbol} of {this}");

        if (symbols.RemoveAt(symbol.Key))
        {
            PropertyChanged?.Invoke(this, (uint.MaxValue, uint.MaxValue, symbol));
            return JSValue.BooleanTrue;
        }

        return JSValue.BooleanTrue;
    }
    internal override bool TryGetValue(uint i, out JSProperty value) => elements.TryGetValue(i, out value);

    internal override bool TryGetElement(uint i, out JSValue value)
    {
        if (elements.TryGetValue(i, out var p))
        {
            value = this.GetValue(p);
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Moves elements from `start` to `to`.
    /// </summary>
    /// <param name="start"></param>
    /// <param name="count"></param>
    /// <param name="to"></param>
    internal override void MoveElements(int start, int to)
    {
        ref var elements = ref CreateElements();

        var end = Length - 1;
        var diff = to - start;
        if (start > to)
        {

            for (uint i = (uint)start, j = (uint)to; i <= end; i++, j++)
            {
                if (TryRemove(i, out var p))
                    elements.Put(j) = p;
            }

            Length += diff;
            return;
        }
        else
        {
            for (int i = end, j = Length + diff - 1; i >= start; i--, j--)
            {
                if (TryRemove((uint)i, out var p))
                    elements.Put((uint)j) = p;
            }

            Length += diff;
        }

        PropertyChanged?.Invoke(this, (uint.MaxValue, uint.MaxValue, null));
    }

    /// <summary>
    /// Used in pop
    /// </summary>
    /// <param name="i"></param>
    /// <param name="p"></param>
    /// <returns></returns>
    internal override bool TryRemove(uint i, out JSProperty p)
    {
        if (elements.TryRemove(i, out p))
        {
            PropertyChanged?.Invoke(this, (uint.MaxValue, i, null));
            return true;
        }

        if (prototypeChain != null)
            return ((IJSPrototype)prototypeChain).TryRemove(i, out p);

        return false;
    }
    public override IElementEnumerator GetElementEnumerator()
    {
        if (HasIterator)
        {
            var v = this.GetValue(symbols[JSValue.SymbolIterator.Key]);
            return new JSIterator(v.InvokeFunction(new Arguments(this)));
        }

        return new ElementEnumerator(this);
    }

    private readonly struct ElementEnumerator(JSObject @object) : IElementEnumerator
    {
        readonly IEnumerator<(uint Key, JSProperty Value)> en = @object.elements.AllValues().GetEnumerator();

        public bool MoveNext(out bool hasValue, out JSValue value, out uint index)
        {
            if (en?.MoveNext() ?? false)
            {
                var (Key, Value) = en.Current;
                value = @object.GetValue(Value);
                index = Key;
                hasValue = true;
                return true;
            }

            hasValue = false;
            value = JSValue.UndefinedValue;
            index = 0;
            return false;
        }

        public bool MoveNext(out JSValue value)
        {
            if (en?.MoveNext() ?? false)
            {
                var (Key, Value) = en.Current;
                value = @object.GetValue(Value);
                return true;
            }

            value = JSValue.UndefinedValue;
            return false;
        }

        public bool MoveNextOrDefault(out JSValue value, JSValue @default)
        {
            if (en?.MoveNext() ?? false)
            {
                var (_, Value) = en.Current;
                value = @object.GetValue(Value);
                return true;
            }

            value = @default;
            return false;
        }

        public JSValue NextOrDefault(JSValue @default)
        {
            if (en?.MoveNext() ?? false)
            {
                var (Key, Value) = en.Current;
                return @object.GetValue(Value);
            }

            return @default;
        }
    }
}
