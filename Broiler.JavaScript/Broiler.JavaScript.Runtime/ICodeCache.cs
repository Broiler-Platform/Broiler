using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.Runtime;


/// <summary>
/// Factory delegate that compiles JavaScript source code into an
/// expression tree representing a callable function.
/// </summary>
public delegate YExpression<JSFunctionDelegate> JSCodeCompiler();

/// <summary>
/// Defines the contract for caching compiled JavaScript code.
/// Implementations map a <see cref="JSCode"/> descriptor (source text,
/// location, and argument list) to a ready-to-invoke
/// <see cref="JSFunctionDelegate"/>, compiling on the first access and
/// returning the cached delegate on subsequent calls.
/// </summary>
public interface ICodeCache
{
    /// <summary>
    /// Returns a cached <see cref="JSFunctionDelegate"/> for the given
    /// <paramref name="code"/>, compiling it via
    /// <see cref="JSCode.Compiler"/> on the first invocation.
    /// </summary>
    /// <param name="code">
    /// The JavaScript code descriptor containing the source text, source
    /// location, optional argument list, and the compiler delegate to use
    /// when the code is not yet cached.
    /// </param>
    /// <returns>A compiled delegate ready for invocation.</returns>
    JSFunctionDelegate GetOrCreate(in JSCode code);
}
