using System;
using Broiler.Graphics;
using Broiler.UI;

namespace Broiler.UI.Standard;

public static class StandardRenderTraversal
{
    public static BRenderList Render(UiSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.RenderFrame();
    }
}

