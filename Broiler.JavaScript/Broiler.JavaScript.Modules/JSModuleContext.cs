using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Broiler.JavaScript.ExpressionCompiler.Core;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.Engine.Core;

namespace Broiler.JavaScript.Modules;

public delegate Task JSModuleDelegate(JSModule module);

/// <summary>
/// Enables Modules, both CommonJS and ES Modules
/// </summary>
public class JSModuleContext : JSContext
{
    internal readonly JSObject ModulePrototype;
    internal readonly JSFunction Module;

    public JSModuleContext(SynchronizationContext ctx = null, bool enableClrIntegration = true) : base(ctx ?? new SynchronizationContext())
    {
        // this.CreateSharedObject(KeyStrings.assert, typeof(JSAssert), true);
        this[KeyStrings.assert] = JSAssert.CreateClass(this, false);

        Module = JSModule.CreateClass(this, false); // this.Create<JSModule>(KeyStrings.Module, null, false);
        ModulePrototype = Module.prototype;

        if (enableClrIntegration && JSEngine.ClrModuleProvider != null)
            moduleCache[ModuleCache.clr] = new JSModule(this, JSEngine.ClrModuleProvider(), "clr");

        moduleCache[ModuleCache.module] = new JSModule(this, Module, "module");

        this[KeyStrings.globalThis] = this;
        this[KeyStrings.global] = this;
    }


    /// <summary>
    /// Pass Exports as Module with unique name
    /// After register module can get in script
    /// <example>
    ///  //in js script
    /// import module from "module_name_that_used_in_name_arg";
    /// import {prop in export object} from "module_name";
    /// const module = require("module_name_that_used_in_name_arg");
    /// const {some_prop} = require("module_name_that_used_in_name_arg");
    /// </example>
    /// </summary>
    /// <param name="name">Unique module name</param>
    /// <param name="exports">JSObject, that you import by import or require</param>
    public void RegisterModule(in KeyString name, JSObject exports)
    {
        var n = name.ToString();
        moduleCache.GetOrCreate(name.Value, () => new JSModule(this, exports, n));
    }

    /// <summary>
    /// Modules are isolated by Context and are identified by Id.
    /// 
    /// Specially in server environment with multiple context, module names
    /// are identified by unique id present in ModuleName.
    /// </summary>
    readonly ModuleCache moduleCache = ModuleCache.Create();

    [Browsable(false)]
    public IEnumerable<JSModule> All => moduleCache.All;

    private string[] paths;

    protected string[] extensions = [".js"];

    internal string Resolve(string dirPath, string relativePath)
    {
        bool Exists(string folder, string file, out string path)
        {
            string fullName = Path.Combine(folder, file);
            if (!file.StartsWith("."))
            {
                if (Directory.Exists(fullName))
                {
                    var pkgJson = fullName + "/package.json";

                    if (File.Exists(pkgJson))
                    {
                        var json = File.ReadAllText(pkgJson);
                        var pkg = JsonNode.Parse(json) as JsonObject;

                        if (pkg.TryGetPropertyValue("main", out var token))
                        {
                            var v = token.GetValue<string>();
                            path = Path.Combine(fullName, v);

                            if (File.Exists(path))
                                return true;

                            foreach (var ext in extensions)
                            {
                                var np = path + ext;
                                if (File.Exists(np))
                                {
                                    path = np;
                                    return true;
                                }
                            }

                            throw new FileNotFoundException(path);
                        }
                    }
                }
            }

            if (File.Exists(fullName))
            {
                path = fullName;
                return true;
            }

            path = null;
            return false;
        }

        foreach (var ext in extensions)
        {
            if (relativePath.StartsWith("."))
            {
                if (Exists(dirPath, relativePath, out var path))
                    return path;

                if (Exists(dirPath, relativePath + ext, out path))
                    return path;

                continue;
            }

            foreach (var folder in paths)
            {
                if (Exists(folder, relativePath, out var path))
                    return path;

                if (Exists(folder, relativePath + ext, out path))
                    return path;

                if (Exists(folder, relativePath + "/index" + ext, out path))
                    return path;

                // check if package.json exists...
            }
        }

        return null;
    }

    void UpdatePaths(string[] paths = null)
    {
        if (paths != null)
        {
            var np = new string[paths.Length + 2];
            np[0] = CurrentPath;
            np[1] = CurrentPath + "/node_modules";

            System.Array.Copy(paths, 0, np, 2, paths.Length);
            paths = np;
        }

        this.paths = paths ??
        [
            CurrentPath,
            CurrentPath + "/node_modules",
            // system npm paths...
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/yantra/node_modules"
        ];
    }

    private static IDisposable CreateSynchronizationContext()
    {
        if (SynchronizationContext.Current == null)
        {
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            return new DisposableAction(() => { SynchronizationContext.SetSynchronizationContext(null); });
        }

        return DisposableAction.Empty;
    }

    public async Task<JSValue> RunAsync(string folder, string relativeFile, string[] paths = null)
    {
        using var sc = CreateSynchronizationContext();

        CurrentPath = folder;
        UpdatePaths(paths);

        var filePath = Resolve(folder, relativeFile.StartsWith(".") ? relativeFile : ("./" + relativeFile)) ?? throw new FileNotFoundException($"{relativeFile} not found");
        var r = await LoadModuleAsync(null, filePath);
        var w = WaitTask;
        if (w != null)
            await w;

        return r;
    }

