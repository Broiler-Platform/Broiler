using Broiler.JavaScript.BuiltIns.Null;
using System.Text;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{

    private JSValue JsElementInterfacesGetCaption001Core(global::Broiler.HtmlBridge.DomElement element, in Arguments _)
    {
        var cap = element.Children.Find(c => string.Equals(c.TagName, "caption", StringComparison.OrdinalIgnoreCase));
        return cap != null ? ToJSObject(cap) : JSNull.Value;
    }


    private JSValue JsElementInterfacesGetTHead002Core(global::Broiler.HtmlBridge.DomElement element, in Arguments _)
    {
        var th = element.Children.Find(c => string.Equals(c.TagName, "thead", StringComparison.OrdinalIgnoreCase));
        return th != null ? ToJSObject(th) : JSNull.Value;
    }


    private JSValue JsElementInterfacesGetTFoot003Core(global::Broiler.HtmlBridge.DomElement element, in Arguments _)
    {
        var tf = element.Children.Find(c => string.Equals(c.TagName, "tfoot", StringComparison.OrdinalIgnoreCase));
        return tf != null ? ToJSObject(tf) : JSNull.Value;
    }


    private JSValue JsElementInterfacesGetTBodies005Core(global::Broiler.HtmlBridge.DomElement element, in Arguments _)
    {
        var bodies = new List<JSValue>();
        foreach (var c in element.Children)
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


    private JSValue JsElementInterfacesCreateCaption007Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomElement element, in Arguments _)
    {
        var cap = element.Children.Find(c => string.Equals(c.TagName, "caption", StringComparison.OrdinalIgnoreCase));
        if (cap != null)
            return ToJSObject(cap);
        cap = new DomElement(_document, "caption", null, null, string.Empty);
        bridge._knownNodes.Add(cap);
        cap.Parent = element;
        element.Children.Insert(0, cap);
        return ToJSObject(cap);
    }


    private JSValue JsElementInterfacesCreateTHead008Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomElement element, in Arguments _)
    {
        var th = element.Children.Find(c => string.Equals(c.TagName, "thead", StringComparison.OrdinalIgnoreCase));
        if (th != null)
            return ToJSObject(th);
        th = new DomElement(_document, "thead", null, null, string.Empty);
        bridge._knownNodes.Add(th);
        th.Parent = element;
        element.Children.Add(th);
        return ToJSObject(th);
    }


    private JSValue JsElementInterfacesCreateTFoot009Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomElement element, in Arguments _)
    {
        var tf = element.Children.Find(c => string.Equals(c.TagName, "tfoot", StringComparison.OrdinalIgnoreCase));
        if (tf != null)
            return ToJSObject(tf);
        tf = new DomElement(_document, "tfoot", null, null, string.Empty);
        bridge._knownNodes.Add(tf);
        tf.Parent = element;
        element.Children.Add(tf);
        return ToJSObject(tf);
    }


    private JSValue JsElementInterfacesDeleteCaption010Core(global::Broiler.HtmlBridge.DomElement element, in Arguments _)
    {
        var cap = element.Children.Find(c => string.Equals(c.TagName, "caption", StringComparison.OrdinalIgnoreCase));
        if (cap != null)
        {
            cap.Parent = null;
            element.Children.Remove(cap);
        }

        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesDeleteTHead011Core(global::Broiler.HtmlBridge.DomElement element, in Arguments _)
    {
        var th = element.Children.Find(c => string.Equals(c.TagName, "thead", StringComparison.OrdinalIgnoreCase));
        if (th != null)
        {
            th.Parent = null;
            element.Children.Remove(th);
        }

        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesDeleteTFoot012Core(global::Broiler.HtmlBridge.DomElement element, in Arguments _)
    {
        var tf = element.Children.Find(c => string.Equals(c.TagName, "tfoot", StringComparison.OrdinalIgnoreCase));
        if (tf != null)
        {
            tf.Parent = null;
            element.Children.Remove(tf);
        }

        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesInsertRow013Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomElement element, in Arguments a)
    {
        var index = a.Length > 0 ? (int)a[0].DoubleValue : -1;
        return InsertTableRow(element, index, bridge);
    }


    private JSValue JsElementInterfacesDeleteRow014Core(global::Broiler.HtmlBridge.DomElement element, in Arguments a)
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
            row.Parent?.Children.Remove(row);
            row.Parent = null;
        }

        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesGetRows016Core(global::Broiler.HtmlBridge.DomElement element, in Arguments _)
    {
        var rows = new List<JSValue>();
        foreach (var c in element.Children)
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


    private JSValue JsElementInterfacesInsertRow017Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomElement element, in Arguments a)
    {
        var index = a.Length > 0 ? (int)a[0].DoubleValue : -1;
        var tr = new DomElement(_document, "tr", null, null, string.Empty);
        bridge._knownNodes.Add(tr);
        tr.Parent = element;
        var trRows = element.Children.Where(c => string.Equals(c.TagName, "tr", StringComparison.OrdinalIgnoreCase)).ToList();
        if (index < 0 || index >= trRows.Count)
            element.Children.Add(tr);
        else
        {
            var refRow = trRows[index];
            var idx = element.Children.IndexOf(refRow);
            element.Children.Insert(idx, tr);
        }

        return ToJSObject(tr);
    }


    private JSValue JsElementInterfacesGetRowIndex018Core(global::Broiler.HtmlBridge.DomElement element, in Arguments _)
    {
        // Find parent table
        var tableEl = element.Parent;
        if (tableEl != null && (string.Equals(tableEl.TagName, "thead", StringComparison.OrdinalIgnoreCase) || string.Equals(tableEl.TagName, "tbody", StringComparison.OrdinalIgnoreCase) || string.Equals(tableEl.TagName, "tfoot", StringComparison.OrdinalIgnoreCase)))
            tableEl = tableEl.Parent;
        if (tableEl == null || !string.Equals(tableEl.TagName, "table", StringComparison.OrdinalIgnoreCase))
            return new JSNumber(-1);
        var rows = CollectTableRows(tableEl);
        return new JSNumber(rows.IndexOf(element));
    }


    private JSValue JsElementInterfacesGetSectionRowIndex019Core(global::Broiler.HtmlBridge.DomElement element, in Arguments _)
    {
        var section = element.Parent;
        if (section == null)
            return new JSNumber(-1);
        var idx = 0;
        foreach (var c in section.Children)
        {
            if (ReferenceEquals(c, element))
                return new JSNumber(idx);
            if (string.Equals(c.TagName, "tr", StringComparison.OrdinalIgnoreCase))
                idx++;
        }

        return new JSNumber(-1);
    }


    private JSValue JsElementInterfacesGetCells021Core(global::Broiler.HtmlBridge.DomElement element, in Arguments _)
    {
        var cells = new List<JSValue>();
        foreach (var c in element.Children)
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


    private JSValue JsElementInterfacesInsertCell022Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomElement element, in Arguments a)
    {
        var index = a.Length > 0 ? (int)Math.Truncate(a[0].DoubleValue) : -1;
        var td = new DomElement(_document, "td", null, null, string.Empty);
        bridge._knownNodes.Add(td);
        td.Parent = element;
        var cells = element.Children.Where(c => !c.IsTextNode && IsTableCellElement(c)).ToList();
        if (index < 0 || index >= cells.Count)
        {
            element.Children.Add(td);
        }
        else
        {
            var referenceCell = cells[index];
            var childIndex = element.Children.IndexOf(referenceCell);
            if (childIndex < 0)
                element.Children.Add(td);
            else
                element.Children.Insert(childIndex, td);
        }

        return ToJSObject(td);
    }


    private JSValue JsElementInterfacesDeleteCell023Core(global::Broiler.HtmlBridge.DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'deleteCell' on 'HTMLTableRowElement': 1 argument required, but only 0 present.");
        var index = (int)Math.Truncate(a[0].DoubleValue);
        var cells = element.Children.Where(c => !c.IsTextNode && IsTableCellElement(c)).ToList();
        if (index < 0)
            index = cells.Count + index;
        if (index < 0 || index >= cells.Count)
            throw new JSException("INDEX_SIZE_ERR");
        var cell = cells[index];
        cell.Parent = null;
        element.Children.Remove(cell);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesGetLength025Core(global::Broiler.HtmlBridge.DomElement element, in Arguments _)
    {
        var controls = CollectFormControls(element);
        return new JSNumber(controls.Count);
    }


    private JSValue JsElementInterfacesSetAction027Core(global::Broiler.HtmlBridge.DomElement element, in Arguments a)
    {
        SetAttr(element, "action", a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesSetOpen029Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomElement element, in Arguments a)
    {
        if (a.Length > 0 && a[0].BooleanValue)
            SetAttr(element, "open", "");
        else
            RemoveAttr(element, "open");
        bridge.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesShowModal030Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomElement element, in Arguments _)
    {
        SetAttr(element, "open", "");
        GetElementRuntimeState(element).Dialog.Modal.Set(true);
        GetElementRuntimeState(element).Dialog.TopLayerOrder.Set(++bridge._topLayerCounter);
        bridge.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesShow031Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomElement element, in Arguments _)
    {
        SetAttr(element, "open", "");
        bridge.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }


    // Popover API (HTML §popover): showPopover() promotes the element to the top
    // layer (so its ::backdrop renders). Modeled with the same runtime flag +
    // top-layer order the modal-dialog path uses.
    private JSValue JsElementInterfacesShowPopoverCore(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomElement element, in Arguments _)
    {
        GetElementRuntimeState(element).Dialog.PopoverOpen.Set(true);
        GetElementRuntimeState(element).Dialog.TopLayerOrder.Set(++bridge._topLayerCounter);
        bridge.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesHidePopoverCore(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomElement element, in Arguments _)
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


    private JSValue JsElementInterfacesClose032Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomElement element, in Arguments a)
    {
        RemoveAttr(element, "open");
        GetElementRuntimeState(element).Dialog.Modal.Remove();
        if (a.Length > 0)
            GetElementRuntimeState(element).FormControl.ReturnValue.Set(a[0].ToString());
        bridge.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesSetOpen034Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomElement element, in Arguments a)
    {
        if (a.Length > 0 && a[0].BooleanValue)
            SetAttr(element, "open", "");
        else
            RemoveAttr(element, "open");
        bridge.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesSetReturnValue036Core(global::Broiler.HtmlBridge.DomElement element, in Arguments a)
    {
        GetElementRuntimeState(element).FormControl.ReturnValue.Set(a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesAdd037Core(global::Broiler.HtmlBridge.DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSUndefined.Value;
        var optObj = a[0] as JSObject;
        if (optObj == null)
            return JSUndefined.Value;
        var optEl = FindDomElementByJSObject(optObj);
        if (optEl == null)
            return JSUndefined.Value;
        DomElement? refEl = null;
        if (a.Length > 1 && !a[1].IsNull && !a[1].IsUndefined)
        {
            var refObj = a[1] as JSObject;
            if (refObj != null)
                refEl = FindDomElementByJSObject(refObj);
        }

        optEl.Parent?.Children.Remove(optEl);
        optEl.Parent = element;
        if (refEl != null)
        {
            var idx = element.Children.IndexOf(refEl);
            if (idx >= 0)
                element.Children.Insert(idx, optEl);
            else
                element.Children.Add(optEl);
        }
        else
            element.Children.Add(optEl);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesGetOptions039Core(global::Broiler.HtmlBridge.DomElement element, in Arguments _)
    {
        var opts = new List<JSValue>();
        foreach (var c in element.Children)
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


    private JSValue JsElementInterfacesSetSelectedIndex041Core(global::Broiler.HtmlBridge.DomElement element, in Arguments a)
    {
        var index = a.Length == 0 ? -1 : (int)Math.Truncate(a[0].DoubleValue);
        SetSelectSelectedIndex(element, index);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesGetSize042Core(global::Broiler.HtmlBridge.DomElement element, in Arguments _)
    {
        if (TryGetAttribute(element, "size", out var rawSize) && int.TryParse(rawSize, out var parsedSize) && parsedSize > 0)
        {
            return new JSNumber(parsedSize);
        }

        return new JSNumber(0);
    }


    private JSValue JsElementInterfacesSetSize043Core(global::Broiler.HtmlBridge.DomElement element, in Arguments a)
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


    private JSValue JsElementInterfacesSetDefaultSelected045Core(global::Broiler.HtmlBridge.DomElement element, in Arguments a)
    {
        GetElementRuntimeState(element).FormControl.DefaultSelected.Set(a.Length > 0 && a[0].BooleanValue);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesSetHtmlFor047Core(global::Broiler.HtmlBridge.DomElement element, in Arguments a)
    {
        SetAttr(element, "for", a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesSetHttpEquiv049Core(global::Broiler.HtmlBridge.DomElement element, in Arguments a)
    {
        SetAttr(element, "http-equiv", a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesGetData050Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomElement element, in Arguments _)
    {
        if (!TryGetAttribute(element, "data", out var d))
            return new JSString(string.Empty);
        // Resolve relative URI against base URL
        if (Uri.TryCreate(bridge._pageUrl, UriKind.Absolute, out var baseUri) && Uri.TryCreate(baseUri, d, out var resolved))
            return new JSString(resolved.AbsoluteUri);
        return new JSString(d);
    }


    private JSValue JsElementInterfacesSetData051Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomElement element, in Arguments a)
    {
        SetAttr(element, "data", a.Length > 0 ? a[0].ToString() : string.Empty);
        // Invalidate cached sub-document when data changes
        bridge.InvalidateCachedSubDocument(element);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesSetType053Core(global::Broiler.HtmlBridge.DomElement element, in Arguments a)
    {
        SetAttr(element, "type", a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesGetContentDocument054Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomElement element, in Arguments _)
    {
        var dataUrl = TryGetAttribute(element, "data", out var d) ? d : string.Empty;
        if (IsCrossOrigin(dataUrl, bridge._pageUrl))
            return JSNull.Value;
        // Check if the resource actually loaded successfully
        if (bridge.IsObjectLoadFailed(element))
            return JSNull.Value;
        return bridge.GetOrCreateSubDocument(element);
    }


    private JSValue JsElementInterfacesGetSVGDocument055Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomElement element, in Arguments _)
    {
        var dataUrl = TryGetAttribute(element, "data", out var d) ? d : string.Empty;
        if (IsCrossOrigin(dataUrl, bridge._pageUrl))
            return JSNull.Value;
        return bridge.GetOrCreateSubDocument(element);
    }


    private JSValue JsElementInterfacesGetHref056Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomElement element, in Arguments _)
    {
        if (!TryGetAttribute(element, "href", out var h))
            return new JSString(string.Empty);
        if (Uri.TryCreate(bridge._pageUrl, UriKind.Absolute, out var baseUri) && Uri.TryCreate(baseUri, h, out var resolved))
            return new JSString(resolved.AbsoluteUri);
        return new JSString(h);
    }


    private JSValue JsElementInterfacesSetHref057Core(global::Broiler.HtmlBridge.DomElement element, in Arguments a)
    {
        SetAttr(element, "href", a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesCallback059Core(global::System.String? captured, global::Broiler.HtmlBridge.DomElement element, in Arguments a)
    {
        SetAttr(element, captured, a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesGetHref060Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::Broiler.HtmlBridge.DomElement element, in Arguments _)
    {
        if (!TryGetAttribute(element, "href", out var h))
            return new JSString(string.Empty);
        if (Uri.TryCreate(bridge._pageUrl, UriKind.Absolute, out var baseUri) && Uri.TryCreate(baseUri, h, out var resolved))
            return new JSString(resolved.AbsoluteUri);
        return new JSString(h);
    }


    private JSValue JsElementInterfacesSetHref061Core(global::Broiler.HtmlBridge.DomElement element, in Arguments a)
    {
        SetAttr(element, "href", a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesCallback062Core(global::Broiler.HtmlBridge.DomBridge? bridge, global::System.String? dimName, global::Broiler.HtmlBridge.DomElement element, in Arguments _)
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


    private JSValue JsElementInterfacesCallback063Core(global::System.String? dimName, global::Broiler.HtmlBridge.DomElement element, in Arguments a)
    {
        SetAttr(element, dimName, a.Length > 0 ? a[0].ToString() : "0");
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesGetScrollTop072Core(global::Broiler.HtmlBridge.DomBridge? bridgeForOffset, global::Broiler.HtmlBridge.DomElement element, in Arguments _)
    {
        if (bridgeForOffset.GetElementScrollOffset(element, vertical: true) is double sv)
            return new JSNumber(sv);
        return new JSNumber(0);
    }


    private JSValue JsElementInterfacesSetScrollTop073Core(global::Broiler.HtmlBridge.DomBridge? bridgeForOffset, global::Broiler.HtmlBridge.DomElement element, in Arguments a)
    {
        if (a.Length > 0)
            bridgeForOffset.SetElementScrollOffsetsWithBehavior(element, top: a[0].DoubleValue);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesGetScrollLeft074Core(global::Broiler.HtmlBridge.DomBridge? bridgeForOffset, global::Broiler.HtmlBridge.DomElement element, in Arguments _)
    {
        if (bridgeForOffset.GetElementScrollOffset(element, vertical: false) is double sv)
            return new JSNumber(sv);
        return new JSNumber(0);
    }


    private JSValue JsElementInterfacesSetScrollLeft075Core(global::Broiler.HtmlBridge.DomBridge? bridgeForOffset, global::Broiler.HtmlBridge.DomElement element, in Arguments a)
    {
        if (a.Length > 0)
            bridgeForOffset.SetElementScrollOffsetsWithBehavior(element, left: a[0].DoubleValue);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesGetOffsetParent078Core(global::Broiler.HtmlBridge.DomBridge? bridgeForOffset, global::Broiler.HtmlBridge.DomElement? elForOffset, in Arguments _)
    {
        var offsetParent = bridgeForOffset.GetOffsetParentForDomElement(elForOffset);
        return offsetParent != null ? bridgeForOffset.ToJSObject(offsetParent) : JSNull.Value;
    }


    private JSValue JsElementInterfacesGetBoundingClientRect079Core(global::Broiler.HtmlBridge.DomBridge? bridgeForOffset, global::Broiler.HtmlBridge.DomElement? elForOffset, global::System.Boolean isViewportElement, in Arguments _)
    {
        var rectData = bridgeForOffset.GetBoundingClientRectForDomElement(elForOffset, isViewportElement);
        var rect = new JSObject();
        rect.FastAddValue((KeyString)"x", new JSNumber(rectData.Left), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"y", new JSNumber(rectData.Top), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"top", new JSNumber(rectData.Top), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"left", new JSNumber(rectData.Left), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"right", new JSNumber(rectData.Left + rectData.Width), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"bottom", new JSNumber(rectData.Top + rectData.Height), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"width", new JSNumber(rectData.Width), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"height", new JSNumber(rectData.Height), JSPropertyAttributes.EnumerableConfigurableValue);
        return rect;
    }


    private JSValue JsElementInterfacesGetClientRects080Core(global::Broiler.HtmlBridge.DomBridge? bridgeForOffset, global::Broiler.HtmlBridge.DomElement? elForOffset, global::System.Boolean isViewportElement, in Arguments a2)
    {
        var rectData = bridgeForOffset.GetBoundingClientRectForDomElement(elForOffset, isViewportElement);
        var rect = new JSObject();
        rect.FastAddValue((KeyString)"x", new JSNumber(rectData.Left), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"y", new JSNumber(rectData.Top), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"top", new JSNumber(rectData.Top), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"left", new JSNumber(rectData.Left), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"right", new JSNumber(rectData.Left + rectData.Width), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"bottom", new JSNumber(rectData.Top + rectData.Height), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"width", new JSNumber(rectData.Width), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"height", new JSNumber(rectData.Height), JSPropertyAttributes.EnumerableConfigurableValue);
        return rectData.Width > 0 || rectData.Height > 0 || isViewportElement ? new JSArray([rect]) : new JSArray();
    }


    private JSValue JsElementInterfacesScrollIntoView081Core(global::Broiler.HtmlBridge.DomBridge? bridgeForOffset, global::Broiler.HtmlBridge.DomElement? elForOffset, in Arguments a)
    {
        var options = bridgeForOffset.GetScrollIntoViewOptions(a);
        bridgeForOffset.ScrollElementIntoView(elForOffset, options.Block, options.Inline, options.Behavior);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesScroll082Core(global::Broiler.HtmlBridge.DomBridge? bridgeForOffset, global::Broiler.HtmlBridge.DomElement element, in Arguments a)
    {
        var (left, top, behavior) = bridgeForOffset.GetScrollArguments(a);
        bridgeForOffset.SetElementScrollOffsetsWithBehavior(element, left, top, clamp: false, behavior: behavior);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesScrollTo083Core(global::Broiler.HtmlBridge.DomBridge? bridgeForOffset, global::Broiler.HtmlBridge.DomElement element, in Arguments a)
    {
        var (left, top, behavior) = bridgeForOffset.GetScrollArguments(a);
        bridgeForOffset.SetElementScrollOffsetsWithBehavior(element, left, top, clamp: false, behavior: behavior);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesScrollBy084Core(global::Broiler.HtmlBridge.DomBridge? bridgeForOffset, global::Broiler.HtmlBridge.DomElement element, in Arguments a)
    {
        var (left, top, behavior) = bridgeForOffset.GetScrollArguments(a);
        bridgeForOffset.SetElementScrollOffsetsWithBehavior(element, left, top, relative: true, clamp: false, behavior: behavior);
        return JSUndefined.Value;
    }


    private JSValue JsElementInterfacesScrollParent085Core(global::Broiler.HtmlBridge.DomBridge? bridgeForOffset, global::Broiler.HtmlBridge.DomElement? elForOffset, in Arguments _)
    {
        var scrollParent = bridgeForOffset.GetScrollParentForDomElement(elForOffset);
        return scrollParent != null ? bridgeForOffset.ToJSObject(scrollParent) : JSNull.Value;
    }


    private JSValue JsElementInterfacesCallback086Core(global::System.String? attrName, global::Broiler.HtmlBridge.DomElement element, in Arguments _)
    {
        var animLength = new JSObject();
        var valueStr = TryGetAttribute(element, attrName, out var v) ? v : "0";
        double.TryParse(valueStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var numVal);
        var baseVal = CreateSvgLengthValue(numVal);
        var animVal = CreateSvgLengthValue(numVal);
        animLength.FastAddValue((KeyString)"baseVal", baseVal, JSPropertyAttributes.EnumerableConfigurableValue);
        animLength.FastAddValue((KeyString)"animVal", animVal, JSPropertyAttributes.EnumerableConfigurableValue);
        return animLength;
    }


    private JSValue JsElementInterfacesGetViewBox087Core(global::Broiler.HtmlBridge.DomElement element, in Arguments _)
    {
        var animRect = new JSObject();
        var baseRect = new JSObject();
        double vbX = 0, vbY = 0, vbW = 0, vbH = 0;
        if (TryGetAttribute(element, "viewBox", out var vb) && !string.IsNullOrWhiteSpace(vb))
        {
            var parts = vb.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 4)
            {
                double.TryParse(parts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out vbX);
                double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out vbY);
                double.TryParse(parts[2], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out vbW);
                double.TryParse(parts[3], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out vbH);
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


    private JSValue JsElementInterfacesGetNumberOfChars088Core(global::Broiler.HtmlBridge.DomElement element, in Arguments _)
    {
        var sb = new StringBuilder();
        CollectTextContent(element, sb);
        return new JSNumber(sb.Length);
    }


    private JSValue JsElementInterfacesGetComputedTextLength089Core(global::Broiler.HtmlBridge.DomElement element, in Arguments _)
    {
        var sb = new StringBuilder();
        CollectTextContent(element, sb);
        // Stub: estimate using font-size * character count * 0.6 average advance ratio
        double fontSize = 16;
        if (TryGetAttribute(element, "font-size", out var fs))
        {
            var fsClean = fs.Replace("px", "").Replace("pt", "").Trim();
            double.TryParse(fsClean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out fontSize);
        }

        return new JSNumber(sb.Length * fontSize * 0.6);
    }


    private JSValue JsElementInterfacesGetSubStringLength090Core(global::Broiler.HtmlBridge.DomElement element, in Arguments a)
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
            double.TryParse(fsClean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out fontSize);
        }

        return new JSNumber(nchars * fontSize * 0.6);
    }


    private JSValue JsElementInterfacesGetStartPositionOfChar091Core(global::Broiler.HtmlBridge.DomElement element, in Arguments a)
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
            double.TryParse(fsClean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out fontSize);
        }

        var pt = new JSObject();
        pt.FastAddValue((KeyString)"x", new JSNumber(charnum * fontSize * 0.6), JSPropertyAttributes.EnumerableConfigurableValue);
        pt.FastAddValue((KeyString)"y", new JSNumber(fontSize), JSPropertyAttributes.EnumerableConfigurableValue);
        return pt;
    }


    private JSValue JsElementInterfacesGetEndPositionOfChar092Core(global::Broiler.HtmlBridge.DomElement element, in Arguments a)
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
            double.TryParse(fsClean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out fontSize);
        }

        var pt = new JSObject();
        pt.FastAddValue((KeyString)"x", new JSNumber((charnum + 1) * fontSize * 0.6), JSPropertyAttributes.EnumerableConfigurableValue);
        pt.FastAddValue((KeyString)"y", new JSNumber(fontSize), JSPropertyAttributes.EnumerableConfigurableValue);
        return pt;
    }


    private JSValue JsElementInterfacesGetRotationOfChar093Core(global::Broiler.HtmlBridge.DomElement element, in Arguments a)
    {
        var sb = new StringBuilder();
        CollectTextContent(element, sb);
        var charnum = a.Length > 0 ? (int)a[0].DoubleValue : 0;
        if (charnum < 0 || charnum >= sb.Length)
            throw new JSException("INDEX_SIZE_ERR");
        // Default rotation is 0 degrees (horizontal text)
        return new JSNumber(0);
    }


    private JSValue JsElementInterfacesSetCurrentTime095Core(ref global::System.Double currentTime, in Arguments a)
    {
        if (a.Length > 0)
            currentTime = a[0].DoubleValue;
        return JSUndefined.Value;
    }

}
