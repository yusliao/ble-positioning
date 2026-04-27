using System.Text;
using BlePositioning.Admin.Components;
using BlePositioning.Admin.Options;
using BlePositioning.Admin.Services;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

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
builder.Services.AddSingleton<CustomerShowcaseMarkdown>();

var app = builder.Build();
if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
var staticContentTypes = new FileExtensionContentTypeProvider();
staticContentTypes.Mappings[".md"] = "text/markdown; charset=utf-8";
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = staticContentTypes });
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
