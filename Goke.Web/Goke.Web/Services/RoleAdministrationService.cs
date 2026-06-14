using Microsoft.AspNetCore.Identity;

namespace Goke.Web.Services;

public sealed class RoleAdministrationService(RoleManager<IdentityRole> roleManager)
{
    public async Task<RoleCommandResult> CreateRoleAsync(string? roleName)
    {
        var normalizedRoleName = roleName?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedRoleName))
        {
            return RoleCommandResult.Failure("Enter a role name.");
        }

        if (await roleManager.RoleExistsAsync(normalizedRoleName))
        {
            return RoleCommandResult.Failure("That role already exists.");
        }

        var result = await roleManager.CreateAsync(new IdentityRole(normalizedRoleName));
        if (!result.Succeeded)
        {
            return RoleCommandResult.Failure(IdentityErrorFormatter.Format(result));
        }

        return RoleCommandResult.Success(normalizedRoleName);
    }
}

public sealed record RoleCommandResult(bool Succeeded, string? RoleName, string? ErrorMessage)
{
    public static RoleCommandResult Success(string roleName) => new(true, roleName, null);

    public static RoleCommandResult Failure(string errorMessage) => new(false, null, errorMessage);
}
