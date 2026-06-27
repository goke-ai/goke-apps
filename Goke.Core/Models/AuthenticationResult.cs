namespace Goke.Core.Models;

public sealed record AuthenticationResult(bool Succeeded, string? ErrorMessage = null)
{
    public static AuthenticationResult Success() => new(true);

    public static AuthenticationResult Failed(string? errorMessage) => new(false, errorMessage);
}
