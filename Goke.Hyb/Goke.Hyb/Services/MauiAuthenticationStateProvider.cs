using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Goke.Core.Enums;
using Goke.Core.Interfaces;
using Goke.Core.Models;

namespace Goke.Hyb.Services;

/// <summary>
/// This class manages the authentication state of the user.
/// The class handles user login, logout, and token validation, including refreshing tokens when they are close to expiration.
/// It uses secure storage to save and retrieve tokens, ensuring that users do not need to log in every time.
/// </summary>
public class MauiAuthenticationStateProvider(ILogger<MauiAuthenticationStateProvider> logger) : AuthenticationStateProvider, IAuthenticationService
{
    //TODO: Place this in AppSettings or Client config file
    private const string AuthenticationType = "Custom authentication";
    private const int TokenExpirationBuffer = 30; //minutes

    private static ClaimsPrincipal defaultUser = new ClaimsPrincipal(new ClaimsIdentity());
    private static Task<AuthenticationState> defaultAuthState = Task.FromResult(new AuthenticationState(defaultUser));

    private bool persistTokenToSecureStorage; 
    private bool refreshInProgress = false;
    private readonly SemaphoreSlim refreshLock = new(1, 1);

    public LoginStatus LoginStatus { get; set; } = LoginStatus.None;
    public string LoginFailureMessage { get; set; } = "";
    public string? StatusMessage { get; private set; }

    public string? CurrentEmail => accessToken?.Email;

    private Task<AuthenticationState> currentAuthState = defaultAuthState;
    private AccessTokenInfo? accessToken;

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (currentAuthState != defaultAuthState)
        {
            return currentAuthState;
        }

        currentAuthState = CreateAuthenticationStateFromSecureStorageAsync();
        NotifyAuthenticationStateChanged(currentAuthState);

