using System.Runtime.CompilerServices;
using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.Core.Clr;
using Broiler.JavaScript.Core.Core.Module;
using Broiler.JavaScript.Core.Emit;

// Type forwarding for types moved to Broiler.JavaScript.Runtime assembly.
// These ensure binary compatibility for downstream consumers that reference
// Broiler.JavaScript.Core.

[assembly: TypeForwardedTo(typeof(JSExportAttribute))]
[assembly: TypeForwardedTo(typeof(JSExportSameNameAttribute))]
[assembly: TypeForwardedTo(typeof(ClrMemberNamingConvention))]
[assembly: TypeForwardedTo(typeof(ExportAttribute))]
[assembly: TypeForwardedTo(typeof(DefaultExportAttribute))]
[assembly: TypeForwardedTo(typeof(DictionaryCodeCache))]
[assembly: TypeForwardedTo(typeof(MethodProvider))]
[assembly: TypeForwardedTo(typeof(ListMethodProvider))]
[assembly: TypeForwardedTo(typeof(IJavaScriptObject))]
