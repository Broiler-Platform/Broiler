using Broiler.DevSite.Services;

// Composition root: register the concrete image codecs Broiler.Graphics decodes/encodes with.
Broiler.Graphics.BImageCodecs.Use(
    new Broiler.Media.MediaCodecCatalog(Broiler.Media.Image.Managed.ManagedImageCodecs.CreateCodecs()));

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddScoped<RenderingService>();
builder.Services.AddSingleton(new ComplianceService(builder.Environment.ContentRootPath));
builder.Services.AddSingleton(new ApiDocService(builder.Environment.ContentRootPath));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

// API endpoint for snippet playground live rendering
app.MapPost("/api/render", async (HttpContext context, RenderingService renderer) =>
{
    // Limit request body to 1 MB to prevent denial-of-service
    if (context.Request.ContentLength > 1_048_576)
        return Results.BadRequest("Request body too large (max 1 MB).");

    using var reader = new StreamReader(context.Request.Body);
    char[] buffer = new char[1_048_576];
    int charsRead = await reader.ReadAsync(buffer, 0, buffer.Length);
    if (charsRead == 0)
        return Results.BadRequest("No HTML content provided.");

    string html = new string(buffer, 0, charsRead);

    int width = 1024;
    int height = 768;
    if (context.Request.Query.TryGetValue("width", out var wVal) && int.TryParse(wVal, out var w))
        width = Math.Clamp(w, 100, 4096);
    if (context.Request.Query.TryGetValue("height", out var hVal) && int.TryParse(hVal, out var h))
        height = Math.Clamp(h, 100, 4096);

    byte[] png = renderer.RenderHtmlToPng(html, width, height);
    return Results.File(png, "image/png");
});

app.Run();
