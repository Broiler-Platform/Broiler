using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Storage;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;

namespace Broiler.JavaScript.Runtime;

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

    /// <summary>
    /// Delegate that retrieves the current JavaScript execution context.
    /// Wired by Core's module initializer to point to JSEngine.Current.
    /// </summary>
    internal static Func<object> GetCurrentContext;

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

            if (GetCurrentContext?.Invoke() is JSObject ctx)
            {
                var old = ctx[key];
                if (old != value && !value.IsUndefined)
                    ctx[key] = value;
            }
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JSVariable(Exception e, string name) : this(e is JSException je ? je.Error : JSException.From(e).Error, name) { }

    public static Expression ValueExpression(Expression exp) => Expression.Property(exp, _ValueProperty);
}
