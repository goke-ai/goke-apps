using Goke.Core.Interfaces;
using Goke.Core.Models;
using Goke.Core.Models.Goke.Core.Models;
using Goke.WebServer.Client.Pages;
using Goke.WebServer.Components;
using Goke.WebServer.Components.Account;
using Goke.WebServer.Data;
using Goke.WebServer.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents()
    .AddAuthenticationStateSerialization();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

//builder.Services.AddAuthentication(options =>
//    {
//        options.DefaultScheme = IdentityConstants.ApplicationScheme;
//        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
//    })
//    .AddIdentityCookies();

// Ensure unauthenticated web clients redirect to login rather than receive 401.
// Only DefaultChallengeScheme is set here; AddIdentityApiEndpoints sets DefaultScheme
// to BearerAndApplicationScheme which handles both bearer tokens (MAUI) and cookies (web).
builder.Services.AddAuthentication(options =>
{
    options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

//builder.Services.AddIdentityCore<ApplicationUser>(options =>
//    {
//        options.SignIn.RequireConfirmedAccount = true;
//        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
//    })
//    .AddRoles<IdentityRole>()
//    .AddEntityFrameworkStores<ApplicationDbContext>()
//    .AddSignInManager()
//    .AddDefaultTokenProviders();

// Needed for external clients to log in
builder.Services.AddIdentityApiEndpoints<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
    options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Configure options from configuration.
builder.Services.Configure<EmailSenderOptions>(builder.Configuration.GetSection(EmailSenderOptions.SectionName));

// Register the services.
builder.Services.AddSingleton<ApplicationEmailSender>();
builder.Services.AddSingleton<SeedConfirmationService>();
builder.Services.AddSingleton<AdminActivityLog>();
builder.Services.AddScoped<AdminStatusMessageStore>();
builder.Services.AddScoped<RoleAdministrationService>();
builder.Services.AddSingleton<IFormFactor, FormFactor>();

// Register the IdentityEmailSender as the implementation of IEmailSender<ApplicationUser>.
//builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();
builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityEmailSender>();

// Add Swagger/OpenAPI support for API documentation and testing.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Seed the database with test data if needed.
await ApplicationSeeder.SeedAllAsync(app, app.Environment.IsDevelopment());

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.UseMigrationsEndPoint();

    app.UseSwagger();
    app.UseSwaggerUI();
}
else
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
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Goke.WebServer.Client._Imports).Assembly);

// Needed for external clients to log in
app.MapGroup("/identity").MapIdentityApi<ApplicationUser>();

app.MapGet("/identity/me", async (
    ClaimsPrincipal principal,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager) =>
{
    var user = await userManager.GetUserAsync(principal);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    var roles = await userManager.GetRolesAsync(user);
    var userClaims = await userManager.GetClaimsAsync(user);

    var allClaims = new List<AuthenticatedUserClaimResponse>();

    // Add user claims
    foreach (var claim in userClaims)
    {
        if (!string.IsNullOrWhiteSpace(claim.Type) && !string.IsNullOrWhiteSpace(claim.Value))
        {
            allClaims.Add(new AuthenticatedUserClaimResponse
            {
                Type = claim.Type,
                Value = claim.Value
            });
        }
    }

    // Add role claims
    foreach (var roleName in roles)
    {
        // Get the role by name
        var role = await roleManager.FindByNameAsync(roleName);
        if (role is null)
        {
            continue;
        }

        // Get the claims associated with the role
        var roleClaims = await roleManager.GetClaimsAsync(role);
        foreach (var claim in roleClaims)
        {
            if (!string.IsNullOrWhiteSpace(claim.Type) && !string.IsNullOrWhiteSpace(claim.Value))
            {
                allClaims.Add(new AuthenticatedUserClaimResponse
                {
                    Type = claim.Type,
                    Value = claim.Value
                });
            }
        }
    }

    return Results.Ok(new AuthenticatedUserResponse
    {
        UserId = user.Id,
        Email = user.Email ?? string.Empty,
        Name = user.UserName ?? user.Email ?? string.Empty,
        Roles = [.. roles],
        Claims = [.. allClaims.DistinctBy(c => new { c.Type, c.Value })]
    });
}).RequireAuthorization();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();



app.Run();
