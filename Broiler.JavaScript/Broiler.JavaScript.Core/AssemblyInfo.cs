using System.Runtime.CompilerServices;
using Broiler.JavaScript.Core.Core.Module;

#if !WEB_ATOMS
[assembly: InternalsVisibleTo("Broiler.JavaScript.Core.Tests")]

// used by Dynamic Assembly to access internals
[assembly: InternalsVisibleTo("Broiler.JavaScript.Runtime")]
[assembly: InternalsVisibleTo("WebAtoms.XF")]
#endif

// Type forwards for contracts moved to Broiler.JavaScript.Runtime.
[assembly: TypeForwardedTo(typeof(IJSModuleResolver))]
[assembly: TypeForwardedTo(typeof(ExportAttribute))]
[assembly: TypeForwardedTo(typeof(DefaultExportAttribute))]
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.Core.CancellableDisposableAction))]

// Type forwards for KeyString types moved to Broiler.JavaScript.Storage.
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.Core.KeyType))]
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.Core.KeyString))]
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.Core.KeyStrings))]

// Type forwards for ObjectStatus moved to Broiler.JavaScript.Runtime.
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.Core.Object.ObjectStatus))]

// Type forwards for JSProperty, PropertySequence, ElementArray moved to Broiler.JavaScript.Storage.
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.Core.Storage.JSProperty))]
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.Core.Storage.JSObjectProperty))]
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.Core.Storage.PropertySequence))]
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.Core.Storage.ElementArray))]
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.Core.Storage.Updater<,>))]

// Type forward for StringExtensions moved to Broiler.JavaScript.Runtime.
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.Extensions.StringExtensions))]

// Type forwards for Phase 9b types moved to Broiler.JavaScript.Runtime.
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.Core.JSValue))]
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.Core.Arguments))]
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.Core.PropertyKey))]
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.Core.JSFunctionDelegate))]
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.Core.IElementEnumerator))]
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.Core.IJSPrototype))]
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.Core.IJSSymbol))]

// Type forwards for Phase 9c contract interfaces moved to Broiler.JavaScript.Runtime.
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.Debugger.IDebugger))]
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.Core.Clr.IClrInterop))]
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.Emit.ICodeCache))]
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.Emit.JSCode))]
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.Emit.JSCodeCompiler))]
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.FastParser.Compiler.IJSCompiler))]
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.Core.IBuiltInRegistry))]
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.Core.IJSContext))]
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.Core.IJSFunction))]

// Type forward for Phase 9d: CoreScript moved to Broiler.JavaScript.Runtime.
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.CoreScript))]
