using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using YantraJS.Extensions;
using Yantra.Core;
using YantraJS.Core.Clr;

namespace YantraJS.Core;

// [JSRuntime(typeof(JSArrayStatic), typeof(JSArrayPrototype))]
[JSBaseClass("Object")]
[JSFunctionGenerator("Array")]

public partial class JSArray: JSObject
{
    internal uint _length;

    //internal JSArray(JSObject prototype) : base(prototype)
    //{

    //}


    public JSArray() : base((JSObject)null)
    {

    }

    public JSArray(params JSValue[] items): this((IEnumerable<JSValue>)items)
    {

    }

    public JSArray(IElementEnumerator en): this()
    {
        ref var elements = ref GetElements(true);
        while (en.MoveNextOrDefault(out var v, JSUndefined.Value))
            elements.Put(_length++, v);
    }

    public JSArray(IEnumerable<JSValue> items): this()
    {
        ref var elements = ref GetElements(true);
        foreach (var item in items)
            elements.Put(_length++, item);
    }


    internal IElementEnumerator GetEntries() => new EntryEnumerator(this);

    public JSArray(uint count): this()
    {
        AllocateElements(count);
        CreateElements(count);
        _length = count;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        for(uint i =0; i<_length;i++)
        {
            if (i > 0)
                sb.Append(',');
            var item = this[i];
            if (item != null && !item.IsNullOrUndefined)
                sb.Append(item);
        }
        return sb.ToString();
    }

    public override string ToDetailString()
    {
        return $"[{ToString()}]"; ;
    }

    public override bool IsArray => true;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IEnumerable<(uint index, JSValue value)> GetArrayElements(bool withHoles = true)
    {
        var elements = GetElements();
        uint l = _length;
        for (uint i = 0; i < l; i++)
        {
            if(elements.TryGetValue(i, out var p))
            {
                yield return (i, p.value);
                continue;
            }
            if (withHoles)
                yield return (i, JSUndefined.Value);
        }
    }

    [JSExport("length")]
    public double ArrayLength
    {
        get => _length;
        set
        {
            if (IsSealedOrFrozen())
                throw JSContext.Current.NewTypeError("Cannot modify property length");
            var prev = _length;
            ref var elements = ref GetElements();
            double n = value;
            if (n < 0 || n > uint.MaxValue || double.IsNaN(n))
                throw JSContext.Current.NewRangeError("Invalid length");
            _length = (uint)n;
            if (prev > _length)
            {
                // remove.. 
                for (uint i = _length; i < prev; i++)
                {
                    elements.RemoveAt(i);
                }
            } else
            {
                elements.Resize(_length);
            }
        }
    }

    public override int Length { 
        get => (int)_length;
        set => ArrayLength = value;
    }


    public void Add(JSValue item)
    {
        if (item == null)
        {
            _length++;
        }
        else
        {
            ref var elements = ref CreateElements();
            elements.Put(_length++, item);
        }
        // return this;
    }

    //internal override bool TryRemove(uint i, out JSProperty p)
    //{
    //    ref var elements = ref GetElements();
    //    return elements.TryRemove(i, out p);
    //}

    public override IElementEnumerator GetElementEnumerator()
    {
        if(HasIterator)
        {
            var v = this.GetValue(GetSymbols()[JSSymbol.iterator.Key]);
            return v.InvokeFunction(Arguments.Empty).GetElementEnumerator();
        }
        return new ElementEnumerator(this);
    }


    private struct ElementEnumerator(JSArray array) : IElementEnumerator
    {
        uint length = array._length;
        uint index = uint.MaxValue;

        public bool MoveNext(out JSValue value)
        {
            if ((index = (index == uint.MaxValue) ? 0 : (index + 1)) < length)
            {
                value = array[index];
                return true;
            }
            value = JSUndefined.Value;
            return false;
        }

