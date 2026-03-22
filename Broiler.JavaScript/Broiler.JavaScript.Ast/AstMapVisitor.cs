using Broiler.JavaScript.Ast.Expressions;
using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Ast.Patterns;
using Broiler.JavaScript.Ast.Statements;
using System;

namespace Broiler.JavaScript.Ast;


public abstract class AstMapVisitor<T>
{
    public bool IsStrictMode { get; set; } = false;

    public bool Debug { get; set; } = true;

    public T Visit(AstNode node) {
        
        if (node == null)
            return default;

        return node.Type switch
        {
            FastNodeType.ArrayPattern => VisitArrayPattern(node as AstArrayPattern),
            FastNodeType.Block => VisitBlock(node as AstBlock),
            FastNodeType.Program => VisitProgram(node as AstProgram),
            FastNodeType.BreakStatement => VisitBreakStatement(node as AstBreakStatement),
            FastNodeType.BinaryExpression => VisitBinaryExpression(node as AstBinaryExpression),
            FastNodeType.VariableDeclaration => VisitVariableDeclaration(node as AstVariableDeclaration),
            FastNodeType.ExpressionStatement => VisitExpressionStatement(node as AstExpressionStatement),
            FastNodeType.FunctionExpression => VisitFunctionExpression(node as AstFunctionExpression),
            FastNodeType.Identifier => VisitIdentifier(node as AstIdentifier),
            FastNodeType.ObjectPattern => VisitObjectPattern(node as AstObjectPattern),
            FastNodeType.SpreadElement => VisitSpreadElement(node as AstSpreadElement),
            FastNodeType.IfStatement => VisitIfStatement(node as AstIfStatement),
            FastNodeType.WhileStatement => VisitWhileStatement(node as AstWhileStatement),
            FastNodeType.DoWhileStatement => VisitDoWhileStatement(node as AstDoWhileStatement),
            FastNodeType.SequenceExpression => VisitSequenceExpression(node as AstSequenceExpression),
            FastNodeType.ForStatement => VisitForStatement(node as AstForStatement),
            FastNodeType.ForInStatement => VisitForInStatement(node as AstForInStatement),
            FastNodeType.ForOfStatement => VisitForOfStatement(node as AstForOfStatement),
            FastNodeType.ContinueStatement => VisitContinueStatement(node as AstContinueStatement),
            FastNodeType.ThrowStatement => VisitThrowStatement(node as AstThrowStatement),
            FastNodeType.TryStatement => VisitTryStatement(node as AstTryStatement),
            FastNodeType.DebuggerStatement => VisitDebuggerStatement(node as AstDebuggerStatement),
            FastNodeType.LabeledStatement => VisitLabeledStatement(node as AstLabeledStatement),
            FastNodeType.Literal => VisitLiteral(node as AstLiteral),
            FastNodeType.MemberExpression => VisitMemberExpression(node as AstMemberExpression),
            FastNodeType.ClassStatement => VisitClassStatement(node as AstClassExpression),
            FastNodeType.SwitchStatement => VisitSwitchStatement(node as AstSwitchStatement),
            FastNodeType.EmptyExpression => VisitEmptyExpression(node as AstEmptyExpression),
            FastNodeType.ArrayExpression => VisitArrayExpression(node as AstArrayExpression),
            FastNodeType.ObjectLiteral => VisitObjectLiteral(node as AstObjectLiteral),
            FastNodeType.TemplateExpression => VisitTemplateExpression(node as AstTemplateExpression),
            FastNodeType.UnaryExpression => VisitUnaryExpression(node as AstUnaryExpression),
            FastNodeType.CallExpression => VisitCallExpression(node as AstCallExpression),
            FastNodeType.ConditionalExpression => VisitConditionalExpression(node as AstConditionalExpression),
            FastNodeType.YieldExpression => VisitYieldExpression(node as AstYieldExpression),
            FastNodeType.ClassProperty => VisitClassProperty(node as AstClassProperty),
            FastNodeType.ReturnStatement => VisitReturnStatement(node as AstReturnStatement),
            FastNodeType.NewExpression => VisitNewExpression(node as AstNewExpression),
            FastNodeType.ImportStatement => VisitImportStatement(node as AstImportStatement),
            FastNodeType.ExportStatement => VisitExportStatement(node as AstExportStatement),
            FastNodeType.Meta => VisitMeta(node as AstMeta),
            FastNodeType.TaggedTemplateExpression => VisitTaggedTemplateExpression(node as AstTaggedTemplateExpression),
            FastNodeType.AwaitExpression => VisitAwaitExpression(node as AstAwaitExpression),
            _ => throw new NotImplementedException($"No implementation for {node.Type}"),
        };
    }

