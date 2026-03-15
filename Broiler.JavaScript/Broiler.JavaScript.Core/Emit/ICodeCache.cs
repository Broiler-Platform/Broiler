#if !WEBATOMS
// using FastExpressionCompiler;
#endif
using YantraJS.Core;
using YantraJS.Expressions;


namespace YantraJS.Emit;


public delegate YExpression<JSFunctionDelegate> JSCodeCompiler();

public interface ICodeCache
{

    JSFunctionDelegate GetOrCreate(in JSCode code);


}
