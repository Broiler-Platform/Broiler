using System.Runtime.CompilerServices;
using Broiler.JavaScript.Runtime;

// Type forwarding for JSObject types moved to Broiler.JavaScript.Runtime assembly.
// These ensure binary compatibility for downstream consumers that reference
// Broiler.JavaScript.Core.

[assembly: TypeForwardedTo(typeof(JSObject))]
[assembly: TypeForwardedTo(typeof(JSObjectStatic))]
[assembly: TypeForwardedTo(typeof(PropertyEnumerator))]
[assembly: TypeForwardedTo(typeof(IntKeyEnumerator))]
[assembly: TypeForwardedTo(typeof(JSObjectBuilder))]
[assembly: TypeForwardedTo(typeof(JSIterator))]
[assembly: TypeForwardedTo(typeof(PropertyValueEnumerator))]
[assembly: TypeForwardedTo(typeof(JSPropertyAttributesBuilder))]
