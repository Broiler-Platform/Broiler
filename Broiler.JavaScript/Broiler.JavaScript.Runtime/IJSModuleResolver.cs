using System.Threading.Tasks;

namespace Broiler.JavaScript.Runtime;

/// <summary>
/// Defines the contract for resolving and loading JavaScript modules.
/// Implementations handle file system lookup, package.json resolution,
/// and extension matching for CommonJS and ES Module imports.
///
/// This interface lives in the Runtime assembly and is implemented by the
/// Modules assembly, enabling pluggable module resolution without a hard
/// reference from Runtime to Modules.
/// </summary>
public interface IJSModuleResolver
{
    /// <summary>
    /// Resolves a module specifier to a full file path.
    /// Returns <c>null</c> when the module cannot be found.
    /// </summary>
    /// <param name="currentPath">The directory of the importing module.</param>
    /// <param name="moduleName">The module specifier from an import or require statement.</param>
    /// <returns>The resolved full file path, or <c>null</c> if the module cannot be found.</returns>
    string Resolve(string currentPath, string moduleName);

    /// <summary>
    /// Loads the source code of a module from its resolved path.
    /// </summary>
    /// <param name="resolvedPath">The full path to the module file.</param>
    /// <returns>The module source code.</returns>
    Task<string> LoadSourceAsync(string resolvedPath);
}
