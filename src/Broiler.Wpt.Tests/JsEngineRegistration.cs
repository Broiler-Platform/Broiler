using System.Runtime.CompilerServices;
using Broiler.HtmlBridge;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Registers the JavaScript engine providers this test assembly links with the bridge's engine registry
/// when the assembly loads.
/// </summary>
/// <remarks>
/// A bridge adopts its realm from whichever registered provider recognises the script context the
/// host built, and with none registered every <c>Attach</c> fails ("No registered JavaScript engine
/// provider recognised the script context the host supplied"). The provider's own module initializer
/// runs only once something names one of its types, which nothing here does, so the registration is
/// made explicit through <see cref="JsEngineHosting.EnsureRegistered"/> — idempotent, and the entry
/// point the bridge documents for hosts.
/// </remarks>
internal static class JsEngineRegistration
{
    [ModuleInitializer]
    internal static void Register() => JsEngineHosting.EnsureRegistered();
}
