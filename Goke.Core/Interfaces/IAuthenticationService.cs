using Goke.Core.Enums;
using Goke.Core.Models;

namespace Goke.Core.Interfaces;

public interface IAuthenticationService
{
    LoginStatus LoginStatus { get; }
    string LoginFailureMessage { get; }
    string? CurrentEmail { get; }

    Task<bool> IsAuthenticatedAsync();
    Task<AccessTokenInfo?> GetAccessTokenInfoAsync();
    Task<AuthenticationResult> AuthenticateAsync(LoginRequest loginRequest);
    Task<AuthenticationResult> RegisterAsync(RegisterRequest registerRequest);
    void Logout();
}
