using System.Collections.Generic;
using YantraJS.Core;

namespace YantraJS.Expressions;

public class YBlockBuilder
{

    private Sequence<YExpression> expressions = [];
    private Sequence<YParameterExpression> variables = [];

    public YBlockBuilder()
    {

    }

    public void AddVariable(YParameterExpression pe) => variables.Add(pe);

    private YParameterExpression AddVariable(YParameterExpression pe, YExpression init)
    {
        // break init if it is block..
        if(init.NodeType == YExpressionType.Block)
        {
            var block = init as YBlockExpression;
            variables.AddRange(block.FlattenVariables);
            foreach(var (e, last) in block.FlattenExpressions)
            {
                if (last)
                {
                    if(e.NodeType == YExpressionType.Parameter)
                    {
                        AddExpression(e);
                        return e as YParameterExpression;
                    }
                    variables.Add(pe);
                    AddExpression(YExpression.Assign(pe, e));
                    return pe;
                }
                AddExpression(e);
            }
        }
        variables.Add(pe);
        AddExpression(YExpression.Assign(pe, init));
        return pe;
    }

    public Sequence<YExpression> ConvertToVariables(IFastEnumerable<YExpression> inputs, YExpressionMapVisitor visitor)
    {
        var newInputs = new Sequence<YExpression>(inputs.Count);
        var en = inputs.GetFastEnumerator();
        while(en.MoveNext(out var input))
        {
            newInputs.Add(ConvertToVariable(visitor.Visit(input)));
        }
        return newInputs;
    }


    public YExpression ConvertToVariable(YExpression init)
    {
        if (init.NodeType == YExpressionType.Parameter)
            return init;
        YParameterExpression pe;
        // break init if it is block..
        if (init.NodeType == YExpressionType.Block)
        {
            var block = init as YBlockExpression;
            variables.AddRange(block.FlattenVariables);
            foreach (var (e, last) in block.FlattenExpressions)
            {
                if (last)
                {
                    if (e.NodeType == YExpressionType.Parameter)
                    {
                        AddExpression(e);
                        return e as YParameterExpression;
                    }
                    pe = YExpression.Parameter(e.Type);
                    variables.Add(pe);
                    AddExpression(YExpression.Assign(pe, e));
                    return pe;
                }
                AddExpression(e);
            }
        }
        pe = YExpression.Parameter(init.Type);
        variables.Add(pe);
        AddExpression(YExpression.Assign(pe, init));
        return pe;
    }

    public YBlockBuilder AddExpressionRange(IEnumerable<YExpression> exps)
    {
        foreach (var e in exps)
            AddExpression(e);
        return this;
    }


    public YBlockBuilder AddExpression(YExpression exp)
    {
        switch(exp.NodeType)
        {
            case YExpressionType.Block:
                var block = (exp as YBlockExpression)!;
                variables.AddRange(block.Variables);
                {
                    var en = block.Expressions.GetFastEnumerator();
                    while(en.MoveNext(out var e))
                    {
                        AddExpression(e);
                    }
                }
                return this;
            case YExpressionType.Return:
                var @return = (exp as YReturnExpression)!;
                if(@return.Default?.NodeType == YExpressionType.Block)
                {
                    block = (@return.Default as YBlockExpression)!;
                    var en = block.Enumerate();
                    while(en.MoveNext(out var e, out var isLast))
                    {
                        if (isLast)
                        {
                            return AddExpression(@return.Update(@return.Target, e));
                        }
                        AddExpression(e);
                    }
                    return this;
                }
                break;
        }
        expressions.Add(exp);
        return this;
    }

    public YExpression Build()
    {
        if (variables.Count == 0 && expressions.Count == 1)
            return expressions.First();
        return new YBlockExpression(variables, expressions);
    }

}
