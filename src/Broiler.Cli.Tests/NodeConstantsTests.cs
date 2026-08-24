using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;

namespace Broiler.Cli.Tests;

/// <summary>
/// The <c>Node</c> interface constants (DOM §4.4) — the twelve <c>*_NODE</c> type values and the six
/// <c>DOCUMENT_POSITION_*</c> bits — on the <c>Node</c> global and on node instances.
/// </summary>
/// <remarks>
/// The position bits were absent everywhere. <c>compareDocumentPosition</c> already returned a
/// correct bitmask, but with the names undefined a page could not decode it:
/// <c>result &amp; Node.DOCUMENT_POSITION_CONTAINED_BY</c> is <c>result &amp; undefined</c>, which is
/// <c>0</c> rather than an error — so a containment test did not throw, it silently answered "not
/// contained" for every pair of nodes. The decode tests below are the ones that were wrong; the raw
/// bitmask test guards the half that was already right.
/// <para>
/// The type constants were also installed from five hand-copied blocks that had drifted to different
/// subsets — the element and non-element wrappers carried eight of the twelve, the document and
/// sub-document only six (no <c>ATTRIBUTE_NODE</c>, no <c>CDATA_SECTION_NODE</c>) — so the
/// completeness assertions here cover instances of each kind.
/// </para>
/// </remarks>
public class NodeConstantsTests
{
    private static string ExecJs(string jsCode)
    {
        var html = $@"<!doctype html>
<html><head><title>T</title></head>
<body><div id=""a""><span id=""b"">x</span></div><p id=""c"">y</p><div id=""result""></div>
<script>
{jsCode}
</script>
</body></html>";
        return CaptureService.ExecuteScriptsWithDom(html, "https://example.com/page");
    }

    // The decode that silently answered "not contained" for every pair of nodes.
    [Fact(Timeout = 600000)]
    public void Containment_Decodes_Through_The_Named_Position_Bits()
    {
        var result = ExecJs(@"
            var a = document.getElementById('a'), b = document.getElementById('b');
            document.getElementById('result').textContent =
                'CONTAINED:' + !!(a.compareDocumentPosition(b) & Node.DOCUMENT_POSITION_CONTAINED_BY) +
                ',CONTAINS:' + !!(b.compareDocumentPosition(a) & Node.DOCUMENT_POSITION_CONTAINS);
        ");
        Assert.Contains("CONTAINED:true", result);
        Assert.Contains("CONTAINS:true", result);
    }

    [Fact(Timeout = 600000)]
    public void Document_Order_Decodes_Through_The_Named_Position_Bits()
    {
        var result = ExecJs(@"
            var a = document.getElementById('a'), c = document.getElementById('c');
            document.getElementById('result').textContent =
                'FOLLOWING:' + !!(a.compareDocumentPosition(c) & Node.DOCUMENT_POSITION_FOLLOWING) +
                ',PRECEDING:' + !!(c.compareDocumentPosition(a) & Node.DOCUMENT_POSITION_PRECEDING);
        ");
        Assert.Contains("FOLLOWING:true", result);
        Assert.Contains("PRECEDING:true", result);
    }

    [Fact(Timeout = 600000)]
    public void Node_Global_Exposes_Every_Position_Bit_With_Its_Specified_Value()
    {
        var result = ExecJs(@"
            document.getElementById('result').textContent = 'BITS:' + [
                Node.DOCUMENT_POSITION_DISCONNECTED,
                Node.DOCUMENT_POSITION_PRECEDING,
                Node.DOCUMENT_POSITION_FOLLOWING,
                Node.DOCUMENT_POSITION_CONTAINS,
                Node.DOCUMENT_POSITION_CONTAINED_BY,
                Node.DOCUMENT_POSITION_IMPLEMENTATION_SPECIFIC
            ].join(',');
        ");
        Assert.Contains("BITS:1,2,4,8,16,32", result);
    }

    // An element, a text node and the document are minted by three different code paths; each must
    // carry the full constant set.
    [Theory(Timeout = 600000)]
    [InlineData("document.getElementById('a')")]
    [InlineData("document.getElementById('b').firstChild")]
    [InlineData("document")]
    public void Every_Node_Kind_Carries_The_Full_Constant_Set(string node)
    {
        var result = ExecJs($@"
            var n = {node};
            document.getElementById('result').textContent = 'VALS:' + [
                n.ELEMENT_NODE, n.ATTRIBUTE_NODE, n.TEXT_NODE, n.CDATA_SECTION_NODE,
                n.ENTITY_REFERENCE_NODE, n.ENTITY_NODE, n.PROCESSING_INSTRUCTION_NODE,
                n.COMMENT_NODE, n.DOCUMENT_NODE, n.DOCUMENT_TYPE_NODE,
                n.DOCUMENT_FRAGMENT_NODE, n.NOTATION_NODE,
                n.DOCUMENT_POSITION_DISCONNECTED, n.DOCUMENT_POSITION_PRECEDING,
                n.DOCUMENT_POSITION_FOLLOWING, n.DOCUMENT_POSITION_CONTAINS,
                n.DOCUMENT_POSITION_CONTAINED_BY, n.DOCUMENT_POSITION_IMPLEMENTATION_SPECIFIC
            ].join(',');
        ");
        Assert.Contains("VALS:1,2,3,4,5,6,7,8,9,10,11,12,1,2,4,8,16,32", result);
    }

    // The half that was already correct, and the nodeType it is read against, must stay correct.
    [Fact(Timeout = 600000)]
    public void The_Returned_Bitmask_And_NodeTypes_Are_Unchanged()
    {
        var result = ExecJs(@"
            var a = document.getElementById('a'), b = document.getElementById('b'), c = document.getElementById('c');
            document.getElementById('result').textContent =
                'MASKS:' + [a.compareDocumentPosition(b), a.compareDocumentPosition(c),
                            c.compareDocumentPosition(a), a.compareDocumentPosition(a)].join(',') +
                ',TYPES:' + [a.nodeType, b.firstChild.nodeType, document.nodeType].join(',');
        ");
        Assert.Contains("MASKS:20,4,2,0", result);
        Assert.Contains("TYPES:1,3,9", result);
    }
}
