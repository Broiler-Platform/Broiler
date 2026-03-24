using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Object;
using Broiler.JavaScript.Core.Utils;
using Broiler.JavaScript.ExpressionCompiler;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Storage;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Broiler.JavaScript.Core;


internal delegate void PropertyChangedEventHandler(JSObject sender, (uint keyString, uint index, IJSSymbol symbol) index);

[JSBaseClass("Object")]
[JSFunctionGenerator("Object", Register = false)]
public partial class JSObject : JSValue
{
    private JSPrototype currentPrototype;
    protected bool HasIterator = false;

    public override JSValue BasePrototypeObject
    {
        set
        {
            prototypeChain = (value as JSObject)?.PrototypeObject;
            PropertyChanged?.Invoke(this, (uint.MaxValue, uint.MaxValue, null));
            currentPrototype?.Dirty();
        }
    }

    internal JSPrototype PrototypeObject
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => currentPrototype ??= new JSPrototype(this);
    }

    internal void Dirty() => PropertyChanged?.Invoke(this, (uint.MaxValue, uint.MaxValue, null));

    internal ObjectStatus status = ObjectStatus.None;

    // internal long version = 0;
    internal event PropertyChangedEventHandler PropertyChanged;
    private ElementArray elements;
    private PropertySequence ownProperties;
    private SAUint32Map<JSProperty> symbols;
    private long? uid;

    private static long NextID = 0;
    internal long UniqueID => uid ??= Interlocked.Increment(ref NextID);

    public override bool BooleanValue => true;

    public override bool IsObject => true;


    public JSObject() : base(null) { }

    public virtual IEnumerable<(string Key, JSValue value)> Entries
    {
        get
        {
            var es = GetElementEnumerator();

            while (es.MoveNext(out var hasValue, out var value, out var index))
            {
                if (hasValue)
                    yield return (index.ToString(), value);
            }

            var ownProperties = GetOwnProperties();
            var en = new PropertyValueEnumerator(this, false);

            while (en.MoveNext(out var value, out var key))
                yield return (KeyStrings.GetNameString(key.Key).Value, value);
        }
    }

    public JSObject(IEnumerable<JSProperty> entries) : this(JSContext.Current?.ObjectPrototype)
    {
        foreach (var p in entries)
            ownProperties.Put(p.key) = p;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static JSObject NewWithProperties() => new() { };

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static JSObject NewWithElements() => new() { };

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static JSObject NewWithPropertiesAndElements() => new JSObject { };

    private bool toStringCalled = false;

    public override double DoubleValue
    {
        get
        {
            try
            {
                var fx = this[KeyStrings.valueOf];
                if (fx.IsUndefined)
                    return NumberParser.CoerceToNumber(ToString());

                var v = fx.InvokeFunction(new Arguments(this));
                if (v == this)
                    return double.NaN;

                return v.DoubleValue;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                return double.NaN;
            }
        }
    }

    public override int Length
    {
        get
        {
            try
            {
                ref var ownp = ref ownProperties;
                if (ownp.IsEmpty)
                    return -1;

                ref var l = ref ownp.GetValue(KeyStrings.length.Key);
                if (!l.IsEmpty)
                {
                    var n = this.GetValue(l);
                    var nvalue = ((uint)n.DoubleValue) >> 0;
                    return (int)nvalue;
                }

                return -1;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                return -1;
            }
        }
        set
        {
            if (IsSealedOrFrozenOrNonExtensible())
                throw JSContext.NewTypeError($"Cannot modify property length of {this}");

            ref var ownp = ref GetOwnProperties();
            ownp.Put(KeyStrings.length, JSValue.CreateNumber(value));
            PropertyChanged?.Invoke(this, (KeyStrings.length.Key, uint.MaxValue, null));
        }
    }
}
