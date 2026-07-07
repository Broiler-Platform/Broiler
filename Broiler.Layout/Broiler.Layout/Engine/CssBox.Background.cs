using Broiler.CSS;
using System.Drawing;


namespace Broiler.Layout.Engine;

internal partial class CssBox : CssBoxProperties, IDisposable
{
    /// <summary>
    /// Loads the CSS background image if one is specified and not yet loaded.
    /// Called from <see cref="MeasureWordsSize"/> and overridden versions in
    /// subclasses (e.g. <see cref="CssBoxImage"/>) that replace the base
    /// measurement logic.
    /// </summary>
    protected void LoadBackgroundImageIfNeeded()
    {
        if (BackgroundImage == CssConstants.None || _backgroundImagesInitialized)
            return;

        _backgroundImagesInitialized = true;
        var layers = SplitBackgroundImageLayers(BackgroundImage);
        
        if (layers.Count == 0)
            return;

        _backgroundImageLoadHandlers = new List<ILayoutImageLoader?>(layers.Count);
        
        foreach (var layer in layers)
        {
            var src = TryExtractBackgroundImageUrl(layer);
            
            if (string.IsNullOrEmpty(src))
            {
                _backgroundImageLoadHandlers.Add(null);
                continue;
            }

            var imageLoadHandler = LayoutEnvironment.CreateImageLoader(OnImageLoadComplete);
            
            _backgroundImageLoadHandlers.Add(imageLoadHandler);
            imageLoadHandler.LoadImage(src, HtmlTag?.Attributes, BaseUrl);
        }
    }

    private static List<string> SplitBackgroundImageLayers(string backgroundImage)
    {
        var layers = new List<string>();
        
        if (string.IsNullOrWhiteSpace(backgroundImage))
            return layers;

        int depth = 0;
        int start = 0;
        
        for (int i = 0; i < backgroundImage.Length; i++)
        {
            switch (backgroundImage[i])
            {
                case '(':
                    depth++;
                    break;
                
                case ')':
                    if (depth > 0)
                        depth--;
                    break;
                
                case ',' when depth == 0:
                    layers.Add(backgroundImage[start..i].Trim());
                    start = i + 1;
                    break;
            }
        }

        layers.Add(backgroundImage[start..].Trim());
        return layers;
    }

    private static string? TryExtractBackgroundImageUrl(string layer)
    {
        if (string.IsNullOrWhiteSpace(layer))
            return null;

        layer = layer.Trim();
        
        if (!layer.StartsWith("url(", StringComparison.OrdinalIgnoreCase))
            return layer.Contains('(') ? null : layer;

        if (!layer.EndsWith(')'))
            return null;

        var src = layer[4..^1].Trim();
        if (src.Length >= 2 &&
            ((src[0] == '\'' && src[^1] == '\'') ||
             (src[0] == '"' && src[^1] == '"')))
        {
            src = src[1..^1];
        }

        return src;
    }

    private void OnImageLoadComplete(object? image, RectangleF rectangle, bool async)
    {
        if (image != null && async)
            LayoutEnvironment.RequestRefresh(false);
    }
}
