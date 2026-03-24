#nullable enable
using System;
using Broiler.JavaScript.ExpressionCompiler.Core;

namespace Broiler.JavaScript.Parser;

public class DisposableList : IDisposable
{
    private Sequence<IDisposable>? list;

    public void Register(IDisposable d)
    {
        list ??= [];
        list.Add(d);
    }

    public void Dispose()
    {
        var l = list;
        list = null;

        if (l == null)
            return;

        foreach (var i in l)
            i.Dispose();
    }
}
