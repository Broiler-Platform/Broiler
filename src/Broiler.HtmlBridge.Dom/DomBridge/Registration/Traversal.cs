using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.BuiltIns.Function;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    private void RegisterDocumentTraversalApis(JSContext context, JSObject document)
    {
        // -- Phase 2: NodeFilter, TreeWalker, NodeIterator, Range --

        // NodeFilter constants
        var nodeFilter = new JSObject();
        nodeFilter.FastAddValue((KeyString)"FILTER_ACCEPT", new JSNumber(1), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"FILTER_REJECT", new JSNumber(2), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"FILTER_SKIP", new JSNumber(3), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_ALL", new JSNumber(0xFFFFFFFF), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_ELEMENT", new JSNumber(0x1), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_ATTRIBUTE", new JSNumber(0x2), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_TEXT", new JSNumber(0x4), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_CDATA_SECTION", new JSNumber(0x8), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_ENTITY_REFERENCE", new JSNumber(0x10), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_ENTITY", new JSNumber(0x20), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_PROCESSING_INSTRUCTION", new JSNumber(0x40), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_COMMENT", new JSNumber(0x80), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_DOCUMENT", new JSNumber(0x100), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_DOCUMENT_TYPE", new JSNumber(0x200), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_DOCUMENT_FRAGMENT", new JSNumber(0x400), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_NOTATION", new JSNumber(0x800), JSPropertyAttributes.EnumerableConfigurableValue);
        context["NodeFilter"] = nodeFilter;
        // document.createTreeWalker(root, whatToShow, filter)
        var bridgeForTraversal = this;
        document.FastAddValue(
            (KeyString)"createTreeWalker",
            new JSFunction((in Arguments a) => JsRegistrationCreateTreeWalker038Core(bridgeForTraversal, in a), "createTreeWalker", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // document.createNodeIterator(root, whatToShow, filter)
        document.FastAddValue(
            (KeyString)"createNodeIterator",
            new JSFunction((in Arguments a) => JsRegistrationCreateNodeIterator039Core(bridgeForTraversal, in a), "createNodeIterator", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // document.createRange()
        document.FastAddValue(
            (KeyString)"createRange",
            new JSFunction((in Arguments a) => bridgeForTraversal.BuildRange(), "createRange", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // document.createComment(data)
        document.FastAddValue(
            (KeyString)"createComment",
            new JSFunction(JsRegistrationCreateComment041Core, "createComment", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
    }

}
