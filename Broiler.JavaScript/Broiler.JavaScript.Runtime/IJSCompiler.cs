using System.Collections.Generic;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.Ast.Misc;

namespace Broiler.JavaScript.Runtime;

/// <summary>
/// Defines the contract for compiling JavaScript source code into an executable
/// expression tree. Implementations convert source code through parsing and AST
/// compilation to produce a <see cref="YExpression{T}"/> of
/// <see cref="JSFunctionDelegate"/>.
/// </summary>
public interface IJSCompiler
{
    /// <summary>
    /// Compiles JavaScript source code into an expression tree that can be
    /// further compiled to a callable delegate.
    /// </summary>
    /// <param name="code">The JavaScript source code to compile.</param>
    /// <param name="location">Optional source location identifier for diagnostics.</param>
    /// <param name="argsList">Optional list of argument names for function-style compilation.</param>
    /// <param name="codeCache">Optional code cache for compiled script reuse.</param>
    /// <returns>A compiled expression tree representing the JavaScript program.</returns>
    YExpression<JSFunctionDelegate> Compile(in StringSpan code, string location = null, IList<string> argsList = null, ICodeCache codeCache = null);
}
