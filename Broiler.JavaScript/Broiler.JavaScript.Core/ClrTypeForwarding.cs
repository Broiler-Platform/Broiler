using System.Runtime.CompilerServices;
using Broiler.JavaScript.Runtime;

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

// JSException, JSVariable, KeyStringCoreExtensions moved to Runtime
[assembly: TypeForwardedTo(typeof(JSException))]
[assembly: TypeForwardedTo(typeof(JSVariable))]
[assembly: TypeForwardedTo(typeof(KeyStringCoreExtensions))]

// ClrEnumerableElementEnumerator, JSValueToClrConverter moved to Runtime
[assembly: TypeForwardedTo(typeof(ClrEnumerableElementEnumerator))]
[assembly: TypeForwardedTo(typeof(JSValueToClrConverter))]
[assembly: TypeForwardedTo(typeof(KeyEnumerator))]

// Enumerator types and JavaScriptObject moved to Runtime
[assembly: TypeForwardedTo(typeof(ListElementEnumerator))]
[assembly: TypeForwardedTo(typeof(ClrObjectEnumerator<>))]
[assembly: TypeForwardedTo(typeof(ClrObjectEnumerable<>))]
[assembly: TypeForwardedTo(typeof(EnumerableElementEnumerable))]
[assembly: TypeForwardedTo(typeof(JavaScriptObject))]

// Utils types moved to Runtime
[assembly: TypeForwardedTo(typeof(BigIntegerExtensions))]
[assembly: TypeForwardedTo(typeof(DateParser))]
[assembly: TypeForwardedTo(typeof(NumberParser))]
[assembly: TypeForwardedTo(typeof(TypeConverter))]
[assembly: TypeForwardedTo(typeof(AsyncPump))]
