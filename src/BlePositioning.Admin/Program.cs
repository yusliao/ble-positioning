using BlePositioning.Admin.Components;
using BlePositioning.Admin.Options;
using BlePositioning.Admin.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection(ApiOptions.SectionName));
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddScoped<AdminAuthState>();
builder.Services.AddHttpClient("bleApi", (sp, c) =>
{
    var b = sp.GetRequiredService<IOptions<ApiOptions>>().Value.Base?.Trim() ?? "http://localhost:5230";
    if (!b.EndsWith('/'))
        b += "/";
    c.BaseAddress = new Uri(b);
});
builder.Services.AddScoped<BlePositioningApiClient>();

var app = builder.Build();
if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
