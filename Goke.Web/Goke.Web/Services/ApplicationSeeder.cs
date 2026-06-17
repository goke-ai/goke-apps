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

                var passwordToUse = GetPassword(userName, email, password); // Use a default password if none is provided

                var result = await userManager.CreateAsync(user, passwordToUse);
                if (result.Succeeded)
                {
                    //result = await userManager.ConfirmEmailAsync(user, await userManager.GenerateEmailConfirmationTokenAsync(user));
                    await userManager.AddToRoleAsync(user, role);
                }

            }
        }
    }

    private static string GetPassword(string userName, string email, string password)
    {
        // If the password is null or empty, generate a default password based on the username and email.
        if (string.IsNullOrWhiteSpace(password))
        {
            password = $"{userName.Split('@')[0]}@{email.Split('@')[1]}#1";
        }

        // Ensure the password meets the minimum requirements (e.g., length, complexity).
        if (password.Length < 8)
        {
            password = $"{password}#1"; // Append a default suffix to meet length requirements.
        }

        // You can add more complexity checks here if needed (e.g., uppercase, lowercase, digits, special characters).


        return password;
    }

    public static string GeneratePin(int multiplyby3=4)
    {
        Random random = new();
        // Generate a random 12-digit pin add at least one special character, one uppercase letter, and one lowercase letter.
        var pin = string.Empty;

        // Generate a random 4-byte array
        var pinBytes = new byte[multiplyby3];

        // Fill the byte array with random bytes
        random.NextBytes(pinBytes);

        // generate a (4*3) 12-digit pin using the random bytes
        // Convert the byte array to a string representation of the pin
        pin = string.Join("", pinBytes.Select(s => s.ToString("000")));

        //
        string SPECIAL = "@#$%&+=?<>!/~-";
        string ALPHABETH = "ABCDEFGHIJKLMNPQRSTUVWXYZ";
        string LOWERALPHABETH = "abcdefghkmnpqswxyz";

        // insert a special character at a random position in the pin
        int k = random.Next(SPECIAL.Length);
        var s = SPECIAL.ElementAt(k).ToString();
        k = random.Next(1, pin.Length);
        pin = pin.Insert(k, s);

        // insert an uppercase character at a random position in the pin
        k = random.Next(ALPHABETH.Length);
        s = ALPHABETH.ElementAt(k).ToString();
        k = random.Next(pin.Length);
        pin = pin.Insert(k, s);

        k = random.Next(ALPHABETH.Length);
        s = ALPHABETH.ElementAt(k).ToString();
        k = random.Next(pin.Length);
        pin = pin.Insert(k, s);

        // insert a lowercase character at a random position in the pin
        k = random.Next(LOWERALPHABETH.Length);
        s = LOWERALPHABETH.ElementAt(k).ToString();
        k = random.Next(pin.Length);
        pin = pin.Insert(k, s);

        return pin;
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