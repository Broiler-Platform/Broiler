using System.Reflection;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Hosting.WindowsServices;

// ── BOSS: the Broiler Office Standalone Server ────────────────────────────────────────────────────
// A Kestrel host that serves the Broiler Office web apps. Today that is the Broiler Writer word
// processor (Broiler.Writer.WebAssembly), mounted at the site root and served from its vendored,
// content-hashed WebAssembly bundle (see the .csproj for how the bundle gets here). Additional Office
// apps register the same way: vendor the client's published wwwroot and add it to the app list.

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = ResolveContentRoot(args),
});

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

// The apps this server hosts. Extend this list as more Office web clients come online.
var hostedApps = new[]
{
    new OfficeApp(
        Name: "Writer",
        Description: "Broiler Writer — word processor",
        Path: "/",
        Formats: new[] { "rtf", "docx", "html", "md" }),
};

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/error");

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

    // Cache versioning for the vendored WebAssembly bundle. The SDK content-fingerprints every runtime
    // asset (dotnet.<hash>.js, m.<hash>.js, *.<hash>.wasm): the URL itself carries the version, so the
    // bytes behind a given URL never change and the asset can be cached immutably (a new build changes
    // the hash, hence the URL, and the browser fetches fresh). The two entry points keep a *stable* URL
    // across releases — index.html (the shell) and broiler.graphics.webassembly.js (the stable-named
    // Canvas replay module) — so they are marked no-cache and revalidated on every load. Without that, a
    // browser can pair a cached shell/replay module with a newer fingerprinted runtime and desync the
    // replay op stream (the "Unknown replay op …" failure).
    OnPrepareResponse = context =>
    {
        string name = context.File.Name;
        bool stableEntryPoint =
            name.Equals("index.html", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("broiler.graphics.webassembly.js", StringComparison.OrdinalIgnoreCase);
        context.Context.Response.Headers.CacheControl = stableEntryPoint
            ? "no-cache"
            : "public, max-age=31536000, immutable";
    },
};

// Serve the vendored Writer bundle: its transformed index.html references content-hashed physical
// files (dotnet.<hash>.js, the replay module, m.<hash>.js) that exist on disk, so plain static file
// serving is all that's needed — no import-map rewriting.
app.UseStaticFiles(staticFiles);

app.UseRouting();

// ── Server API ────────────────────────────────────────────────────────────────────────────────────
var serverVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

app.MapGet("/healthz", () => Results.Text("OK", "text/plain"));

app.MapGet("/api/info", () => Results.Json(new
{
    server = "BOSS",
    product = "Broiler Office Standalone Server",
    version = serverVersion,
    apps = hostedApps,
}));

app.MapGet("/error", () => Results.Problem("The hosted application failed to load."));

// Anything that is neither an API route nor a physical static asset is an in-app (client-routed) path:
// hand back the Writer shell so the WebAssembly runtime boots and takes over.
app.MapFallbackToFile("index.html", staticFiles);

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
        logger.LogInformation("  Writer:  {Address}/", address.TrimEnd('/'));
});

app.Run();

// The content root is where the vendored Writer bundle (wwwroot/) is looked up. The host default is
// the *working directory*, which is right for `dotnet run` (the build vendors the bundle into the
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
internal sealed record OfficeApp(string Name, string Description, string Path, string[] Formats);
