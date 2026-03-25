using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.Core.Core;

/// <summary>
/// Provides static access to the current JavaScript execution context and
/// shared infrastructure (error factories, CLR interop, built-in registry).
/// When <c>JSContext</c> lived inside Core every type could reach these
/// members directly.  Now that <c>JSContext</c> has moved to the Engine
/// assembly, this static class keeps the same functionality available to
/// Core without introducing a circular reference.
/// </summary>
public static class JSEngine
{
    // ── Current context ─────────────────────────────────────────────

    [ThreadStatic]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IJSExecutionContext Current;

    private static readonly AsyncLocal<IJSExecutionContext> _current =
        new((e) => { Current = e.CurrentValue ?? e.PreviousValue; });

    public static IJSExecutionContext CurrentContext
    {
        get => Current;
        set
        {
            _current.Value = value;
            Current = value;
        }
    }

    /// <summary>
    /// Clears the async-local context reference. Called by
    /// <c>JSContext.Dispose()</c> in the Engine assembly.
    /// </summary>
    internal static void ClearAsyncLocal()
    {
        _current.Value = null;
    }

    // ── Built-in registry ───────────────────────────────────────────

    /// <summary>
    /// Gets or sets the built-in object registry used to populate new contexts.
    /// Set by the BuiltIns assembly's module initializer.
    /// </summary>
    public static IBuiltInRegistry BuiltInRegistry { get; set; }

    /// <summary>
    /// Delegate for registering Core's source-generated built-in classes.
    /// Wired by Core's module initializer.
    /// </summary>
    internal static Action<JSObject> CoreClassRegistrations { get; set; }

    // ── CLR interop ─────────────────────────────────────────────────

    /// <summary>
    /// Gets or sets the CLR interop provider used to marshal between .NET
    /// objects and JavaScript values.
    /// </summary>
    public static IClrInterop ClrInterop { get; set; } = FallbackClrInterop.Instance;

    /// <summary>
    /// Factory delegate that provides the default CLR module object.
    /// Set by the Clr assembly during initialization.
    /// </summary>
    public static Func<JSObject> ClrModuleProvider { get; set; }

    // ── Error factory delegates (wired by BuiltInsAssemblyInitializer) ──

    internal static Func<string, string, string, int, JSException> CreateTypeError;
    internal static Func<string, string, string, int, JSException> CreateSyntaxError;
    internal static Func<string, string, string, int, JSException> CreateURIError;
    internal static Func<string, string, string, int, JSException> CreateRangeError;
    internal static Func<string, string, string, int, JSException> CreateError;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSException NewTypeError(string message,
        [CallerMemberName] string function = null,
        [CallerFilePath] string filePath = null,
        [CallerLineNumber] int line = 0) =>
        CreateTypeError(message, function, filePath, line);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSException NewSyntaxError(string message,
        [CallerMemberName] string function = null,
        [CallerFilePath] string filePath = null,
        [CallerLineNumber] int line = 0) =>
        CreateSyntaxError(message, function, filePath, line);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSException NewURIError(string message,
        [CallerMemberName] string function = null,
        [CallerFilePath] string filePath = null,
        [CallerLineNumber] int line = 0) =>
        CreateURIError(message, function, filePath, line);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSException NewRangeError(string message,
        [CallerMemberName] string function = null,
        [CallerFilePath] string filePath = null,
        [CallerLineNumber] int line = 0) =>
        CreateRangeError(message, function, filePath, line);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSException NewError(string message,
        [CallerMemberName] string function = null,
        [CallerFilePath] string filePath = null,
        [CallerLineNumber] int line = 0) =>
        CreateError(message, function, filePath, line);

    // ── Promise factory delegates ───────────────────────────────────

    internal static Func<JSValue, bool, JSValue> CreateResolvedOrRejectedPromise;
    internal static Func<JSPromiseDelegate, IJSPromise> CreatePromiseFromDelegate;

    // ── Function class factory ──────────────────────────────────────

    /// <summary>
    /// Factory delegate for creating the Function class.
    /// Wired by the BuiltIns assembly via <c>[ModuleInitializer]</c>.
    /// </summary>
    internal static Func<JSObject, bool, JSValue> CreateFunctionClass;

    /// <summary>
    /// Factory delegate for creating the Object class.
    /// Wired by Core's module initializer from the source-generated code.
    /// </summary>
    internal static Func<JSObject, bool, JSValue> CreateObjectClass;

    // ── new.target helpers ──────────────────────────────────────────

    public static JSValue NewTarget => Current?.Top?.NewTarget;

    public static JSObject NewTargetPrototype =>
        (Current?.Top?.NewTarget as IJSFunction)?.Prototype as JSObject;

    // ── Assembly loading ────────────────────────────────────────────

    /// <summary>
    /// Attempts to load satellite assemblies and run their module
    /// constructors so that <c>[ModuleInitializer]</c> methods register
    /// factory delegates and additional built-in type registrations.
    /// </summary>
    internal static void EnsureBuiltInsAssemblyLoaded()
    {
        if (BuiltInRegistry != null)
            return;

        TryLoadAssembly("Broiler.JavaScript.BuiltIns");
        TryLoadAssembly("Broiler.JavaScript.Globals");
        TryLoadAssembly("Broiler.JavaScript.Extensions");
    }

    private static void TryLoadAssembly(string name)
    {
        try
        {
            var assembly = Assembly.Load(name);
            RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);
        }
        catch (Exception ex) when (
            ex is System.IO.FileNotFoundException
            or System.IO.FileLoadException
            or BadImageFormatException)
        {
            // Assembly is not available – silently skip.
        }
    }
}