        public bool MoveNext(out bool hasValue, out JSValue value, out uint index)
        {
            ref var elements = ref array.GetElements();
            if((this.index = (this.index == uint.MaxValue) ? 0 : (this.index + 1)) < length)
            {
                index = this.index;
                if(elements.TryGetValue(index, out var property))
                {
                    value = property.IsEmpty 
                        ? null 
                        : (property.IsValue
                        ? property.value
                        : property.set.InvokeFunction(new Arguments(array)));
                    hasValue = true;
                } else
                {
                    hasValue = false;
                    value = JSUndefined.Value;
                }
                return true;
            }
            index = 0;
            value = JSUndefined.Value;
            hasValue = false;
            return false;
        }

        public bool MoveNextOrDefault(out JSValue value, JSValue @default)
        {
            ref var elements = ref array.GetElements();
            if ((index = (index == uint.MaxValue) ? 0 : (index + 1)) < length)
            {
                if (elements.TryGetValue(index, out var property))
                {
                    value = property.IsEmpty
                        ? null
                        : (property.IsValue
                        ? property.value
                        : property.set.InvokeFunction(new Arguments(array)));
                }
                else
                {
                    value = @default;
                }
                return true;
            }
            value = @default;
            return false;
        }

        public JSValue NextOrDefault(JSValue @default)
        {
            ref var elements = ref array.GetElements();
            if ((index = (index == uint.MaxValue) ? 0 : (index + 1)) < length)
            {
                if (elements.TryGetValue(index, out var property))
                {
                    return property.IsEmpty
                        ? null
                        : (property.IsValue
                        ? property.value
                        : property.set.InvokeFunction(new Arguments(array)));
                }
                return @default;
            }
            return @default;
        }


    }

    public void AddRange(JSValue iterator)
    {
        ref var et = ref CreateElements();
        // var et = this.elements;
        var el = _length;
        if (iterator is JSArray ary)
        {
            var l = ary._length;
            ref var e = ref ary.GetElements();
            for (uint i = 0; i < l; i++)
            {
                et.Put(el++, ary[i]);
            }
            _length = el;
            return;
        }

        var en = iterator.GetElementEnumerator();
        while (en.MoveNext(out var hasValue, out var item, out var  index))
        {
            if (hasValue)
            {
                et.Put(el++, item);
            } else
            {
                el++;
            }
        }
        _length = el;
        // return this;
    }

    internal protected override bool SetValue(uint name, JSValue value, JSValue receiver, bool throwError = true)
    {
        if(base.SetValue(name, value, receiver, throwError))
        {
            if (_length <= name)
            {
                _length = name + 1;
            }
            return true;
        }
        return false;
    }

    //IEnumerator IEnumerable.GetEnumerator()
    //{
    //    throw new NotImplementedException();
    //}
}


struct EntryEnumerator(JSArray typedArray) : IElementEnumerator
{
    private int index = -1;

    public bool MoveNext(out bool hasValue, out JSValue value, out uint index)
    {
        if (++this.index < typedArray.Length)
        {
            hasValue = true;
            index = (uint)this.index;
            value = new JSArray(new JSNumber(index), typedArray[index]);
            return true;
        }

        hasValue = false;
        index = 0;
        value = JSUndefined.Value;
        return false;
    }

    public bool MoveNext(out JSValue value)
    {
        if (++index < typedArray.Length)
        {
            value = new JSArray(new JSNumber(index), typedArray[(uint)index]);
            return true;
        }

        value = JSUndefined.Value;
        return false;
    }

    public bool MoveNextOrDefault(out JSValue value, JSValue @default)
    {
        if (++index < typedArray.Length)
        {
            value = new JSArray(new JSNumber(index), typedArray[(uint)index]);
            return true;
        }

        value = @default;
        return false;
    }

    public JSValue NextOrDefault(JSValue @default)
    {
        if (++index < typedArray.Length)
        {
            return new JSArray(new JSNumber(index), typedArray[(uint)index]);
        }
        return @default;
    }

}
