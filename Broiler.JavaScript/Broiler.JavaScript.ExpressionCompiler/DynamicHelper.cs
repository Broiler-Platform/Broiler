using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Broiler.JavaScript.ExpressionCompiler;

internal static class DynamicHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T CompileFast<T>(this Expression<T> exp)
    {
        var fx = exp.FastCompile();
        return fx;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T CompileDynamic<T>(this Expression<T> exp)
    {
        var fx = exp.CompileInAssembly();
        return fx;
    }
}
