using Broiler.HtmlBridge;
using Broiler.HTML.Image;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// The top layer inside a nested browsing context — a modal <c>&lt;dialog&gt;</c> in a frame.
/// <para>
/// The renderer's UA sheet has no <c>dialog:modal</c> rule: the bridge supplies it, baking
/// <c>position: fixed</c>, the <c>inset: 0</c>/<c>margin: auto</c> centring and the
/// <c>::backdrop</c> scrim onto the element before serialization. That pass only ever walked the
/// main document, and a frame's document is severed from it, so a modal dialog in a frame got none
/// of it — it laid out as an ordinary absolutely-positioned box in its <c>&lt;body&gt;</c>, with no
/// scrim behind it. WPT <c>css-view-transitions/dialog-in-rtl-iframe</c> is exactly that: the dialog
/// landed in the frame's top corner instead of centred, over white instead of grey (issue #1552).
/// </para>
/// </summary>
public class FrameTopLayerRenderTests
{
    private const int FrameWidth = 400;
    private const int FrameHeight = 300;

    /// <summary>The child document: a dialog the parent can open, sized so a centred one and a
    /// corner-anchored one never overlap.</summary>
    private const string Child = """
<style>
  body { margin: 0; background-color: white }
  dialog { width: 100px; height: 100px; box-sizing: border-box; background-color: limegreen; border: 0; padding: 0 }
</style>
<dialog id='d'></dialog>
""";

    /// <summary>
    /// Renders a page whose frame is sized by a <em>stylesheet rule</em> — the shape that also
    /// exercised the frame viewport — after running <paramref name="script"/>, and returns the
    /// pixel at the centre of the frame and one in its top-left corner.
    /// </summary>
    private static ((int R, int G, int B) Centre, (int R, int G, int B) Corner) Render(
        string script, bool nativeBackdrop = true)
    {
        var html = $$"""
<!DOCTYPE html>
<body style="margin:0;background:blue">
<style>iframe { width: {{FrameWidth}}px; height: {{FrameHeight}}px; border: 0 }</style>
<iframe srcdoc="{{Child.Replace("\n", " ")}}"></iframe>
</body>
""";
        using var context = new JSContext();
        var bridge = new DomBridge { NativeBackdrop = nativeBackdrop };
        bridge.Attach(context, html, "file:///frame-top-layer.html");
        bridge.FireWindowLoadEvent();
        bridge.FlushTimers();
        context.Eval(script);
        bridge.FlushTimers();
        bridge.ResolveAnchorPositions();

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(bridge.SerializeToHtml(), 600, 500);
        var centre = bitmap.GetPixel(FrameWidth / 2, FrameHeight / 2);
        var corner = bitmap.GetPixel(20, 20);
        return ((centre.R, centre.G, centre.B), (corner.R, corner.G, corner.B));
    }

    private const string ShowModal = "document.querySelector('iframe').contentDocument.getElementById('d').showModal();";
    private const string Show = "document.querySelector('iframe').contentDocument.getElementById('d').show();";

    private static readonly (int R, int G, int B) LimeGreen = (50, 205, 50);
    private static readonly (int R, int G, int B) White = (255, 255, 255);

    /// <summary>The UA scrim a modal dialog's <c>::backdrop</c> paints over a white canvas — the
    /// same value the main document's dialogs resolve.</summary>
    private static readonly (int R, int G, int B) Scrim = (229, 229, 229);

    /// <summary>The bug itself: a modal dialog in a frame is centred in that frame's viewport.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ModalDialogInFrame_IsCentredInTheFrameViewport()
    {
        var (centre, _) = Render(ShowModal);

        Assert.Equal(LimeGreen, centre);
    }

    /// <summary>And its <c>::backdrop</c> scrim paints behind it, over the rest of the frame.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ModalDialogInFrame_PaintsItsBackdrop()
    {
        var (_, corner) = Render(ShowModal);

        Assert.Equal(Scrim, corner);
    }

    /// <summary>
    /// The same on the synthesized-<c>&lt;div&gt;</c> backdrop path — the mode the WPT runner runs
    /// in. That div is sized in pixels from the frame's viewport, so it is the case that needs the
    /// frame's stylesheet-set size to be readable at all: against a 0×0 viewport the scrim was
    /// generated and then painted nothing.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ModalDialogInFrame_PaintsItsBackdrop_WhenSynthesizedAsADiv()
    {
        var (_, corner) = Render(ShowModal, nativeBackdrop: false);

        Assert.Equal(Scrim, corner);
    }

    /// <summary>
    /// A non-modal <c>show()</c> is not a top-layer dialog: no centring, no scrim. It stays where
    /// normal flow puts it, so the frame's corner is its own white canvas and the centre is empty.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void NonModalDialogInFrame_IsLeftInNormalFlow()
    {
        var (centre, corner) = Render(Show);

        Assert.Equal(LimeGreen, corner);
        Assert.Equal(White, centre);
    }

    /// <summary>A frame with no open dialog is untouched — the frame's own canvas, no scrim.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void FrameWithNoOpenDialog_IsUntouched()
    {
        var (centre, corner) = Render(string.Empty);

        Assert.Equal(White, centre);
        Assert.Equal(White, corner);
    }
}
