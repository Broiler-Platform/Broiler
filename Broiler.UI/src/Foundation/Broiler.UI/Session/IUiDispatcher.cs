using System;

namespace Broiler.UI;

public interface IUiDispatcher
{
    bool CheckAccess();

    void Post(Action callback);
}

