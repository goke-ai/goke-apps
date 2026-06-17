using Goke.Core.Extensions;
using Goke.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

internal class ApplicationSeeder
{

    public static async Task SeedAllAsync(WebApplication app, bool resetDatabase)
    {
        // Apply migrations & create database if needed at startup
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            if (resetDatabase)
            {
                dbContext.Database.EnsureDeleted();
            }

            // Apply any pending migrations to the database.
            dbContext.Database.Migrate();

            // Add roles, users, and other initial data here if needed.
            await AddRolesAsync(scope);
            await AddUsersAsync(scope);

            // Seed the database with data if needed.
            SeedData(dbContext);
        }
    }

 
    // Helper methods for database initialization and seeding.
    static bool DatabaseNeedReset(ApplicationDbContext dbContext)
    {
        return false;
    }

    static void SeedData(ApplicationDbContext dbContext)
    {
        // Seed the database with test data if needed.        

        AddCommonData();

        dbContext.SaveChanges();
    }

    private static void AddCommonData()
    {


    }

    private static async Task AddUsersAsync(IServiceScope scope)
    {
        // Add users to the database if needed.
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var users = new List<(string UserName, string Email, string Password, string Role)>
        {
            ("admin@ark.com", "admin@ark.com", "admin@ARK#1", "Administrators"),
            ("user@ark.com", "user@ark.com", "user@ARK#1", "Users"),
            ("manager@ark.com", "manager@ark.com", "manager@ARK#1", "Managers"),
            ("officer@ark.com", "officer@ark.com", "officer@ARK#1", "Officers")
        };

        foreach (var (userName, email, password, role) in users)
        {
            var user = await userManager.FindByNameAsync(userName);
            if (user == null)
            {
                user = new ApplicationUser { UserName = userName, Email = email, EmailConfirmed = true };

                var passwordToUse = string.GeneratePassword(8); // Use a default password if none is provided
                Console.WriteLine($"Creating user: {userName} with password: {passwordToUse}");

                var result = await userManager.CreateAsync(user, passwordToUse);
                if (result.Succeeded)
                {
                    //result = await userManager.ConfirmEmailAsync(user, await userManager.GenerateEmailConfirmationTokenAsync(user));
                    await userManager.AddToRoleAsync(user, role);
                }

            }
        }
    }

    private static async Task AddRolesAsync(IServiceScope scope)
    {
        // Administrators, Managers, Officers and Users
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        var roles = new List<string> { "Administrators", "Managers", "Officers", "Users" };

        foreach (var roleName in roles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

    }
}