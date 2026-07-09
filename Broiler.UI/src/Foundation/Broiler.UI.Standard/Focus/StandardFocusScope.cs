using System;

namespace Broiler.UI.Standard;

public sealed class StandardFocusScope
{
    private readonly UiSession _session;

    public StandardFocusScope(UiSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public UiElement? FocusedElement => _session.FocusedElement;

    public bool TryFocus(UiElement? element)
    {
        if (element is not null && element.Session != _session)
            return false;

        _session.SetFocus(element);
        return true;
    }
}

