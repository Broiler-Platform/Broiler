using System.Runtime.CompilerServices;
using Broiler.Graphics;
using Broiler.Media;
using Broiler.Media.Image.Managed;

namespace Broiler.Cli.Tests;

/// <summary>
/// Test-host composition root: registers the concrete managed image codecs with
/// <see cref="BImageCodecs"/> so the Broiler.Graphics decode/encode path (used by the
/// HTML render pipeline exercised in these tests) has a catalog. Runs once when the
/// test assembly loads.
/// </summary>
internal static class ImageCodecTestBootstrap
{
    [ModuleInitializer]
    internal static void Register() =>
        BImageCodecs.Use(new MediaCodecCatalog(ManagedImageCodecs.CreateCodecs()));
}