        return currentAuthState;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        return await UpdateAndValidateAccessTokenAsync();
    }

    public async Task<AccessTokenInfo?> GetAccessTokenInfoAsync()
    {
        if (await UpdateAndValidateAccessTokenAsync())
        {
            return accessToken;
        }

        LogoutCore("Your session expired. Sign in again.");
        return null;
    }

    public void Logout()
    {
        LogoutCore();
    }


    private async Task<AuthenticatedUserResponse?> GetAuthenticatedUserAsync(string bearerToken)
    {
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            return null;
        }

        using var httpClient = HttpClientHelper.GetHttpClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

        using var response = await httpClient.GetAsync(HttpClientHelper.MeUrl);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Unable to load authenticated user details. Status code: {StatusCode}", response.StatusCode);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<AuthenticatedUserResponse>();
    }

    private void ClearTokenState()
    {
        accessToken = null;
        persistTokenToSecureStorage = false;
        TokenStorage.RemoveToken();
    }

    private void LogoutCore(string? statusMessage = null)
    {
        LoginStatus = LoginStatus.None;
        LoginFailureMessage = string.Empty;
        StatusMessage = statusMessage;
        currentAuthState = defaultAuthState;
        ClearTokenState();
        NotifyAuthenticationStateChanged(defaultAuthState);
    }

    public async Task<AuthenticationResult> AuthenticateAsync(LoginRequest loginRequest)
    {
        await LogInAsync(loginRequest);
        return LoginStatus == LoginStatus.Success
            ? AuthenticationResult.Success()
            : AuthenticationResult.Failed(LoginFailureMessage);
    }

    public async Task<AuthenticationResult> RegisterAsync(RegisterRequest registerRequest)
    {
        LoginStatus = LoginStatus.None;
        LoginFailureMessage = string.Empty;
        StatusMessage = null;

        try
        {
            var httpClient = HttpClientHelper.GetHttpClient();
            var registerData = new
            {
                email = registerRequest.Email,
                password = registerRequest.Password
            };

            using var response = await httpClient.PostAsJsonAsync(HttpClientHelper.RegisterUrl, registerData);
            if (response.IsSuccessStatusCode)
            {
                StatusMessage = "Registration succeeded. Check your email to confirm your account before signing in.";
                return AuthenticationResult.Success();
            }

            var error = await response.Content.ReadAsStringAsync();
            LoginStatus = LoginStatus.Failed;
            LoginFailureMessage = string.IsNullOrWhiteSpace(error)
                ? "Registration failed. Please try again."
                : error;
            return AuthenticationResult.Failed(LoginFailureMessage);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error registering against the remote identity endpoint.");
            Debug.WriteLine($"Error registering against the remote identity endpoint: {ex}");
            LoginStatus = LoginStatus.Failed;
            LoginFailureMessage = "Server error.";
            return AuthenticationResult.Failed(LoginFailureMessage);
        }
    }

    public Task LogInAsync(LoginRequest loginModel)
    {
        currentAuthState = LogInAsyncCore(loginModel);
        NotifyAuthenticationStateChanged(currentAuthState);

        return currentAuthState;

        async Task<AuthenticationState> LogInAsyncCore(LoginRequest loginModel)
        {
            var user = await LoginWithProviderAsync(loginModel);
            return new AuthenticationState(user);
        }
    }

    private async Task<ClaimsPrincipal> LoginWithProviderAsync(LoginRequest loginModel)
    {
        var authenticatedUser = defaultUser;
        LoginStatus = LoginStatus.None;
        LoginFailureMessage = string.Empty;
        StatusMessage = null;
        persistTokenToSecureStorage = loginModel.RememberMe;

        try
        {
            var httpClient = HttpClientHelper.GetHttpClient();
            var loginData = new { loginModel.Email, loginModel.Password };
            using var response = await httpClient.PostAsJsonAsync(HttpClientHelper.LoginUrl, loginData);

            LoginStatus = response.IsSuccessStatusCode ? LoginStatus.Success : LoginStatus.Failed;

            if (LoginStatus != LoginStatus.Success)
            {
                LoginFailureMessage = "Invalid Email or Password. Please try again.";
                return authenticatedUser;
            }

            var token = await response.Content.ReadAsStringAsync();
            accessToken = persistTokenToSecureStorage
                ? await TokenStorage.SaveTokenToSecureStorageAsync(token, loginModel.Email)
                : TokenStorage.DeserializeToken(token, loginModel.Email);

            if (accessToken is null)
            {
                LoginStatus = LoginStatus.Failed;
                LoginFailureMessage = "Authentication response was invalid.";
                ClearTokenState();
                return authenticatedUser;
            }

            var userInfo = await GetAuthenticatedUserAsync(accessToken.LoginResponse.AccessToken);
            if (userInfo is null)
            {
                LoginStatus = LoginStatus.Failed;
                LoginFailureMessage = "Unable to load the signed-in user.";
                ClearTokenState();
                return authenticatedUser;
            }

            authenticatedUser = CreateAuthenticatedUser(userInfo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error logging in to the remote identity endpoint.");
            Debug.WriteLine($"Error logging in: {ex}");
            LoginFailureMessage = "Server error.";
            LoginStatus = LoginStatus.Failed;
            ClearTokenState();
        }

        return authenticatedUser;
    }

    private async Task<AuthenticationState> CreateAuthenticationStateFromSecureStorageAsync()
    {
        LoginStatus = LoginStatus.None;

        if (!await UpdateAndValidateAccessTokenAsync() || accessToken is null)
        {
            return new AuthenticationState(defaultUser);
        }

        var userInfo = await GetAuthenticatedUserAsync(accessToken.LoginResponse.AccessToken);
        if (userInfo is null)
        {
            LogoutCore("Your session expired. Sign in again.");
            return new AuthenticationState(defaultUser);
        }

        LoginStatus = LoginStatus.Success;
        return new AuthenticationState(CreateAuthenticatedUser(userInfo));
    }

    private async Task<bool> UpdateAndValidateAccessTokenAsync()
    {
        try
        {
            if (accessToken is null)
            {
                accessToken = await TokenStorage.GetTokenFromSecureStorageAsync();
                persistTokenToSecureStorage = accessToken is not null;
            }

            if (accessToken is null)
            {
                return false;
            }

            var refreshThreshold = DateTime.UtcNow.AddMinutes(TokenExpirationBuffer);
            if (refreshThreshold < accessToken.AccessTokenExpiration)
            {
                return true;
            }

            return await RefreshAccessTokenAsync(accessToken.LoginResponse.RefreshToken, accessToken.Email);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking the access token for validity.");
            Debug.WriteLine($"Error checking token for validity: {ex}");
            return false;
        }
    }



    private async Task RefreshAuthenticationStateAsync()
    {
        if (accessToken is null)
        {
            return;
        }

        var userInfo = await GetAuthenticatedUserAsync(accessToken.LoginResponse.AccessToken);
        if (userInfo is null)
        {
            return;
        }

        currentAuthState = Task.FromResult(new AuthenticationState(CreateAuthenticatedUser(userInfo)));
        NotifyAuthenticationStateChanged(currentAuthState);
    }

    private async Task<bool> RefreshAccessTokenAsync(string refreshToken, string email)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return false;
        }

        await refreshLock.WaitAsync();

        try
        {
            if (refreshInProgress)
            {
                return accessToken is not null &&
                       DateTime.UtcNow.AddMinutes(TokenExpirationBuffer) < accessToken.AccessTokenExpiration;
            }

            refreshInProgress = true;

            using var httpClient = HttpClientHelper.GetHttpClient();
            var refreshData = new { refreshToken };
            using var response = await httpClient.PostAsJsonAsync(HttpClientHelper.RefreshUrl, refreshData);

            if (!response.IsSuccessStatusCode)
            {
                LogoutCore("Your session expired. Sign in again.");
                return false;
            }

            var token = await response.Content.ReadAsStringAsync();
            accessToken = persistTokenToSecureStorage
                ? await TokenStorage.SaveTokenToSecureStorageAsync(token, email)
                : TokenStorage.DeserializeToken(token, email);

            if (accessToken is null)
            {
                LogoutCore("Your session expired. Sign in again.");
                return false;
            }

            await RefreshAuthenticationStateAsync();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error refreshing the access token.");
            Debug.WriteLine($"Error refreshing access token: {ex}");
            LogoutCore("Your session expired. Sign in again.");
            return false;
        }
        finally
        {
            refreshInProgress = false;
            refreshLock.Release();
        }
    }

    private ClaimsPrincipal CreateAuthenticatedUser(AuthenticatedUserResponse user)
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

        AddClaim(ClaimTypes.Name, user.Name);
        AddClaim(ClaimTypes.Email, user.Email);

        foreach (var role in user.Roles)
        {
            AddClaim(ClaimTypes.Role, role);
        }

        foreach (var claim in user.Claims)
        {
            AddClaim(claim.Type, claim.Value);
        }

        var identity = new ClaimsIdentity(claims, AuthenticationType);
        return new ClaimsPrincipal(identity);
    }
}
