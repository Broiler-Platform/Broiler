using System;
using System.Linq;
using Exp = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using ParameterExpression = Broiler.JavaScript.ExpressionCompiler.Expressions.YParameterExpression;
using LambdaExpression = Broiler.JavaScript.ExpressionCompiler.Expressions.YLambdaExpression;
using LabelTarget = Broiler.JavaScript.ExpressionCompiler.Expressions.YLabelTarget;
using GotoExpression = Broiler.JavaScript.ExpressionCompiler.Expressions.YGoToExpression;
using TryExpression = Broiler.JavaScript.ExpressionCompiler.Expressions.YTryCatchFinallyExpression;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Core;
using Broiler.JavaScript.ExpressionCompiler.ClosureSeparator;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.LinqExpressions.LinqExpressions.GeneratorsV2;



public class GeneratorRewriter(ParameterExpression pe, LabelTarget @return, ParameterExpression replaceArguments, ParameterExpression replaceContext) : YExpressionMapVisitor
{
    private readonly ParameterExpression args = Expression.Parameter(typeof(Arguments).MakeByRefType(), "args");
    private readonly ParameterExpression nextJump = Expression.Parameter(typeof(int), "nextJump");
    private readonly ParameterExpression nextValue = Expression.Parameter(typeof(JSValue), "nextValue");
    private readonly ParameterExpression exception = Expression.Parameter(typeof(Exception), "ex");
    private readonly YFieldExpression Context = Expression.Field(pe, "Context");
    private readonly LabelTarget generatorReturn = Expression.Label(typeof(GeneratorState), "RETURN");
    private readonly Sequence<(ParameterExpression original, ParameterExpression box, int index, Expression boxField)> lifted = [];

    // private readonly ParameterExpression replaceScriptInfo;
    private Sequence<(LabelTarget label, int id)> jumps = [];

    public static LambdaExpression Rewrite(in FunctionName name, Expression body, LabelTarget r, ParameterExpression generator, ParameterExpression replaceArgs,
       ParameterExpression replaceStackItem, ParameterExpression replaceContext, ParameterExpression replaceScriptInfo)
    {
        var gw = new GeneratorRewriter(generator, r, replaceArgs /*,replaceStackItem,*/, replaceContext /*,replaceScriptInfo*/);

        body = MethodRewriter.Rewrite(body);

        var flatten = new FlattenBlocks();
        var innerBody = flatten.Visit(gw.Visit(body));

        // setup jump table...

        var @break = Expression.Label("generatorEnd");
        var jumpExp = gw.GenerateJumps(@break);
        var (boxes, inits) = gw.LoadBoxes();

        YBlockExpression newBody;

        if (boxes == null)
        {
            newBody = Expression.Block(jumpExp, innerBody, Expression.Label(gw.generatorReturn, GeneratorStateBuilder.New(0)));
        }
        else
        {
            newBody = Expression.Block(boxes, inits, jumpExp, Expression.Label(@break), innerBody, Expression.Label(gw.generatorReturn, GeneratorStateBuilder.New(0)));
        }

        return Expression.Lambda<JSGeneratorDelegateV2>(in name, newBody, generator, gw.args, gw.nextJump, gw.nextValue, gw.exception);
    }

    private (Sequence<ParameterExpression> boxes, Expression init) LoadBoxes()
    {
        var boxes = new Sequence<Expression>(lifted.Count) { ClrGeneratorV2Builder.InitVariables(pe, lifted.Count) };
        var vlist = new Sequence<ParameterExpression>(lifted.Count);

        foreach (var (original, box, index, _) in lifted)
        {
            vlist.Add(box);
            boxes.Add(Expression.Assign(box, ClrGeneratorV2Builder.GetVariable(pe, index, original.Type)));
        }

        if (vlist.Count == 0)
            return (null, null);

        return (vlist, Expression.Block(boxes));
    }

    private Expression GenerateJumps(LabelTarget @break)
    {
        if (jumps.Count == 0)
            return Expression.Empty;

        var cases = new Sequence<LabelTarget>();
        var offset = 1;

        jumps = [.. jumps.OrderBy(x => x.id)];

        var en = jumps.GetFastEnumerator();

        while (en.MoveNext(out var jump, out var i))
        {
            var (label, id) = jump;
            var index = id + offset;

            // this will fill the gap in between jumps, if any
            while (index > cases.Count)
                cases.Add(@break);

            cases.Add(label);
        }

        return Expression.JumpSwitch(nextJump + offset, cases);
    }

