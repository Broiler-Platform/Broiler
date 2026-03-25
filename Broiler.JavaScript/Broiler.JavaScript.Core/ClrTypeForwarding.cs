using System.Runtime.CompilerServices;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Core.Core.Primitive;
using Broiler.JavaScript.Core.Core.Storage;

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
[assembly: TypeForwardedTo(typeof(MemberType))]
[assembly: TypeForwardedTo(typeof(SymbolAttribute))]
[assembly: TypeForwardedTo(typeof(PrototypeAttribute))]
[assembly: TypeForwardedTo(typeof(GetProperty))]
[assembly: TypeForwardedTo(typeof(SetProperty))]
[assembly: TypeForwardedTo(typeof(StaticGetProperty))]
[assembly: TypeForwardedTo(typeof(StaticSetProperty))]
[assembly: TypeForwardedTo(typeof(StaticAttribute))]

// Types moved as part of module reorganization
[assembly: TypeForwardedTo(typeof(JSPropertyFactory))]
[assembly: TypeForwardedTo(typeof(JSUndefined))]
[assembly: TypeForwardedTo(typeof(JSPrimitiveObject))]
