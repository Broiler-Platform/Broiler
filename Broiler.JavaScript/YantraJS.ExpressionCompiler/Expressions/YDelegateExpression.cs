#nullable enable
using System;
using System.CodeDom.Compiler;
using System.Reflection;

namespace YantraJS.Expressions;

public class YDelegateExpression(MethodInfo method, Type? type = null) : YExpression(YExpressionType.Delegate, type ?? GetSignature(method))
{
    public readonly MethodInfo Method = method;

    private static Type GetSignature(MethodInfo method) => throw new NotImplementedException();

    public override void Print(IndentedTextWriter writer) => writer.Write($"delegate({Method.Name})");
}