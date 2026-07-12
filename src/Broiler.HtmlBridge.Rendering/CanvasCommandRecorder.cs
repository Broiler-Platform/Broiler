namespace Broiler.HtmlBridge;

// Raster image decoding, format probing, data-URI/HTTP fetching, and SVG parsing
// formerly lived here as a parallel bridge-owned pipeline. That duplicate has been
// removed: raster images decode through the Broiler.Media codec catalog (see
// Broiler.HTML.Image.BBitmap.Decode), URL/data-URI fetching and CSP/CORS policy live
// in the HTML resource-loading layer, and SVG is rasterized by Broiler.HTML.Image's
// BSvgRasterizer / Broiler.Layout's SvgRenderer. See
// docs/roadmap/broiler-media-component.md (Phase 4) and
// docs/roadmap/htmlbridge-out-of-scope-routing-roadmap.md (Bucket 1).
//
// What remains is the HTML5 Canvas 2D command recorder, which is genuine bridge
// runtime: it backs the JS `canvas.getContext("2d")` binding (DomBridge) and records
// drawing commands. It is not an image codec.

/// <summary>Canvas 2D draw command types.</summary>
public enum CanvasDrawCommandType
{
    /// <summary>Fill a rectangle.</summary>
    FillRect,
    /// <summary>Stroke a rectangle outline.</summary>
    StrokeRect,
    /// <summary>Clear a rectangular area.</summary>
    ClearRect,
    /// <summary>Begin a new path.</summary>
    BeginPath,
    /// <summary>Move the pen to a point.</summary>
    MoveTo,
    /// <summary>Draw a line to a point.</summary>
    LineTo,
    /// <summary>Draw an arc.</summary>
    Arc,
    /// <summary>Close the current path.</summary>
    ClosePath,
    /// <summary>Fill the current path.</summary>
    Fill,
    /// <summary>Stroke the current path.</summary>
    Stroke,
    /// <summary>Fill text at a position.</summary>
    FillText,
    /// <summary>Stroke text at a position.</summary>
    StrokeText,
    /// <summary>Save the current state.</summary>
    Save,
    /// <summary>Restore the previously saved state.</summary>
    Restore,
}

/// <summary>Represents a single canvas drawing command with all relevant parameters.</summary>
public class CanvasDrawCommand
{
    /// <summary>Type of draw command.</summary>
    public CanvasDrawCommandType Type { get; set; }
    /// <summary>X coordinate.</summary>
    public float X { get; set; }
    /// <summary>Y coordinate.</summary>
    public float Y { get; set; }
    /// <summary>Width.</summary>
    public float Width { get; set; }
    /// <summary>Height.</summary>
    public float Height { get; set; }
    /// <summary>Radius (for arc commands).</summary>
    public float Radius { get; set; }
    /// <summary>Start angle in radians (for arc commands).</summary>
    public float StartAngle { get; set; }
    /// <summary>End angle in radians (for arc commands).</summary>
    public float EndAngle { get; set; }
    /// <summary>Text content.</summary>
    public string Text { get; set; } = string.Empty;
    /// <summary>Fill style at the time of the command.</summary>
    public string FillStyle { get; set; } = string.Empty;
    /// <summary>Stroke style at the time of the command.</summary>
    public string StrokeStyle { get; set; } = string.Empty;
    /// <summary>Line width at the time of the command.</summary>
    public float LineWidth { get; set; }
    /// <summary>Global alpha at the time of the command.</summary>
    public float GlobalAlpha { get; set; }
}

/// <summary>Represents the HTML5 Canvas 2D rendering context with basic drawing operations.</summary>
/// <remarks>Initializes a new <see cref="CanvasRenderingContext2D"/> with the given dimensions.</remarks>
public class CanvasRenderingContext2D(int width, int height)
{
    /// <summary>Canvas width in pixels.</summary>
    public int Width { get; } = width;
    /// <summary>Canvas height in pixels.</summary>
    public int Height { get; } = height;
    /// <summary>Current fill color.</summary>
    public string FillStyle { get; set; } = "#000000";
    /// <summary>Current stroke color.</summary>
    public string StrokeStyle { get; set; } = "#000000";
    /// <summary>Current line width.</summary>
    public float LineWidth { get; set; } = 1.0f;
    /// <summary>Current font specification.</summary>
    public string Font { get; set; } = "10px sans-serif";
    /// <summary>Current text alignment.</summary>
    public string TextAlign { get; set; } = "start";
    /// <summary>Current global alpha (transparency).</summary>
    public float GlobalAlpha { get; set; } = 1.0f;

    /// <summary>Recorded drawing commands for later rendering.</summary>
    internal List<CanvasDrawCommand> Commands { get; } = [];

    private readonly Stack<CanvasState> _stateStack = new();

    /// <summary>Fills a rectangle at the specified position and size.</summary>
    public void FillRect(float x, float y, float width, float height) => Commands.Add(new CanvasDrawCommand
    {
        Type = CanvasDrawCommandType.FillRect,
        X = x,
        Y = y,
        Width = width,
        Height = height,
        FillStyle = FillStyle,
        LineWidth = LineWidth,
        GlobalAlpha = GlobalAlpha,
    });

