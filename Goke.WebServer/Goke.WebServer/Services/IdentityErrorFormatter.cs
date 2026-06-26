using Microsoft.AspNetCore.Identity;

namespace Goke.WebServer.Services;

public static class IdentityErrorFormatter
{
    public static string Format(IdentityResult result)
    {
        return string.Join(" ", result.Errors.Select(error => error.Description));
    }
}
