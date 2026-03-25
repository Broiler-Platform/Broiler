using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Storage;
using System;
using System.ComponentModel;
using System.Dynamic;
using System.Globalization;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Broiler.JavaScript.Runtime;

/// <summary>
/// Base class for all JavaScript values.  Every JS type (number, string,
/// boolean, object, function, symbol, null, undefined) derives from this
/// class and overrides the relevant virtual members.
/// </summary>
public abstract partial class JSValue : IDynamicMetaObjectProvider, IPropertyAccessor
{
    // ── Factory infrastructure ──
    // Initialized by Core's ModuleInitializer so that Runtime types can
    // create concrete JS values without a direct dependency on Core.
    // These statics prepare for a future move of JSValue to Runtime.
    internal static JSValue UndefinedValue;
    internal static JSValue NullValue;
    internal static JSValue BooleanTrue;
    internal static JSValue BooleanFalse;
    internal static JSValue NumberOne;
    internal static JSValue NumberNaN;
    internal static JSValue NumberZero;
    internal static JSValue NumberMinusOne;
    internal static JSValue NumberTwo;
    internal static JSValue NumberNegativeZero;
    internal static JSValue NumberPositiveInfinity;
    internal static JSValue NumberNegativeInfinity;
    internal static Func<double, JSValue> CreateNumber;
    internal static Func<double, bool> IsPositiveZeroCheck;
    internal static Func<double, bool> IsNegativeZeroCheck;
    internal static Func<string, JSValue> CreateString;

    /// <summary>
    /// Cached empty-string value.  Wired by the BuiltIns assembly.
    /// </summary>
    internal static JSValue EmptyString;

    /// <summary>
    /// Factory delegate for creating a <c>JSString</c> that already has
    /// a pre-computed <see cref="KeyString"/>.
    /// Wired by the BuiltIns assembly via <c>[ModuleInitializer]</c>.
    /// </summary>
    internal static Func<string, KeyString, JSValue> CreateStringWithKey;

    internal static Func<string, Exception> NewTypeError;
    internal static Func<object, JSValue> MarshalObject;
    internal static Func<JSValue, object, bool, object> ForceConvertHelper;
    internal static Func<Expression, JSValue, DynamicMetaObject> CreateDynamicMetaObject;
    internal static Func<double, string> NumberToECMAString;
    internal static Func<JSValue, IJSPrototype> CreatePrototypeObject;
    internal static Func<IPropertyAccessor, JSValue, JSValue> InvokePropertyGetter;

    /// <summary>
    /// Factory delegate for creating a <c>JSDecimal</c> from a <c>decimal</c> value.
    /// Wired by the BuiltIns assembly via <c>[ModuleInitializer]</c>.
    /// </summary>
    internal static Func<decimal, JSValue> CreateDecimalFactory;

    /// <summary>
    /// Factory delegate for creating a <c>JSDecimal</c> from a <c>string</c> value.
    /// Wired by the BuiltIns assembly via <c>[ModuleInitializer]</c>.
    /// Used by the Compiler for decimal literal compilation.
    /// </summary>
    public static Func<string, JSValue> CreateDecimalFromStringFactory;

    /// <summary>
    /// Creates a <c>JSDecimal</c> from a <c>decimal</c> value via the registered factory delegate.
    /// </summary>
    public static JSValue CreateDecimal(decimal value) => CreateDecimalFactory(value);

    /// <summary>
    /// Creates a <c>JSDecimal</c> from a <c>string</c> value via the registered factory delegate.
    /// Used by the Compiler for decimal literal compilation.
    /// </summary>
    public static JSValue CreateDecimalFromString(string value) => CreateDecimalFromStringFactory(value);

    /// <summary>
    /// Factory delegate for creating a <c>JSBigInt</c> from a <c>string</c> value.
    /// Wired by the BuiltIns assembly via <c>[ModuleInitializer]</c>.
    /// Used by the Compiler for BigInt literal compilation.
    /// </summary>
    public static Func<string, JSValue> CreateBigIntFromStringFactory;

