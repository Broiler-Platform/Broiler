using System;

using Exp = YantraJS.Expressions.YExpression;
using Expression = YantraJS.Expressions.YExpression;

namespace YantraJS.Core.FastParser.Compiler;

partial class FastCompiler
{
    protected override Expression VisitLabeledStatement(AstLabeledStatement labeledStatement)
    {
        switch (labeledStatement.Body.Type)
        {
            case FastNodeType.ForStatement:
                return VisitForStatement(labeledStatement.Body as AstForStatement, labeledStatement.Label.Span.Value);
            case FastNodeType.ForOfStatement:
                return VisitForOfStatement(labeledStatement.Body as AstForOfStatement, labeledStatement.Label.Span.Value);
            case FastNodeType.ForInStatement:
                return VisitForInStatement(labeledStatement.Body as AstForInStatement, labeledStatement.Label.Span.Value);
            case FastNodeType.WhileStatement:
                return VisitWhileStatement(labeledStatement.Body as AstWhileStatement, labeledStatement.Label.Span.Value);
            case FastNodeType.DoWhileStatement:
                return VisitDoWhileStatement(labeledStatement.Body as AstDoWhileStatement, labeledStatement.Label.Span.Value);
            default:
                {
                    var breakTarget = Exp.Label();
                    var label = labeledStatement.Label.Span.Value;
                    using var s = scope.Top.Loop.Push(new LoopScope(breakTarget, null, false, label));
                    return Exp.Block(VisitStatement(labeledStatement.Body), Exp.Label(breakTarget));
                }
        }
        throw new NotImplementedException();
    }
}
