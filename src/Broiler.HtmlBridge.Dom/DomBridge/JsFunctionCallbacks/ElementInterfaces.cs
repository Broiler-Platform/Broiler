using Broiler.JavaScript.BuiltIns.Null;
using System.Text;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.Dom;
using System.Globalization;

namespace Broiler.HtmlBridge.Dom;

public sealed partial class DomBridge
{

    private JSValue JsElementInterfacesGetCaption001Core(DomElement element, in Arguments _)
    {
        var cap = ChildElements(element).FirstOrDefault(c => string.Equals(c.TagName, "caption", StringComparison.OrdinalIgnoreCase));
        return cap != null ? ToJSObject(cap) : JSNull.Value;
    }


    private JSValue JsElementInterfacesGetTHead002Core(DomElement element, in Arguments _)
    {
        var th = ChildElements(element).FirstOrDefault(c => string.Equals(c.TagName, "thead", StringComparison.OrdinalIgnoreCase));
        return th != null ? ToJSObject(th) : JSNull.Value;
    }


    private JSValue JsElementInterfacesGetTFoot003Core(DomElement element, in Arguments _)
    {
        var tf = ChildElements(element).FirstOrDefault(c => string.Equals(c.TagName, "tfoot", StringComparison.OrdinalIgnoreCase));
        return tf != null ? ToJSObject(tf) : JSNull.Value;
    }


    private JSValue JsElementInterfacesGetTBodies005Core(DomElement element, in Arguments _)
    {
        var bodies = new List<JSValue>();
        foreach (var c in ChildElements(element))
            if (string.Equals(c.TagName, "tbody", StringComparison.OrdinalIgnoreCase))
                bodies.Add(ToJSObject(c));
        var arr = new JSArray(bodies);
        JSValue JsElementInterfacesGetLength004(in Arguments __)
        {
            return new JSNumber(bodies.Count);
        }

        arr.FastAddProperty((KeyString)"length", new JSFunction(JsElementInterfacesGetLength004, "get length"), null, JSPropertyAttributes.EnumerableConfigurableProperty);
        return arr;
    }


    private JSValue JsElementInterfacesCreateCaption007Core(DomBridge? bridge, DomElement element, in Arguments _)
    {
        var cap = ChildElements(element).FirstOrDefault(c => string.Equals(c.TagName, "caption", StringComparison.OrdinalIgnoreCase));
        if (cap != null)
            return ToJSObject(cap);
        cap = CreateBridgeElement("caption");
        bridge._knownNodes.Add(cap);
        SetParent(cap, element);
        InsertChildAt(element, 0, cap);
        return ToJSObject(cap);
    }


    private JSValue JsElementInterfacesCreateTHead008Core(DomBridge? bridge, DomElement element, in Arguments _)
    {
        var th = ChildElements(element).FirstOrDefault(c => string.Equals(c.TagName, "thead", StringComparison.OrdinalIgnoreCase));
        if (th != null)
            return ToJSObject(th);
        th = CreateBridgeElement("thead");
        bridge._knownNodes.Add(th);
        SetParent(th, element);
        element.AppendChild(th);
        return ToJSObject(th);
    }


    private JSValue JsElementInterfacesCreateTFoot009Core(DomBridge? bridge, DomElement element, in Arguments _)
    {
        var tf = ChildElements(element).FirstOrDefault(c => string.Equals(c.TagName, "tfoot", StringComparison.OrdinalIgnoreCase));
        if (tf != null)
            return ToJSObject(tf);
        tf = CreateBridgeElement("tfoot");
        bridge._knownNodes.Add(tf);
        SetParent(tf, element);
        element.AppendChild(tf);
        return ToJSObject(tf);
    }