    /// <summary>Strokes a rectangle outline at the specified position and size.</summary>
    public void StrokeRect(float x, float y, float width, float height) => Commands.Add(new CanvasDrawCommand
    {
        Type = CanvasDrawCommandType.StrokeRect,
        X = x,
        Y = y,
        Width = width,
        Height = height,
        StrokeStyle = StrokeStyle,
        LineWidth = LineWidth,
        GlobalAlpha = GlobalAlpha,
    });

    /// <summary>Clears a rectangular area, making it fully transparent.</summary>
    public void ClearRect(float x, float y, float width, float height) => Commands.Add(new CanvasDrawCommand
    {
        Type = CanvasDrawCommandType.ClearRect,
        X = x,
        Y = y,
        Width = width,
        Height = height,
        GlobalAlpha = GlobalAlpha,
    });

    /// <summary>Begins a new drawing path.</summary>
    public void BeginPath() => Commands.Add(new CanvasDrawCommand { Type = CanvasDrawCommandType.BeginPath });

    /// <summary>Moves the pen to the specified point without drawing.</summary>
    public void MoveTo(float x, float y) => Commands.Add(new CanvasDrawCommand
    {
        Type = CanvasDrawCommandType.MoveTo,
        X = x,
        Y = y,
    });

    /// <summary>Draws a straight line from the current point to the specified point.</summary>
    public void LineTo(float x, float y) => Commands.Add(new CanvasDrawCommand
    {
        Type = CanvasDrawCommandType.LineTo,
        X = x,
        Y = y,
    });

    /// <summary>Draws an arc centered at (x, y) with the given radius and angles.</summary>
    public void Arc(float x, float y, float radius, float startAngle, float endAngle) => Commands.Add(new CanvasDrawCommand
    {
        Type = CanvasDrawCommandType.Arc,
        X = x,
        Y = y,
        Radius = radius,
        StartAngle = startAngle,
        EndAngle = endAngle,
    });

    /// <summary>Closes the current path by connecting the last point to the first.</summary>
    public void ClosePath() => Commands.Add(new CanvasDrawCommand { Type = CanvasDrawCommandType.ClosePath });

    /// <summary>Fills the current path with the current fill style.</summary>
    public void Fill() => Commands.Add(new CanvasDrawCommand
    {
        Type = CanvasDrawCommandType.Fill,
        FillStyle = FillStyle,
        GlobalAlpha = GlobalAlpha,
    });

    /// <summary>Strokes the current path with the current stroke style.</summary>
    public void Stroke() => Commands.Add(new CanvasDrawCommand
    {
        Type = CanvasDrawCommandType.Stroke,
        StrokeStyle = StrokeStyle,
        LineWidth = LineWidth,
        GlobalAlpha = GlobalAlpha,
    });

    /// <summary>Fills text at the specified position.</summary>
    public void FillText(string text, float x, float y) => Commands.Add(new CanvasDrawCommand
    {
        Type = CanvasDrawCommandType.FillText,
        Text = text,
        X = x,
        Y = y,
        FillStyle = FillStyle,
        GlobalAlpha = GlobalAlpha,
    });

    /// <summary>Strokes text at the specified position.</summary>
    public void StrokeText(string text, float x, float y) => Commands.Add(new CanvasDrawCommand
    {
        Type = CanvasDrawCommandType.StrokeText,
        Text = text,
        X = x,
        Y = y,
        StrokeStyle = StrokeStyle,
        LineWidth = LineWidth,
        GlobalAlpha = GlobalAlpha,
    });

    /// <summary>Saves the current drawing state onto a stack.</summary>
    public void Save()
    {
        _stateStack.Push(new CanvasState
        {
            FillStyle = FillStyle,
            StrokeStyle = StrokeStyle,
            LineWidth = LineWidth,
            Font = Font,
            TextAlign = TextAlign,
            GlobalAlpha = GlobalAlpha,
        });
        Commands.Add(new CanvasDrawCommand { Type = CanvasDrawCommandType.Save });
    }

    /// <summary>Restores the most recently saved drawing state from the stack.</summary>
    public void Restore()
    {
        if (_stateStack.Count > 0)
        {
            var state = _stateStack.Pop();
            FillStyle = state.FillStyle;
            StrokeStyle = state.StrokeStyle;
            LineWidth = state.LineWidth;
            Font = state.Font;
            TextAlign = state.TextAlign;
            GlobalAlpha = state.GlobalAlpha;
        }
        Commands.Add(new CanvasDrawCommand { Type = CanvasDrawCommandType.Restore });
    }

    private class CanvasState
    {
        public string FillStyle { get; set; } = string.Empty;
        public string StrokeStyle { get; set; } = string.Empty;
        public float LineWidth { get; set; }
        public string Font { get; set; } = string.Empty;
        public string TextAlign { get; set; } = string.Empty;
        public float GlobalAlpha { get; set; }
    }
}
