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
