using Broiler.JavaScript.Runtime;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    private static double GetCoordinateArgument(in Arguments args, int index) =>
        args.Length > index && !args[index].IsNull && !args[index].IsUndefined
            ? args[index].DoubleValue
            : double.NaN;

    private IReadOnlyList<DomElement> HitTestDocumentPoint(DomElement docRoot, double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y))
            return Array.Empty<DomElement>();

        var documentElement = IsDocumentElement(docRoot)
            ? docRoot
            : docRoot.Children.FirstOrDefault(c => !c.IsTextNode && !c.TagName.StartsWith("#"));
        if (documentElement == null)
            return Array.Empty<DomElement>();

        if (!DocumentHasViewport(documentElement))
            return Array.Empty<DomElement>();

        var viewportWidth = GetViewportReferenceLength(documentElement, vertical: false);
        var viewportHeight = GetViewportReferenceLength(documentElement, vertical: true);
        if (viewportWidth <= 0 || viewportHeight <= 0 || x < 0 || y < 0 || x >= viewportWidth || y >= viewportHeight)
            return Array.Empty<DomElement>();

        var hits = new List<DomElement>();
        CollectHitTestMatches(documentElement, x, y, hits);
        return hits;
    }

    private void CollectHitTestMatches(DomElement element, double x, double y, List<DomElement> hits)
    {
        for (var i = element.Children.Count - 1; i >= 0; i--)
        {
            var child = element.Children[i];
            if (!child.IsTextNode && !child.TagName.StartsWith("#", StringComparison.Ordinal))
                CollectHitTestMatches(child, x, y, hits);
        }

        if (IsElementHitTestCandidate(element, x, y))
            hits.Add(element);
    }


    private bool IsElementHitTestCandidate(DomElement element, double x, double y)
    {
        if (IsAreaElement(element))
            return IsImageMapAreaHit(element, x, y);

        if (!IsElementRenderedForHitTesting(element))
            return false;

        if (IsTableStructuralHitTestOnlyElement(element))
            return false;

        var props = GetComputedProps(element);
        if (string.Equals(props.GetValueOrDefault("pointer-events"), "none", StringComparison.OrdinalIgnoreCase))
            return false;

        var rect = GetHitTestRectForElement(element);
        if (rect.Width <= 0 || rect.Height <= 0)
            return false;

        if (!IsPointInsideRoundedHitRect(element, rect, x, y))
            return false;

        return x >= rect.Left && x < rect.Left + rect.Width &&
               y >= rect.Top && y < rect.Top + rect.Height;
    }

    private (double Left, double Top, double Width, double Height) GetHitTestRectForElement(DomElement element)
    {
        if (IsDocumentElement(element))
            return GetBoundingClientRectForDomElement(element, isRoot: true);

        if (IsTableCellElement(element) &&
            TryGetSimpleTableCellHitTestRect(element, out var tableCellRect))
        {
            return tableCellRect;
        }

        if (TryGetListItemMarkerHitTestRect(element, out var listItemRect))
            return listItemRect;

        if (IsSvgGroupElement(element) &&
            TryGetSvgChildrenUnionRect(element, out var svgGroupRect))
        {
            return svgGroupRect;
        }

        if (IsSvgTextContentElement(element) &&
            TryGetSvgTextHitTestRect(element, out var svgTextRect))
        {
            return svgTextRect;
        }

        if (IsSvgTextContentElement(element) &&
            TryGetSvgChildrenUnionRect(element, out var svgTextChildrenRect))
        {
            return svgTextChildrenRect;
        }

        var rect = GetBoundingClientRectForDomElement(element, isRoot: false);
        return rect;
    }

    private static bool IsTableCellElement(DomElement element)
    {
        var tag = element.TagName;
        return string.Equals(tag, "td", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tag, "th", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAreaElement(DomElement element) =>
        string.Equals(element.TagName, "area", StringComparison.OrdinalIgnoreCase);

    private bool TryGetListItemMarkerHitTestRect(
        DomElement element,
        out (double Left, double Top, double Width, double Height) rect)
    {
        rect = default;
        var props = GetComputedProps(element);
        if (!IsOutsideListItemMarkerCandidate(element, props))
            return false;

        var baseRect = GetBoundingClientRectForDomElement(element, isRoot: false);
        if (baseRect.Width <= 0 || baseRect.Height <= 0)
            return false;

        var markerExtent = EstimateOutsideListMarkerExtent(element, props);
        if (markerExtent <= 0)
            return false;

        var isVertical = IsVerticalWritingMode(props.GetValueOrDefault("writing-mode"));
        if (isVertical)
        {
            rect = (baseRect.Left, baseRect.Top - markerExtent, baseRect.Width, baseRect.Height + markerExtent);
            return true;
        }

        var isRtl = string.Equals(props.GetValueOrDefault("direction"), "rtl", StringComparison.OrdinalIgnoreCase);
        rect = isRtl
            ? (baseRect.Left, baseRect.Top, baseRect.Width + markerExtent, baseRect.Height)
            : (baseRect.Left - markerExtent, baseRect.Top, baseRect.Width + markerExtent, baseRect.Height);
        return true;
    }

    private static bool IsOutsideListItemMarkerCandidate(
        DomElement element,
        IReadOnlyDictionary<string, string> props)
    {
        var isListItem = string.Equals(element.TagName, "li", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(props.GetValueOrDefault("display"), "list-item", StringComparison.OrdinalIgnoreCase);
        if (!isListItem)
            return false;

        if (string.Equals(props.GetValueOrDefault("list-style-position"), "inside", StringComparison.OrdinalIgnoreCase))
            return false;

        var listStyleType = props.GetValueOrDefault("list-style-type");
        var listStyleImage = props.GetValueOrDefault("list-style-image");
        return !string.Equals(listStyleType, "none", StringComparison.OrdinalIgnoreCase) ||
               (!string.IsNullOrWhiteSpace(listStyleImage) &&
                !string.Equals(listStyleImage, "none", StringComparison.OrdinalIgnoreCase));
    }

    private double EstimateOutsideListMarkerExtent(DomElement element, IReadOnlyDictionary<string, string> props)
    {
        var fontSize = Math.Max(8, ResolveFontSizeForElement(element));
        var listStyleType = props.GetValueOrDefault("list-style-type");
        var listStyleImage = props.GetValueOrDefault("list-style-image");
        var markerCore = !string.IsNullOrWhiteSpace(listStyleImage) &&
                         !string.Equals(listStyleImage, "none", StringComparison.OrdinalIgnoreCase)
            ? Math.Max(16, fontSize)
            : string.Equals(listStyleType, "decimal", StringComparison.OrdinalIgnoreCase)
                ? fontSize * 2
                : fontSize;

        return Math.Min(40, markerCore + 8);
    }

    private static bool IsTableStructuralHitTestOnlyElement(DomElement element)
    {
        var tag = element.TagName;
        return string.Equals(tag, "tr", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tag, "thead", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tag, "tbody", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tag, "tfoot", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tag, "colgroup", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tag, "col", StringComparison.OrdinalIgnoreCase);
    }

    private bool TryGetSimpleTableCellHitTestRect(
        DomElement element,
        out (double Left, double Top, double Width, double Height) rect)
    {
        rect = default;
        var row = element.Parent;
        if (row == null || !string.Equals(row.TagName, "tr", StringComparison.OrdinalIgnoreCase))
            return false;

        var table = row.Parent;
        if (table != null &&
            (string.Equals(table.TagName, "thead", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(table.TagName, "tbody", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(table.TagName, "tfoot", StringComparison.OrdinalIgnoreCase)))
        {
            table = table.Parent;
        }

        if (table == null || !string.Equals(table.TagName, "table", StringComparison.OrdinalIgnoreCase))
            return false;

        var rows = CollectTableRows(table);
        var rowIndex = rows.FindIndex(candidate => ReferenceEquals(candidate, row));
        if (rowIndex < 0)
            return false;

        var cells = row.Children
            .Where(child => !child.IsTextNode && IsTableCellElement(child))
            .ToList();
        var cellIndex = cells.FindIndex(candidate => ReferenceEquals(candidate, element));
        if (cellIndex < 0)
            return false;

        var columnCount = rows
            .Select(candidate => candidate.Children.Count(child => !child.IsTextNode && IsTableCellElement(child)))
            .DefaultIfEmpty(0)
            .Max();
        if (columnCount <= 0 || rows.Count <= 0)
            return false;

        var tableRect = GetBoundingClientRectForDomElement(table, isRoot: false);
        if (tableRect.Width <= 0 || tableRect.Height <= 0)
            return false;

        var (spacingX, spacingY) = GetEffectiveTableBorderSpacing(table);
        var cellWidth = Math.Max(0, (tableRect.Width - (columnCount + 1) * spacingX) / columnCount);
        var cellHeight = Math.Max(0, (tableRect.Height - (rows.Count + 1) * spacingY) / rows.Count);
        if (cellWidth <= 0 || cellHeight <= 0)
            return false;

        var tableProps = GetComputedProps(table);
        var isVertical = IsVerticalWritingMode(tableProps.GetValueOrDefault("writing-mode"));
        var isRtl = string.Equals(tableProps.GetValueOrDefault("direction"), "rtl", StringComparison.OrdinalIgnoreCase);
        var visualCellIndex = isRtl ? Math.Max(0, columnCount - 1 - cellIndex) : cellIndex;

        rect = !isVertical
            ? (
                tableRect.Left + spacingX + visualCellIndex * (cellWidth + spacingX),
                tableRect.Top + spacingY + rowIndex * (cellHeight + spacingY),
                cellWidth,
                cellHeight)
            : (
                tableRect.Left + spacingX + rowIndex * (cellWidth + spacingX),
                tableRect.Top + spacingY + visualCellIndex * (cellHeight + spacingY),
                cellWidth,
                cellHeight);
        return true;
    }

    private (double Horizontal, double Vertical) GetEffectiveTableBorderSpacing(DomElement table)
    {
        var rawValue = GetComputedProps(table).GetValueOrDefault("border-spacing");
        if (string.IsNullOrWhiteSpace(rawValue))
            return (2, 2);

        var parts = rawValue
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return (2, 2);

        var horizontal = ParseCssLengthToPixelsWithViewport(parts[0], table);
        var vertical = parts.Length > 1
            ? ParseCssLengthToPixelsWithViewport(parts[1], table)
            : horizontal;

        if (horizontal <= 0 && vertical <= 0)
            return (2, 2);

        return (horizontal, vertical);
    }

    private bool IsImageMapAreaHit(DomElement area, double x, double y)
    {
        var image = FindAssociatedImageMapImage(area);
        if (image == null)
            return false;

        var imageRect = GetBoundingClientRectForDomElement(image, isRoot: false);
        if (imageRect.Width <= 0 || imageRect.Height <= 0)
            return false;

        var localX = x - imageRect.Left;
        var localY = y - imageRect.Top;
        if (localX < 0 || localY < 0 || localX >= imageRect.Width || localY >= imageRect.Height)
            return false;

        var coordinateScale = GetImageMapCoordinateScale(image, imageRect);
        var scaledX = coordinateScale.ScaleX > 0 ? localX / coordinateScale.ScaleX : localX;
        var scaledY = coordinateScale.ScaleY > 0 ? localY / coordinateScale.ScaleY : localY;
        return IsPointInsideAreaShape(area, scaledX, scaledY);
    }

    private DomElement? FindAssociatedImageMapImage(DomElement area)
    {
        var map = area.Parent;
        if (map == null || !string.Equals(map.TagName, "map", StringComparison.OrdinalIgnoreCase))
            return null;

        var mapName = map.Attributes.GetValueOrDefault("name");
        if (string.IsNullOrWhiteSpace(mapName))
            mapName = map.Attributes.GetValueOrDefault("id");
        if (string.IsNullOrWhiteSpace(mapName))
            return null;

        var expectedUseMap = "#" + mapName.Trim();
        foreach (var candidate in EnumerateDomDescendants(GetOwningDocumentElement(area)))
        {
            if (!string.Equals(candidate.TagName, "img", StringComparison.OrdinalIgnoreCase))
                continue;

            var useMap = candidate.Attributes.GetValueOrDefault("usemap")?.Trim();
            if (string.Equals(useMap, expectedUseMap, StringComparison.OrdinalIgnoreCase))
                return candidate;
        }

        return null;
    }

    private IEnumerable<DomElement> EnumerateDomDescendants(DomElement root)
    {
        foreach (var child in root.Children)
        {
            if (child.IsTextNode || child.TagName.StartsWith("#", StringComparison.Ordinal))
                continue;

            yield return child;
            foreach (var descendant in EnumerateDomDescendants(child))
                yield return descendant;
        }
    }

    private (double ScaleX, double ScaleY) GetImageMapCoordinateScale(
        DomElement image,
        (double Left, double Top, double Width, double Height) imageRect)
    {
        var widthBasis = ParsePositiveDouble(image.Attributes.GetValueOrDefault("width"));
        var heightBasis = ParsePositiveDouble(image.Attributes.GetValueOrDefault("height"));

        return (
            widthBasis > 0 ? imageRect.Width / widthBasis : 1,
            heightBasis > 0 ? imageRect.Height / heightBasis : 1);
    }

    private bool IsPointInsideAreaShape(DomElement area, double x, double y)
    {
        var shape = area.Attributes.GetValueOrDefault("shape")?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(shape))
            shape = "rect";

        var coords = ParseAreaCoords(area.Attributes.GetValueOrDefault("coords"));
        return shape switch
        {
            "default" => true,
            "circle" => coords.Count >= 3 && IsPointInsideCircleArea(coords, x, y),
            "poly" or "polygon" => coords.Count >= 6 && IsPointInsidePolygonArea(coords, x, y),
            _ => coords.Count >= 4 && IsPointInsideRectArea(coords, x, y)
        };
    }

    private bool IsPointInsideRoundedHitRect(
        DomElement element,
        (double Left, double Top, double Width, double Height) rect,
        double x,
        double y)
    {
        var radius = GetUniformHitTestBorderRadius(element, rect.Width, rect.Height);
        if (radius <= 0)
            return true;

        var rx = Math.Min(radius, rect.Width / 2);
        var ry = Math.Min(radius, rect.Height / 2);
        if (rx <= 0 || ry <= 0)
            return true;

        var localX = x - rect.Left;
        var localY = y - rect.Top;
        if (localX < 0 || localY < 0 || localX >= rect.Width || localY >= rect.Height)
            return false;

        return !IsPointInsideRoundedCorner(localX, localY, rx, ry, rect.Width, rect.Height, top: true, left: true) &&
               !IsPointInsideRoundedCorner(localX, localY, rx, ry, rect.Width, rect.Height, top: true, left: false) &&
               !IsPointInsideRoundedCorner(localX, localY, rx, ry, rect.Width, rect.Height, top: false, left: true) &&
               !IsPointInsideRoundedCorner(localX, localY, rx, ry, rect.Width, rect.Height, top: false, left: false);
    }

    private bool IsPointInsideRoundedCorner(
        double localX,
        double localY,
        double rx,
        double ry,
        double width,
        double height,
        bool top,
        bool left)
    {
        var cornerLeft = left ? 0 : width - rx;
        var cornerTop = top ? 0 : height - ry;
        if (localX < cornerLeft || localX >= cornerLeft + rx || localY < cornerTop || localY >= cornerTop + ry)
            return false;

        var centerX = left ? rx : width - rx;
        var centerY = top ? ry : height - ry;
        var normalizedX = (localX - centerX) / rx;
        var normalizedY = (localY - centerY) / ry;
        return normalizedX * normalizedX + normalizedY * normalizedY > 1;
    }

    private double GetUniformHitTestBorderRadius(DomElement element, double width, double height)
    {
        var rawRadius = GetComputedProps(element).GetValueOrDefault("border-radius");
        if (string.IsNullOrWhiteSpace(rawRadius) || string.Equals(rawRadius, "0", StringComparison.Ordinal))
            return 0;

        var firstToken = rawRadius
            .Split([' ', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(firstToken))
            return 0;

        return ParseCssLengthToPixelsWithViewport(firstToken, element, percentageBasis: Math.Min(width, height));
    }

    private static List<double> ParseAreaCoords(string? rawCoords)
    {
        if (string.IsNullOrWhiteSpace(rawCoords))
            return [];

        return rawCoords
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(ParsePositiveOrNegativeDouble)
            .ToList();
    }

    private static bool IsPointInsideRectArea(IReadOnlyList<double> coords, double x, double y)
    {
        var left = Math.Min(coords[0], coords[2]);
        var right = Math.Max(coords[0], coords[2]);
        var top = Math.Min(coords[1], coords[3]);
        var bottom = Math.Max(coords[1], coords[3]);
        return x >= left && x <= right && y >= top && y <= bottom;
    }

    private static bool IsPointInsideCircleArea(IReadOnlyList<double> coords, double x, double y)
    {
        var dx = x - coords[0];
        var dy = y - coords[1];
        return dx * dx + dy * dy <= coords[2] * coords[2];
    }

    private static bool IsPointInsidePolygonArea(IReadOnlyList<double> coords, double x, double y)
    {
        var inside = false;
        var pointCount = coords.Count / 2;
        for (int i = 0, j = pointCount - 1; i < pointCount; j = i++)
        {
            var xi = coords[i * 2];
            var yi = coords[i * 2 + 1];
            var xj = coords[j * 2];
            var yj = coords[j * 2 + 1];

            var intersects = ((yi > y) != (yj > y)) &&
                             (x < (xj - xi) * (y - yi) / ((yj - yi) == 0 ? double.Epsilon : (yj - yi)) + xi);
            if (intersects)
                inside = !inside;
        }

        return inside;
    }

    private static double ParsePositiveDouble(string? rawValue)
    {
        var parsed = ParsePositiveOrNegativeDouble(rawValue);
        return parsed > 0 ? parsed : 0;
    }

    private static double ParsePositiveOrNegativeDouble(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return 0;

        return double.TryParse(
            rawValue.Trim(),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : 0;
    }

    private bool IsElementRenderedForHitTesting(DomElement element)
    {
        for (var current = element; current != null; current = current.Parent)
        {
            if (current.IsTextNode)
                return false;

            if (current.TagName.StartsWith("#", StringComparison.Ordinal))
                continue;

            var props = GetComputedProps(current);
            var display = props.GetValueOrDefault("display");
            if (string.Equals(display, "none", StringComparison.OrdinalIgnoreCase))
                return false;

            var visibility = props.GetValueOrDefault("visibility");
            if (string.Equals(visibility, "hidden", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(visibility, "collapse", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool DocumentHasViewport(DomElement documentElement)
    {
        var docRoot = documentElement.OwnerDocRoot;
        if (docRoot == null)
            return true;

        return !GetElementRuntimeState(docRoot).Document.HasViewport.TryGet(out var value) ||
               value is not bool hasViewport ||
               hasViewport;
    }

    /// <summary>Collects all elements matching a tag name in a sub-tree.</summary>
}
