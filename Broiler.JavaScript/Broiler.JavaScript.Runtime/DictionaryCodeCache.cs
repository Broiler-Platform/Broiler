using Broiler.JavaScript.ExpressionCompiler.Runtime;
using Broiler.JavaScript.Storage;

namespace Broiler.JavaScript.Runtime;

public class DictionaryCodeCache : ICodeCache
{
    private static readonly ConcurrentStringMap<JSFunctionDelegate> cache = ConcurrentStringMap<JSFunctionDelegate>.Create();

    public static ICodeCache Current = new DictionaryCodeCache();

    public JSFunctionDelegate GetOrCreate(in JSCode code)
    {
        var compiler = code.Compiler;
        return cache.GetOrCreate(code.Key, (k) =>
        {
            var exp = compiler();
            return exp.CompileWithNestedLambdas();
        });
    }
}
