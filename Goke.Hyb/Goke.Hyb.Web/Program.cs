using Goke.Core.Interfaces;
using Goke.Hyb.Web.Components;
using Goke.Hyb.Web.Endpoints;
using Goke.Hyb.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents()
    .AddAuthenticationStateSerialization();

//+
// Add authentication and authorization services
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/access-denied";
    });

//
builder.Services.AddHttpContextAccessor();

// Add httpclient service for API calls
builder.Services.AddHttpClient("BackendApi", client =>
    client.BaseAddress = new Uri(builder.Configuration["Backend:BaseUrl"] ?? throw new InvalidOperationException("API base URL is not configured")));

builder.Services.AddHttpClient<RemoteAuthenticationService>(client =>
    client.BaseAddress = new Uri(builder.Configuration["Backend:BaseUrl"] ?? throw new InvalidOperationException("API base URL is not configured")));

//-


// Add device-specific services used by the Goke.Hyb.Shared project
builder.Services.AddSingleton<IFormFactor, FormFactor>();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

//+ 
app.UseAuthentication();
app.UseAuthorization();
//-

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(
        typeof(Goke.SharedUI._Imports).Assembly,
        typeof(Goke.Hyb.Shared._Imports).Assembly,
        typeof(Goke.Hyb.Web.Client._Imports).Assembly);

app.MapAccountEndpoints();

app.Run();