    /// <summary>
    /// Factory delegate for creating a <c>JSBigInt</c> from a <c>long</c> value.
    /// Wired by the BuiltIns assembly via <c>[ModuleInitializer]</c>.
    /// Used by JSGlobal for timer IDs.
    /// </summary>
    internal static Func<long, JSValue> CreateBigIntFactory;

    /// <summary>
    /// Creates a <c>JSBigInt</c> from a <c>string</c> value via the registered factory delegate.
    /// Used by the Compiler for BigInt literal compilation.
    /// </summary>
    public static JSValue CreateBigIntFromString(string value) => CreateBigIntFromStringFactory(value);

    /// <summary>
    /// Creates a <c>JSBigInt</c> from a <c>long</c> value via the registered factory delegate.
    /// Used by JSGlobal for timer IDs.
    /// </summary>
    public static JSValue CreateBigInt(long value) => CreateBigIntFactory(value);

    /// <summary>
    /// Factory delegate for creating a <c>JSDate</c> from a <c>DateTimeOffset</c>.
    /// Wired by the BuiltIns assembly via <c>[ModuleInitializer]</c>.
    /// Used by Core and Clr for DateTime/DateTimeOffset marshaling.
    /// </summary>
    internal static Func<DateTimeOffset, JSValue> CreateDateFactory;

    /// <summary>
    /// Creates a <c>JSDate</c> from a <c>DateTimeOffset</c> via the registered factory delegate.
    /// </summary>
    public static JSValue CreateDate(DateTimeOffset value) => CreateDateFactory(value);

    /// <summary>
    /// Factory delegate for creating a <c>JSPromise</c> from a <c>Task&lt;JSValue&gt;</c>.
    /// Wired by the BuiltIns assembly via <c>[ModuleInitializer]</c>.
    /// Used by Clr for Task marshaling without referencing the concrete JSPromise type.
    /// </summary>
    internal static Func<Task<JSValue>, JSValue> CreatePromiseFromTask;

    /// <summary>
    /// Factory delegate for creating a <c>JSPromise</c> from a <c>Task</c> (non-generic).
    /// Wired by the BuiltIns assembly via <c>[ModuleInitializer]</c>.
    /// Used by Clr for Task marshaling without referencing the concrete JSPromise type.
    /// </summary>
    internal static Func<Task, JSValue> CreatePromiseFromUntypedTask;

    /// <summary>
    /// Factory delegate for creating a <c>JSPromise</c> from a generic <c>Task&lt;T&gt;</c>.
    /// Wired by the BuiltIns assembly via <c>[ModuleInitializer]</c>.
    /// </summary>
    internal static Func<Task, JSValue> CreatePromiseFromGenericTask;

    /// <summary>
    /// Factory delegate for creating a <c>JSFunction</c> from a delegate.
    /// Wired by the BuiltIns assembly via <c>[ModuleInitializer]</c>.
    /// </summary>
    internal static Func<JSFunctionDelegate, JSValue> CreateFunctionFactory;

    /// <summary>
    /// Factory delegate for creating a <c>JSFunction</c> with full parameters.
    /// Wired by the BuiltIns assembly via <c>[ModuleInitializer]</c>.
    /// </summary>
    internal static Func<JSFunctionDelegate, string, string, int, bool, JSValue> CreateFunctionFullFactory;

    /// <summary>
    /// Creates a <c>JSFunction</c> from a delegate via the registered factory.
    /// </summary>
    public static JSValue CreateFunction(JSFunctionDelegate f) => CreateFunctionFactory(f);

    /// <summary>
    /// Creates a <c>JSFunction</c> with full parameters via the registered factory.
    /// </summary>
    public static JSValue CreateFunction(JSFunctionDelegate f, string name, string source = null, int length = 0, bool createPrototype = true)
        => CreateFunctionFullFactory(f, name, source, length, createPrototype);

