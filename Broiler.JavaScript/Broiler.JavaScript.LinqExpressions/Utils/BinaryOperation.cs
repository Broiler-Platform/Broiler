using System;
using System.Linq;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using ParameterExpression = Broiler.JavaScript.ExpressionCompiler.Expressions.YParameterExpression;
using Broiler.JavaScript.ExpressionCompiler.Core;
using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.LinqExpressions.LambdaGen;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;


namespace Broiler.JavaScript.LinqExpressions.Utils;

public delegate Expression CaseExpression(ParameterExpression pe);

public class SwitchExpression
{
    protected static TypeCheckCase Case<T>(in CaseExpression e) => new()
    {
        Type = typeof(T),
        TrueCase = e
    };

    protected static TypeCheckCase Case(Type type, CaseExpression e) => new()
    {
        Type = type,
        TrueCase = e
    };

    protected static TypeCheckCase Default(Expression e) => new()
    {
        Type = null,
        TrueCase = (a) => e
    };

    public class TypeCheckCase
    {
        public Type Type { get; set; }

        public CaseExpression TrueCase { get; set; }

    }

    protected static Expression Switch(Expression right, params TypeCheckCase[] cases)
    {
        var defaultCase = cases.First(x => x.Type == null);
        var allCases = cases.Where(x => x.Type != null);

        Expression condition = defaultCase.TrueCase(null);
        foreach (var @case in allCases)
        {
            var bp = Expression.Parameter(@case.Type);

            if (@case.Type.IsValueType)
            {
                condition = Expression.Condition(Expression.TypeIs(right, @case.Type), Expression.Block(bp.AsSequence(), @case.TrueCase(bp)), condition, typeof(JSValue));
                continue;
            }

            var nbt = Expression.Constant(null, @case.Type);
            condition = Expression.Block(bp.AsSequence(), Expression.Assign(bp, Expression.TypeAs(right, @case.Type)), Expression.Condition(Expression.NotEqual(nbt, bp),
                    @case.TrueCase(bp), condition, typeof(JSValue)));
        }

        return condition;
    }
}

public class BinaryOperation : SwitchExpression
{
    public static Expression Assign(Expression left, Expression right, TokenTypes assignmentOperator)
    {
        var oneF = Expression.Constant(0x1F);

        switch (assignmentOperator)
        {
            case TokenTypes.Assign:
                return Assign(left, right);

            case TokenTypes.AssignAdd:
                return Assign(left, JSValueBuilder.Add(left, right));
        }

        var leftDouble = JSValueBuilder.DoubleValue(left);
        var rightDouble = JSValueBuilder.DoubleValue(right);

        var leftInt = JSValueBuilder.IntValue(left);
        var rightInt = JSValueBuilder.IntValue(right);

        var leftUInt = Expression.Convert(leftDouble, typeof(uint));
        var rightUInt = Expression.Convert(rightDouble, typeof(uint));

        // convert to double...
        return assignmentOperator switch
        {
            TokenTypes.AssignSubtract => Assign(left, JSNumberBuilder.New(Expression.Subtract(leftDouble, rightDouble))),
            TokenTypes.AssignMultiply => Assign(left, JSNumberBuilder.New(Expression.Multiply(leftDouble, rightDouble))),
            TokenTypes.AssignDivide => Assign(left, JSNumberBuilder.New(Expression.Divide(leftDouble, rightDouble))),
            TokenTypes.AssignMod => Assign(left, JSNumberBuilder.New(Expression.Modulo(leftDouble, rightDouble))),
            TokenTypes.AssignBitwideAnd => Assign(left, JSNumberBuilder.New(Expression.And(leftInt, rightInt))),
            TokenTypes.AssignBitwideOr => Assign(left, JSNumberBuilder.New(Expression.Or(leftInt, rightInt))),
            TokenTypes.AssignXor => Assign(left, JSNumberBuilder.New(Expression.ExclusiveOr(leftInt, rightInt))),
            TokenTypes.AssignLeftShift => Assign(left, JSNumberBuilder.New(Expression.LeftShift(leftInt, rightInt))),
            TokenTypes.AssignRightShift => Assign(left, JSNumberBuilder.New(Expression.RightShift(leftInt, Expression.And(rightInt, oneF)))),
            TokenTypes.AssignUnsignedRightShift => Assign(left, JSNumberBuilder.New(Expression.RightShift(leftInt, rightInt))),
            TokenTypes.AssignPower => Assign(left, JSNumberBuilder.New(Expression.Power(leftDouble, rightDouble))),
            TokenTypes.AssignCoalesce => Assign(left, JSValueBuilder.Coalesce(left, right)),
            _ => throw new NotSupportedException(),
        };
    }

