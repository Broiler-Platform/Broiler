using System.Runtime.CompilerServices;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Promise;
using Broiler.JavaScript.Core.Emit;
using Broiler.JavaScript.Core.FastParser.Compiler;

namespace Broiler.JavaScript.Core;

/// <summary>
/// Initializes <see cref="CoreScript"/> factory delegates so that the
/// Runtime-hosted <see cref="CoreScript"/> can access Core-only types
/// (<see cref="JSContext"/>, <see cref="DefaultJSCompiler"/>,
/// <see cref="AsyncPump"/>, etc.) without a direct assembly reference.
/// </summary>
internal static class CoreScriptCoreExtensions
{
    [ModuleInitializer]
    internal static void InitializeFactories()
    {
        CoreScript.CreateDefaultCompiler = () => new DefaultJSCompiler();
        CoreScript.GetDefaultCodeCache = () => DictionaryCodeCache.Current;
        CoreScript.GetCurrentContext = () =>
        {
            var ctx = JSContext.Current;
            return ((JSValue)ctx, ctx?.CodeCache);
        };
        CoreScript.GetCurrentWaitTask = () => JSContext.Current?.WaitTask;
        CoreScript.CreateSyntaxError = (msg, fn, path, line) =>
            JSContext.NewSyntaxError(msg, fn, path, line);
        CoreScript.RunAsyncPump = AsyncPump.Run;
    }
}