    /// <summary>
    /// Factory delegate for creating an empty <c>JSArray</c>.
    /// Wired by the BuiltIns assembly via <c>[ModuleInitializer]</c>.
    /// Used by Core when it needs to create arrays without referencing the concrete type.
    /// </summary>
    internal static Func<JSValue> CreateArrayFactory;

    /// <summary>
    /// Factory delegate for creating a <c>JSArray</c> with a specified length.
    /// Wired by the BuiltIns assembly via <c>[ModuleInitializer]</c>.
    /// </summary>
    internal static Func<uint, JSValue> CreateArrayWithLengthFactory;

    /// <summary>
    /// Creates an empty <c>JSArray</c> via the registered factory delegate.
    /// </summary>
    public static JSValue CreateArray() => CreateArrayFactory();

    /// <summary>
    /// Creates a <c>JSArray</c> with the specified length via the registered factory delegate.
    /// </summary>
    public static JSValue CreateArray(uint length) => CreateArrayWithLengthFactory(length);

    // ── JSSymbol factory infrastructure ──
    // Wired by the BuiltIns assembly's ModuleInitializer so that Core and
    // other assemblies can work with symbols without depending on the
    // concrete JSSymbol type.

    /// <summary>
    /// Well-known <c>Symbol.iterator</c> singleton.
    /// Wired by the BuiltIns assembly via <c>[ModuleInitializer]</c>.
    /// </summary>
    internal static IJSSymbol SymbolIterator;

    /// <summary>
    /// Well-known <c>Symbol.dispose</c> singleton.
    /// Wired by the BuiltIns assembly via <c>[ModuleInitializer]</c>.
    /// </summary>
    internal static IJSSymbol SymbolDispose;

    /// <summary>
    /// Well-known <c>Symbol.asyncDispose</c> singleton.
    /// Wired by the BuiltIns assembly via <c>[ModuleInitializer]</c>.
    /// </summary>
    internal static IJSSymbol SymbolAsyncDispose;

    /// <summary>
    /// Factory delegate for creating a new <c>JSSymbol</c> from a name string.
    /// Wired by the BuiltIns assembly via <c>[ModuleInitializer]</c>.
    /// </summary>
    internal static Func<string, JSValue> CreateSymbolFactory;

    /// <summary>
    /// Factory delegate for registering the <c>Symbol</c> constructor on a
    /// <see cref="JSContext"/>.  Mirrors <c>JSSymbol.CreateClass</c>.
    /// Wired by the BuiltIns assembly via <c>[ModuleInitializer]</c>.
    /// </summary>
    internal static Func<IJSContext, bool, JSValue> CreateSymbolClassFactory;

    /// <summary>
    /// Factory delegate for looking up a well-known symbol by name.
    /// Mirrors <c>JSSymbol.GlobalSymbol</c>.
    /// Wired by the BuiltIns assembly via <c>[ModuleInitializer]</c>.
    /// </summary>
    internal static Func<string, IJSSymbol> GetGlobalSymbolFactory;

