using Goke.Hyb.Services;
using Goke.Core.Interfaces;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;

namespace Goke.Hyb;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        //+
        //Register needed elements for authentication:
        // This is the core functionality
        builder.Services.AddAuthorizationCore();

        // Add app services
        builder.Services.AddSingleton<TokenStorage>();
        // This is our custom provider
        builder.Services.AddScoped<MauiAuthenticationStateProvider>();
        // Use our custom provider when the app needs an AuthenticationStateProvider
        builder.Services.AddScoped<AuthenticationStateProvider>(s => (MauiAuthenticationStateProvider)s.GetRequiredService<MauiAuthenticationStateProvider>());
        builder.Services.AddScoped<IAuthenticationService>(s => s.GetRequiredService<MauiAuthenticationStateProvider>());


        //-

        // Add device-specific services used by the Goke.Hyb.Shared project
        builder.Services.AddSingleton<IFormFactor, FormFactor>();

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
