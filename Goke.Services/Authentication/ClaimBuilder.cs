using Goke.Core.Models;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace Goke.Services.Authentication
{
    public class ClaimBuilder
    {
        public static List<Claim> BuildClaimFomUserInfo(AuthenticatedUserResponse user, string fallbackEmail)
        {
            var claims = new List<Claim>();
            var seenClaims = new HashSet<(string Type, string Value)>();

            void AddClaim(string type, string? value)
            {
                if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                if (seenClaims.Add((type, value)))
                {
                    claims.Add(new Claim(type, value));
                }
            }

            AddClaim(ClaimTypes.NameIdentifier, user.UserId);
            AddClaim(ClaimTypes.Name, user.Name ?? fallbackEmail);
            AddClaim(ClaimTypes.Email, user.Email ?? fallbackEmail);

            foreach (var role in user.Roles)
            {
                AddClaim(ClaimTypes.Role, role);
            }

            foreach (var claim in user.Claims)
            {
                AddClaim(claim.Type, claim.Value);
            }

            return claims;
        }

        //private static List<Claim> BuildClaimsFromAccessToken(string accessToken, string fallbackEmail)
        //{
        //    var claims = new List<Claim>();

        //    var handler = new JwtSecurityTokenHandler();
        //    if (handler.CanReadToken(accessToken))
        //    {
        //        var jwt = handler.ReadJwtToken(accessToken);

        //        foreach (var claim in jwt.Claims)
        //        {
        //            if (claim.Type is "role" or "roles")
        //            {
        //                claims.Add(new Claim(ClaimTypes.Role, claim.Value));
        //                continue;
        //            }

        //            claims.Add(claim);
        //        }
        //    }

        //    if (!claims.Any(c => c.Type == ClaimTypes.Name) && !string.IsNullOrWhiteSpace(fallbackEmail))
        //    {
        //        claims.Add(new Claim(ClaimTypes.Name, fallbackEmail));
        //    }

        //    if (!claims.Any(c => c.Type == ClaimTypes.Email) && !string.IsNullOrWhiteSpace(fallbackEmail))
        //    {
        //        claims.Add(new Claim(ClaimTypes.Email, fallbackEmail));
        //    }

        //    return claims;
        //}



    }
}
