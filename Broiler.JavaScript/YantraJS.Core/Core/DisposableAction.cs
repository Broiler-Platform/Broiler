using System;

namespace YantraJS.Core;

public class CancellableDisposableAction(Action action) : IDisposable
{
    public void Cancel() => action = null;

    public T Commit<T>(T value)
    {
        action = null;
        return value;
    }

    public bool Commit()
    {
        action = null;
        return true;
    }


    public void Dispose() => action?.Invoke();
}
