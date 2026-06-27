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

    private static ClaimsPrincipal _defaultUser = new ClaimsPrincipal(new ClaimsIdentity());
    private static Task<AuthenticationState> _defaultAuthState = Task.FromResult(new AuthenticationState(_defaultUser));

    private bool _refreshInProgress;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public LoginStatus LoginStatus { get; set; } = LoginStatus.None;
    public string LoginFailureMessage { get; set; } = "";
    public string? StatusMessage { get; private set; }

    public string? CurrentEmail => _accessToken?.Email;

    private Task<AuthenticationState> _currentAuthState = _defaultAuthState;
    private AccessTokenInfo? _accessToken;

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (_currentAuthState != _defaultAuthState)
        {
            return _currentAuthState;
        }

        _currentAuthState = CreateAuthenticationStateFromSecureStorageAsync();
        NotifyAuthenticationStateChanged(_currentAuthState);

        return _currentAuthState;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        return await UpdateAndValidateAccessTokenAsync();
    }

    public async Task<AccessTokenInfo?> GetAccessTokenInfoAsync()
    {
        if (await UpdateAndValidateAccessTokenAsync())
        {
            return _accessToken;
        }

        LogoutCore("Your session expired. Sign in again.");
        return null;
    }

    public void Logout()
    {
        LogoutCore();
    }

    private void LogoutCore(string? statusMessage = null)
    {
        LoginStatus = LoginStatus.None;
        LoginFailureMessage = string.Empty;
        StatusMessage = statusMessage;
        _currentAuthState = _defaultAuthState;
        _accessToken = null;
        TokenStorage.RemoveToken();
        NotifyAuthenticationStateChanged(_defaultAuthState);
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
        _currentAuthState = LogInAsyncCore(loginModel);
        NotifyAuthenticationStateChanged(_currentAuthState);

        return _currentAuthState;

        async Task<AuthenticationState> LogInAsyncCore(LoginRequest loginModel)
        {
            var user = await LoginWithProviderAsync(loginModel);
            return new AuthenticationState(user);
        }
    }

    private async Task<ClaimsPrincipal> LoginWithProviderAsync(LoginRequest loginModel)
    {
        var authenticatedUser = _defaultUser;
        LoginStatus = LoginStatus.None;
        LoginFailureMessage = string.Empty;
        StatusMessage = null;

        try
        {
            //Call the Login endpoint and pass the email and password
            var httpClient = HttpClientHelper.GetHttpClient();
            var loginData = new { loginModel.Email, loginModel.Password };
            using var response = await httpClient.PostAsJsonAsync(HttpClientHelper.LoginUrl, loginData);

            LoginStatus = response.IsSuccessStatusCode ? LoginStatus.Success : LoginStatus.Failed;

            if (LoginStatus == LoginStatus.Success)
            {
                var token = await response.Content.ReadAsStringAsync();

                if (loginModel.RememberMe)
                {
                    // Save token to secure storage so the user doesn't have to login every time
                    _accessToken = await TokenStorage.SaveTokenToSecureStorageAsync(token, loginModel.Email);
                }
                else
                {
                    // Keep token in memory only — cleared when app closes
                    _accessToken = TokenStorage.DeserializeToken(token, loginModel.Email);
                }

                authenticatedUser = CreateAuthenticatedUser(loginModel.Email);
            }
            else
            {
                LoginFailureMessage = "Invalid Email or Password. Please try again.";
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error logging in to the remote identity endpoint."); 
            Debug.WriteLine($"Error logging in: {ex}");
            LoginFailureMessage = "Server error.";
            LoginStatus = LoginStatus.Failed;
        }

        return authenticatedUser;
    }

    private async Task<AuthenticationState> CreateAuthenticationStateFromSecureStorageAsync()
    {
        var authenticatedUser = _defaultUser;
        LoginStatus = LoginStatus.None;

        if (await UpdateAndValidateAccessTokenAsync())
        {
            authenticatedUser = CreateAuthenticatedUser(_accessToken!.Email);
            LoginStatus = LoginStatus.Success;
        }

        return new AuthenticationState(authenticatedUser);
    }

    private async Task<bool> UpdateAndValidateAccessTokenAsync()
    {
        try
        {
            var now = DateTime.UtcNow;
            var thirtyMinutesFromNow = now.AddMinutes(TokenExpirationBuffer);

            if (_accessToken is null || thirtyMinutesFromNow > _accessToken.AccessTokenExpiration)
            {
                _accessToken = await TokenStorage.GetTokenFromSecureStorageAsync();
            }

            if (_accessToken is null)
            {
                return false;
            }

            // The refresh token expiration is unknown, so we always try to refresh even if the access token expires. It defaults to 14 days.
            // However, we start trying to refresh the access token 30 minutes before it expires to avoid race conditions.
            if (thirtyMinutesFromNow >= _accessToken.AccessTokenExpiration)
            {
                return await RefreshAccessTokenAsync(_accessToken.LoginResponse.RefreshToken, _accessToken.Email);
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking the access token for validity."); 
            Debug.WriteLine($"Error checking token for validity: {ex}");
            return false;
        }
    }

    private async Task<bool> RefreshAccessTokenAsync(string refreshToken, string email)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return false;
        }

        await _refreshLock.WaitAsync();

        try
        {
            if (_refreshInProgress)
            {
                return _accessToken is not null && DateTime.UtcNow.AddMinutes(TokenExpirationBuffer) < _accessToken.AccessTokenExpiration;
            }

            _refreshInProgress = true;

            //Call the Refresh endpoint and pass the refresh token
            var httpClient = HttpClientHelper.GetHttpClient();
            var refreshData = new { refreshToken };
            using var response = await httpClient.PostAsJsonAsync(HttpClientHelper.RefreshUrl, refreshData);
            response.EnsureSuccessStatusCode();
            var token = await response.Content.ReadAsStringAsync();
            _accessToken = await TokenStorage.SaveTokenToSecureStorageAsync(token, email);

            if (_accessToken is null)
            {
                LogoutCore("Your session expired. Sign in again.");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error refreshing the access token.");
            Debug.WriteLine($"Error refreshing access token: {ex}");
            LogoutCore("Your session expired. Sign in again.");
            throw;
        }
        finally
        {
            _refreshInProgress = false;
            _refreshLock.Release();
        }
    }

    private ClaimsPrincipal CreateAuthenticatedUser(string email)
    {
        var claims = new[] { new Claim(ClaimTypes.Name, email) };  //TODO: Add more claims as needed
        var identity = new ClaimsIdentity(claims, AuthenticationType);
        return new ClaimsPrincipal(identity);
    }
}
