using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Storage;
using YantraJS.Runtime;

namespace Broiler.JavaScript.Core.Emit;

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
