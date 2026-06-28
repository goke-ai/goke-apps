using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Goke.Hyb.Web.Services;
using Goke.Core.Models;
using Goke.Services;
using Goke.Services.Authentication;

namespace Goke.Hyb.Web.Endpoints;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/account/login", async (
            [FromForm] BlazorLoginForm form,
            HttpContext httpContext,
            AuthApiClient client) =>
        {
            var returnUrl = string.IsNullOrWhiteSpace(form.ReturnUrl) ? "/" : form.ReturnUrl;
            var loginResponse = await client.LoginAsync(form.Email, form.Password, httpContext.RequestAborted);

            if (loginResponse is null)
            {
                var loginUrl = $"/login?error={Uri.EscapeDataString("Invalid login attempt.")}&returnUrl={Uri.EscapeDataString(returnUrl)}";
                return Results.LocalRedirect(loginUrl);
            }

            //// Build claims from the access token and user information
            //var claims = BuildClaimsFromAccessToken(loginResponse.AccessToken, form.Email);

            // Retrieve the current user information using the access token
            var currentUser = await client.GetCurrentUserAsync(loginResponse.AccessToken, httpContext.RequestAborted);

            if (currentUser is null)
            {
                var loginUrl = $"/login?error={Uri.EscapeDataString("Unable to load user profile.")}&returnUrl={Uri.EscapeDataString(returnUrl)}";
                return Results.LocalRedirect(loginUrl);
            }

            var claims = ClaimBuilder.BuildClaimFomUserInfo(currentUser, form.Email);

            // Create authentication properties with the access token and refresh token
            var authenticationProperties = new AuthenticationProperties
            {
                IsPersistent = form.RememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(loginResponse.ExpiresIn)
            };

            authenticationProperties.StoreTokens(
            [
                new AuthenticationToken { Name = "access_token", Value = loginResponse.AccessToken },
                new AuthenticationToken { Name = "refresh_token", Value = loginResponse.RefreshToken },
                new AuthenticationToken { Name = "token_type", Value = loginResponse.TokenType }
            ]);

            var principal = new ClaimsPrincipal(
                new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

            await httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                authenticationProperties);

            return Results.LocalRedirect(returnUrl);
        });

        endpoints.MapPost("/account/register", async (
            [FromForm] BlazorRegisterForm form,
            HttpContext httpContext,
            AuthApiClient client) =>
        {
            var returnUrl = string.IsNullOrWhiteSpace(form.ReturnUrl) ? "/" : form.ReturnUrl;

            if (string.IsNullOrWhiteSpace(form.Email) || string.IsNullOrWhiteSpace(form.Password))
            {
                var registerUrl = $"/register?error={Uri.EscapeDataString("Email and password are required.")}&returnUrl={Uri.EscapeDataString(returnUrl)}";
                return Results.LocalRedirect(registerUrl);
            }

            if (!string.Equals(form.Password, form.ConfirmPassword, StringComparison.Ordinal))
            {
                var registerUrl = $"/register?error={Uri.EscapeDataString("The password and confirmation password do not match.")}&returnUrl={Uri.EscapeDataString(returnUrl)}";
                return Results.LocalRedirect(registerUrl);
            }

            var registrationError = await client.RegisterAsync(form.Email, form.Password, httpContext.RequestAborted);
            if (!string.IsNullOrWhiteSpace(registrationError))
            {
                var registerUrl = $"/register?error={Uri.EscapeDataString(registrationError)}&returnUrl={Uri.EscapeDataString(returnUrl)}";
                return Results.LocalRedirect(registerUrl);
            }

            var loginUrl = $"/login?status={Uri.EscapeDataString("Registration succeeded. Check your email to confirm your account before signing in.")}&returnUrl={Uri.EscapeDataString(returnUrl)}";
            return Results.LocalRedirect(loginUrl);
        });

        endpoints.MapPost("/account/logout", async ([FromForm] BlazorLogoutForm form, HttpContext httpContext) =>
        {
            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.LocalRedirect(string.IsNullOrWhiteSpace(form.ReturnUrl) ? "/" : form.ReturnUrl);
        }).RequireAuthorization();

        return endpoints;
    }

}