    protected override Expression VisitBlock(YBlockExpression node)
    {
        if (!node.HasYield())
            return base.VisitBlock(node);

        var list = new Sequence<Expression>(node.Variables.Count + node.Expressions.Count);
        var ve = node.Variables.GetFastEnumerator();

        while (ve.MoveNext(out var v))
        {
            int index = lifted.Count;
            var box = Expression.Parameter(typeof(Box<>).MakeGenericType(v.Type));
            lifted.Add((v, box, index, Expression.Field(box, "Value")));
        }

        var vne = node.Expressions.GetFastEnumerator();
        while (vne.MoveNext(out var s))
            list.Add(Visit(s));

        return Expression.Block(list);
    }

    protected override Exp VisitReturn(YReturnExpression node)
    {
        if (node.Default == null || node.Default.NodeType != YExpressionType.Yield)
            return Expression.Return(generatorReturn, GeneratorStateBuilder.New(Visit(node.Default), -1));

        // return yield case... need to expand..
        var yield = node.Default as YYieldExpression;
        var arg = Visit(yield.Argument);
        var (label, id) = GetNextYieldJumpTarget();

        return Expression.Block(Expression.Return(generatorReturn, GeneratorStateBuilder.New(arg, id)), Expression.Label(label),
            Expression.Return(generatorReturn, GeneratorStateBuilder.New(nextValue, -1)));
    }

    protected override Expression VisitGoto(GotoExpression node) => base.VisitGoto(node);

    protected override Expression VisitParameter(ParameterExpression node)
    {
        if (node == replaceArguments)
            return args;

        if (node == replaceContext)
            return Context;

        foreach (var (original, _, _, boxField) in lifted)
        {
            if (original == node)
                return boxField;
        }

        return base.VisitParameter(node);
    }

    private (LabelTarget label, int id) GetNextYieldJumpTarget()
    {
        int id = jumps.Count + 1;
        var label = Expression.Label(typeof(void), "next" + id);
        var r = (label, id);
        jumps.Add(r);
        return r;
    }

    protected override Exp VisitYield(YYieldExpression node)
    {
        var arg = Visit(node.Argument);
        var (label, id) = GetNextYieldJumpTarget();

        return Expression.Block(Expression.Return(generatorReturn, GeneratorStateBuilder.New(arg, id, node.DelegateYield)), Expression.Label(label), nextValue);
    }

    protected override Exp VisitLambda(LambdaExpression yLambdaExpression)
    {
        // we need to rewrite nested lambda to replace `this` or closures
        // with boxes...

        var replaces = lifted.ToDictionary((x) => (Expression)x.original, x => x.boxField);
        var parameterReplacer = new ReplaceParameters(replaces);

        return parameterReplacer.Visit(yLambdaExpression);
    }

    protected override Exp VisitTryCatchFinally(TryExpression node)
    {
        if (!node.HasYield())
            return base.VisitTryCatchFinally(node);

        var hasFinally = node.Finally != null;
        var @catch = node.Catch;
        var hasCatch = @catch != null;

        LabelTarget catchLabel = null;
        int catchId = 0;
        LabelTarget finallyLabel = null;
        int finallyId = 0;

        var tryList = new YBlockBuilder();
        if (hasCatch)
            (catchLabel, catchId) = GetNextYieldJumpTarget();

        if (hasFinally)
            (finallyLabel, finallyId) = GetNextYieldJumpTarget();

        var (endLabel, endId) = GetNextYieldJumpTarget();

        tryList.AddExpression(ClrGeneratorV2Builder.Push(pe, catchId, finallyId, endId));
        tryList.AddExpression(Visit(node.Try));
        tryList.AddExpression(Expression.Goto(hasFinally ? finallyLabel : endLabel));

        if (hasCatch)
        {
            tryList.AddExpression(Expression.Label(catchLabel));
            tryList.AddExpression(ClrGeneratorV2Builder.BeginCatch(pe));
            tryList.AddExpression(Expression.Assign(Visit(@catch.Parameter), exception));
            tryList.AddExpression(Visit(@catch.Body));
            tryList.AddExpression(Expression.Empty);
            tryList.AddExpression(Expression.Goto(hasFinally ? finallyLabel : endLabel));
        }

        if (hasFinally)
        {
            tryList.AddExpression(Expression.Label(finallyLabel));
            tryList.AddExpression(ClrGeneratorV2Builder.BeginFinally(pe));
            tryList.AddExpression(Visit(node.Finally));
            tryList.AddExpression(ClrGeneratorV2Builder.Throw(pe, endId));
        }

        tryList.AddExpression(Expression.Label(endLabel));
        tryList.AddExpression(ClrGeneratorV2Builder.Pop(pe));

        var b = tryList.Build();
        return b;
    }
}
