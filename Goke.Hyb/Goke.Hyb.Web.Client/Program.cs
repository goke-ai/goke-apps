using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Goke.Core.Interfaces;
using Goke.Hyb.Web.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

//+
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationStateDeserialization();
// Add httpclient service for API calls
//builder.Services.AddHttpClient<ApiHttpClient>(client =>
//    client.BaseAddress = new Uri(builder.Configuration["Backend:BaseUrl"] ?? throw new InvalidOperationException("API base URL is not configured")));

// Add device-specific services used by the Goke.Hyb.Shared project
builder.Services.AddSingleton<IFormFactor, FormFactor>();
//-

await builder.Build().RunAsync();
