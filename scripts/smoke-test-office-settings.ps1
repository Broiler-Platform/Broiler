[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $LauncherDirectory,
    [Parameter(Mandatory = $true)][string] $OutputDirectory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$launcher = (Resolve-Path -LiteralPath $LauncherDirectory).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Settings smoke output must be a new directory.' }
New-Item -ItemType Directory -Path $output | Out-Null
$references = @(Get-ChildItem -LiteralPath $launcher -Filter 'Broiler*.dll' | ForEach-Object {
    '<Reference Include="' + [Security.SecurityElement]::Escape($_.BaseName) + '"><HintPath>' + [Security.SecurityElement]::Escape($_.FullName) + '</HintPath></Reference>'
}) -join "`n"
foreach ($dependency in @('System.Drawing.Common.dll', 'Microsoft.Win32.SystemEvents.dll')) {
    $dependencyPath = Join-Path $launcher $dependency
    if (Test-Path -LiteralPath $dependencyPath) {
        $references += '<Reference Include="' + [IO.Path]::GetFileNameWithoutExtension($dependency) + '"><HintPath>' + [Security.SecurityElement]::Escape($dependencyPath) + '</HintPath></Reference>'
    }
}
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
  <ItemGroup>$references</ItemGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $output 'Settings.csproj') -Encoding utf8NoBOM
'<configuration><packageSources><clear/></packageSources></configuration>' | Set-Content -LiteralPath (Join-Path $output 'NuGet.config')
@'
using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Rendering;
using Broiler.Launcher.Models;
using Broiler.Launcher.Services;
using Broiler.Launcher.UI;
using Broiler.UI;
using Broiler.UI.Standard;

string output = args[0];
foreach (double scale in new[] { 1.0, 2.0 })
{
    var host = new Host(new BSize(640, 480), scale);
    using var session = new StandardUiSessionBuilder().Build(host);
    using var view = new SettingsView(new FixtureService());
    session.AddRoot(view); StandardThemeController.Apply(session, LauncherView.Theme);
    for (int section = 0; section < 4; section++)
    {
        view.ShowSection(section);
        var list = session.RenderFrame();
        using var renderer = new BImageRenderer();
        using var image = renderer.RenderToImage(list, new BSurfaceDescriptor(host.ViewportSize, scale), new BFrameContext(BColor.White, 0, new BRenderOptions()));
        string path = Path.Combine(output, $"settings-{section}-{scale:0}x.png");
        WritePng(path, image); Console.WriteLine(path);
        var scroll = (Broiler.UI.ScrollView.Standard.StandardScrollView)typeof(SettingsView).GetField("_scroll", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(view)!;
        scroll.SetOffset(new BPoint(0, scroll.ExtentSize.Height));
        var bottomList = session.RenderFrame();
        using var bottom = renderer.RenderToImage(bottomList, new BSurfaceDescriptor(host.ViewportSize, scale), new BFrameContext(BColor.White, 0, new BRenderOptions()));
        WritePng(Path.Combine(output, $"settings-{section}-{scale:0}x-bottom.png"), bottom);
    }
}
Console.WriteLine("Packaged settings render smoke passed at 640x480 DIP, 100% and 200% DPI.");
// The launcher needs no image encoder at runtime. Encode the render fixture with
// framework compression so this smoke checks its existing dependency closure.
static void WritePng(string path, Broiler.Graphics.Imaging.BBitmap image)
{
    using var file = File.Create(path);
    file.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
    void Chunk(string type, byte[] data)
    {
        byte[] length = new byte[4]; System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length, data.Length); file.Write(length);
        byte[] name = System.Text.Encoding.ASCII.GetBytes(type); file.Write(name); file.Write(data);
        uint crc = 0xffffffff;
        foreach (byte b in name.Concat(data)) { crc ^= b; for (int i = 0; i < 8; i++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0); }
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(length, crc ^ 0xffffffff); file.Write(length);
    }
    byte[] header = new byte[13]; System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header, image.Width);
    System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), image.Height); header[8] = 8; header[9] = 6; Chunk("IHDR", header);
    using var compressed = new MemoryStream();
    using (var zip = new System.IO.Compression.ZLibStream(compressed, System.IO.Compression.CompressionLevel.Fastest, true))
        for (int row = 0; row < image.Height; row++) { zip.WriteByte(0); zip.Write(image.Rgba.Slice(row * image.Width * 4, image.Width * 4)); }
    Chunk("IDAT", compressed.ToArray()); Chunk("IEND", []);
}
sealed class Host(BSize size, double scale) : IUiHost
{
    public BSize ViewportSize => size;
    public double Scale => scale;
    public BRenderList CreateRenderList(int capacity = 0) => new(capacity);
    public void Invalidate(UiInvalidation invalidation) { }
    public void Present(BRenderList list) { }
}
sealed class FixtureService : IAppLauncherService
{
    public IReadOnlyList<BroilerAppInfo> Apps { get; } = BroilerAppInfo.CreateDefaultSuite();
    public LauncherSettings Settings { get; private set; } = new();
    public void UpdateSettings(LauncherSettings settings) => Settings = settings.Clone();
    public AppResolution AutoDetect(BroilerAppInfo app, string? baseDirectory = null) => AppResolution.NotFound("Render fixture: no installed product");
    public AppResolution ResolveApp(BroilerAppInfo app, string? baseDirectory = null) => AutoDetect(app);
    public LaunchResult LaunchApp(BroilerAppInfo app, string? arguments = null, string? baseDirectory = null) => throw new InvalidOperationException("Render smoke never launches products.");
}
'@ | Set-Content -LiteralPath (Join-Path $output 'Program.cs') -Encoding utf8NoBOM
& dotnet run --project (Join-Path $output 'Settings.csproj') -- $output
if ($LASTEXITCODE -ne 0) { throw 'Packaged settings render smoke failed.' }
[ordered]@{ passed = $true; viewportDip = '640x480'; dpiScales = @(1, 2); sections = @('Products', 'Installation', 'BOSS', 'Updates'); checkedScrollEnds = $true; manualNativeInteractionRequired = $true } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'settings-render-results.json') -Encoding utf8NoBOM
