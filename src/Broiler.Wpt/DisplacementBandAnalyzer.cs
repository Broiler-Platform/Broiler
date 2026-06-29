using System;
using System.Collections.Generic;
using System.Linq;
using Broiler.HTML.Image;

namespace Broiler.Wpt;

/// <summary>
/// One vertical band of a mismatched region with its own estimated content
/// displacement.
/// </summary>
internal sealed record DisplacementBand(int Top, int Bottom, int ShiftX, int ShiftY, int SampleCount);

/// <summary>
/// Resolves a pixel mismatch's content displacement <b>per vertical band</b>
/// instead of as a single global centroid (diagnostic #11).
///
/// <para>The classifier's global <see cref="MismatchDiagnostics.Displacement"/>
/// averages the whole dirty region into one shift; when a shift affects only
/// <i>part</i> of the image — everything below some point translated, the classic
/// line-height / inter-line-spacing / <c>&lt;br&gt;</c>-flow signature — that
/// average blurs it away (it was a leading cause of misdiagnosis in issue
/// #1121). This analyzer segments the sampled mismatches into contiguous vertical
/// bands and estimates each band's shift independently, so a non-uniform profile
/// (e.g. an aligned upper band but a shifted lower band) becomes explicit.</para>
///
/// <para>Lives in the triage layer (not <see cref="MismatchClassifier"/>) because
/// it only needs the public <see cref="PixelDiffResult.Mismatches"/> and keeps the
/// diagnostic improvement in the main repo.</para>
/// </summary>
internal static class DisplacementBandAnalyzer
{
    /// <summary>
    /// Vertical gap (px) between consecutive mismatch rows large enough to start a
    /// new band — bridges sampling holes within one content block while still
    /// separating visually distinct groups.
    /// </summary>
    private const int BandGapTolerance = 24;

    /// <summary>Minimum sampled mismatches a band needs before its shift is trusted.</summary>
    private const int MinBandSamples = 8;

    /// <summary>
    /// Per-band shift spread (px, max − min across bands on either axis) at or
    /// above which the displacement is reported as non-uniform.
    /// </summary>
    private const double BandShiftVarianceThreshold = 6.0;

    /// <summary>Channel value at or above which a pixel counts as near-white (background).</summary>
    private const byte WhiteThreshold = 240;

    /// <summary>Minimum per-axis shift (px) before it is described (anti-aliasing guard).</summary>
    private const double DisplacementThreshold = 5.0;

    /// <summary>
    /// Segments <paramref name="mismatches"/> into contiguous vertical bands
    /// (merging sampling holes up to <see cref="BandGapTolerance"/>) and estimates
    /// each band's content shift as the centroid of "content only in the output"
    /// minus the centroid of "content only in the reference" within that band.
    /// Bands with too few samples or no background↔content transition are skipped.
    /// </summary>
    internal static IReadOnlyList<DisplacementBand> Analyze(IReadOnlyList<PixelMismatch> mismatches)
    {
        var bands = new List<DisplacementBand>();
        if (mismatches is null || mismatches.Count == 0)
            return bands;

        var sorted = mismatches.OrderBy(m => m.Y).ToList();

        int i = 0;
        while (i < sorted.Count)
        {
            int bandTop = sorted[i].Y;
            int bandBottom = bandTop;
            long aX = 0, aY = 0, bX = 0, bY = 0;
            int aC = 0, bC = 0, count = 0;

            int j = i;
            while (j < sorted.Count && sorted[j].Y - bandBottom <= BandGapTolerance)
            {
                var m = sorted[j];
                bandBottom = m.Y;
                count++;

                bool actualWhite = IsWhite(m.ActualR, m.ActualG, m.ActualB);
                bool baselineWhite = IsWhite(m.BaselineR, m.BaselineG, m.BaselineB);
                if (!actualWhite && baselineWhite) { aX += m.X; aY += m.Y; aC++; }
                else if (actualWhite && !baselineWhite) { bX += m.X; bY += m.Y; bC++; }

                j++;
            }
            i = j;

            if (count < MinBandSamples || aC == 0 || bC == 0)
                continue;

            double dx = (double)aX / aC - (double)bX / bC;
            double dy = (double)aY / aC - (double)bY / bC;
            bands.Add(new DisplacementBand(
                bandTop, bandBottom,
                (int)Math.Round(dx), (int)Math.Round(dy), count));
        }

        return bands;
    }

    /// <summary>
    /// Returns a one-line "non-uniform shift across N bands (…)" phrase when the
    /// per-band displacements disagree by at least <see cref="BandShiftVarianceThreshold"/>
    /// on either axis, or null when there are fewer than two bands or they agree (a
    /// uniform shift the global displacement already describes).
    /// </summary>
    internal static string? DescribeNonUniform(IReadOnlyList<DisplacementBand> bands)
    {
        if (bands is null || bands.Count < 2)
            return null;

        int minDx = bands.Min(b => b.ShiftX), maxDx = bands.Max(b => b.ShiftX);
        int minDy = bands.Min(b => b.ShiftY), maxDy = bands.Max(b => b.ShiftY);
        if (maxDx - minDx < BandShiftVarianceThreshold && maxDy - minDy < BandShiftVarianceThreshold)
            return null;

        var descs = bands.Select(b => $"y[{b.Top}-{b.Bottom}] {DescribeBandShift(b.ShiftX, b.ShiftY)}");
        return "non-uniform shift across " + bands.Count + " bands ("
            + string.Join("; ", descs)
            + ") — a band-localised shift points at line-height / inter-line spacing / <br> flow "
            + "rather than a single mis-placed element";
    }

    private static bool IsWhite(byte r, byte g, byte b)
        => r >= WhiteThreshold && g >= WhiteThreshold && b >= WhiteThreshold;

    private static string DescribeBandShift(int dx, int dy)
    {
        var parts = new List<string>();
        if (Math.Abs(dx) >= DisplacementThreshold)
            parts.Add($"{(dx > 0 ? "right" : "left")} ~{Math.Abs(dx)}px");
        if (Math.Abs(dy) >= DisplacementThreshold)
            parts.Add($"{(dy > 0 ? "down" : "up")} ~{Math.Abs(dy)}px");
        return parts.Count > 0 ? string.Join(" & ", parts) : "aligned";
    }
}
