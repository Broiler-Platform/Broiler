using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Expression = YantraJS.Expressions.YExpression;

namespace Broiler.JavaScript.Core.Core;

public class JSVariable
{
    // BROILER-PATCH: Support read-only variables for function expression names (ES3 §13)
    private JSValue _value;
    public JSValue Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set { if (!IsReadOnly) _value = value; }
    }
    internal bool IsReadOnly;

    static readonly PropertyInfo _ValueProperty = typeof(JSVariable).GetProperty("Value");
    internal readonly StringSpan Name;
    private KeyString key;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JSVariable(JSValue v, string name)
    {
        _value = v;
        Name = name;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JSVariable(JSValue v, in StringSpan name)
    {
        _value = v;
        Name = name;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JSVariable(in Arguments a, int i, string name)
    {
        _value = a.GetAt(i);
        Name = name;
    }

    public JSValue GlobalValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            _value = value;
            if (key.Value == null)
                key = KeyStrings.GetOrCreate(Name);

            var old = JSContext.Current[key];
            if (old != value && !value.IsUndefined)
                JSContext.Current[key] = value;
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JSVariable(Exception e, string name) : this(e is JSException je ? je.Error : JSException.From(e).Error, name) { }

    internal static Expression ValueExpression(Expression exp) => Expression.Property(exp, _ValueProperty);
}
