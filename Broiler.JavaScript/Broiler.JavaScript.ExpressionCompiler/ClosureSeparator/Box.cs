using System;
using System.Reflection;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.ExpressionCompiler.ClosureSeparator;

public abstract class BoxHelper
{
    public static BoxHelper For(Type type) => Activator.CreateInstance(typeof(BoxHelper<>).MakeGenericType(type)) as BoxHelper;
    public abstract Type BoxType { get; }
    public abstract YExpression New(YExpression value);
    public abstract ConstructorInfo Constructor { get; }
}

public class BoxHelper<T> : BoxHelper
{
    public static readonly Type _BoxType = typeof(Box<T>);
    public override Type BoxType => _BoxType;
    public static readonly ConstructorInfo _new = _BoxType.GetConstructor([]);
    private static ConstructorInfo _newFromValue = _BoxType.GetConstructor([typeof(T)]);
    public override ConstructorInfo Constructor => _new;
    public override YExpression New(YExpression value) => YExpression.New(_newFromValue, value);
}

public abstract class Box
{
}

public class Box<T> : Box
{
    public Box() { }
    public Box(T value) => Value = value;
    public T Value;
}
