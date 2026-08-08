using System;
using System.Collections.Generic;
using Broiler.Graphics;

namespace Broiler.Layout.IR;

/// <summary>
/// An SVG <c>&lt;filter&gt;</c> whose primitives only transform colour, reduced to the one thing
/// such a chain does to a <em>uniformly filled</em> shape: map its fill colour to another colour.
/// <para>
/// This is the same kind of modelling <see cref="SvgFilterTable.FloodFilter"/> already applies to an
/// <c>&lt;feFlood&gt;</c>-only filter — no raster filter pipeline, just the closed-form answer for the
/// input the engine can characterise exactly. A shape filled with one solid colour has a source
/// graphic that is that colour everywhere inside it and transparent black everywhere outside, so a
/// chain of per-pixel colour operations produces exactly two colours, and the outside one stays
/// transparent for every chain modelled here (each step maps zero alpha to zero alpha).
/// </para>
/// <para>
/// Deliberately narrow. Only <c>feColorMatrix</c> (<c>type="matrix"</c>, which is also its default)
/// and <c>feComposite</c> (<c>operator="arithmetic"</c>) are modelled, only in a straight chain where
/// each primitive consumes the previous one's result, and only when the filter declares
/// <c>color-interpolation-filters="sRGB"</c> — the default is linearRGB, and the conversion is not
/// modelled, so a filter that does not say sRGB is left unfiltered rather than rendered wrongly.
/// Anything outside that is not modelled and the shape renders unfiltered, exactly as before.
/// </para>
/// </summary>
public sealed class SvgColorFilter
{
    private readonly List<Step> _steps;

    internal SvgColorFilter(List<Step> steps) => _steps = steps;

    /// <summary>One modelled primitive. Exactly one of the two forms is populated.</summary>
    internal readonly struct Step
    {
        /// <summary>The 20 <c>feColorMatrix</c> values in row-major order, or <c>null</c> for a composite.</summary>
        public float[]? Matrix { get; init; }

        /// <summary><c>feComposite operator="arithmetic"</c> coefficients k1..k4.</summary>
        public (float K1, float K2, float K3, float K4) Arithmetic { get; init; }
    }

    /// <summary>
    /// The colour a shape uniformly filled with <paramref name="source"/> renders as once this filter
    /// has been applied.
    /// </summary>
    public BColor Apply(BColor source)
    {
        float r = source.R / 255f, g = source.G / 255f, b = source.B / 255f, a = source.A / 255f;

        foreach (var step in _steps)
        {
            if (step.Matrix is { } m)
            {
                // Filter Effects §feColorMatrix: a 5x4 matrix over non-premultiplied RGBA,
                // with the fifth column an additive term.
                float nr = m[0] * r + m[1] * g + m[2] * b + m[3] * a + m[4];
                float ng = m[5] * r + m[6] * g + m[7] * b + m[8] * a + m[9];
                float nb = m[10] * r + m[11] * g + m[12] * b + m[13] * a + m[14];
                float na = m[15] * r + m[16] * g + m[17] * b + m[18] * a + m[19];
                r = Clamp01(nr); g = Clamp01(ng); b = Clamp01(nb); a = Clamp01(na);
                continue;
            }

            // Filter Effects §feComposite: the arithmetic operator works on *premultiplied*
            // values. Both inputs are the previous result here (the chain is straight), so
            // i1 == i2 and the k1 term is that value squared.
            var (k1, k2, k3, k4) = step.Arithmetic;
            float pr = r * a, pg = g * a, pb = b * a;

            float or_ = Clamp01(k1 * pr * pr + k2 * pr + k3 * pr + k4);
            float og = Clamp01(k1 * pg * pg + k2 * pg + k3 * pg + k4);
            float ob = Clamp01(k1 * pb * pb + k2 * pb + k3 * pb + k4);
            float oa = Clamp01(k1 * a * a + k2 * a + k3 * a + k4);

            if (oa <= 0f)
            {
                r = g = b = a = 0f;
                continue;
            }

            r = Clamp01(or_ / oa); g = Clamp01(og / oa); b = Clamp01(ob / oa); a = oa;
        }

        return BColor.FromArgb(
            (int)MathF.Round(a * 255f),
            (int)MathF.Round(r * 255f),
            (int)MathF.Round(g * 255f),
            (int)MathF.Round(b * 255f));
    }

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
}
