using ElectricalStoreAI.Application.Products;
using ElectricalStoreAI.Infrastructure;
using ElectricalStoreAI.Infrastructure.Persistence;
using ElectricalStoreAI.Web;
using ElectricalStoreAI.Web.Components;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options => options.DetailedErrors = builder.Environment.IsDevelopment())
    .AddHubOptions(options => options.MaximumReceiveMessageSize = 2 * 1024 * 1024);

builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddHostedService<ModelWarmUpService>();

var app = builder.Build();

// Apply database migrations (creates tables on first run).
await using (var databaseContext = await app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync())
{
    await databaseContext.Database.MigrateAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();

// Serves reference-image thumbnails by database id only; the client never supplies a path.
app.MapGet("/product-images/{id:guid}", async (Guid id, IProductImageService images, HttpContext http, CancellationToken cancellationToken) =>
{
    var image = await images.OpenAsync(id, cancellationToken);
    if (image is null)
    {
        return Results.NotFound();
    }

    http.Response.Headers.CacheControl = "private, max-age=86400";
    http.Response.Headers.XContentTypeOptions = "nosniff";
    return Results.File(image.Content, image.ContentType);
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
