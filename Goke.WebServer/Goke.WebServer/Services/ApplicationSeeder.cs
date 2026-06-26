using Goke.Core.Extensions;
using Goke.Core.Models;
using Goke.WebServer.Data;
using Goke.WebServer.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

internal class ApplicationSeeder
{
    public static async Task SeedAllAsync(WebApplication app, bool resetDatabase)
    {
        // Apply migrations & create database if needed at startup
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (resetDatabase)
        {
            dbContext.Database.EnsureDeleted();
        }

        // Apply any pending migrations to the database.
        dbContext.Database.Migrate();

        // Add roles, users, and other initial data here if needed.
        await AddRolesAsync(scope);
        await AddUsersAsync(scope, app.Environment.IsProduction());

        // Seed the database with data if needed.
        SeedData(dbContext);
    }

    public static void SeedData(ApplicationDbContext dbContext)
    {
        // Seed the database with test data if needed.        

        AddCommonData();

        dbContext.SaveChanges();
    }

    private static void AddCommonData()
    {
        
    }

    public static async Task AddRolesAsync(IServiceScope scope)
    {
        // Administrators, Managers, Officers and Users
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        // get roles from configuration or define them here
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        await AddRoleAsync(roleManager, configuration);

    }

    public static async Task AddRoleAsync(RoleManager<IdentityRole> roleManager, IConfiguration configuration)
    {
        var rolesSection = configuration.GetSection("SeedRoles");
        var rolesFromConfig = rolesSection.Get<string[]>();

        var roles = rolesFromConfig != null && rolesFromConfig.Length > 0 ? [.. rolesFromConfig] : new List<string> { "Administrators", "Users" };

        foreach (var roleName in roles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }
    }


    public static async Task AddUsersAsync(IServiceScope scope, bool isProduction)
    {
        // Add users to the database if needed.
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var appEmailSender = scope.ServiceProvider.GetRequiredService<ApplicationEmailSender>();

        // get users from configuration or define them here
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        bool flowControl = await AddUsersAsync(userManager, appEmailSender, configuration, isProduction);
        if (!flowControl)
        {
            return;
        }
    }

    public static async Task<bool> AddUsersAsync(UserManager<ApplicationUser> userManager, ApplicationEmailSender appEmailSender, IConfiguration configuration, bool isProduction = true)
    {
        var usersSection = configuration.GetSection("SeedUsers");
        var usersFromConfig = usersSection.Get<SeedUserOptions[]>();


        if (usersFromConfig == null || usersFromConfig.Length == 0)
        {
            Console.WriteLine("No users found in configuration. Please add users to the 'SeedUsers' section in appsettings.json.");
            return false;
        }

        foreach (var userOptions in usersFromConfig)
        {
            var user = await userManager.FindByNameAsync(userOptions.UserName);

            if (user != null)
            {
                // delete the user if it exists and create a new one
                Console.WriteLine($"User {userOptions.UserName} already exists. Deleting and recreating the user.");
                var deleteResult = await userManager.DeleteAsync(user);
                if (deleteResult == null || !deleteResult.Succeeded)
                {
                    Console.WriteLine($"Failed to delete user {userOptions.UserName}. Skipping user creation.");
                    continue;
                }
            }

            user = new ApplicationUser { UserName = userOptions.UserName, Email = userOptions.Email, EmailConfirmed = true };

            var passwordToUse = GetPasswordToUse(isProduction, userOptions.Password); // Use a default password if none is provided

            Console.WriteLine($"Creating user: {userOptions.UserName} with password: {passwordToUse}");
            //await EmailSender.SendConfirmationLinkAsync(user, Input.Email, HtmlEncoder.Default.Encode(callbackUrl));
            var htmlMessage = $"Hello {userOptions.UserName},<br><br>Your account has been created. Please use the following password to log in: {passwordToUse}<br><br>Please change your password after logging in for the first time.";
            await appEmailSender.SendEmailAsync(userOptions.Email, $"Create User: {userOptions.UserName}", htmlMessage);


            var result = await userManager.CreateAsync(user, passwordToUse);
            if (result.Succeeded)
            {
                //result = await userManager.ConfirmEmailAsync(user, await userManager.GenerateEmailConfirmationTokenAsync(user));
                if (!string.IsNullOrEmpty(userOptions.Role))
                {
                    await userManager.AddToRoleAsync(user, userOptions.Role);
                }
            }
        }

        return true;
    }

    private static string GetPasswordToUse(bool isProduction, string? password)
    {
        return isProduction || string.IsNullOrEmpty(password) ? string.GeneratePassword(20) : password;
    }
}