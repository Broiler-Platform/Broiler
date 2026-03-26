using System;
using System.Collections.Generic;

namespace Broiler.HTML.Core.Core.Entities;

public sealed class CssBlock
{
    private readonly Dictionary<string, string> _properties;
    private HashSet<string> _importantProperties;

    public CssBlock(string @class, Dictionary<string, string> properties, List<CssBlockSelectorItem> selectors = null, bool hover = false, string pseudoClass = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(@class);
        ArgumentNullException.ThrowIfNull(properties);

        Class = @class;
        Selectors = selectors;
        _properties = properties;
        Hover = hover;
        PseudoClass = pseudoClass;
    }

    public string Class { get; }
    public List<CssBlockSelectorItem> Selectors { get; }
    public IDictionary<string, string> Properties => _properties;
    public bool Hover { get; }

    /// <summary>
    /// Optional structural pseudo-class on the terminal selector
    /// (e.g. "first-child" for <c>h1:first-child</c>).  CSS2.1 §5.11.
    /// </summary>
    public string PseudoClass { get; private set; }

    /// <summary>
    /// Internal setter for <see cref="PseudoClass"/>, used by the CSS parser
    /// when the pseudo-class is determined after initial construction.
    /// </summary>
    internal string PseudoClassInternal { set => PseudoClass = value; }

    /// <summary>
    /// Property names in this block that were declared with <c>!important</c>.
    /// CSS2.1 §6.4.2: Important declarations override normal declarations
    /// regardless of specificity.
    /// </summary>
    public IReadOnlySet<string> ImportantProperties => _importantProperties ?? (IReadOnlySet<string>)_emptySet;
    private static readonly HashSet<string> _emptySet = [];

    /// <summary>
    /// Marks the given property name as <c>!important</c>.
    /// </summary>
    public void MarkImportant(string propertyName)
    {
        _importantProperties ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _importantProperties.Add(propertyName);
    }

    public void Merge(CssBlock other)
    {
        ArgumentNullException.ThrowIfNull(other);

        foreach (var prop in other._properties.Keys)
            _properties[prop] = other._properties[prop];

        if (other._importantProperties != null)
        {
            _importantProperties ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in other._importantProperties)
                _importantProperties.Add(prop);
        }
    }

    public CssBlock Clone()
    {
        var clone = new CssBlock(Class, new Dictionary<string, string>(_properties), Selectors != null ? [.. Selectors] : null, pseudoClass: PseudoClass);
        if (_importantProperties != null)
            clone._importantProperties = new HashSet<string>(_importantProperties, StringComparer.OrdinalIgnoreCase);
        return clone;
    }

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

    public override int GetHashCode() => HashCode.Combine(Class.GetHashCode(), _properties.GetHashCode());

    public override string ToString()
    {
        var str = Class + " { ";

        foreach (var property in _properties)
            str += $"{property.Key}={property.Value}; ";

        return str + " }";
    }
}