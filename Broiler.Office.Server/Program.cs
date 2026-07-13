using System.Reflection;
using Microsoft.AspNetCore.StaticFiles;

// ── BOSS: the Broiler Office Standalone Server ────────────────────────────────────────────────────
// A Kestrel host that serves the Broiler Office web apps. Today that is the Broiler Writer word
// processor (Broiler.Writer.WebAssembly), mounted at the site root and served from its vendored,
// content-hashed WebAssembly bundle (see the .csproj for how the bundle gets here). Additional Office
// apps register the same way: vendor the client's published wwwroot and add it to the app list.

var builder = WebApplication.CreateBuilder(args);

// Kestrel is the default (and only) server here; make the intent explicit.
builder.WebHost.UseKestrel();

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

/// <summary>An Office web app mounted by BOSS, as reported by <c>/api/info</c>.</summary>
internal sealed record OfficeApp(string Name, string Description, string Path, string[] Formats);
