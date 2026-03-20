using System.Runtime.CompilerServices;

namespace Broiler.JavaScript.Clr.Tests;

internal static class ClrTestsBootstrap
{
    [ModuleInitializer]
    internal static void EnsureClrLoaded()
    {
        _ = typeof(DefaultClrInterop);
    }
}
