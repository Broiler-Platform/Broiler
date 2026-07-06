using Broiler.Graphics;

namespace Broiler.UI;

public interface IUiHost
{
    BSize ViewportSize { get; }

    double Scale { get; }

    BRenderList CreateRenderList(int capacity = 0);

    void Invalidate(UiInvalidation invalidation);

    void Present(BRenderList renderList);
}

