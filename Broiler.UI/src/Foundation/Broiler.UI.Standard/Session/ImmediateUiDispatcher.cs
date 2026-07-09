using System;

namespace Broiler.UI.Standard;

public sealed class ImmediateUiDispatcher : IUiDispatcher
{
    public bool CheckAccess() => true;

    public void Post(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        callback();
    }
}

