using System.Drawing;
using SkiaSharp;

namespace Broiler.HTML.Image.Adapters;

internal interface IFontTypefaceResolver
{
    string RegisterFontFile(string path, string alias = null);

    bool HasDeferredLoadedTypefacePath(string family);

    bool HasMaterializedLoadedTypeface(string family);

    SKTypeface ResolveTypeface(string family, FontStyle style);
}
