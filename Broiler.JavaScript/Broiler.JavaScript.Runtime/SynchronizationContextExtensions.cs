using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Broiler.JavaScript.Runtime;

internal static class SynchronizationContextExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Post<T>(this SynchronizationContext ctx, T value, Action<T> item) => ctx.Post((x) =>
    {
        item((T)x);
    }, value);
}
