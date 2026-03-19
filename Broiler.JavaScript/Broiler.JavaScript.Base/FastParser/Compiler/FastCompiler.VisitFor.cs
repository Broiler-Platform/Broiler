#nullable enable
using Broiler.JavaScript.Core.CodeGen;
using Broiler.JavaScript.Core.Enumerators;
using Broiler.JavaScript.Core.FastParser;
using Broiler.JavaScript.Core.FastParser.Ast;
using Broiler.JavaScript.Core.LinqExpressions;
using Exp = YantraJS.Expressions.YExpression;
using Expression = YantraJS.Expressions.YExpression;

namespace YantraJS.Core.FastParser.Compiler;


partial class FastCompiler
{
    protected override Expression VisitForInStatement(AstForInStatement forInStatement, string? label = null)
    {
        var breakTarget = Exp.Label();
        var continueTarget = Exp.Label();
        // this will create a variable if needed...
        // desugar takes care of let so do not worry
        Expression? identifier = forInStatement.Init.Type switch
        {
            FastNodeType.Identifier or FastNodeType.VariableDeclaration => Visit(forInStatement.Init),
            _ => throw new FastParseException(forInStatement.Start, $"Unexpcted"),
        };

        using var s = scope.Top.Loop.Push(new LoopScope(breakTarget, continueTarget, false, label));
        var en = Exp.Variable(typeof(IElementEnumerator));
        var pList = en.AsSequence();
        var body = VisitStatement(forInStatement.Body);
        var bodyList = Exp.Block(Exp.IfThen(Exp.Not(IElementEnumeratorBuilder.MoveNext(en, identifier)), Exp.Goto(s.Break)), body);
        var right = VisitExpression(forInStatement.Target);

        return Exp.Block(pList, Exp.Assign(en, JSValueBuilder.GetAllKeys(right)), Exp.Loop(bodyList, s.Break, s.Continue));
    }

    protected override Expression VisitForOfStatement(AstForOfStatement forOfStatement, string? label = null)
    {
        var breakTarget = Exp.Label();
        var continueTarget = Exp.Label();
        // this will create a variable if needed...
        // desugar takes care of let so do not worry

        Expression? identifier = forOfStatement.Init.Type switch
        {
            FastNodeType.Identifier or FastNodeType.VariableDeclaration => Visit(forOfStatement.Init),
            _ => throw new FastParseException(forOfStatement.Start, $"Unexpcted"),
        };

        using var s = scope.Top.Loop.Push(new LoopScope(breakTarget, continueTarget, false, label));
        var en = Exp.Variable(typeof(IElementEnumerator));
        var pList = en.AsSequence();
        var body = VisitStatement(forOfStatement.Body);
        var bodyList = Exp.Block(Exp.IfThen(Exp.Not(IElementEnumeratorBuilder.MoveNext(en, identifier)), Exp.Goto(s.Break)), body);
        var right = VisitExpression(forOfStatement.Target);
        var r = Exp.Block(pList, Exp.Assign(en, IElementEnumeratorBuilder.Get(right)), Exp.Loop(bodyList, s.Break, s.Continue));

        return r;
    }

    protected override Expression VisitForStatement(AstForStatement forStatement, string? label = null)
    {
        var breakTarget = Exp.Label();
        var continueTarget = Exp.Label();
        // this will create a variable if needed...
        // desugar takes care of let so do not worry
        Exp init = Visit(forStatement.Init);
        var innerBody = new Sequence<Exp>();

        var update = Visit(forStatement.Update);
        var test = Visit(forStatement.Test);

        if (test != null)
        {
            test = Exp.IfThen(Exp.Not(JSValueBuilder.BooleanValue(test)), Exp.Goto(breakTarget));
            innerBody.Add(test);
        }

        using var s = scope.Top.Loop.Push(new LoopScope(breakTarget, continueTarget, false, label));
        var body = VisitStatement(forStatement.Body);

        innerBody.Add(body);
        innerBody.Add(Exp.Label(continueTarget));

        if (update != null)
            innerBody.Add(update);

        if (init == null)
        {
            var r1 = Exp.Loop(Exp.Block(innerBody), breakTarget);
            return r1;
        }

        var r = Exp.Block(init, Exp.Loop(Exp.Block(innerBody), breakTarget));
        return r;
    }
}
