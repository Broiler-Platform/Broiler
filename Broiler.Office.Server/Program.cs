using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Hosting.WindowsServices;

// ── BOSS: the Broiler Office Standalone Server ────────────────────────────────────────────────────
// A Kestrel host and local WebAssembly launcher for the Broiler Office web suite:
//   - /        Broiler Office Launcher (Broiler.Launcher.WebAssembly)
//   - /writer/ Broiler Writer          (Broiler.Writer.WebAssembly)
//   - /cell/   Broiler Cell            (Broiler.Cells.WebAssembly)
//   - /plate/  Broiler Plate           (Broiler.Plate.WebAssembly)
//
// Each client is served from its vendored, content-hashed WebAssembly bundle (see the .csproj for
// how the bundles get here). Same-origin catalog metadata (/office-web.json) and shell link
// adaptation connect the Launcher and every Office app locally without modifying any SRI-hashed
// JavaScript or WebAssembly asset.

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = ResolveContentRoot(args),
});
using var launcherIntegration = LauncherIntegration.FromEnvironment();
if (launcherIntegration is not null)
{
    builder.Logging.ClearProviders(); // No inherited console/pipe can tie this server to the launcher's lifetime.
    builder.Logging.AddProvider(launcherIntegration.Logs);
}

// Kestrel is the default (and only) server here; make the intent explicit.
builder.WebHost.UseKestrel();

// Run as a managed service when launched by one — see packaging/ for the prepared unit files.
// Both calls are no-ops for an ordinary console launch (`dotnet run`, a shell, a container), so the
// interactive experience is unchanged. Under systemd, UseSystemd switches the host to
// Type=notify readiness/stop signalling and journald-style log formatting; under the Windows SCM,
// UseWindowsService answers the service control protocol, logs to the event log, and roots the
// content root at the executable's directory (so the vendored wwwroot is found regardless of the
// working directory the SCM hands us).
builder.Host.UseSystemd();
builder.Host.UseWindowsService();

var app = builder.Build();
launcherIntegration?.Start(app.Lifetime);

// The apps this server hosts.
var hostedApps = new[]
{
    new OfficeApp(
        Id: "launcher",
        Name: "Office",
        Description: "Broiler Office — WebAssembly launcher",
        Path: "/",
        Formats: Array.Empty<string>()),
    new OfficeApp(
        Id: "writer",
        Name: "Writer",
        Description: "Broiler Writer — word processor",
        Path: "/writer/",
        Formats: new[] { "rtf", "docx", "odt", "html", "md", "pdf" }),
    new OfficeApp(
        Id: "cell",
        Name: "Cell",
        Description: "Broiler Cell — spreadsheet",
        Path: "/cell/",
        Formats: new[] { "csv", "xlsx", "ods" }),
    new OfficeApp(
        Id: "plate",
        Name: "Plate",
        Description: "Broiler Plate — document and graphics viewer",
        Path: "/plate/",
        Formats: new[] { "pdf", "png", "jpg", "bmp", "svg" }),
};

var mountedSubApps = new[]
{
    new SubAppMount("writer", "/writer", "/writer/", "writer/index.html"),
    new SubAppMount("cell", "/cell", "/cell/", "cell/index.html"),
    new SubAppMount("plate", "/plate", "/plate/", "plate/index.html"),
};

var serverVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
var catalogVersion = SanitizeCatalogVersion(serverVersion);

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/error");

