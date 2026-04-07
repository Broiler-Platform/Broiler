using Broiler.JavaScript.Engine.Core;
using System;
using System.Runtime.CompilerServices;

namespace Broiler.JavaScript.Engine;

public static class JSContextExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void EnsureSufficientExecutionStack(this IJSExecutionContext context)
    {
#if NETSTANDARD2_1_OR_GREATER
        if(RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            return;
        }
#else
        try
        {
            RuntimeHelpers.EnsureSufficientExecutionStack();
            return;
        }
        catch (Exception ex)
        {
            if (ex is not InsufficientExecutionStackException)
            {
                throw;
            }
        }
#endif
        throw JSEngine.NewRangeError("Maximum call stack size exceeded");
    }
}
