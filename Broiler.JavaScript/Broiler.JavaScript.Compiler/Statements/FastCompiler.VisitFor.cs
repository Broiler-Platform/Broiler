using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Ast.Statements;
using Broiler.JavaScript.ExpressionCompiler.Core;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.Compiler;


partial class FastCompiler
{
    protected override YExpression VisitForInStatement(AstForInStatement forInStatement, string? label = null)
    {
        var breakTarget = YExpression.Label();
        var continueTarget = YExpression.Label();
        // this will create a variable if needed...
        // desugar takes care of let so do not worry
        YExpression? identifier = forInStatement.Init.Type switch
        {
            FastNodeType.Identifier or FastNodeType.VariableDeclaration => Visit(forInStatement.Init),
            _ => throw new FastParseException(forInStatement.Start, $"Unexpcted"),
        };

        using var s = scope.Top.Loop.Push(new LoopScope(breakTarget, continueTarget, false, label));
        var en = YExpression.Variable(typeof(IElementEnumerator));
        var pList = en.AsSequence();
        var body = VisitStatement(forInStatement.Body);
        var bodyList = YExpression.Block(YExpression.IfThen(YExpression.Not(IElementEnumeratorBuilder.MoveNext(en, identifier)), YExpression.Goto(s.Break)), body);
        var right = VisitExpression(forInStatement.Target);

        return YExpression.Block(pList, YExpression.Assign(en, JSValueBuilder.GetAllKeys(right)), YExpression.Loop(bodyList, s.Break, s.Continue));
    }

    protected override YExpression VisitForOfStatement(AstForOfStatement forOfStatement, string? label = null)
    {
        var breakTarget = YExpression.Label();
        var continueTarget = YExpression.Label();
        // this will create a variable if needed...
        // desugar takes care of let so do not worry

        YExpression? identifier = forOfStatement.Init.Type switch
        {
            FastNodeType.Identifier or FastNodeType.VariableDeclaration => Visit(forOfStatement.Init),
            _ => throw new FastParseException(forOfStatement.Start, $"Unexpcted"),
        };

        using var s = scope.Top.Loop.Push(new LoopScope(breakTarget, continueTarget, false, label));
        var en = YExpression.Variable(typeof(IElementEnumerator));
        var pList = en.AsSequence();
        var body = VisitStatement(forOfStatement.Body);
        var bodyList = YExpression.Block(YExpression.IfThen(YExpression.Not(IElementEnumeratorBuilder.MoveNext(en, identifier)), YExpression.Goto(s.Break)), body);
        var right = VisitExpression(forOfStatement.Target);
        var r = YExpression.Block(pList, YExpression.Assign(en, IElementEnumeratorBuilder.Get(right)), YExpression.Loop(bodyList, s.Break, s.Continue));

        return r;
    }

    protected override YExpression VisitForStatement(AstForStatement forStatement, string? label = null)
    {
        var breakTarget = YExpression.Label();
        var continueTarget = YExpression.Label();
        // this will create a variable if needed...
        // desugar takes care of let so do not worry
        YExpression init = Visit(forStatement.Init);
        var innerBody = new Sequence<YExpression>();

        var update = Visit(forStatement.Update);
        var test = Visit(forStatement.Test);

        if (test != null)
        {
            test = YExpression.IfThen(YExpression.Not(JSValueBuilder.BooleanValue(test)), YExpression.Goto(breakTarget));
            innerBody.Add(test);
        }

        using var s = scope.Top.Loop.Push(new LoopScope(breakTarget, continueTarget, false, label));
        var body = VisitStatement(forStatement.Body);

        innerBody.Add(body);
        innerBody.Add(YExpression.Label(continueTarget));

        if (update != null)
            innerBody.Add(update);

        if (init == null)
        {
            var r1 = YExpression.Loop(YExpression.Block(innerBody), breakTarget);
            return r1;
        }

        var r = YExpression.Block(init, YExpression.Loop(YExpression.Block(innerBody), breakTarget));
        return r;
    }
}
