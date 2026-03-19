using Broiler.JavaScript.Core.Core;
using YantraJS.Expressions;

namespace Broiler.JavaScript.Core.Emit;


public delegate YExpression<JSFunctionDelegate> JSCodeCompiler();

public interface ICodeCache
{
    JSFunctionDelegate GetOrCreate(in JSCode code);
}
