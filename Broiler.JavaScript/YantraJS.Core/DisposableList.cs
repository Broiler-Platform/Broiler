#nullable enable
using System;

namespace YantraJS.Core;

public class DisposableList : IDisposable
{

    private Sequence<IDisposable>? list;

    public void Register(IDisposable d)
    {
        list = list ?? [];
        list.Add(d);
    }

    public void Dispose()
    {
        var l = list;
        list = null;
        if (l != null) {
            foreach (var i in l)
            {
                i.Dispose();
            }
        }
    }
}
