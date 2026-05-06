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
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection(EmailSettings.SectionName));
builder.Services.AddScoped<IColorNameResolver, ColorNameResolver>();
builder.Services.AddScoped<ProductEventService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<IOrderEmailService, OrderEmailService>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=app.db"));

var app = builder.Build();

var emailSettings = app.Configuration
    .GetSection(EmailSettings.SectionName)
    .Get<EmailSettings>()
    ?? new EmailSettings();

app.Logger.LogInformation(
    "Email settings loaded: SmtpHost={SmtpHost}, SmtpPort={SmtpPort}, UseSsl={UseSsl}, UserName={UserName}, Password={Password}, FromAddress={FromAddress}, FromName={FromName}, OrdersRecipientAddress={OrdersRecipientAddress}",
    string.IsNullOrWhiteSpace(emailSettings.SmtpHost) ? "(empty)" : emailSettings.SmtpHost,
    emailSettings.SmtpPort,
    emailSettings.UseSsl,
    string.IsNullOrWhiteSpace(emailSettings.UserName) ? "(empty)" : emailSettings.UserName,
    MaskSecret(emailSettings.Password),
    string.IsNullOrWhiteSpace(emailSettings.FromAddress) ? "(empty)" : emailSettings.FromAddress,
    string.IsNullOrWhiteSpace(emailSettings.FromName) ? "(empty)" : emailSettings.FromName,
    string.IsNullOrWhiteSpace(emailSettings.OrdersRecipientAddress) ? "(empty)" : emailSettings.OrdersRecipientAddress);

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var ownerUrl = config["AccessLinks:OwnerUrl"];
    var accessUrl = config["AccessLinks:AccessUrl"];

    if (!string.IsNullOrWhiteSpace(ownerUrl) && !db.AccessLinks.Any(l => l.UrlValue == ownerUrl))
        db.AccessLinks.Add(new AccessLink { UrlValue = ownerUrl, CreatedAtUtc = DateTime.MinValue });

    if (!string.IsNullOrWhiteSpace(accessUrl) && !db.AccessLinks.Any(l => l.UrlValue == accessUrl))
        db.AccessLinks.Add(new AccessLink { UrlValue = accessUrl, CreatedAtUtc = DateTime.UtcNow });

    db.SaveChanges();
}

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

static string MaskSecret(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
        return "(empty)";

    return new string('*', Math.Min(value.Length, 12));
}
