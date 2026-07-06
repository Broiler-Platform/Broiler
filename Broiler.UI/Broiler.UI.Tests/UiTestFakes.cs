using Broiler.Graphics;
using Broiler.UI;

namespace Broiler.UI.Tests;

internal sealed class RecordingUiHost : IUiHost
{
    public RecordingUiHost(BSize viewportSize)
    {
        ViewportSize = viewportSize;
    }

    public BSize ViewportSize { get; set; }

    public double Scale { get; set; } = 1.0;

    public List<UiInvalidation> Invalidations { get; } = [];

    public List<BRenderList> Presented { get; } = [];

    public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

    public void Invalidate(UiInvalidation invalidation) => Invalidations.Add(invalidation);

    public void Present(BRenderList renderList) => Presented.Add(renderList);
}

internal sealed class ManualUiClock : IUiClock
{
    public UiTimestamp Now { get; private set; }

    public void Advance(TimeSpan elapsed) => Now = new UiTimestamp(Now.Elapsed + elapsed);
}

internal sealed class InlineUiDispatcher : IUiDispatcher
{
    public bool CheckAccess() => true;

    public void Post(Action callback) => callback();
}

internal sealed class TestElement : UiElement
{
    private readonly List<string>? _disposeOrder;

    public TestElement(string name, List<string>? disposeOrder = null)
    {
        Name = name;
        _disposeOrder = disposeOrder;
    }

    public string Name { get; }

    public int AttachedCount { get; private set; }

    public int DetachedCount { get; private set; }

    public int MeasureCount { get; private set; }

    public int ArrangeCount { get; private set; }

    public int RenderCount { get; private set; }

    public int InputCount { get; private set; }

    public bool HandlesInput { get; set; }

    protected override void OnAttached() => AttachedCount++;

    protected override void OnDetached() => DetachedCount++;

    protected override BSize MeasureCore(BSize availableSize)
    {
        MeasureCount++;
        base.MeasureCore(availableSize);
        return new BSize(Math.Min(availableSize.Width, 16), Math.Min(availableSize.Height, 12));
    }

    protected override void ArrangeCore(BRect finalRect)
    {
        ArrangeCount++;
        base.ArrangeCore(finalRect);
    }

    protected override void RenderCore(UiRenderContext context)
    {
        RenderCount++;
        context.RenderList.FillRect(Bounds, BColor.Blue);
        base.RenderCore(context);
    }

    protected override bool OnInput(UiInputEvent input)
    {
        InputCount++;
        return HandlesInput;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _disposeOrder?.Add(Name);
    }
}