    private static Expression Assign(Expression left, Expression right) => JSValueExtensionsBuilder.Assign(left, right);

    public static Expression Operation(Expression left, Expression right, TokenTypes op)
    {
        return op switch
        {
            TokenTypes.Equal => JSValueBuilder.Equals(left, right),
            TokenTypes.NotEqual => JSValueBuilder.NotEquals(left, right),
            TokenTypes.StrictlyEqual => JSValueBuilder.StrictEquals(left, right),
            TokenTypes.StrictlyNotEqual => JSValueBuilder.NotStrictEquals(left, right),
            TokenTypes.InstanceOf => JSValueExtensionsBuilder.InstanceOf(left, right),
            TokenTypes.In => JSValueExtensionsBuilder.IsIn(left, right),
            TokenTypes.Plus => JSValueBuilder.Add(left, right),
            TokenTypes.Minus => left.CallExpression<JSValue, JSValue, JSValue>(() => (a, b) => a.Subtract(b), right),
            TokenTypes.Multiply => left.CallExpression<JSValue, JSValue, JSValue>(() => (a, b) => a.Multiply(b), right),
            TokenTypes.Divide => left.CallExpression<JSValue, JSValue, JSValue>(() => (a, b) => a.Divide(b), right),
            TokenTypes.Mod => left.CallExpression<JSValue, JSValue, JSValue>(() => (a, b) => a.Modulo(b), right),
            TokenTypes.Greater => JSValueBuilder.Greater(left, right),
            TokenTypes.GreaterOrEqual => JSValueBuilder.GreaterOrEqual(left, right),
            TokenTypes.Less => JSValueBuilder.Less(left, right),
            TokenTypes.LessOrEqual => JSValueBuilder.LessOrEqual(left, right),
            TokenTypes.BitwiseAnd => left.CallExpression<JSValue, JSValue, JSValue>(() => (a, b) => a.BitwiseAnd(b), right),
            TokenTypes.BitwiseOr => left.CallExpression<JSValue, JSValue, JSValue>(() => (a, b) => a.BitwiseOr(b), right),
            TokenTypes.Xor => left.CallExpression<JSValue, JSValue, JSValue>(() => (a, b) => a.BitwiseXor(b), right),
            TokenTypes.LeftShift => left.CallExpression<JSValue, JSValue, JSValue>(() => (a, b) => a.LeftShift(b), right),
            TokenTypes.RightShift => left.CallExpression<JSValue, JSValue, JSValue>(() => (a, b) => a.RightShift(b), right),
            TokenTypes.UnsignedRightShift => left.CallExpression<JSValue, JSValue, JSValue>(() => (a, b) => a.UnsignedRightShift(b), right),
            TokenTypes.BooleanAnd => JSValueBuilder.LogicalAnd(left, right),
            TokenTypes.BooleanOr => JSValueBuilder.LogicalOr(left, right),
            TokenTypes.Power => JSValueBuilder.Power(left, right),
            TokenTypes.Coalesce => JSValueExtensionsBuilder.Coalesce(left, right),
            _ => null,
        };
    }
}
