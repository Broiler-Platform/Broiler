namespace Broiler.Cli.Tests;

/// <summary>
/// The Canvas 2D context's pixel surface: drawing rasterises into a real bitmap, and
/// <c>getImageData</c>/<c>putImageData</c>/<c>createImageData</c>/<c>toDataURL</c> read it back.
///
/// Before this, the context was a style-state stub whose drawing methods were empty bodies and whose
/// pixel API did not exist, so <c>ctx.getImageData(...)</c> and <c>canvas.toDataURL(...)</c> threw
/// <c>TypeError: undefined is not a function</c> — three of the four exceptions html5test.com's feature
/// probes hit (<c>engine.js</c> lines 3030, 3055 and 3071). The last three tests here are those probes
/// verbatim. See <c>docs/html5test-exceptions.md</c>.
/// </summary>
public class CanvasPixelSurfaceTests
{
    private static string Run(string script)
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
" + script + @"
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";
        return CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
    }

    [Fact(Timeout = 600000)]
    public void GetContext_Returns_The_Same_Context_Every_Call()
    {
        // Not a nicety: a fresh context per call would hand back a fresh blank bitmap, so nothing a
        // page drew through an earlier reference would ever be readable.
        var result = Run(@"
var c = document.createElement('canvas');
r.push(c.getContext('2d') === c.getContext('2d'));
r.push(c.getContext('2d') !== document.createElement('canvas').getContext('2d'));
r.push(c.getContext('2d').canvas === c);
");
        Assert.Contains("true,true,true", result);
    }

    [Fact(Timeout = 600000)]
    public void FillRect_Is_Readable_Through_GetImageData()
    {
        var result = Run(@"
var c = document.createElement('canvas');
c.width = 4; c.height = 4;
var ctx = c.getContext('2d');
ctx.fillStyle = 'rgb(10, 20, 30)';
ctx.fillRect(0, 0, 2, 2);
var d = ctx.getImageData(0, 0, 1, 1).data;
r.push(d[0] + '/' + d[1] + '/' + d[2] + '/' + d[3]);
// Outside the filled rect the canvas is still transparent black.
var outside = ctx.getImageData(3, 3, 1, 1).data;
r.push(outside[0] + '/' + outside[3]);
");
        Assert.Contains("10/20/30/255,0/0", result);
    }

    [Fact(Timeout = 600000)]
    public void ClearRect_Restores_Transparency()
    {
        var result = Run(@"
var c = document.createElement('canvas');
c.width = 2; c.height = 2;
var ctx = c.getContext('2d');
ctx.fillStyle = '#ffffff';
ctx.fillRect(0, 0, 2, 2);
r.push(ctx.getImageData(0, 0, 1, 1).data[3]);
ctx.clearRect(0, 0, 1, 1);
r.push(ctx.getImageData(0, 0, 1, 1).data[3]);
// The pixel next to it is untouched.
r.push(ctx.getImageData(1, 0, 1, 1).data[3]);
");
        Assert.Contains("255,0,255", result);
    }

    [Fact(Timeout = 600000)]
    public void GlobalAlpha_Is_Composited_Into_The_Fill()
    {
        var result = Run(@"
var c = document.createElement('canvas');
c.width = 1; c.height = 1;
var ctx = c.getContext('2d');
ctx.globalAlpha = 0.5;
ctx.fillStyle = '#ffffff';
ctx.fillRect(0, 0, 1, 1);
var a = ctx.getImageData(0, 0, 1, 1).data[3];
r.push(a > 100 && a < 155);
");
        Assert.Contains("true", result);
    }

    [Fact(Timeout = 600000)]
    public void GetImageData_Reads_Out_Of_Bounds_As_Transparent_Black()
    {
        var result = Run(@"
var c = document.createElement('canvas');
c.width = 1; c.height = 1;
var ctx = c.getContext('2d');
ctx.fillStyle = '#ffffff';
ctx.fillRect(0, 0, 1, 1);
var d = ctx.getImageData(0, 0, 2, 2);
r.push(d.width + 'x' + d.height);
r.push(d.data.length);
r.push(d.data[3]);   // inside  -> opaque
r.push(d.data[7]);   // outside -> transparent
");
        Assert.Contains("2x2,16,255,0", result);
    }

    [Fact(Timeout = 600000)]
    public void PutImageData_Writes_Pixels_Back()
    {
        var result = Run(@"
var c = document.createElement('canvas');
c.width = 2; c.height = 1;
var ctx = c.getContext('2d');
var img = ctx.createImageData(1, 1);
r.push(img.width + 'x' + img.height + '/' + img.data.length);
img.data[0] = 1; img.data[1] = 2; img.data[2] = 3; img.data[3] = 255;
ctx.putImageData(img, 1, 0);
var back = ctx.getImageData(1, 0, 1, 1).data;
r.push(back[0] + '/' + back[1] + '/' + back[2] + '/' + back[3]);
// putImageData ignores globalAlpha and the composite operator, and leaves its neighbour alone.
r.push(ctx.getImageData(0, 0, 1, 1).data[3]);
");
        Assert.Contains("1x1/4,1/2/3/255,0", result);
    }

    [Fact(Timeout = 600000)]
    public void ImageData_Data_Is_A_Real_Uint8ClampedArray()
    {
        var result = Run(@"
var ctx = document.createElement('canvas').getContext('2d');
var d = ctx.createImageData(1, 1);
r.push(d.data instanceof Uint8ClampedArray);
r.push(Object.prototype.toString.call(d.data));
// Clamping is the typed array's, not ours.
d.data[0] = 300; r.push(d.data[0]);
d.data[1] = -5;  r.push(d.data[1]);
");
        Assert.Contains("true,[object Uint8ClampedArray],255,0", result);
    }

    [Fact(Timeout = 600000)]
    public void Canvas_And_ImageData_Interfaces_Answer_Instanceof()
    {
        var result = Run(@"
var c = document.createElement('canvas');
var ctx = c.getContext('2d');
r.push(typeof CanvasRenderingContext2D === 'function');
r.push(ctx instanceof CanvasRenderingContext2D);
r.push(!(c instanceof CanvasRenderingContext2D));
r.push(typeof ImageData === 'function');
r.push(ctx.createImageData(1, 1) instanceof ImageData);
r.push(new ImageData(2, 2) instanceof ImageData);
r.push(new ImageData(2, 2).data.length);
");
        Assert.Contains("true,true,true,true,true,true,16", result);
    }

    [Fact(Timeout = 600000)]
    public void ToDataUrl_Encodes_Png_And_Jpeg_And_Falls_Back_For_Unsupported_Types()
    {
        // HTML: an image format the user agent does not support falls back to image/png rather than
        // throwing, so the returned URL names the type actually produced.
        var result = Run(@"
var c = document.createElement('canvas');
c.width = 2; c.height = 2;
var ctx = c.getContext('2d');
ctx.fillStyle = '#ff0000';
ctx.fillRect(0, 0, 2, 2);
r.push(c.toDataURL().substring(0, 22));
r.push(c.toDataURL('image/png').substring(0, 22));
r.push(c.toDataURL('image/jpeg').substring(0, 23));
r.push(c.toDataURL('image/webp').substring(0, 22));
r.push(c.toDataURL('image/vnd.ms-photo').substring(0, 22));
r.push(c.toDataURL().length > 30);
");
        Assert.Contains(
            "data:image/png;base64,,data:image/png;base64,,data:image/jpeg;base64,," +
            "data:image/png;base64,,data:image/png;base64,,true",
            result);
    }

    [Fact(Timeout = 600000)]
    public void ToDataUrl_On_A_Zero_Area_Canvas_Is_The_Empty_Data_Url()
    {
        var result = Run(@"
var c = document.createElement('canvas');
c.setAttribute('width', '0');
r.push(c.toDataURL());
r.push(c.toDataURL('image/png'));
");
        Assert.Contains("data:,,data:,", result);
    }

    [Fact(Timeout = 600000)]
    public void GlobalCompositeOperation_Rejects_An_Operator_It_Cannot_Composite()
    {
        // An unrecognised value must be ignored and the state keep its previous value. The context
        // treats an operator it cannot actually apply the same way, so the round-trip a feature
        // detector reads is never a claim it did not honour.
        var result = Run(@"
var ctx = document.createElement('canvas').getContext('2d');
r.push(ctx.globalCompositeOperation);
ctx.globalCompositeOperation = 'screen';
r.push(ctx.globalCompositeOperation);
ctx.globalCompositeOperation = 'not-a-real-operator';
r.push(ctx.globalCompositeOperation);
");
        Assert.Contains("source-over,screen,screen", result);
    }

    [Fact(Timeout = 600000)]
    public void Paths_And_Text_Put_Pixels_On_The_Bitmap()
    {
        // The point is not the exact coverage — it is that these are no longer empty bodies, so a
        // readback of a canvas that was drawn on does not report a blank one.
        var result = Run(@"
var c = document.createElement('canvas');
c.width = 40; c.height = 40;
var ctx = c.getContext('2d');
ctx.fillStyle = '#000000';
ctx.beginPath();
ctx.moveTo(2, 2); ctx.lineTo(38, 2); ctx.lineTo(38, 38); ctx.closePath();
ctx.fill();
var painted = 0, d = ctx.getImageData(0, 0, 40, 40).data;
for (var i = 3; i < d.length; i += 4) if (d[i] > 0) painted++;
r.push(painted > 100);

var t = document.createElement('canvas');
t.width = 120; t.height = 40;
var tctx = t.getContext('2d');
tctx.font = '20px sans-serif';
tctx.fillStyle = '#000000';
tctx.fillText('Hi', 10, 30);
var inked = 0, td = tctx.getImageData(0, 0, 120, 40).data;
for (var j = 3; j < td.length; j += 4) if (td[j] > 0) inked++;
r.push(inked > 0);
");
        Assert.Contains("true,true", result);
    }

    [Fact(Timeout = 600000)]
    public void Width_And_Height_Default_And_Reflect_The_Content_Attribute()
    {
        var result = Run(@"
var c = document.createElement('canvas');
r.push(c.width + 'x' + c.height);
c.width = 40; c.height = 20;
r.push(c.width + 'x' + c.height);
r.push(c.getAttribute('width') + 'x' + c.getAttribute('height'));
// The bitmap follows the attribute, so the readback bounds move with it.
var ctx = c.getContext('2d');
ctx.fillStyle = '#ffffff';
ctx.fillRect(0, 0, 40, 20);
r.push(ctx.getImageData(39, 19, 1, 1).data[3]);
");
        Assert.Contains("300x150,40x20,40x20,255", result);
    }

    [Fact(Timeout = 600000)]
    public void Assigning_Width_Resets_The_Bitmap_And_The_Drawing_State()
    {
        // `canvas.width = canvas.width` is the idiomatic way to clear a canvas: HTML resets the
        // bitmap on every assignment, including one that does not change the value.
        var result = Run(@"
var c = document.createElement('canvas');
c.width = 4; c.height = 4;
var ctx = c.getContext('2d');
ctx.fillStyle = '#ff0000';
ctx.globalAlpha = 0.25;
ctx.fillRect(0, 0, 4, 4);
r.push(ctx.getImageData(0, 0, 1, 1).data[3] > 0);

c.width = c.width;

r.push(ctx.getImageData(0, 0, 1, 1).data[3]);   // cleared to transparent black
r.push(ctx.fillStyle);                          // state reset to its default
r.push(ctx.globalAlpha);
r.push(c.getContext('2d') === ctx);             // still the same context object
");
        Assert.Contains("true,0,#000000,1,true", result);
    }

    [Fact(Timeout = 600000)]
    public void Canvas_Members_Are_Not_Installed_On_Other_Elements()
    {
        // These names belong to HTMLCanvasElement. getContext used to be installed on every element
        // and answer null from a tag check inside, which made the call right and the name wrong;
        // width/height would additionally have shadowed the reflected dimensions elsewhere.
        var result = Run(@"
var div = document.createElement('div');
r.push('getContext' in div);
r.push('toDataURL' in div);
r.push(typeof div.width);
var c = document.createElement('canvas');
r.push('getContext' in c);
r.push('toDataURL' in c);
// An image keeps the reflected dimensions its own interface defines.
var img = document.createElement('img');
img.width = 12;
r.push(img.getAttribute('width'));
");
        Assert.Contains("false,false,undefined,true,true,12", result);
    }

    [Fact(Timeout = 600000)]
    public void GetContext_Answers_Null_For_A_Context_Type_This_Engine_Has_Not_Got()
    {
        var result = Run(@"
var c = document.createElement('canvas');
r.push(c.getContext('webgl') === null);
r.push(c.getContext('bitmaprenderer') === null);
r.push(c.getContext('2d') !== null);
");
        Assert.Contains("true,true,true", result);
    }

    [Fact(Timeout = 600000)]
    public void FillText_Of_Enormous_Text_Is_Bounded_By_The_Canvas()
    {
        // The scratch surface a run is rasterised on is sized to the part of it the canvas can show,
        // not to the run: sized to the run, this call would ask for a bitmap of some 10^11 pixels.
        // What is asserted is only that it returns — whether any ink lands in a 60x30 window on the
        // bottom-left corner of a 1000px glyph is a question about that glyph's shape, not about this.
        var result = Run(@"
var c = document.createElement('canvas');
c.width = 60; c.height = 30;
var ctx = c.getContext('2d');
ctx.font = '1000px sans-serif';
ctx.fillStyle = '#000000';
var huge = new Array(20000).join('W');
try { ctx.fillText(huge, 0, 25); r.push('survived'); }
catch (e) { r.push('threw: ' + e); }
");
        Assert.Contains("survived", result);
    }

    [Fact(Timeout = 600000)]
    public void FillText_Starting_Off_The_Left_Edge_Still_Paints_What_Is_On_Canvas()
    {
        // Clipping the scratch to the visible region must move no glyph: the run's origin is shifted
        // by however much of it falls off the edge. A run starting left of the canvas is where an
        // error in that shift would show up as either blank output or text in the wrong place.
        var result = Run(@"
var c = document.createElement('canvas');
c.width = 60; c.height = 30;
var ctx = c.getContext('2d');
ctx.font = '20px sans-serif';
ctx.fillStyle = '#000000';
ctx.fillText('MMMM', -25, 20);
var d = ctx.getImageData(0, 0, 60, 30).data, inked = 0;
for (var i = 3; i < d.length; i += 4) if (d[i] > 0) inked++;
r.push(inked > 0 ? 'painted' : 'blank');
");
        Assert.Contains("painted", result);
    }

    [Fact(Timeout = 600000)]
    public void FillText_Entirely_Off_Canvas_Draws_Nothing_And_Does_Not_Throw()
    {
        var result = Run(@"
var c = document.createElement('canvas');
c.width = 20; c.height = 20;
var ctx = c.getContext('2d');
ctx.font = '10px sans-serif';
ctx.fillStyle = '#000000';
ctx.fillText('far away', 5000, 5000);
ctx.fillText('far away', -5000, -5000);
var d = ctx.getImageData(0, 0, 20, 20).data, inked = 0;
for (var i = 3; i < d.length; i += 4) if (d[i] > 0) inked++;
r.push(inked);
");
        Assert.Contains("0", result);
    }

    [Fact(Timeout = 600000)]
    public void GetImageData_Refuses_A_Rectangle_Too_Large_To_Allocate()
    {
        // The rectangle is not clipped to the canvas, so its size comes from the arguments alone.
        var result = Run(@"
var ctx = document.createElement('canvas').getContext('2d');
try { ctx.getImageData(0, 0, 100000, 100000); r.push('no throw'); }
catch (e) { r.push('threw'); }
");
        Assert.Contains("threw", result);
    }

    // ---- the html5test probes, verbatim -----------------------------------------------------------

    [Fact(Timeout = 600000)]
    public void Html5Test_Canvas_Blending_Probe_Completes()
    {
        // engine.js:3013-3041 (`canvas.blending`). Line 3030 is the getImageData that used to throw.
        var result = Run(@"
var canvas = document.createElement('canvas');
var passed = false;
if (canvas.getContext) {
    canvas.width = 1;
    canvas.height = 1;
    try {
        var ctx = canvas.getContext('2d');
        ctx.fillStyle = '#fff';
        ctx.fillRect(0, 0, 1, 1);
        ctx.globalCompositeOperation = 'screen';
        ctx.fillStyle = '#000';
        ctx.fillRect(0, 0, 1, 1);

        var data = ctx.getImageData(0, 0, 1, 1);

        passed = ctx.globalCompositeOperation == 'screen' && data.data[0] == 255;
    }
    catch (e) {
        passed = 'threw: ' + e;
    }
}
r.push(passed);
");
        Assert.Contains("true", result);
    }

    [Fact(Timeout = 600000)]
    public void Html5Test_Canvas_Png_Probe_Passes()
    {
        // engine.js:3047-3064 (`canvas.png`). Line 3055 used to throw.
        var result = Run(@"
var canvas = document.createElement('canvas');
var passed = false;
if (canvas.getContext) {
    try {
        passed = canvas.toDataURL('image/png').substring(5, 14) == 'image/png';
    }
    catch (e) {
        passed = 'threw: ' + e;
    }
}
r.push(passed);
");
        Assert.Contains("true", result);
    }

    [Fact(Timeout = 600000)]
    public void Html5Test_Canvas_Jpeg_Probe_Passes()
    {
        // engine.js:3066-3080 (`canvas.jpeg`). Line 3071 used to throw.
        var result = Run(@"
var canvas = document.createElement('canvas');
var passed = false;
if (canvas.getContext) {
    try {
        passed = canvas.toDataURL('image/jpeg').substring(5, 15) == 'image/jpeg';
    }
    catch (e) {
        passed = 'threw: ' + e;
    }
}
r.push(passed);
");
        Assert.Contains("true", result);
    }

    [Fact(Timeout = 600000)]
    public void Html5Test_Canvas_JpegXr_And_Webp_Probes_Still_Report_No()
    {
        // engine.js:3084-3112. There is no JPEG-XR or WebP encoder, so the PNG fallback is the right
        // answer and these must keep reporting "no" rather than becoming a false claim of support.
        var result = Run(@"
var canvas = document.createElement('canvas');
var jpegxr = false, webp = false;
try { jpegxr = canvas.toDataURL('image/vnd.ms-photo').substring(5, 23) == 'image/vnd.ms-photo'; } catch (e) { jpegxr = 'threw'; }
try { webp = canvas.toDataURL('image/webp').substring(5, 15) == 'image/webp'; } catch (e) { webp = 'threw'; }
r.push(jpegxr);
r.push(webp);
");
        Assert.Contains("false,false", result);
    }
}
