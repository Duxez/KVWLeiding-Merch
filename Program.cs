using System.Globalization;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.AspNetCore.Localization;
using BitzArt.Blazor.Cookies;
using kvwleidingmerch.Components;
using kvwleidingmerch.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddBlazorCookiesClientSideServices();
builder.Services.AddLocalization();

var defaultCulture = new CultureInfo("nl-NL");
var supportedCultures = new[]
{
    defaultCulture,
    new CultureInfo("en-US")
};

CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;

var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(defaultCulture),
}
    .AddSupportedCultures(supportedCultures.Select(culture => culture.Name).ToArray())
    .AddSupportedUICultures(supportedCultures.Select(culture => culture.Name).ToArray());

localizationOptions.RequestCultureProviders.Clear();
localizationOptions.RequestCultureProviders.Add(new CookieRequestCultureProvider());

builder.Services.AddFluentUIComponents();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IColorNameResolver, ColorNameResolver>();
builder.Services.AddScoped<ProductEventService>();
builder.Services.AddScoped<CartService>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=app.db"));

var app = builder.Build();

app.UseRequestLocalization(localizationOptions);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/api/colors/catalog", () =>
{
    var items = ColorNameResolver.DutchCatalogHex
        .OrderBy(kvp => kvp.Key)
        .Select(kvp => new { name = kvp.Key, hex = kvp.Value });
    return Results.Ok(items);
});

app.MapGet("/api/colors/resolve", (string name) =>
{
    var key = name.Trim();
    if (ColorNameResolver.DutchCatalogHex.TryGetValue(key, out var hex))
        return Results.Ok(new { name = key, hex });
    return Results.NotFound(new { name = key, message = "Color not found in local Dutch catalog." });
});

app.Run();