    private JSValue JsElementInterfacesDeleteCaption010Core(DomElement element, in Arguments _)
    {
        var cap = ChildElements(element).FirstOrDefault(c => string.Equals(c.TagName, "caption", StringComparison.OrdinalIgnoreCase));
        if (cap != null)
        {
            SetParent(cap, null);
            RemoveChildFrom(element, cap);
        }

        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesDeleteTHead011Core(DomElement element, in Arguments _)
    {
        var th = ChildElements(element).FirstOrDefault(c => string.Equals(c.TagName, "thead", StringComparison.OrdinalIgnoreCase));
        if (th != null)
        {
            SetParent(th, null);
            RemoveChildFrom(element, th);
        }

        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesDeleteTFoot012Core(DomElement element, in Arguments _)
    {
        var tf = ChildElements(element).FirstOrDefault(c => string.Equals(c.TagName, "tfoot", StringComparison.OrdinalIgnoreCase));
        if (tf != null)
        {
            SetParent(tf, null);
            RemoveChildFrom(element, tf);
        }

        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesInsertRow013Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        var index = a.Length > 0 ? (int)a[0].DoubleValue : -1;
        return InsertTableRow(element, index, bridge);
    }


    private JSValue JsElementInterfacesDeleteRow014Core(DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSUndefined.Value;
        var index = (int)a[0].DoubleValue;
        var rows = CollectTableRows(element);
        if (index < 0)
            index = rows.Count + index;
        if (index >= 0 && index < rows.Count)
        {
            var row = rows[index];
            row.Remove();
            SetParent(row, null);
        }

        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesGetRows016Core(DomElement element, in Arguments _)
    {
        var rows = new List<JSValue>();
        foreach (var c in ChildElements(element))
            if (string.Equals(c.TagName, "tr", StringComparison.OrdinalIgnoreCase))
                rows.Add(ToJSObject(c));
        var arr = new JSArray(rows);
        JSValue JsElementInterfacesGetLength015(in Arguments __)
        {
            return new JSNumber(rows.Count);
        }

        arr.FastAddProperty((KeyString)"length", new JSFunction(JsElementInterfacesGetLength015, "get length"), null, JSPropertyAttributes.EnumerableConfigurableProperty);
        return arr;
    }


    private JSValue JsElementInterfacesInsertRow017Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        var index = a.Length > 0 ? (int)a[0].DoubleValue : -1;
        var tr = CreateBridgeElement("tr");
        bridge._knownNodes.Add(tr);
        SetParent(tr, element);
        var trRows = ChildElements(element).Where(c => string.Equals(c.TagName, "tr", StringComparison.OrdinalIgnoreCase)).ToList();
        if (index < 0 || index >= trRows.Count)
            element.AppendChild(tr);
        else
        {
            var refRow = trRows[index];
            var idx = ChildIndexOf(element, refRow);
            InsertChildAt(element, idx, tr);
        }

        return ToJSObject(tr);
    }


    private JSValue JsElementInterfacesGetRowIndex018Core(DomElement element, in Arguments _)
    {
        // Find parent table
        var tableEl = ParentEl(element);
        if (tableEl != null && (string.Equals(tableEl.TagName, "thead", StringComparison.OrdinalIgnoreCase) || string.Equals(tableEl.TagName, "tbody", StringComparison.OrdinalIgnoreCase) || string.Equals(tableEl.TagName, "tfoot", StringComparison.OrdinalIgnoreCase)))
            tableEl = ParentEl(tableEl);
        if (tableEl == null || !string.Equals(tableEl.TagName, "table", StringComparison.OrdinalIgnoreCase))
            return new JSNumber(-1);
        var rows = CollectTableRows(tableEl);
        return new JSNumber(rows.IndexOf(element));
    }


    private JSValue JsElementInterfacesGetSectionRowIndex019Core(DomElement element, in Arguments _)
    {
        var section = ParentEl(element);
        if (section == null)
            return new JSNumber(-1);
        var idx = 0;
        foreach (var c in ChildElements(section))
        {
            if (ReferenceEquals(c, element))
                return new JSNumber(idx);
            if (string.Equals(c.TagName, "tr", StringComparison.OrdinalIgnoreCase))
                idx++;
        }

        return new JSNumber(-1);
    }


    private JSValue JsElementInterfacesGetCells021Core(DomElement element, in Arguments _)
    {
        var cells = new List<JSValue>();
        foreach (var c in ChildElements(element))
            if (string.Equals(c.TagName, "td", StringComparison.OrdinalIgnoreCase) || string.Equals(c.TagName, "th", StringComparison.OrdinalIgnoreCase))
                cells.Add(ToJSObject(c));
        var arr = new JSArray(cells);
        JSValue JsElementInterfacesGetLength020(in Arguments __)
        {
            return new JSNumber(cells.Count);
        }

        arr.FastAddProperty((KeyString)"length", new JSFunction(JsElementInterfacesGetLength020, "get length"), null, JSPropertyAttributes.EnumerableConfigurableProperty);
        return arr;
    }


    private JSValue JsElementInterfacesInsertCell022Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        var index = a.Length > 0 ? (int)Math.Truncate(a[0].DoubleValue) : -1;
        var td = CreateBridgeElement("td");
        bridge._knownNodes.Add(td);
        SetParent(td, element);
        var cells = ChildElements(element).Where(c => !IsText(c) && IsTableCellElement(c)).ToList();
        if (index < 0 || index >= cells.Count)
        {
            element.AppendChild(td);
        }
        else
        {
            var referenceCell = cells[index];
            var childIndex = ChildIndexOf(element, referenceCell);
            if (childIndex < 0)
                element.AppendChild(td);
            else
                InsertChildAt(element, childIndex, td);
        }

        return ToJSObject(td);
    }


    private JSValue JsElementInterfacesDeleteCell023Core(DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'deleteCell' on 'HTMLTableRowElement': 1 argument required, but only 0 present.");
        var index = (int)Math.Truncate(a[0].DoubleValue);
        var cells = ChildElements(element).Where(c => !IsText(c) && IsTableCellElement(c)).ToList();
        if (index < 0)
            index = cells.Count + index;
        if (index < 0 || index >= cells.Count)
            throw new JSException("INDEX_SIZE_ERR");
        var cell = cells[index];
        SetParent(cell, null);
        RemoveChildFrom(element, cell);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesGetLength025Core(DomElement element, in Arguments _)
    {
        var controls = CollectFormControls(element);
        return new JSNumber(controls.Count);
    }


    private JSValue JsElementInterfacesSetAction027Core(DomElement element, in Arguments a)
    {
        SetAttr(element, "action", a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesSetOpen029Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        if (a.Length > 0 && a[0].BooleanValue)
            SetAttr(element, "open", "");
        else
            RemoveAttr(element, "open");
        bridge.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesShowModal030Core(DomBridge? bridge, DomElement element, in Arguments _)
    {
        SetAttr(element, "open", "");
        GetElementRuntimeState(element).Dialog.Modal.Set(true);
        GetElementRuntimeState(element).Dialog.TopLayerOrder.Set(++bridge._topLayerCounter);
        bridge.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesShow031Core(DomBridge? bridge, DomElement element, in Arguments _)
    {
        SetAttr(element, "open", "");
        bridge.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }


    // Popover API (HTML §popover): showPopover() promotes the element to the top
    // layer (so its ::backdrop renders). Modeled with the same runtime flag +
    // top-layer order the modal-dialog path uses.
    private JSValue JsElementInterfacesShowPopoverCore(DomBridge? bridge, DomElement element, in Arguments _)
    {
        GetElementRuntimeState(element).Dialog.PopoverOpen.Set(true);
        GetElementRuntimeState(element).Dialog.TopLayerOrder.Set(++bridge._topLayerCounter);
        bridge.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesHidePopoverCore(DomBridge? bridge, DomElement element, in Arguments _)
    {
        // CSS Position §overlay: hiding a popover whose `overlay` is transitioned
        // with `transition-behavior: allow-discrete` keeps it in the top layer for
        // the duration of the transition. A static render snapshots mid-transition,
        // so the popover (and its ::backdrop) must stay rendered — leave the flag
        // set. Without such a transition, hidePopover() removes it immediately.
        if (!bridge.PopoverKeepsOverlayOnHide(element))
            GetElementRuntimeState(element).Dialog.PopoverOpen.Remove();
        bridge.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesClose032Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        RemoveAttr(element, "open");
        GetElementRuntimeState(element).Dialog.Modal.Remove();
        if (a.Length > 0)
            GetElementRuntimeState(element).FormControl.ReturnValue.Set(a[0].ToString());
        bridge.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesSetOpen034Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        if (a.Length > 0 && a[0].BooleanValue)
            SetAttr(element, "open", "");
        else
            RemoveAttr(element, "open");
        bridge.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesSetReturnValue036Core(DomElement element, in Arguments a)
    {
        GetElementRuntimeState(element).FormControl.ReturnValue.Set(a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesAdd037Core(DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSUndefined.Value;

        if (a[0] is not JSObject optObj)
            return JSUndefined.Value;

        var optEl = FindDomElementByJSObject(optObj);
        if (optEl == null)
            return JSUndefined.Value;

        DomElement? refEl = null;
        if (a.Length > 1 && !a[1].IsNull && !a[1].IsUndefined)
        {
            if (a[1] is JSObject refObj)
                refEl = FindDomElementByJSObject(refObj);
        }

        optEl.Remove();
        SetParent(optEl, element);
        if (refEl != null)
        {
            var idx = ChildIndexOf(element, refEl);
            if (idx >= 0)
                InsertChildAt(element, idx, optEl);
            else
                element.AppendChild(optEl);
        }
        else
            element.AppendChild(optEl);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesGetOptions039Core(DomElement element, in Arguments _)
    {
        var opts = new List<JSValue>();
        foreach (var c in ChildElements(element))
            if (string.Equals(c.TagName, "option", StringComparison.OrdinalIgnoreCase))
                opts.Add(ToJSObject(c));
        var arr = new JSArray(opts);
        JSValue JsElementInterfacesGetLength038(in Arguments __)
        {
            return new JSNumber(opts.Count);
        }

        arr.FastAddProperty((KeyString)"length", new JSFunction(JsElementInterfacesGetLength038, "get length"), null, JSPropertyAttributes.EnumerableConfigurableProperty);
        return arr;
    }


    private JSValue JsElementInterfacesSetSelectedIndex041Core(DomElement element, in Arguments a)
    {
        var index = a.Length == 0 ? -1 : (int)Math.Truncate(a[0].DoubleValue);
        SetSelectSelectedIndex(element, index);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesGetSize042Core(DomElement element, in Arguments _)
    {
        if (TryGetAttribute(element, "size", out var rawSize) && int.TryParse(rawSize, out var parsedSize) && parsedSize > 0)
        {
            return new JSNumber(parsedSize);
        }

        return new JSNumber(0);
    }


    private JSValue JsElementInterfacesSetSize043Core(DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSUndefined.Value;
        var size = (int)Math.Truncate(a[0].DoubleValue);
        if (size > 0)
            SetAttr(element, "size", size.ToString());
        else
            RemoveAttr(element, "size");
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesSetDefaultSelected045Core(DomElement element, in Arguments a)
    {
        GetElementRuntimeState(element).FormControl.DefaultSelected.Set(a.Length > 0 && a[0].BooleanValue);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesSetHtmlFor047Core(DomElement element, in Arguments a)
    {
        SetAttr(element, "for", a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesSetHttpEquiv049Core(DomElement element, in Arguments a)
    {
        SetAttr(element, "http-equiv", a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesGetData050Core(DomBridge? bridge, DomElement element, in Arguments _)
    {
        if (!TryGetAttribute(element, "data", out var d))
            return new JSString(string.Empty);
        // Resolve relative URI against base URL
        if (Uri.TryCreate(bridge._pageUrl, UriKind.Absolute, out var baseUri) && Uri.TryCreate(baseUri, d, out var resolved))
            return new JSString(resolved.AbsoluteUri);
        return new JSString(d);
    }


    private JSValue JsElementInterfacesSetData051Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        SetAttr(element, "data", a.Length > 0 ? a[0].ToString() : string.Empty);
        // Invalidate cached sub-document when data changes
        bridge.InvalidateCachedSubDocument(element);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesSetType053Core(DomElement element, in Arguments a)
    {
        SetAttr(element, "type", a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesGetContentDocument054Core(DomBridge? bridge, DomElement element, in Arguments _)
    {
        var dataUrl = TryGetAttribute(element, "data", out var d) ? d : string.Empty;
        if (IsCrossOrigin(dataUrl, bridge._pageUrl))
            return JSNull.Value;
        // Check if the resource actually loaded successfully
        if (bridge.IsObjectLoadFailed(element))
            return JSNull.Value;
        return bridge.GetOrCreateSubDocument(element);
    }


    private JSValue JsElementInterfacesGetSVGDocument055Core(DomBridge? bridge, DomElement element, in Arguments _)
    {
        var dataUrl = TryGetAttribute(element, "data", out var d) ? d : string.Empty;
        if (IsCrossOrigin(dataUrl, bridge._pageUrl))
            return JSNull.Value;
        return bridge.GetOrCreateSubDocument(element);
    }


    private JSValue JsElementInterfacesGetHref056Core(DomBridge? bridge, DomElement element, in Arguments _)
    {
        if (!TryGetAttribute(element, "href", out var h))
            return new JSString(string.Empty);
        if (Uri.TryCreate(bridge._pageUrl, UriKind.Absolute, out var baseUri) && Uri.TryCreate(baseUri, h, out var resolved))
            return new JSString(resolved.AbsoluteUri);
        return new JSString(h);
    }


    private JSValue JsElementInterfacesSetHref057Core(DomElement element, in Arguments a)
    {
        SetAttr(element, "href", a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesCallback059Core(string? captured, DomElement element, in Arguments a)
    {
        SetAttr(element, captured, a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesGetHref060Core(DomBridge? bridge, DomElement element, in Arguments _)
    {
        if (!TryGetAttribute(element, "href", out var h))
            return new JSString(string.Empty);
        if (Uri.TryCreate(bridge._pageUrl, UriKind.Absolute, out var baseUri) && Uri.TryCreate(baseUri, h, out var resolved))
            return new JSString(resolved.AbsoluteUri);
        return new JSString(h);
    }


    private JSValue JsElementInterfacesSetHref061Core(DomElement element, in Arguments a)
    {
        SetAttr(element, "href", a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesCallback062Core(DomBridge? bridge, string? dimName, DomElement element, in Arguments _)
    {
        // First check computed style for this element
        var computed = bridge.BuildComputedStyleObject(element);
        var csVal = computed[(KeyString)dimName];
        if (csVal != null && !csVal.IsNull && !csVal.IsUndefined)
        {
            var cssStr = csVal.ToString();
            if (!string.IsNullOrEmpty(cssStr))
            {
                var px = ParseCssLengthToPixels(cssStr);
                if (!double.IsNaN(px))
                    return new JSNumber(px);
            }
        }

        // Fallback: HTML attribute
        if (TryGetAttribute(element, dimName, out var attrVal) && double.TryParse(attrVal, out var attrNum))
            return new JSNumber(attrNum);
        return new JSNumber(0);
    }


    private JSValue JsElementInterfacesCallback063Core(string? dimName, DomElement element, in Arguments a)
    {
        SetAttr(element, dimName, a.Length > 0 ? a[0].ToString() : "0");
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesGetScrollTop072Core(DomBridge? bridgeForOffset, DomElement element, in Arguments _)
    {
        if (bridgeForOffset.GetElementScrollOffset(element, vertical: true) is double sv)
            return new JSNumber(sv);
        return new JSNumber(0);
    }


    private JSValue JsElementInterfacesSetScrollTop073Core(DomBridge? bridgeForOffset, DomElement element, in Arguments a)
    {
        if (a.Length > 0)
            bridgeForOffset.SetElementScrollOffsetsWithBehavior(element, top: a[0].DoubleValue);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesGetScrollLeft074Core(DomBridge? bridgeForOffset, DomElement element, in Arguments _)
    {
        if (bridgeForOffset.GetElementScrollOffset(element, vertical: false) is double sv)
            return new JSNumber(sv);
        return new JSNumber(0);
    }


    private JSValue JsElementInterfacesSetScrollLeft075Core(DomBridge? bridgeForOffset, DomElement element, in Arguments a)
    {
        if (a.Length > 0)
            bridgeForOffset.SetElementScrollOffsetsWithBehavior(element, left: a[0].DoubleValue);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesGetOffsetParent078Core(DomBridge? bridgeForOffset, DomElement? elForOffset, in Arguments _)
    {
        var offsetParent = bridgeForOffset.GetOffsetParentForDomElement(elForOffset);
        return offsetParent != null ? bridgeForOffset.ToJSObject(offsetParent) : JSNull.Value;
    }


    private JSValue JsElementInterfacesGetBoundingClientRect079Core(DomBridge? bridgeForOffset, DomElement? elForOffset, bool isViewportElement, in Arguments _)
    {
        var (Left, Top, Width, Height) = bridgeForOffset.GetBoundingClientRectForDomElement(elForOffset, isViewportElement);
        var rect = new JSObject();

        rect.FastAddValue((KeyString)"x", new JSNumber(Left), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"y", new JSNumber(Top), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"top", new JSNumber(Top), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"left", new JSNumber(Left), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"right", new JSNumber(Left + Width), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"bottom", new JSNumber(Top + Height), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"width", new JSNumber(Width), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"height", new JSNumber(Height), JSPropertyAttributes.EnumerableConfigurableValue);
        
        return rect;
    }


    private JSValue JsElementInterfacesGetClientRects080Core(DomBridge? bridgeForOffset, DomElement? elForOffset, bool isViewportElement, in Arguments a2)
    {
        var (Left, Top, Width, Height) = bridgeForOffset.GetBoundingClientRectForDomElement(elForOffset, isViewportElement);
        var rect = new JSObject();

        rect.FastAddValue((KeyString)"x", new JSNumber(Left), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"y", new JSNumber(Top), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"top", new JSNumber(Top), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"left", new JSNumber(Left), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"right", new JSNumber(Left + Width), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"bottom", new JSNumber(Top + Height), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"width", new JSNumber(Width), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"height", new JSNumber(Height), JSPropertyAttributes.EnumerableConfigurableValue);
        
        return Width > 0 || Height > 0 || isViewportElement ? new JSArray([rect]) : new JSArray();
    }


    private JSValue JsElementInterfacesScrollIntoView081Core(DomBridge? bridgeForOffset, DomElement? elForOffset, in Arguments a)
    {
        var (Block, Inline, Behavior) = bridgeForOffset.GetScrollIntoViewOptions(a);
        bridgeForOffset.ScrollElementIntoView(elForOffset, Block, Inline, Behavior);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesScroll082Core(DomBridge? bridgeForOffset, DomElement element, in Arguments a)
    {
        var (left, top, behavior) = bridgeForOffset.GetScrollArguments(a);
        bridgeForOffset.SetElementScrollOffsetsWithBehavior(element, left, top, clamp: false, behavior: behavior);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesScrollTo083Core(DomBridge? bridgeForOffset, DomElement element, in Arguments a)
    {
        var (left, top, behavior) = bridgeForOffset.GetScrollArguments(a);
        bridgeForOffset.SetElementScrollOffsetsWithBehavior(element, left, top, clamp: false, behavior: behavior);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesScrollBy084Core(DomBridge? bridgeForOffset, DomElement element, in Arguments a)
    {
        var (left, top, behavior) = bridgeForOffset.GetScrollArguments(a);
        bridgeForOffset.SetElementScrollOffsetsWithBehavior(element, left, top, relative: true, clamp: false, behavior: behavior);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesScrollParent085Core(DomBridge? bridgeForOffset, DomElement? elForOffset, in Arguments _)
    {
        var scrollParent = bridgeForOffset.GetScrollParentForDomElement(elForOffset);
        return scrollParent != null ? bridgeForOffset.ToJSObject(scrollParent) : JSNull.Value;
    }


    private JSValue JsElementInterfacesCallback086Core(string? attrName, DomElement element, in Arguments _)
    {
        var animLength = new JSObject();
        var valueStr = TryGetAttribute(element, attrName, out var v) ? v : "0";
        double.TryParse(valueStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var numVal);
        var baseVal = CreateSvgLengthValue(numVal);
        var animVal = CreateSvgLengthValue(numVal);
        animLength.FastAddValue((KeyString)"baseVal", baseVal, JSPropertyAttributes.EnumerableConfigurableValue);
        animLength.FastAddValue((KeyString)"animVal", animVal, JSPropertyAttributes.EnumerableConfigurableValue);
        return animLength;
    }


    private JSValue JsElementInterfacesGetViewBox087Core(DomElement element, in Arguments _)
    {
        var animRect = new JSObject();
        var baseRect = new JSObject();
        double vbX = 0, vbY = 0, vbW = 0, vbH = 0;
        if (TryGetAttribute(element, "viewBox", out var vb) && !string.IsNullOrWhiteSpace(vb))
        {
            var parts = vb.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 4)
            {
                double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out vbX);
                double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out vbY);
                double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out vbW);
                double.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out vbH);
            }
        }

        baseRect.FastAddValue((KeyString)"x", new JSNumber(vbX), JSPropertyAttributes.EnumerableConfigurableValue);
        baseRect.FastAddValue((KeyString)"y", new JSNumber(vbY), JSPropertyAttributes.EnumerableConfigurableValue);
        baseRect.FastAddValue((KeyString)"width", new JSNumber(vbW), JSPropertyAttributes.EnumerableConfigurableValue);
        baseRect.FastAddValue((KeyString)"height", new JSNumber(vbH), JSPropertyAttributes.EnumerableConfigurableValue);
        animRect.FastAddValue((KeyString)"baseVal", baseRect, JSPropertyAttributes.EnumerableConfigurableValue);
        animRect.FastAddValue((KeyString)"animVal", baseRect, JSPropertyAttributes.EnumerableConfigurableValue);
        return animRect;
    }


    private JSValue JsElementInterfacesGetNumberOfChars088Core(DomElement element, in Arguments _)
    {
        var sb = new StringBuilder();
        CollectTextContent(element, sb);
        return new JSNumber(sb.Length);
    }


    private JSValue JsElementInterfacesGetComputedTextLength089Core(DomElement element, in Arguments _)
    {
        var sb = new StringBuilder();
        CollectTextContent(element, sb);
        // Stub: estimate using font-size * character count * 0.6 average advance ratio
        double fontSize = 16;
        if (TryGetAttribute(element, "font-size", out var fs))
        {
            var fsClean = fs.Replace("px", "").Replace("pt", "").Trim();
            double.TryParse(fsClean, NumberStyles.Any, CultureInfo.InvariantCulture, out fontSize);
        }

        return new JSNumber(sb.Length * fontSize * 0.6);
    }


    private JSValue JsElementInterfacesGetSubStringLength090Core(DomElement element, in Arguments a)
    {
        var sb = new StringBuilder();
        CollectTextContent(element, sb);
        var charnum = a.Length > 0 ? (int)a[0].DoubleValue : 0;
        var nchars = a.Length > 1 ? (int)a[1].DoubleValue : 0;
        if (charnum < 0 || charnum >= sb.Length)
            throw new JSException("INDEX_SIZE_ERR");
        if (nchars == 0)
            return new JSNumber(0);
        double fontSize = 16;
        if (TryGetAttribute(element, "font-size", out var fs))
        {
            var fsClean = fs.Replace("px", "").Replace("pt", "").Trim();
            double.TryParse(fsClean, NumberStyles.Any, CultureInfo.InvariantCulture, out fontSize);
        }

        return new JSNumber(nchars * fontSize * 0.6);
    }


    private JSValue JsElementInterfacesGetStartPositionOfChar091Core(DomElement element, in Arguments a)
    {
        var sb = new StringBuilder();
        CollectTextContent(element, sb);
        var charnum = a.Length > 0 ? (int)a[0].DoubleValue : 0;
        if (charnum < 0 || charnum >= sb.Length)
            throw new JSException("INDEX_SIZE_ERR");
        double fontSize = 16;
        if (TryGetAttribute(element, "font-size", out var fs))
        {
            var fsClean = fs.Replace("px", "").Replace("pt", "").Trim();
            double.TryParse(fsClean, NumberStyles.Any, CultureInfo.InvariantCulture, out fontSize);
        }

        var pt = new JSObject();
        pt.FastAddValue((KeyString)"x", new JSNumber(charnum * fontSize * 0.6), JSPropertyAttributes.EnumerableConfigurableValue);
        pt.FastAddValue((KeyString)"y", new JSNumber(fontSize), JSPropertyAttributes.EnumerableConfigurableValue);
        return pt;
    }


    private JSValue JsElementInterfacesGetEndPositionOfChar092Core(DomElement element, in Arguments a)
    {
        var sb = new StringBuilder();
        CollectTextContent(element, sb);
        var charnum = a.Length > 0 ? (int)a[0].DoubleValue : 0;
        if (charnum < 0 || charnum >= sb.Length)
            throw new JSException("INDEX_SIZE_ERR");
        double fontSize = 16;
        if (TryGetAttribute(element, "font-size", out var fs))
        {
            var fsClean = fs.Replace("px", "").Replace("pt", "").Trim();
            double.TryParse(fsClean, NumberStyles.Any, CultureInfo.InvariantCulture, out fontSize);
        }

        var pt = new JSObject();
        pt.FastAddValue((KeyString)"x", new JSNumber((charnum + 1) * fontSize * 0.6), JSPropertyAttributes.EnumerableConfigurableValue);
        pt.FastAddValue((KeyString)"y", new JSNumber(fontSize), JSPropertyAttributes.EnumerableConfigurableValue);
        return pt;
    }


    private JSValue JsElementInterfacesGetRotationOfChar093Core(DomElement element, in Arguments a)
    {
        var sb = new StringBuilder();
        CollectTextContent(element, sb);
        var charnum = a.Length > 0 ? (int)a[0].DoubleValue : 0;
        if (charnum < 0 || charnum >= sb.Length)
            throw new JSException("INDEX_SIZE_ERR");
        // Default rotation is 0 degrees (horizontal text)
        return new JSNumber(0);
    }


    private JSValue JsElementInterfacesSetCurrentTime095Core(ref double currentTime, in Arguments a)
    {
        if (a.Length > 0)
            currentTime = a[0].DoubleValue;
        return JSUndefined.Value;
    }

}