    /// <summary>Gets whether this value is the <c>undefined</c> singleton.</summary>
    public bool IsUndefined
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this == UndefinedValue;
    }

    /// <summary>Gets whether this value is the <c>null</c> singleton.</summary>
    public bool IsNull
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this == NullValue;
    }

    public bool IsNullOrUndefined
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this == NullValue || this == UndefinedValue;
    }

    /// <summary>Gets whether this value is a JavaScript number.</summary>
    public virtual bool IsNumber => false;

    /// <summary>Gets whether this value is a JavaScript object (including arrays and functions).</summary>
    public virtual bool IsObject => false;

    /// <summary>Gets whether this value is a JavaScript <c>Symbol</c>.</summary>
    public virtual bool IsSymbol => false;

    /// <summary>Gets whether this value is a JavaScript <c>Array</c>.</summary>
    public virtual bool IsArray => false;

    /// <summary>
    /// Updates the internal array length when a numeric key is set.
    /// Overridden by <c>JSArray</c> in the BuiltIns assembly.
    /// </summary>
    internal virtual void UpdateArrayLengthIfNeeded(uint key) { }

    /// <summary>
    /// Appends an item to this array.
    /// Overridden by <c>JSArray</c> in the BuiltIns assembly.
    /// </summary>
    public virtual void AddArrayItem(JSValue item) { }

    /// <summary>Gets whether this value is a JavaScript string.</summary>
    public virtual bool IsString => false;

    /// <summary>Gets whether this value is a JavaScript boolean.</summary>
    public virtual bool IsBoolean => false;

    /// <summary>Gets whether this value is a JavaScript function.</summary>
    public virtual bool IsFunction => false;

    /// <summary>Gets whether this value is a JavaScript <c>Decimal</c> (ES2025 Decimal128).</summary>
    public virtual bool IsDecimal => false;

    /// <summary>Gets the underlying <c>decimal</c> value. Only valid when <see cref="IsDecimal"/> is <c>true</c>.</summary>
    public virtual decimal DecimalValue => throw new InvalidOperationException("Not a decimal value");

    internal virtual bool IsSpread => false;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public object Convert(Type type, object def)
    {
        if (type.IsAssignableFrom(typeof(JSValue)))
            return this;

        if (ConvertTo(type, out var v))
            return v;

        return def;
    }

    public object ForceConvert(Type type)
    {
        if (type.IsAssignableFrom(GetType()))
            return this;
        if (ConvertTo(type, out var value))
            return value;
        var result = ForceConvertHelper?.Invoke(this, type, false);
        if (result != null) return result;
        throw NewTypeError($"Cannot convert {this} to type {type.Name}");
    }

    internal bool TryConvertTo(Type type, out object value)
    {
        if (typeof(JSValue).IsAssignableFrom(type))
        {
            value = this;
            return true;
        }

        return ConvertTo(type, out value);
    }
    public virtual bool ConvertTo(Type type, out object value)
    {
        if (type == typeof(JSValue))
        {
            value = this;
            return true;
        }

        value = null;
        return false;
    }

    public bool CanBeNumber
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IsNumber || IsBoolean || IsNull;
    }

    public virtual int Length
    {
        get => 0;
        set { }
    }

    public virtual double DoubleValue => double.NaN;

    public abstract bool BooleanValue { get; }

    public virtual string StringValue => ToString();

    public abstract JSValue TypeOf();

    public virtual int IntValue => (int)(((long)DoubleValue << 32) >> 32);

    /// <summary>
    /// Integer value restricts value within int.MaxValue and
    /// more than int.MaxValue is returned as int.MaxValue
    /// </summary>
    public virtual int IntegerValue
    {
        get
        {
            var v = DoubleValue;
            if (v > 2147483647.0)
                return 2147483647;
#pragma warning disable 1718
            if (v != v)
                return 0;
#pragma warning restore 1718
            return (int)v;
        }
    }

    public virtual long BigIntValue => (long)(ulong)DoubleValue;

    public virtual uint UIntValue => (uint)(((long)DoubleValue << 32) >> 32);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public IJSPrototype prototypeChain;

    public virtual JSValue BasePrototypeObject
    {
        set => prototypeChain = CreatePrototypeObject?.Invoke(value);
    }


    /// <summary>
    /// Unless overriden, it returns self
    /// </summary>
    /// <returns></returns>
    public virtual JSValue ValueOf() => this;

    public virtual JSValue Negate() => CreateNumber(-DoubleValue);

    public virtual JSValue Subtract(JSValue value) => CreateNumber(DoubleValue - value.DoubleValue);

    public virtual JSValue Multiply(JSValue value) => CreateNumber(DoubleValue * value.DoubleValue);

    /// <summary>
    public virtual JSValue Divide(JSValue value) => CreateNumber(DoubleValue / value.DoubleValue);

    public virtual JSValue BitwiseAnd(JSValue value) => CreateNumber(IntValue & value.IntValue);

    public virtual JSValue BitwiseOr(JSValue value) => CreateNumber(IntValue | value.IntValue);

    public virtual JSValue BitwiseXor(JSValue value) => CreateNumber(IntValue ^ value.IntValue);

    public virtual JSValue LeftShift(JSValue value) => CreateNumber(IntValue << value.IntValue);

    public virtual JSValue RightShift(JSValue value) => CreateNumber(IntValue >> (value.IntValue & 0x1F));

    public virtual JSValue UnsignedRightShift(JSValue value) => CreateNumber(UIntValue >> value.IntValue);

    public virtual JSValue Modulo(JSValue value) => CreateNumber(DoubleValue % value.DoubleValue);

    /// Speed improvements for string contact operations
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public virtual JSValue AddValue(JSValue value)
    {
        var self = ValueOf();
        value = value.IsObject ? value.ValueOf() : value;

        if (self.CanBeNumber && value.CanBeNumber)
            return CreateNumber(self.DoubleValue + value.DoubleValue);

        if (value.ToString().Length == 0)
            return self.IsString ? self : CreateString(self.StringValue);

        return CreateString(self.StringValue + value.StringValue);
    }
    /// <summary>
    /// Speed improvements for string contact operations
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public virtual JSValue AddValue(double value)
    {
        var self = ValueOf();
        if (self.CanBeNumber)
            return CreateNumber(self.DoubleValue + value);

        return CreateString(self.StringValue + NumberToECMAString(value));
    }

    /// <summary>
    /// Speed improvements for string contact operations
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public virtual JSValue AddValue(string value)
    {
        var self = ValueOf();

        if (value.Length == 0)
            return self.IsString ? self : CreateString(self.StringValue);

        return CreateString(self.StringValue + value);
    }

    protected JSValue(JSValue prototype) => BasePrototypeObject = prototype ?? GetCurrentPrototype();

    protected virtual JSValue GetCurrentPrototype() => null;

    internal abstract PropertyKey ToKey(bool create = true);

    public virtual JSValue GetPrototypeOf() => prototypeChain?.Object ?? NullValue;

    public virtual void SetPrototypeOf(JSValue target)
    {
        if (target == NullValue)
        {
            BasePrototypeObject = null;
            return;
        }

        if (!target.IsObject)
            throw NewTypeError($"Prototype must be an object or null");

        BasePrototypeObject = target;
    }

    public virtual JSValue GetOwnPropertyDescriptor(JSValue name) => throw new NotImplementedException();

    /// <summary>
    /// Resolves a <see cref="JSProperty"/> to its runtime value, invoking
    /// getters via the <see cref="InvokePropertyGetter"/> factory delegate.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JSValue GetValue(in JSProperty p)
    {
        if (p.IsEmpty)
            return UndefinedValue;

        return !p.IsProperty ? (JSValue)p.value : InvokePropertyGetter(p.get, this);
    }

    public virtual JSValue GetOwnProperty(in KeyString name)
    {
        var pc = prototypeChain;

        if (pc != null)
            return this.GetValue(pc.GetInternalProperty(name));

        return UndefinedValue;
    }

    public virtual JSValue GetOwnProperty(uint name)
    {
        var pc = prototypeChain;

        if (pc != null)
            return this.GetValue(pc.GetInternalProperty(name));

        return UndefinedValue;
    }

    public virtual JSValue GetOwnProperty(IJSSymbol name)
    {
        var pc = prototypeChain;

        if (pc != null)
            return this.GetValue(pc.GetInternalProperty(name));

        return UndefinedValue;
    }

    public JSValue GetOwnProperty(JSValue name)
    {
        if (name is IJSSymbol symbol)
            return GetOwnProperty(symbol);

        var key = name.ToKey(false);

        if (key.IsUInt)
            return GetOwnProperty(key.Index);

        return GetOwnProperty(in key.KeyString);
    }

    public JSValue PropertyOrUndefined(in KeyString name)
    {
        if (this == NullValue || this == UndefinedValue)
            return UndefinedValue;

        return GetValue(name, this);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public JSValue PropertyOrUndefined(JSValue super, in KeyString name)
    {
        if (this == NullValue || this == UndefinedValue)
            return UndefinedValue;

        var pc = prototypeChain;

        if (pc == null)
            return UndefinedValue;

        return super.GetValue(name, this);
    }

    public JSValue PropertyOrUndefined(uint name)
    {
        if (this == NullValue || this == UndefinedValue)
            return UndefinedValue;

        return GetValue(name, this);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public JSValue PropertyOrUndefined(JSValue super, uint name)
    {
        if (this == NullValue || this == UndefinedValue)
            return UndefinedValue;

        var pc = prototypeChain;
        if (pc == null)
            return UndefinedValue;

        return super.GetValue(name, this);
    }

    public JSValue PropertyOrUndefined(IJSSymbol name)
    {
        if (this == NullValue || this == UndefinedValue)
            return UndefinedValue;

        return GetValue(name, this);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public JSValue PropertyOrUndefined(JSValue super, IJSSymbol name)
    {
        if (this == NullValue || this == UndefinedValue)
            return UndefinedValue;

        var pc = prototypeChain;
        if (pc == null)
            return UndefinedValue;

        return super.GetValue(name, this);
    }

    public JSValue PropertyOrUndefined(JSValue name)
    {
        if (this == NullValue || this == UndefinedValue)
            return UndefinedValue;

        if (name is IJSSymbol s)
            return PropertyOrUndefined(s);

        var k = name.ToKey(false);
        if (k.IsUInt)
            return PropertyOrUndefined(k.Index);

        return PropertyOrUndefined(k.KeyString);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public JSValue PropertyOrUndefined(JSValue super, JSValue name)
    {
        if (this == NullValue || this == UndefinedValue)
            return UndefinedValue;

        if (name is IJSSymbol s)
            return PropertyOrUndefined(super, s);

        var k = name.ToKey(false);
        if (k.IsUInt)
            return PropertyOrUndefined(k.Index);

        return PropertyOrUndefined(k.KeyString);
    }

    public virtual JSValue this[KeyString name]
    {
        get => GetValue(name, this);
        set
        {
            // throw new NotSupportedException();
        }
    }

    public virtual JSValue this[uint key]
    {
        get => GetValue(key, this);
        set { }
    }

    public virtual JSValue this[IJSSymbol symbol]
    {
        get => GetValue(symbol, this);
        set { }
    }

    public JSValue this[JSValue key]
    {
        get => GetValue(key, this); set => SetValue(key, value, this);
    }

    internal virtual JSValue this[KeyString name, JSValue @this]
    {
        get
        {
            if (prototypeChain == null)
                return UndefinedValue;

            return GetValue(name, this);
        }
        set { }
    }

    public virtual JSValue GetValue(uint key, JSValue receiver, bool throwError = true)
    {
        if (prototypeChain != null)
        {
            var p = prototypeChain.GetInternalProperty(key);
            return (receiver ?? this).GetValue(p);
        }

        return UndefinedValue;
    }

    internal protected virtual JSValue GetValue(KeyString key, JSValue receiver, bool throwError = true)
    {
        if (prototypeChain != null)
        {
            var p = prototypeChain.GetInternalProperty(key);
            return (receiver ?? this).GetValue(p);
        }

        return UndefinedValue;
    }

    internal protected virtual JSValue GetValue(IJSSymbol key, JSValue receiver, bool throwError = true)
    {
        if (prototypeChain != null)
        {
            var p = prototypeChain.GetInternalProperty(key);
            return (receiver ?? this).GetValue(p);
        }

        return UndefinedValue;
    }

    internal JSValue GetValue(JSValue key, JSValue receiver, bool throwError = true)
    {
        var k = key.ToKey(false);
        return k.Type switch
        {
            KeyType.UInt => GetValue(k.Index, receiver, throwError),
            KeyType.String => GetValue(k.KeyString, receiver, throwError),
            KeyType.Symbol => GetValue(k.Symbol, receiver, throwError),
            _ => UndefinedValue,
        };
    }

    public virtual bool SetValue(uint key, JSValue value, JSValue receiver, bool throwError = true) => false;

    internal protected virtual bool SetValue(KeyString key, JSValue value, JSValue receiver, bool throwError = true) => false;

    internal protected virtual bool SetValue(IJSSymbol key, JSValue value, JSValue receiver, bool throwError = true) => false;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool SetValue(JSValue key, JSValue value, JSValue receiver, bool throwError = true)
    {
        var k = key.ToKey();
        return k.Type switch
        {
            KeyType.Empty => false,
            KeyType.UInt => SetValue(k.Index, value, receiver, throwError),
            KeyType.String => SetValue(k.KeyString, value, receiver, throwError),
            KeyType.Symbol => SetValue(k.Symbol, value, receiver, throwError),
            _ => false,
        };
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public JSValue this[JSValue super, KeyString name]
    {
        get => super.GetValue(name, this); set => super.SetValue(name, value, this);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public JSValue this[JSValue super, uint index]
    {
        get => super.GetValue(index, this); set => super.SetValue(index, value, this);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public JSValue this[JSValue super, JSValue name]
    {
        get => super.GetValue(name, this); set => super.SetValue(name, value, this);
    }


    public abstract bool Equals(JSValue value);

    public virtual bool EqualsLiteral(string value) => false;
    public virtual bool EqualsLiteral(double value) => false;

    public virtual bool StrictEqualsLiteral(string value) => false;
    public virtual bool StrictEqualsLiteral(double value) => false;


    [EditorBrowsable(EditorBrowsableState.Never)]
    public static bool StaticEquals(JSValue left, JSValue right) => left.Equals(right);

    public abstract bool StrictEquals(JSValue value);

    /// <summary>
    /// 1. NaN is considered equal to NaN.
    /// 2. +0 and -0 are considered to be equal.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public virtual bool SameValueZero(JSValue value) => StrictEquals(value);

    public virtual bool Less(JSValue value)
    {
        if (IsUndefined || value.IsUndefined)
            return false;

        if (!CanBeNumber && !value.CanBeNumber)
        {
            if (ToString().Less(value.ToString()))
                return true;
        }
        else
        {
            if (DoubleValue < value.DoubleValue)
                return true;
        }

        return false;
    }

    public virtual bool LessOrEqual(JSValue value)
    {
        if (IsUndefined || value.IsUndefined)
            return false;

        if (!CanBeNumber && !value.CanBeNumber)
        {
            if (ToString().LessOrEqual(value.ToString()))
                return true;
        }
        else
        {
            if (DoubleValue <= value.DoubleValue)
                return true;
        }

        return false;
    }

    public virtual bool Greater(JSValue value)
    {
        if (IsUndefined || value.IsUndefined)
            return false;

        if (!CanBeNumber && !value.CanBeNumber)
        {
            if (ToString().Greater(value.ToString()))
                return true;
        }
        else
        {
            if (DoubleValue > value.DoubleValue)
                return true;
        }

        return false;
    }

    public virtual bool GreaterOrEqual(JSValue value)
    {
        if (IsUndefined || value.IsUndefined)
            return false;

        if (!CanBeNumber && !value.CanBeNumber)
        {
            if (ToString().Greater(value.ToString()))
                return true;
        }
        else
        {
            if (DoubleValue >= value.DoubleValue)
                return true;
        }

        return false;
    }

    public virtual IElementEnumerator GetAllKeys(bool showEnumerableOnly = true, bool inherited = true) => new ElementEnumerator();

    internal virtual JSValue Is(JSValue value) => ReferenceEquals(this, value) ? BooleanTrue : BooleanFalse;


    public virtual JSValue CreateInstance(in Arguments a) => throw NewTypeError($"Cannot create instance of {this}");

    public abstract JSValue InvokeFunction(in Arguments a);

    internal virtual JSFunctionDelegate GetMethod(in KeyString key) => prototypeChain.GetMethod(key);

    /// <summary>
    /// Warning do not use in concatenation
    /// </summary>
    /// <returns></returns>
    public override string ToString() => throw new NotSupportedException($"Use inherited version ... {GetType().Name} ");


    /// <summary>
    /// Returns a string containing a locale-dependant version of the number.
    /// </summary>
    /// <returns> A string containing a locale-dependant version of the number. </returns>
    /// 
    public virtual string ToLocaleString(string format, CultureInfo culture) => throw new NotImplementedException();
    public virtual string ToDetailString() => ToString();

    public virtual JSValue Delete(in KeyString key) => BooleanTrue;
    public virtual JSValue Delete(uint key) => BooleanTrue;
    public virtual JSValue Delete(IJSSymbol symbol) => BooleanTrue;

    public virtual JSValue Delete(JSValue index)
    {
        var key = index.ToKey(false);
        return key.Type switch
        {
            KeyType.Empty => BooleanFalse,
            KeyType.UInt => Delete(key.Index),
            KeyType.String => Delete(key.KeyString),
            KeyType.Symbol => Delete(key.Symbol),
            _ => BooleanFalse,
        };
    }

    internal JSValue InternalInvoke(object name, in Arguments a)
    {
        JSValue fx = null;
        switch (name)
        {
            case JSValue v:
                fx = this[v];
                break;
            case KeyString ks:
                fx = this[ks];
                break;
            case string str:
                fx = this[str];
                break;
        }

        if (fx.IsUndefined)
            throw NewTypeError($"Cannot invoke {name} of object as it is undefined");

        return fx.InvokeFunction(a.OverrideThis(this));
    }

    DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter) => CreateDynamicMetaObject(parameter, this);

    public JSValue Power(JSValue a)
    {
        var v = DoubleValue;
        var a1 = a.DoubleValue;

        if (a1 == 0)
            return NumberOne;

        if (a1 == double.PositiveInfinity || a1 == double.NegativeInfinity)
        {
            if (v == 1 || v == -1)
                return NumberNaN;
        }

        return CreateNumber(Math.Pow(DoubleValue, a1));
    }

    internal virtual bool TryGetValue(uint i, out JSProperty value)
    {
        value = new JSProperty { };
        return false;
    }

    internal virtual bool TryGetElement(uint i, out JSValue value)
    {
        value = null;
        return false;
    }

    internal virtual void MoveElements(int start, int to) { }

    internal virtual bool TryRemove(uint i, out JSProperty p)
    {
        p = new JSProperty();
        return false;
    }

    public virtual IElementEnumerator GetElementEnumerator() => ElementEnumerator.Empty;

    private readonly struct ElementEnumerator : IElementEnumerator
    {
        public static IElementEnumerator Empty = new ElementEnumerator();

        public bool MoveNext(out bool hasValue, out JSValue value, out uint index)
        {
            value = UndefinedValue;
            index = 0;
            hasValue = false;

            return false;
        }

        public bool MoveNext(out JSValue value)
        {
            value = UndefinedValue;
            return false;
        }

        public bool MoveNextOrDefault(out JSValue value, JSValue @default)
        {
            value = @default;
            return false;
        }
        public JSValue NextOrDefault(JSValue @default) => @default;
    }
}
