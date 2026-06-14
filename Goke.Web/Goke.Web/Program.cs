using Goke.Web.Client.Pages;
using Goke.Web.Components;
using Goke.Web.Components.Account;
using Goke.Web.Data;
using Goke.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Runtime.InteropServices;


// determine the OS platform to conditionally configure services or behavior if needed.
bool isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
bool isLinux = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
bool isOSX = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.OSX);


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

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

// Conditionally configure the database provider based on the OS platform.
var databaseType = DatabaseType.MSSQL;
if (isWindows)
{
    databaseType = DatabaseType.MSSQL;
}
if (isLinux)
{
    databaseType = DatabaseType.MySQL;
}

var connectionString = string.Empty;

switch(databaseType)
{
    case DatabaseType.MSSQL:
        connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, b => b.MigrationsAssembly("Goke.Web.Data.WindowsMigrations")));
        break;
    //case DatabaseType.MySQL:
    //   connectionString = builder.Configuration.GetConnectionString("MariaDBDefaultConnection") ?? throw new InvalidOperationException("Connection string 'MariaDBDefaultConnection' not found.");
    //  var serverVersion = new MariaDbServerVersion(new Version(11, 4, 2)); //ServerVersion.AutoDetect(connectionString);
    //   builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    //       options.UseMySql(connectionString, serverVersion, b => b.MigrationsAssembly("Goke.Web.Data.LinuxMigrations")));
    //   break;
    //case DatabaseType.PostgreSQL:
    //    connectionString = builder.Configuration.GetConnectionString("PostgreSQLDefaultConnection") ?? throw new InvalidOperationException("Connection string 'PostgreSQLDefaultConnection' not found.");
    //    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    //        options.UseNpgsql(connectionString));
    //    break;
    //case DatabaseType.SQLite:
    //    connectionString = builder.Configuration.GetConnectionString("SQLiteDefaultConnection") ?? throw new InvalidOperationException("Connection string 'SQLiteDefaultConnection' not found.");
    //    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    //        options.UseSqlite(connectionString));
    //    break;
    default:
        throw new InvalidOperationException("Unsupported database type.");
}

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

builder.Services.AddSingleton<AdminActivityLog>();
builder.Services.AddScoped<AdminStatusMessageStore>();
builder.Services.AddScoped<RoleAdministrationService>();



var app = builder.Build();

// Seed the database with test data if needed.
await ApplicationSeeder.SeedAllAsync(app, app.Environment.IsDevelopment());

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.UseMigrationsEndPoint();
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
    .AddAdditionalAssemblies(typeof(Goke.Web.Client._Imports).Assembly);

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();

