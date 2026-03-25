using Broiler.JavaScript.Ast.Expressions;
using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Ast.Patterns;
using Broiler.JavaScript.Ast.Statements;
using Broiler.JavaScript.ExpressionCompiler.Core;
using System.Runtime.CompilerServices;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;

namespace Broiler.JavaScript.Compiler;

partial class FastCompiler
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void ExtractName(Sequence<StringSpan> list, AstNode node)
    {
        switch (node.Type)
        {
            case FastNodeType.VariableDeclaration:
                var vd = node as AstVariableDeclaration;
                var ve = vd.Declarators.GetFastEnumerator();

                while (ve.MoveNext(out var d))
                    ExtractName(list, d.Identifier);

                return;

            case FastNodeType.Identifier:
                var id = node as AstIdentifier;
                list.Add(id.Start.Span);
                return;

            case FastNodeType.ArrayPattern:
                var ap = node as AstArrayPattern;
                var ae = ap.Elements.GetFastEnumerator();

                while (ae.MoveNext(out var aitem))
                    ExtractName(list, aitem);

                return;

            case FastNodeType.ObjectPattern:
                var op = node as AstObjectPattern;
                var oe = op.Properties.GetFastEnumerator();

                while (oe.MoveNext(out var oitem))
                    ExtractName(list, oitem.Value);

                return;
        }
    }


    static IFastEnumerable<StringSpan> Names(AstNode expression)
    {
        var list = new Sequence<StringSpan>();
        ExtractName(list, expression);
        return list;
    }

    protected override YExpression VisitExportStatement(AstExportStatement exportStatement)
    {
        var exports = scope.Top.GetVariable("exports");
        var top = scope.Top;
        var declaration = exportStatement.Declaration;
        YExpression left;

        if (exportStatement.IsDefault)
        {
            var defExports = JSValueBuilder.Index(exports.Expression, KeyOfName("default"));
            return YExpression.Assign(defExports, Visit(declaration));
        }

        var list = new Sequence<YExpression>();

        try
        {
            switch (exportStatement.Declaration.Type)
            {
                case FastNodeType.VariableDeclaration:
                    var vd = Visit(declaration);
                    var names = Names(declaration);
                    var en = names.GetFastEnumerator();

                    list.Add(vd);

                    while (en.MoveNext(out var name))
                    {
                        left = JSValueBuilder.Index(exports.Expression, KeyOfName(name));
                        var right = top.GetVariable(name);
                        list.Add(YExpression.Assign(left, right.Expression));
                    }

                    return YExpression.Block(list);

                case FastNodeType.Identifier:
                    var id = exportStatement.Declaration as AstIdentifier;
                    return JSValueBuilder.Index(exports.Expression, KeyOfName(id.Name));

                case FastNodeType.FunctionExpression:
                    var fe = Visit(declaration);
                    var fd = declaration as AstFunctionExpression;

                    if (fd.Id != null)
                    {
                        left = JSValueBuilder.Index(exports.Expression, KeyOfName(fd.Id.Name));
                        return YExpression.Assign(left, fe);
                    }

                    break;

                case FastNodeType.ClassStatement:
                    var ce = Visit(declaration);
                    var cd = declaration as AstFunctionExpression;

                    if (cd.Id != null)
                    {
                        left = JSValueBuilder.Index(exports.Expression, KeyOfName(cd.Id.Name));
                        return YExpression.Assign(left, ce);
                    }

                    break;
            }

            throw new FastParseException(exportStatement.Start, $"Unexpected export type {exportStatement.Declaration.Type}");
        }
        finally
        {
        }
    }
}
