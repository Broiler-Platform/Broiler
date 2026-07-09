using System;
using System.Collections.Generic;
using Broiler.Graphics;

namespace Broiler.UI;

public sealed class UiRenderContext
{
    private readonly List<Action<UiRenderContext>> _deferredRender = [];

    public UiRenderContext(BRenderList renderList, UiSession session, IUiHost host)
    {
        RenderList = renderList ?? throw new ArgumentNullException(nameof(renderList));
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public BRenderList RenderList { get; }

    public UiSession Session { get; }

    public IUiHost Host { get; }

    public void Defer(Action<UiRenderContext> render)
    {
        ArgumentNullException.ThrowIfNull(render);
        _deferredRender.Add(render);
    }

    internal void FlushDeferred()
    {
        for (int index = 0; index < _deferredRender.Count; index++)
            _deferredRender[index](this);

        _deferredRender.Clear();
    }
}
