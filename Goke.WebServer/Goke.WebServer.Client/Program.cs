using Goke.Core.Interfaces;
using Goke.WebServer.Client.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationStateDeserialization();

// Add device-specific services
builder.Services.AddSingleton<IFormFactor, FormFactor>();

await builder.Build().RunAsync();
