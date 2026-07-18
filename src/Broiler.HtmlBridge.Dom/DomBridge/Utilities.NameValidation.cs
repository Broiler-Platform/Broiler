using System.Text.RegularExpressions;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.BuiltIns.Function;

namespace Broiler.HtmlBridge;

/// <summary>
/// Sibling partial peeled out of <c>Utilities.cs</c> (Phase 3 ratchet, 2026-07-17) to keep it
/// under the 750-line guard: the cohesive DOM element/qualified-name validation cluster together
/// with the JS-side constructor globals it validates against — the <c>DOMException</c> constructor
/// (and the C# helper that throws it), plus the <c>Node</c> and <c>SVGLength</c> constant carriers.
/// Pure partial-class relocation — no signature, accessibility, or logic change.
/// </summary>
public sealed partial class DomBridge
{
    // ------------------------------------------------------------------
    //  Element name validation
    // ------------------------------------------------------------------

    /// <summary>
    /// Regex for valid XML Name: must start with a Unicode letter or underscore,
    /// followed by Unicode letters, digits, hyphens, underscores, or dots.
    /// Uses Unicode categories per XML 1.0 §2.3 to accept non-ASCII characters
    /// such as U+212A (Kelvin sign).
    /// Colons are NOT allowed (use <see cref="ValidXmlQualifiedNamePattern"/> for qualified names).
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex ValidXmlNamePattern = ValidXmlNamePatternRegex();

    /// <summary>
    /// Regex for valid XML QName: either a simple name or prefix:localName
    /// where both prefix and localName are valid XML names (no colons).
    /// Uses Unicode categories per XML 1.0 §2.3.
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex ValidXmlQualifiedNamePattern = ValidXmlQualifiedNamePatternRegex();

    /// <summary>
    /// Throws a proper <c>DOMException</c> with the given name/code via the JS-registered constructor.
    /// Constructs the DOMException object in C# and throws it as a <see cref="JSException"/>
    /// so that JS try/catch blocks can intercept it with full <c>.code</c>, <c>.name</c>,
    /// and <c>.message</c> properties intact.
    /// </summary>
    internal static void ThrowDOMException(JSContext context, string message, string name)
    {
        if (context["DOMException"] is JSFunction domExCtor)
        {
            var exObj = domExCtor.CreateInstance(
                new Arguments(domExCtor, new JSString(message), new JSString(name)));
            throw new JSException(exObj);
        }

        // Fallback when DOMException constructor is unavailable
        throw new JSException(new JSString($"DOMException: {message} ({name})"));
    }

    /// <summary>
    /// Validates an element/doctype name per the XML spec.
    /// Throws a DOMException with INVALID_CHARACTER_ERR (code 5) for invalid names.
    /// </summary>
    internal static void ValidateElementName(string name, JSContext context)
    {
        if (string.IsNullOrEmpty(name) || name.Contains('\0') || !ValidXmlNamePattern.IsMatch(name))
        {
            ThrowDOMException(context,
                $"Failed to execute 'createElement': The tag name provided ('{name}') is not a valid name.",
                "InvalidCharacterError");
        }
    }

    /// <summary>
    /// Validates a qualified name and namespace per the Namespaces in XML spec.
    /// Throws a DOMException with NAMESPACE_ERR (code 14) for namespace violations.
    /// </summary>
    internal static void ValidateQualifiedName(string qualifiedName, string? ns, JSContext context)
    {
        // Check for empty prefix (e.g., ":div") first — this is a NamespaceError
        if (!string.IsNullOrEmpty(qualifiedName) && qualifiedName.StartsWith(':'))
        {
            ThrowDOMException(context,
                $"Failed to execute 'createElementNS': The qualified name provided ('{qualifiedName}') has an empty prefix.",
                "NamespaceError");
        }

        // Check for trailing colon (e.g., "a:") — empty local name is a NamespaceError
        if (!string.IsNullOrEmpty(qualifiedName) && qualifiedName.EndsWith(':'))
        {
            ThrowDOMException(context,
                $"Failed to execute 'createElementNS': The qualified name provided ('{qualifiedName}') has an empty local name.",
                "NamespaceError");
        }

        // Validate the name characters (allows optional single colon for prefix:localName)
        if (string.IsNullOrEmpty(qualifiedName) || !ValidXmlQualifiedNamePattern.IsMatch(qualifiedName))
        {
            ThrowDOMException(context,
                $"Failed to execute 'createElementNS': The qualified name provided ('{qualifiedName}') is not a valid name.",
                "InvalidCharacterError");
        }

        var colonIndex = qualifiedName.IndexOf(':');
        if (colonIndex >= 0)
        {
            // Prefixed name: namespace must not be null
            if (string.IsNullOrEmpty(ns))
            {
                ThrowDOMException(context,
                    $"Failed to execute 'createElementNS': The namespace URI provided is empty for qualified name '{qualifiedName}'.",
                    "NamespaceError");
            }

            var prefix = qualifiedName[..colonIndex];
            // "xml" prefix must be the XML namespace
            if (prefix == "xml" && ns != "http://www.w3.org/XML/1998/namespace")
            {
                ThrowDOMException(context,
                    $"Failed to execute 'createElementNS': The namespace URI for prefix 'xml' is invalid.",
                    "NamespaceError");
            }

            // "xmlns" prefix must be the XMLNS namespace
            if (prefix == "xmlns" && ns != "http://www.w3.org/2000/xmlns/")
            {
                ThrowDOMException(context,
                    $"Failed to execute 'createElementNS': The namespace URI for prefix 'xmlns' is invalid.",
                    "NamespaceError");
            }

            // Non-"xmlns" prefix must not use the XMLNS namespace
            if (prefix != "xmlns" && ns == "http://www.w3.org/2000/xmlns/")
            {
                ThrowDOMException(context,
                    $"Failed to execute 'createElementNS': The XMLNS namespace URI may only be used with prefix 'xmlns'.",
                    "NamespaceError");
            }
        }
    }

