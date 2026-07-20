namespace Broiler.HtmlBridge;

/// <summary>
/// Script-observable state for the HTML5 Canvas 2D context that backs the
/// <c>canvas.getContext("2d")</c> binding (see <c>BuildCanvas2DContext</c> and the
/// <c>JsUtilities*</c> canvas callbacks). Internal to the Canvas binding.
/// </summary>
/// <remarks>
/// <para>
/// Phase 6 (P6.1): this was formerly <c>CanvasRenderingContext2D</c> in the standalone
/// <c>Broiler.HtmlBridge.Rendering</c> project, where every drawing call appended a
/// <c>CanvasDrawCommand</c> to a growing <c>List</c>. That command list was written by the
/// script-facing draw methods but <b>never read by any renderer</b> — no code path retrieved a
/// canvas element's recorded commands to paint them — so it was unbounded dead storage. Per the
/// Phase 6 exit criterion ("Canvas commands are either rendered and bounded or are not recorded"),
/// the recorder and its <c>CanvasDrawCommand</c> / <c>CanvasDrawCommandType</c> DTOs are removed and
/// the type is internalized into the Canvas binding.
/// </para>
/// <para>
/// What remains is the script-observable state a canvas program can read back: the current styles
/// (<see cref="FillStyle"/>, <see cref="StrokeStyle"/>, <see cref="LineWidth"/>, <see cref="Font"/>,
/// <see cref="TextAlign"/>, <see cref="GlobalAlpha"/>) and the <see cref="Save"/>/<see cref="Restore"/>
/// state stack that restores them. The pure drawing methods (fillRect, arc, fillText, …) keep their
/// signatures so the JS API stays callable, but perform no work: nothing renders a headless canvas.
/// If a renderer ever consumes canvas output, this becomes a real immutable Broiler.Graphics display
/// list rather than growing an in-memory command log again.
/// </para>
/// </remarks>
internal sealed class CanvasRenderingContext2D(int width, int height)
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

    private readonly Stack<CanvasState> _stateStack = new();

    // Pure drawing operations. Kept callable for the JS API surface; no-ops because a headless
    // canvas is never painted and nothing consumes recorded commands (see the type remarks).
    /// <summary>Fills a rectangle at the specified position and size.</summary>
    public void FillRect(float x, float y, float width, float height) { }

    /// <summary>Strokes a rectangle outline at the specified position and size.</summary>
    public void StrokeRect(float x, float y, float width, float height) { }

    /// <summary>Clears a rectangular area, making it fully transparent.</summary>
    public void ClearRect(float x, float y, float width, float height) { }

    /// <summary>Begins a new drawing path.</summary>
    public void BeginPath() { }

    /// <summary>Moves the pen to the specified point without drawing.</summary>
    public void MoveTo(float x, float y) { }

    /// <summary>Draws a straight line from the current point to the specified point.</summary>
    public void LineTo(float x, float y) { }

    /// <summary>Draws an arc centered at (x, y) with the given radius and angles.</summary>
    public void Arc(float x, float y, float radius, float startAngle, float endAngle) { }

    /// <summary>Closes the current path by connecting the last point to the first.</summary>
    public void ClosePath() { }

    /// <summary>Fills the current path with the current fill style.</summary>
    public void Fill() { }

    /// <summary>Strokes the current path with the current stroke style.</summary>
    public void Stroke() { }

    /// <summary>Fills text at the specified position.</summary>
    public void FillText(string text, float x, float y) { }

    /// <summary>Strokes text at the specified position.</summary>
    public void StrokeText(string text, float x, float y) { }

    /// <summary>Saves the current drawing state onto a stack.</summary>
    public void Save() => _stateStack.Push(new CanvasState
    {
        FillStyle = FillStyle,
        StrokeStyle = StrokeStyle,
        LineWidth = LineWidth,
        Font = Font,
        TextAlign = TextAlign,
        GlobalAlpha = GlobalAlpha,
    });

    /// <summary>Restores the most recently saved drawing state from the stack.</summary>
    public void Restore()
    {
        if (_stateStack.Count == 0)
            return;
        var state = _stateStack.Pop();
        FillStyle = state.FillStyle;
        StrokeStyle = state.StrokeStyle;
        LineWidth = state.LineWidth;
        Font = state.Font;
        TextAlign = state.TextAlign;
        GlobalAlpha = state.GlobalAlpha;
    }

    private sealed class CanvasState
    {
        public string FillStyle { get; set; } = string.Empty;
        public string StrokeStyle { get; set; } = string.Empty;
        public float LineWidth { get; set; }
        public string Font { get; set; } = string.Empty;
        public string TextAlign { get; set; } = string.Empty;
        public float GlobalAlpha { get; set; }
    }
}
