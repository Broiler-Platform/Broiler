using System;
using System.Linq;
using Broiler.JavaScript.Modules;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Storage;

namespace Broiler.JavaScript.ModuleExtensions
{
    public static class JSModuleContextExtension
    {
        /// <summary>
        /// An analogue of the <see cref="JSModuleContext.RegisterModule"/> with fluent interface of creating module.
        /// </summary>
        /// <param name="context">The module context to register the module in.</param>
        /// <param name="moduleName">Unique module name.</param>
        /// <param name="builder">Action delegate with <see cref="ModuleBuilder"/> object that use for configuring.</param>
        public static void CreateModule(this JSModuleContext context, string moduleName, Action<ModuleBuilder> builder)
        {
            var mb = new ModuleBuilder(moduleName);
            builder(mb);
            mb.AddModuleToContext(context);
        }

        /// <summary>
        /// Return JSValue which is a module in js script (require function for C# code side).
        /// Uses linear search via the public <see cref="JSModuleContext.All"/> property.
        /// Suitable for typical module counts; for hot paths, consider adding a
        /// dictionary-backed lookup to <see cref="JSModuleContext"/> directly.
        /// </summary>
        /// <param name="context">The module context to import the module from.</param>
        /// <param name="name">Module name.</param>
        /// <returns>Module object.</returns>
        /// <exception cref="ArgumentException">If module not found.</exception>
        public static JSValue ImportModule(this JSModuleContext context, in KeyString name)
        {
            var n = name.ToString();
            var module = context.All.FirstOrDefault(m => m.filePath == n);
            if (module == null)
                throw new ArgumentException($"module {n} not found");
            return module.Exports;
        }
    }
}