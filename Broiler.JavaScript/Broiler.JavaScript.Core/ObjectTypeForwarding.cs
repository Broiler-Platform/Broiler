using System.Runtime.CompilerServices;
using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Object;
using Broiler.JavaScript.Core.Core.Generator;
using Broiler.JavaScript.Core.LinqExpressions;

// Type forwarding for JSObject types moved to Broiler.JavaScript.Runtime assembly.
// These ensure binary compatibility for downstream consumers that reference
// Broiler.JavaScript.Core.

[assembly: TypeForwardedTo(typeof(JSObject))]
[assembly: TypeForwardedTo(typeof(JSObjectStatic))]
[assembly: TypeForwardedTo(typeof(PropertyEnumerator))]
[assembly: TypeForwardedTo(typeof(KeyEnumerator))]
[assembly: TypeForwardedTo(typeof(JSObjectBuilder))]
[assembly: TypeForwardedTo(typeof(JSIterator))]
[assembly: TypeForwardedTo(typeof(PropertyValueEnumerator))]
[assembly: TypeForwardedTo(typeof(JSPropertyAttributesBuilder))]
