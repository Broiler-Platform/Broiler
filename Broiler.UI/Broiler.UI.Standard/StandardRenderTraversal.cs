using System;
using Broiler.Graphics;

namespace Broiler.UI.Standard;

public static class StandardRenderTraversal
{
    public static BRenderList Render(UiSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.RenderFrame();
    }
}

