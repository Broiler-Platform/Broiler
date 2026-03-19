using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.Core.Emit;


public delegate YExpression<JSFunctionDelegate> JSCodeCompiler();

public interface ICodeCache
{
    JSFunctionDelegate GetOrCreate(in JSCode code);
}
