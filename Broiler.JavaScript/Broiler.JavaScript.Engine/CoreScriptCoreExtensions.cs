using System.Runtime.CompilerServices;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.FastParser.Compiler;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.Core;

internal static class CoreScriptCoreExtensions
{
    [ModuleInitializer]
    internal static void InitializeFactories()
    {
        CoreScript.CreateDefaultCompiler = () => new DefaultJSCompiler();
        CoreScript.GetDefaultCodeCache = () => DictionaryCodeCache.Current;
        CoreScript.GetCurrentContext = () =>
        {
            var ctx = JSEngine.Current;
            return (ctx as JSValue, ctx?.CodeCache);
        };
        CoreScript.GetCurrentWaitTask = () => JSEngine.Current?.WaitTask;
        CoreScript.CreateSyntaxError = (msg, fn, path, line) =>
            JSEngine.NewSyntaxError(msg, fn, path, line);
        CoreScript.RunAsyncPump = AsyncPump.Run;
    }
}
