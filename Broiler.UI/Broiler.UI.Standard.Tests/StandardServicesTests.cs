using Broiler.Graphics;
using Broiler.UI;
using Broiler.UI.Standard;

namespace Broiler.UI.Standard.Tests;

public sealed class StandardServicesTests
{
    [Fact]
    public void Session_Builder_Creates_Session_With_Default_Dispatcher_And_Clock()
    {
        var host = new TestHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);

        Assert.True(session.Dispatcher.CheckAccess());
        Assert.True(session.Clock.Now.Elapsed >= TimeSpan.Zero);
    }

    [Fact]
    public void Render_Traversal_Uses_Session_Render_Frame()
    {
        var host = new TestHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        session.AddRoot(new PaintElement());

        BRenderList renderList = StandardRenderTraversal.Render(session);

        Assert.Single(host.Presented);
        Assert.Single(renderList.Commands);
    }

    private sealed class TestHost : IUiHost
    {
        public BSize ViewportSize { get; } = new(32, 24);

        public double Scale => 1.0;

        public List<BRenderList> Presented { get; } = [];

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation)
        {
        }

        public void Present(BRenderList renderList) => Presented.Add(renderList);
    }

    private sealed class PaintElement : UiElement
    {
        protected override void RenderCore(UiRenderContext context)
        {
            context.RenderList.FillRect(Bounds, BColor.Red);
        }
    }
}

