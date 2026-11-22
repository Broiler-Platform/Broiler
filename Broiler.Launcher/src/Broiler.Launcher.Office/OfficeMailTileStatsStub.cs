namespace Broiler.Mail.Windows.Measurement;

/// <summary>
/// Minimal tile telemetry stub when compiled inside the all-in-one Broiler Office host without
/// the standalone Mail measurement harness.
/// </summary>
internal sealed class HtmlTileStatistics
{
    public void Hit() { }
    public void Drawn((int Row, int Column) tile, long rasterTicks, long uploadTicks, long cachedBytes) { }
    public void Evicted() { }
    public void Discarded(int tiles) { }
    public void LaidOut(long ticks) { }
}
