using System;
using System.Collections.Generic;

namespace Broiler.HTML.Core.Core.Entities;

public sealed class CssBlock
{
    private readonly Dictionary<string, string> _properties;

    public CssBlock(string @class, Dictionary<string, string> properties, List<CssBlockSelectorItem> selectors = null, bool hover = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(@class);
        ArgumentNullException.ThrowIfNull(properties);

        Class = @class;
        Selectors = selectors;
        _properties = properties;
        Hover = hover;
    }

    public string Class { get; }
    public List<CssBlockSelectorItem> Selectors { get; }
    public IDictionary<string, string> Properties => _properties;
    public bool Hover { get; }
    public void Merge(CssBlock other)
    {
        ArgumentNullException.ThrowIfNull(other);

        foreach (var prop in other._properties.Keys)
            _properties[prop] = other._properties[prop];
    }

    public CssBlock Clone() => new(Class, new Dictionary<string, string>(_properties), Selectors != null ? [.. Selectors] : null);

    public bool Equals(CssBlock other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (!Equals(other.Class, Class))
            return false;

        if (!Equals(other._properties.Count, _properties.Count))
            return false;

        foreach (var property in _properties)
        {
            if (!other._properties.TryGetValue(property.Key, out string value))
                return false;

            if (!Equals(value, property.Value))
                return false;
        }

        if (!EqualsSelector(other))
            return false;

        return true;
    }

    public bool EqualsSelector(CssBlock other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (other.Hover != Hover)
            return false;

        if (other.Selectors == null && Selectors != null)
            return false;

        if (other.Selectors != null && Selectors == null)
            return false;

        if (other.Selectors != null && Selectors != null)
        {
            if (!Equals(other.Selectors.Count, Selectors.Count))
                return false;

            for (int i = 0; i < Selectors.Count; i++)
            {
                if (!Equals(other.Selectors[i].Class, Selectors[i].Class))
                    return false;

                if (!Equals(other.Selectors[i].DirectParent, Selectors[i].DirectParent))
                    return false;

                if (!Equals(other.Selectors[i].AdjacentSibling, Selectors[i].AdjacentSibling))
                    return false;
            }
        }

        return true;
    }

    public override bool Equals(object obj)
    {
        if (obj is null)
            return false;

        if (ReferenceEquals(this, obj))
            return true;

        if (obj.GetType() != typeof(CssBlock))
            return false;

        return Equals((CssBlock)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return ((Class != null ? Class.GetHashCode() : 0) * 397) ^ (_properties != null ? _properties.GetHashCode() : 0);
        }
    }

    public override string ToString()
    {
        var str = Class + " { ";

        foreach (var property in _properties)
            str += $"{property.Key}={property.Value}; ";

        return str + " }";
    }
}