using Goke.Core.Models;
using Goke.Core.Models.Goke.Core.Models;
using Goke.WebServer.Data;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace Goke.WebServer.Endpoints
{
    public static class IdentityEndpoints
    {
        public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
        {
            // Map the Identity API endpoints
            var identityGroup = endpoints.MapGroup("/identity");

            // Map the Identity API endpoints for ApplicationUser
            identityGroup.MapIdentityApi<ApplicationUser>();

            // Map the "me" endpoint to get the authenticated user's information
            identityGroup.MapGet("me", async (
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

            return endpoints;
        }
    }
}
