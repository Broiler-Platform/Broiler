using System;
using Broiler.Graphics;

namespace Broiler.UI.Standard;

public sealed class StandardHitTestService
{
    private readonly UiSession _session;

    public StandardHitTestService(UiSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public UiElement? HitTest(BPoint point) => _session.HitTest(point);
}

