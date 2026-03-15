#nullable enable
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace YantraJS.Expressions;


public class YLambdaExpression: YExpression
{
    public readonly FunctionName Name;
    public readonly YExpression Body;
    public new readonly YParameterExpression[] Parameters;
    public readonly Type ReturnType;

    [AllowNull]
    public YParameterExpression This { get; private set; }

    internal YExpression<T> As<T>() => new(Name, Body, This, Parameters, ReturnType);


    public readonly Type[] ParameterTypes;

    public Type[] ParameterTypesWithThis {
        get {
            var l = new List<Type> { This!.Type };
            l.AddRange(ParameterTypes);
            return l.ToArray();
        }
    }

    internal readonly YExpression? Repository;
        

    public YLambdaExpression(
        Type delegateType,
        in FunctionName name, 
        YExpression body, 
        YParameterExpression? @this,
        YParameterExpression[]? parameters,
        Type? returnType = null,
        YExpression? repository = null)
        : base(YExpressionType.Lambda, delegateType)
    {
        Name = name;
        Body = body;
        This = @this;
        ReturnType = returnType ?? body.Type;
        if (parameters != null)
            Parameters = parameters;
        else
            Parameters = [];
        ParameterTypes = Parameters.Select(x => x.Type).ToArray();
        Repository = repository;
    }
    public override void Print(IndentedTextWriter writer)
    {
        writer.Write('(');
        writer.Write(string.Join(", ", Parameters.Select(p => $"{p.Type.GetFriendlyName()} {p.Name}") ));
        writer.Write(") => ");

        Body.Print(writer);
    }

    internal void SetupAsClosure()
    {
        This ??= YParameterExpression.Parameter(typeof(Closures), "this");
    }

    internal YLambdaExpression WithThis(Type type)
    {
        if (This != null)
            throw new ArgumentOutOfRangeException();
        var @this = YExpression.Parameter(type, "this");

        return new YLambdaExpression(Type, Name, Body, @this, Parameters, ReturnType, Repository);
    }
}