    /// <summary>
    /// Run JavaScript module from string
    /// </summary>
    /// <param name="script">string of code</param>
    /// <param name="moduleFolder">base folder for searching modules in import function</param>
    /// <param name="paths"></param>
    /// <param name="uniqueModuleID">Module ID if you want get this module later (in <see cref="ImportModule"/> or import in js)</param>
    /// <returns>Module as JSObject</returns>
    /// <exception cref="JSException"></exception>
    public async Task<JSValue> RunScriptAsync(string script, string moduleFolder, string[] paths = null, string uniqueModuleID = null)
    {
        using var sc = CreateSynchronizationContext();

        CurrentPath = moduleFolder;
        UpdatePaths(paths);
        uniqueModuleID ??= Guid.NewGuid().ToString("N") + ".js";

        var newModule = new JSModule(this, uniqueModuleID, script);
        var dirPath = moduleFolder;

        newModule.Import = new JSFunction((in Arguments a) =>
        {
            var name = a[0];
            if (!name.IsString)
                throw NewTypeError("import method's parameter must be a string");

            var result = LoadModuleAsync(dirPath, name.StringValue);
            return JSEngine.ClrInterop.Marshal(result);
        });

        newModule.Require = new JSFunction((in Arguments a) =>
        {
            var name = a[0];
            if (!name.IsString)
                throw NewTypeError("require method's parameter must be a string");

            var result = LoadModuleAsync(dirPath, name.StringValue);
            return AsyncPump.Run(() => result);
        });

        newModule.Compile = new JSFunction((in Arguments a) =>
        {
            var task = CompileModuleAsync(newModule);
            return JSEngine.ClrInterop.Marshal(task);
        });

        await newModule.InitAsync();
        return newModule.Exports;
    }

    public async static Task<JSValue> RunExportsAsync(string folder, string relativeFile, string exportedFunctionName, Arguments a, string[] paths = null)
    {
        using var m = new JSModuleContext();
        m.CurrentPath = folder;
        m.UpdatePaths(paths);

        var filePath = m.Resolve(folder, relativeFile.StartsWith(".") ? relativeFile : ("./" + relativeFile));
        if (filePath == null)
            throw new FileNotFoundException($"{filePath} not found");
        
        var main = await m.LoadModuleAsync(m.CurrentPath, filePath);
        var exported = main[exportedFunctionName];
        if (exported.IsUndefined)
            throw new KeyNotFoundException($"{exportedFunctionName} not found on the module");
        
        var rv = exported.InvokeFunction(a);
        if (rv is IJSPromise promise)
            return await promise.Task;

        if (m.WaitTask != null)
            await m.WaitTask;

        return rv;
    }

    public string CurrentPath { get; set; }

    public JSModule Main { get; set; }

    protected virtual async Task<JSValue> LoadModuleAsync(string currentPath, string name)
    {
        var relativePath = name;

        // fetch system modules 
        if (moduleCache.TryGetValue(relativePath, out var m))
            return m.Exports;

        // resolve full name..
        var fullPath = Resolve(currentPath, relativePath) ?? throw NewTypeError($"{relativePath} module not found");
        m = moduleCache.GetOrCreate(fullPath, () =>
        {
            var newModule = new JSModule(this, fullPath);
            var dirPath = Path.GetDirectoryName(fullPath);

            newModule.Import = new JSFunction((in Arguments a) =>
            {
                var name = a[0];
                if (!name.IsString)
                    throw NewTypeError("import method's parameter must be a string");

                var result = LoadModuleAsync(dirPath, name.StringValue);
                return JSEngine.ClrInterop.Marshal(result);
            });

            newModule.Require = new JSFunction((in Arguments a) =>
            {
                var name = a[0];
                if (!name.IsString)
                    throw NewTypeError("require method's parameter must be a string");

                var result = LoadModuleAsync(dirPath, name.StringValue);
                return AsyncPump.Run(() => result);
            });

            newModule.Compile = new JSFunction((in Arguments a) =>
            {
                var task = CompileModuleAsync(newModule);
                return JSEngine.ClrInterop.Marshal(task);
            });

            return newModule;
        });

        await m.InitAsync();
        return m.Exports;
    }

    internal protected virtual async Task CompileModuleAsync(JSModule module)
    {
        // Console.WriteLine($"{DateTime.Now} - Compiling module {module.filePath}");
        var filePath = module.filePath;

        // if this is a json file... then pad with module.exports = 
        if (module.Code == null)
        {
            using var reader = new StreamReader(filePath, Encoding.UTF8);
            module.Code = await reader.ReadToEndAsync();
        }

        var code = module.Code;

        if (filePath.EndsWith(".json"))
            code = $"module.exports = {code};";

        // var factory = FastEval(code, filePath);
        var factory = CoreScript.Compile(code, module.filePath,
        [
            "exports",
            "require",
            "module",
            "import",
            "__fileame",
            "__dirname"
        ], codeCache: CodeCache);

        if (factory(new Arguments(module,
        [
            module.Exports,
            module.Require,
            module,
            module.Import,
            module.Id,
            JSValue.CreateString(module.dirPath)
        ])) is IJSPromise result)
        {
            await result.Task;
        }
    }
}