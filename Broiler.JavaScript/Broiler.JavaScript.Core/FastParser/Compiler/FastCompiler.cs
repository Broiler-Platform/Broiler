using Broiler.JavaScript.Core.CodeGen;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Emit;
using Broiler.JavaScript.Core.FastParser.Ast;
using Broiler.JavaScript.Core.FastParser.Parser;
using Broiler.JavaScript.Core.LinqExpressions;
using Broiler.JavaScript.Core.LinqExpressions.GeneratorsV2;
using Broiler.JavaScript.Core.Utils;
using System;
using System.Collections.Generic;
using Exp = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using ParameterExpression = Broiler.JavaScript.ExpressionCompiler.Expressions.YParameterExpression;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Core;

namespace Broiler.JavaScript.Core.FastParser.Compiler;

public partial class FastCompiler : AstMapVisitor<Expression>
{
    private readonly FastPool pool;

    readonly LinkedStack<FastFunctionScope> scope = new();
    private readonly string location;

    public LoopScope LoopScope => scope.Top.Loop.Top;

    private StringArray _keyStrings = new();
    private ParameterExpression scriptInfo;

    public YExpression<JSFunctionDelegate> Method { get; }

    public FastCompiler(in StringSpan code, string location = null, IList<string> argsList = null, ICodeCache codeCache = null)
    {
        pool = new FastPool();

        location = location ?? "vm.js";
        this.location = location;

        // add top level...

        var parserPool = new FastPool();
        var parser = new FastParser(new FastTokenStream(parserPool, code));
        var jScript = parser.ParseProgram();
        parserPool.Dispose();

        using var fx = scope.Push(new FastFunctionScope(pool, null, isAsync: jScript.IsAsync));

        var lScope = fx.Context;

        if (argsList != null && jScript.HoistingScope != null)
        {
            var list = new Sequence<StringSpan>(jScript.HoistingScope.Count);
            var e = jScript.HoistingScope.GetFastEnumerator();

            while (e.MoveNext(out var a))
            {
                if (argsList.Contains(a.Value))
                    continue;

                list.Add(a);
            }

            jScript.HoistingScope = list;
        }

        scriptInfo = Exp.Parameter(typeof(ScriptInfo));

        var args = fx.ArgumentsExpression;
        var te = ArgumentsBuilder.This(args);
        var stackItem = fx.StackItem;
        var vList = new Sequence<ParameterExpression>() { scriptInfo, lScope, stackItem };

        if (argsList != null)
        {
            int i = 0;
            foreach (var arg in argsList)
            {
                // global arguments are set here for FunctionConstructor
                fx.CreateVariable(arg, JSVariableBuilder.FromArgument(fx.ArgumentsExpression, i++, arg));
            }
        }

        var l = fx.ReturnLabel;
        var script = Visit(jScript);
        var sList = new Sequence<Exp>()
        {
            Exp.Assign(scriptInfo, ScriptInfoBuilder.New(location,code.Value)),
            Exp.Assign(lScope, JSContextBuilder.Current)
        };

        JSContextStackBuilder.Push(sList, lScope, stackItem, Exp.Constant(location), StringSpanBuilder.Empty, 0, 0);
        sList.Add(ScriptInfoBuilder.Build(scriptInfo, _keyStrings));

        vList.AddRange(fx.VariableParameters);
        sList.AddRange(fx.InitList);

        // register globals..
        foreach (var v in fx.Variables)
        {
            if (v.Variable != null && v.Variable.Type == typeof(JSVariable))
            {
                if (argsList?.Contains(v.Name) ?? false)
                    continue;

                if (v.Name == "this")
                    continue;

                sList.Add(JSContextBuilder.Register(lScope, v.Variable));
            }
        }

        sList.Add(Exp.Return(l, script.ToJSValue()));
        sList.Add(Exp.Label(l, JSUndefinedBuilder.Value));

        script = Exp.Block(vList, Exp.TryFinally(Exp.Block(sList), JSContextStackBuilder.Pop(stackItem, lScope)));

        if (jScript.IsAsync)
        {
            var g = GeneratorRewriter.Rewrite("vm", script, fx.ReturnLabel, fx.Generator, replaceArgs: fx.Arguments, replaceStackItem: fx.StackItem,
                replaceContext: fx.Context, replaceScriptInfo: scriptInfo);

            var jsf = JSAsyncFunctionBuilder.Create(JSGeneratorFunctionBuilderV2.New(g, StringSpanBuilder.New("vm"), StringSpanBuilder.New(code.Value)));
            var np = Expression.Parameter(ArgumentsBuilder.refType, "a");

            jsf = JSFunctionBuilder.InvokeFunction(jsf, np);

            Method = Exp.Lambda<JSFunctionDelegate>("vm", jsf, [np]);
            return;
        }

        var lambda = Exp.Lambda<JSFunctionDelegate>("body", script, fx.Arguments);
        Method = lambda;
    }

    private Expression VisitExpression(AstExpression exp) => Visit(exp);

    private Expression VisitStatement(AstStatement exp) => Visit(exp);

    protected override Expression VisitClassStatement(AstClassExpression classStatement) => CreateClass(classStatement.Identifier, classStatement.Base, classStatement);

    protected override Expression VisitContinueStatement(AstContinueStatement continueStatement)
    {
        string name = continueStatement.Label?.Name.Value;
        if (name != null)
        {
            var target = LoopScope.Get(name);
            return target == null ? throw JSContext.NewSyntaxError($"No label found for {name}") : Exp.Continue(target.Break);
        }

        return Exp.Continue(scope.Top.Loop.Top.Continue);
    }

    protected override Expression VisitDebuggerStatement(AstDebuggerStatement debuggerStatement) => JSDebuggerBuilder.RaiseBreak();

    protected override Expression VisitEmptyExpression(AstEmptyExpression emptyExpression) => Exp.Empty;

    protected override Expression VisitExpressionStatement(AstExpressionStatement expressionStatement) => Visit(expressionStatement.Expression);

    protected override Expression VisitFunctionExpression(AstFunctionExpression functionExpression) => CreateFunction(functionExpression);

    protected override Expression VisitSpreadElement(AstSpreadElement spreadElement) => throw new NotImplementedException();

    protected override Expression VisitThrowStatement(AstThrowStatement throwStatement) => JSExceptionBuilder.Throw(VisitExpression(throwStatement.Argument));

    protected override Expression VisitYieldExpression(AstYieldExpression yieldExpression)
    {
        var target = VisitExpression(yieldExpression.Argument);
        return Expression.Yield(target, yieldExpression.Delegate);
    }
}