// Intercept canonical sub-app redirects, dynamic same-origin launcher metadata, and HTML shells
// before UseStaticFiles so local navigation works seamlessly while every SRI-hashed JS/WASM asset
// stays untouched on disk.
app.Use(async (context, next) =>
{
    if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
    {
        string path = context.Request.Path.Value ?? "/";

        // Canonicalize /writer -> /writer/, /cell -> /cell/, /plate -> /plate/ so relative asset
        // URLs inside each app's index.html resolve inside its own mount directory.
        foreach (var mount in mountedSubApps)
        {
            if (path.Equals(mount.BarePath, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Redirect(mount.MountedPath + context.Request.QueryString.Value, permanent: false);
                return;
            }
        }

        if (path.Equals("/office-web.json", StringComparison.OrdinalIgnoreCase))
        {
            await WriteOfficeCatalogAsync(context, app.Environment.WebRootPath, mountedSubApps, catalogVersion);
            return;
        }

        if (path.Equals("/", StringComparison.Ordinal) || path.Equals("/index.html", StringComparison.OrdinalIgnoreCase))
        {
            await WriteAdaptedShellAsync(context, app.Environment.WebRootPath, "index.html", isLauncher: true);
            return;
        }

        foreach (var mount in mountedSubApps)
        {
            if (path.Equals(mount.MountedPath, StringComparison.OrdinalIgnoreCase) ||
                path.Equals(mount.MountedPath + "index.html", StringComparison.OrdinalIgnoreCase))
            {
                await WriteAdaptedShellAsync(context, app.Environment.WebRootPath, mount.ShellRelativePath, isLauncher: false);
                return;
            }
        }
    }

    await next();
});

// Content types for the mono-wasm runtime under _framework/. The defaults already cover .wasm
// (application/wasm, required for streaming compilation), .js, and .json; add the runtime's data
// blobs and let anything else through as a binary download so no framework asset 404s.
var contentTypes = new FileExtensionContentTypeProvider();
contentTypes.Mappings[".dat"] = "application/octet-stream";   // ICU / timezone data
contentTypes.Mappings[".blat"] = "application/octet-stream";  // bundled resources
contentTypes.Mappings[".pdb"] = "application/octet-stream";   // debug symbols (debug builds)
contentTypes.Mappings[".dll"] = "application/octet-stream";

var staticFiles = new StaticFileOptions
{
    ContentTypeProvider = contentTypes,
    ServeUnknownFileTypes = true,
    DefaultContentType = "application/octet-stream",

    // Cache versioning for the vendored WebAssembly bundles. The SDK content-fingerprints every
    // runtime asset (dotnet.<hash>.js, main.<hash>.js, *.<hash>.wasm): the URL itself carries the
    // version, so the bytes behind a given URL never change and the asset can be cached immutably.
    // Stable-named entry points (index.html, bootstrap.js, office-web.json, CSS, and the stable
    // replay/host modules) are marked no-cache and revalidated on every load so a browser never
    // pairs a cached shell/replay module with a newer fingerprinted runtime.
    OnPrepareResponse = context =>
    {
        string name = context.File.Name;
        bool stableEntryPoint =
            name.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("bootstrap.js", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("broiler.graphics.webassembly.js", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("broiler.hosting.webassembly.js", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("office.web-", StringComparison.OrdinalIgnoreCase);
        context.Context.Response.Headers.CacheControl = stableEntryPoint
            ? "no-cache"
            : "public, max-age=31536000, immutable";
    },
};

// Serve the vendored Office bundles: each transformed index.html references content-hashed physical
// files (dotnet.<hash>.js, the replay/host modules, main.<hash>.js) that exist on disk, so plain
// static file serving is all that's needed — no import-map rewriting.
app.UseStaticFiles(staticFiles);

app.UseRouting();

// ── Server API ────────────────────────────────────────────────────────────────────────────────────
app.MapGet("/healthz", () => Results.Text("OK", "text/plain"));

app.MapGet("/api/info", () => Results.Json(
    new ServerInfoResponse(
        Server: "BOSS",
        Product: "Broiler Office Standalone Server",
        Version: serverVersion,
        LauncherInstanceId: launcherIntegration?.InstanceId,
        Apps: hostedApps),
    BossJsonContext.Default.ServerInfoResponse));

app.MapGet("/error", () => Results.Problem("The hosted application failed to load."));

// Client-side SPA fallbacks per mounted Office app, plus root fallback to the Office Launcher.
foreach (var mount in mountedSubApps)
{
    var shellPath = mount.ShellRelativePath;
    app.MapFallback($"{mount.BarePath}/{{*path:nonfile}}", async context =>
    {
        await WriteAdaptedShellAsync(context, app.Environment.WebRootPath, shellPath, isLauncher: false);
    });
}

app.MapFallback("{*path:nonfile}", async context =>
{
    await WriteAdaptedShellAsync(context, app.Environment.WebRootPath, "index.html", isLauncher: true);
});

// Startup banner — makes it obvious the standalone server is up and where the Office apps live.
var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("BOSS");
app.Lifetime.ApplicationStarted.Register(() =>
{
    var addresses = app.Services
        .GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>()
        .Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()?
        .Addresses;

    logger.LogInformation("BOSS — Broiler Office Standalone Server v{Version} is up.", serverVersion);
    foreach (var address in addresses ?? Array.Empty<string>())
    {
        var baseAddress = address.TrimEnd('/');
        logger.LogInformation("  Office:  {Address}/", baseAddress);
        logger.LogInformation("  Writer:  {Address}/writer/", baseAddress);
        logger.LogInformation("  Cell:    {Address}/cell/", baseAddress);
        logger.LogInformation("  Plate:   {Address}/plate/", baseAddress);
    }
});

app.Run();

static async Task WriteOfficeCatalogAsync(
    HttpContext context,
    string? webRootPath,
    SubAppMount[] mounts,
    string version)
{
    var products = new List<OfficeCatalogProduct>(mounts.Length);
    foreach (var mount in mounts)
    {
        if (string.IsNullOrEmpty(webRootPath) ||
            File.Exists(Path.Combine(webRootPath, mount.ShellRelativePath)))
        {
            products.Add(new OfficeCatalogProduct(
                Id: mount.Id,
                Url: mount.MountedPath,
                Version: version));
        }
    }

    var payload = JsonSerializer.Serialize(
        new OfficeCatalogResponse(
            SchemaVersion: 1,
            Status: "approved",
            Channel: "preview",
            CatalogVersion: version,
            Products: products.ToArray()),
        BossJsonContext.Default.OfficeCatalogResponse);

    context.Response.StatusCode = StatusCodes.Status200OK;
    context.Response.ContentType = "application/json; charset=utf-8";
    context.Response.Headers.CacheControl = "no-cache";
    if (!HttpMethods.IsHead(context.Request.Method))
        await context.Response.WriteAsync(payload, Encoding.UTF8);
}

static async Task WriteAdaptedShellAsync(
    HttpContext context,
    string? webRootPath,
    string relativePath,
    bool isLauncher)
{
    if (string.IsNullOrEmpty(webRootPath))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    string fullPath = Path.Combine(webRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
    if (!File.Exists(fullPath))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    string html = await File.ReadAllTextAsync(fullPath, Encoding.UTF8);
    if (isLauncher)
    {
        html = html
            .Replace("https://writer.broiler.app/", "/writer/", StringComparison.Ordinal)
            .Replace("https://cell.broiler.app/", "/cell/", StringComparison.Ordinal)
            .Replace("https://plate.broiler.app/", "/plate/", StringComparison.Ordinal);
    }
    else
    {
        html = html.Replace("https://demo.broiler.app/", "/", StringComparison.Ordinal);
    }

    context.Response.StatusCode = StatusCodes.Status200OK;
    context.Response.ContentType = "text/html; charset=utf-8";
    context.Response.Headers.CacheControl = "no-cache";
    if (!HttpMethods.IsHead(context.Request.Method))
        await context.Response.WriteAsync(html, Encoding.UTF8);
}

static string SanitizeCatalogVersion(string version)
{
    if (string.IsNullOrWhiteSpace(version))
        return "0.0.0";

    var chars = new char[Math.Min(version.Length, 80)];
    int length = 0;
    foreach (char c in version)
    {
        if (length == chars.Length)
            break;
        if (char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '+')
            chars[length++] = c;
    }

    return length > 0 ? new string(chars, 0, length) : "0.0.0";
}

// The content root is where the vendored Office bundles (wwwroot/) are looked up. The host default
// is the *working directory*, which is right for `dotnet run` (the build vendors the bundles into the
// project directory) but wrong for a deployed server: systemd, the Windows SCM, an init script or a
// plain `/opt/broiler/office-server/Broiler.Office.Server` invocation from elsewhere all leave the
// working directory pointing somewhere without a wwwroot, and every page 404s while /healthz keeps
// answering OK. Anchor it to the executable's own directory in that case.
//
// Returning null keeps the host default, which is what an explicit --contentRoot / ASPNETCORE_CONTENTROOT
// needs: WebApplicationOptions.ContentRootPath is applied after the command line and would silently win.
static string? ResolveContentRoot(string[] args)
{
    // Under the Windows SCM, UseWindowsService() sets the content root to the executable's directory
    // itself — and WebApplicationBuilder rejects a *late* host-configuration change to a different
    // value ("The content root changed from … to …"). So it has to already be that value here.
    if (WindowsServiceHelpers.IsWindowsService())
        return AppContext.BaseDirectory;

    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_CONTENTROOT")) ||
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_CONTENTROOT")))
        return null;

    foreach (string arg in args)
    {
        // Matches every spelling the command-line configuration provider accepts:
        // --contentRoot <path>, --contentRoot=<path>, /contentRoot=<path>.
        string token = arg.TrimStart('-', '/');
        int separator = token.IndexOf('=');
        if (separator >= 0)
            token = token[..separator];

        if (token.Equals("contentRoot", StringComparison.OrdinalIgnoreCase))
            return null;
    }

    if (Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")))
        return null;

    return Directory.Exists(Path.Combine(AppContext.BaseDirectory, "wwwroot"))
        ? AppContext.BaseDirectory
        : null;
}

/// <summary>An Office web app mounted by BOSS, as reported by <c>/api/info</c>.</summary>
internal sealed record OfficeApp(string Id, string Name, string Description, string Path, string[] Formats);

internal sealed record ServerInfoResponse(
    string Server,
    string Product,
    string Version,
    string? LauncherInstanceId,
    OfficeApp[] Apps);

internal sealed record OfficeCatalogProduct(string Id, string Url, string Version);

internal sealed record OfficeCatalogResponse(
    int SchemaVersion,
    string Status,
    string Channel,
    string CatalogVersion,
    OfficeCatalogProduct[] Products);

internal sealed record SubAppMount(string Id, string BarePath, string MountedPath, string ShellRelativePath);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ServerInfoResponse))]
[JsonSerializable(typeof(OfficeCatalogResponse))]
internal sealed partial class BossJsonContext : JsonSerializerContext;