    /// <summary>
    /// Registers the <c>DOMException</c> constructor on the JS context.
    /// </summary>
    private static void RegisterDOMException(JSContext context)
    {
        context.Eval(@"
            function DOMException(message, name) {
                this.message = message || '';
                this.name = name || 'Error';
                // Map name to legacy code
                var codeMap = {
                    'IndexSizeError': 1,
                    'DOMStringSizeError': 2,
                    'HierarchyRequestError': 3,
                    'WrongDocumentError': 4,
                    'InvalidCharacterError': 5,
                    'NoDataAllowedError': 6,
                    'NoModificationAllowedError': 7,
                    'NotFoundError': 8,
                    'NotSupportedError': 9,
                    'InUseAttributeError': 10,
                    'InvalidStateError': 11,
                    'SyntaxError': 12,
                    'InvalidModificationError': 13,
                    'NamespaceError': 14,
                    'InvalidAccessError': 15,
                    'TypeMismatchError': 17,
                    'SecurityError': 18,
                    'NetworkError': 19,
                    'AbortError': 20,
                    'URLMismatchError': 21,
                    'QuotaExceededError': 22,
                    'TimeoutError': 23,
                    'InvalidNodeTypeError': 24,
                    'DataCloneError': 25
                };
                this.code = codeMap[this.name] || 0;
            }
            DOMException.INDEX_SIZE_ERR = 1;
            DOMException.DOMSTRING_SIZE_ERR = 2;
            DOMException.HIERARCHY_REQUEST_ERR = 3;
            DOMException.WRONG_DOCUMENT_ERR = 4;
            DOMException.INVALID_CHARACTER_ERR = 5;
            DOMException.NO_DATA_ALLOWED_ERR = 6;
            DOMException.NO_MODIFICATION_ALLOWED_ERR = 7;
            DOMException.NOT_FOUND_ERR = 8;
            DOMException.NOT_SUPPORTED_ERR = 9;
            DOMException.INUSE_ATTRIBUTE_ERR = 10;
            DOMException.INVALID_STATE_ERR = 11;
            DOMException.SYNTAX_ERR = 12;
            DOMException.INVALID_MODIFICATION_ERR = 13;
            DOMException.NAMESPACE_ERR = 14;
            DOMException.INVALID_ACCESS_ERR = 15;
            DOMException.TYPE_MISMATCH_ERR = 17;
            DOMException.SECURITY_ERR = 18;
            DOMException.NETWORK_ERR = 19;
            DOMException.ABORT_ERR = 20;
            DOMException.URL_MISMATCH_ERR = 21;
            DOMException.QUOTA_EXCEEDED_ERR = 22;
            DOMException.TIMEOUT_ERR = 23;
            DOMException.INVALID_NODE_TYPE_ERR = 24;
            DOMException.DATA_CLONE_ERR = 25;
            DOMException.prototype = Object.create(Error.prototype);
            DOMException.prototype.constructor = DOMException;
            DOMException.prototype.INDEX_SIZE_ERR = 1;
            DOMException.prototype.DOMSTRING_SIZE_ERR = 2;
            DOMException.prototype.HIERARCHY_REQUEST_ERR = 3;
            DOMException.prototype.WRONG_DOCUMENT_ERR = 4;
            DOMException.prototype.INVALID_CHARACTER_ERR = 5;
            DOMException.prototype.NO_DATA_ALLOWED_ERR = 6;
            DOMException.prototype.NO_MODIFICATION_ALLOWED_ERR = 7;
            DOMException.prototype.NOT_FOUND_ERR = 8;
            DOMException.prototype.NOT_SUPPORTED_ERR = 9;
            DOMException.prototype.INUSE_ATTRIBUTE_ERR = 10;
            DOMException.prototype.INVALID_STATE_ERR = 11;
            DOMException.prototype.SYNTAX_ERR = 12;
            DOMException.prototype.INVALID_MODIFICATION_ERR = 13;
            DOMException.prototype.NAMESPACE_ERR = 14;
            DOMException.prototype.INVALID_ACCESS_ERR = 15;
            DOMException.prototype.TYPE_MISMATCH_ERR = 17;
            DOMException.prototype.SECURITY_ERR = 18;
            DOMException.prototype.NETWORK_ERR = 19;
            DOMException.prototype.ABORT_ERR = 20;
            DOMException.prototype.URL_MISMATCH_ERR = 21;
            DOMException.prototype.QUOTA_EXCEEDED_ERR = 22;
            DOMException.prototype.TIMEOUT_ERR = 23;
            DOMException.prototype.INVALID_NODE_TYPE_ERR = 24;
            DOMException.prototype.DATA_CLONE_ERR = 25;
        ");
    }

    /// <summary>
    /// Registers the <c>Node</c> constructor with DOM type constants on the JS context.
    /// </summary>
    private static void RegisterNodeConstructor(JSContext context)
    {
        context.Eval(@"
            function Node() {}
            Node.ELEMENT_NODE = 1;
            Node.ATTRIBUTE_NODE = 2;
            Node.TEXT_NODE = 3;
            Node.CDATA_SECTION_NODE = 4;
            Node.ENTITY_REFERENCE_NODE = 5;
            Node.ENTITY_NODE = 6;
            Node.PROCESSING_INSTRUCTION_NODE = 7;
            Node.COMMENT_NODE = 8;
            Node.DOCUMENT_NODE = 9;
            Node.DOCUMENT_TYPE_NODE = 10;
            Node.DOCUMENT_FRAGMENT_NODE = 11;
            Node.NOTATION_NODE = 12;
            Node.prototype.ELEMENT_NODE = 1;
            Node.prototype.ATTRIBUTE_NODE = 2;
            Node.prototype.TEXT_NODE = 3;
            Node.prototype.CDATA_SECTION_NODE = 4;
            Node.prototype.ENTITY_REFERENCE_NODE = 5;
            Node.prototype.ENTITY_NODE = 6;
            Node.prototype.PROCESSING_INSTRUCTION_NODE = 7;
            Node.prototype.COMMENT_NODE = 8;
            Node.prototype.DOCUMENT_NODE = 9;
            Node.prototype.DOCUMENT_TYPE_NODE = 10;
            Node.prototype.DOCUMENT_FRAGMENT_NODE = 11;
            Node.prototype.NOTATION_NODE = 12;
        ");
    }

    private static void RegisterSVGLength(JSContext context)
    {
        context.Eval(@"
            function SVGLength() {}
            SVGLength.SVG_LENGTHTYPE_UNKNOWN = 0;
            SVGLength.SVG_LENGTHTYPE_NUMBER = 1;
            SVGLength.SVG_LENGTHTYPE_PERCENTAGE = 2;
            SVGLength.SVG_LENGTHTYPE_EMS = 3;
            SVGLength.SVG_LENGTHTYPE_EXS = 4;
            SVGLength.SVG_LENGTHTYPE_PX = 5;
            SVGLength.SVG_LENGTHTYPE_CM = 6;
            SVGLength.SVG_LENGTHTYPE_MM = 7;
            SVGLength.SVG_LENGTHTYPE_IN = 8;
            SVGLength.SVG_LENGTHTYPE_PT = 9;
            SVGLength.SVG_LENGTHTYPE_PC = 10;
        ");
    }

    [GeneratedRegex(@"^[\p{L}_][\p{L}\p{N}_.\-]*$", RegexOptions.Compiled)]
    private static partial System.Text.RegularExpressions.Regex ValidXmlNamePatternRegex();
    [GeneratedRegex(@"^[\p{L}_][\p{L}\p{N}_.\-]*(?::[\p{L}_][\p{L}\p{N}_.\-]*)?$", RegexOptions.Compiled)]
    private static partial System.Text.RegularExpressions.Regex ValidXmlQualifiedNamePatternRegex();
}
