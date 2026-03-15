using System;
using System.Collections.Generic;
using System.Drawing;

namespace TheArtOfDev.HtmlRenderer.Core.Entities;

public delegate void HtmlImageLoadCallback(string path, Object image, RectangleF imageRectangle);

public sealed class HtmlImageLoadEventArgs : EventArgs
{
    private readonly HtmlImageLoadCallback _callback;

    internal HtmlImageLoadEventArgs(string src, Dictionary<string, string> attributes, HtmlImageLoadCallback callback)
    {
        Src = src;
        Attributes = attributes;
        _callback = callback;
    }

    public string Src { get; }
    public Dictionary<string, string> Attributes { get; }
    public bool Handled { get; set; }

    public void Callback()
    {
        Handled = true;
        _callback(null, null, new RectangleF());
    }

    public void Callback(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        Handled = true;
        _callback(path, null, RectangleF.Empty);
    }

    public void Callback(string path, double x, double y, double width, double height)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        Handled = true;
        _callback(path, null, new RectangleF((float)x, (float)y, (float)width, (float)height));
    }

    public void Callback(Object image)
    {
        ArgumentNullException.ThrowIfNull(image);

        Handled = true;
        _callback(null, image, RectangleF.Empty);
    }

    public void Callback(Object image, double x, double y, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(image);

        Handled = true;
        _callback(null, image, new RectangleF((float)x, (float)y, (float)width, (float)height));
    }
}