    protected abstract T VisitAwaitExpression(AstAwaitExpression node);
    protected abstract T VisitTaggedTemplateExpression(AstTaggedTemplateExpression astTaggedTemplateExpression);
    protected abstract T VisitMeta(AstMeta astMeta);
    protected abstract T VisitExportStatement(AstExportStatement astExportStatement);
    protected abstract T VisitImportStatement(AstImportStatement astImportStatement);
    protected abstract T VisitArrayPattern(AstArrayPattern arrayPattern);
    protected abstract T VisitNewExpression(AstNewExpression newExpression);
    protected abstract T VisitReturnStatement(AstReturnStatement returnStatement);
    protected virtual T VisitClassProperty(AstClassProperty property) => default;
    protected abstract T VisitBreakStatement(AstBreakStatement breakStatement);
    protected abstract T VisitLabeledStatement(AstLabeledStatement labeledStatement);
    protected abstract T VisitYieldExpression(AstYieldExpression yieldExpression);
    protected abstract T VisitCallExpression(AstCallExpression callExpression);
    protected abstract T VisitUnaryExpression(AstUnaryExpression unaryExpression);
    protected abstract T VisitTemplateExpression(AstTemplateExpression templateExpression);
    protected abstract T VisitObjectLiteral(AstObjectLiteral objectLiteral);
    protected abstract T VisitArrayExpression(AstArrayExpression arrayExpression);
    protected abstract T VisitEmptyExpression(AstEmptyExpression emptyExpression);
    protected abstract T VisitSwitchStatement(AstSwitchStatement switchStatement);
    protected abstract T VisitClassStatement(AstClassExpression classStatement);
    protected abstract T VisitMemberExpression(AstMemberExpression memberExpression);
    protected abstract T VisitLiteral(AstLiteral literal);
    protected abstract T VisitDebuggerStatement(AstDebuggerStatement debuggerStatement);
    protected abstract T VisitTryStatement(AstTryStatement tryStatement);
    protected abstract T VisitThrowStatement(AstThrowStatement throwStatement);
    protected abstract T VisitContinueStatement(AstContinueStatement continueStatement);
    protected abstract T VisitForOfStatement(AstForOfStatement forOfStatement, string label = null);
    protected abstract T VisitForInStatement(AstForInStatement forInStatement, string label = null);
    protected abstract T VisitForStatement(AstForStatement forStatement, string label = null);
    protected abstract T VisitSequenceExpression(AstSequenceExpression sequenceExpression);
    protected abstract T VisitDoWhileStatement(AstDoWhileStatement doWhileStatement, string label = null);
    protected abstract T VisitWhileStatement(AstWhileStatement whileStatement, string label = null);
    protected abstract T VisitIfStatement(AstIfStatement ifStatement);
    protected abstract T VisitSpreadElement(AstSpreadElement spreadElement);
    protected abstract T VisitObjectPattern(AstObjectPattern objectPattern);
    protected abstract T VisitIdentifier(AstIdentifier identifier);
    protected abstract T VisitFunctionExpression(AstFunctionExpression functionExpression);
    protected abstract T VisitExpressionStatement(AstExpressionStatement expressionStatement);
    protected abstract T VisitVariableDeclaration(AstVariableDeclaration variableDeclaration);
    protected abstract T VisitBinaryExpression(AstBinaryExpression binaryExpression);
    protected abstract T VisitProgram(AstProgram program);
    protected abstract T VisitBlock(AstBlock block);
    protected abstract T VisitConditionalExpression(AstConditionalExpression conditionalExpression);
}
