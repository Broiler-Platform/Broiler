using System;
using Broiler.Graphics;

namespace Broiler.UI;

/// <summary>
/// An optional host capability: turning encoded image bytes into a
/// <see cref="BImageHandle"/> the render list can draw. Only a host that owns a
/// renderer can do this, so controls that draw document images look for this
/// interface on <see cref="IUiHost"/> and fall back to a placeholder box when it
/// is absent.
/// </summary>
public interface IUiImageHost
{
    /// <summary>
    /// Creates a drawable image from encoded bytes (PNG, JPEG, and whatever else
    /// the backend decodes). Returns <see cref="BImageHandle.Invalid"/> when the
    /// bytes cannot be decoded — a malformed image in a document must not throw
    /// out of a render pass.
    /// </summary>
    BImageHandle CreateImage(ReadOnlySpan<byte> encodedImage);

    /// <summary>Releases a handle returned by <see cref="CreateImage"/>.</summary>
    void ReleaseImage(BImageHandle image);
}
