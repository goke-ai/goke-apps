using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Goke.Core.Models;

namespace Goke.Hyb.Web.Services;

public sealed class RemoteAuthenticationService(HttpClient httpClient, ILogger<RemoteAuthenticationService> logger)
{
    public async Task<LoginResponse?> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync("identity/login", new LoginRequest
            {
                Email = email,
                Password = password
            }, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Remote login failed for {Email} with status code {StatusCode}.", email, response.StatusCode);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: cancellationToken);
            return result;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Remote login request failed for {Email}.", email);
            return null;
        }
    }

    public async Task<string?> RegisterAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync("identity/register", new LoginRequest
            {
                Email = email,
                Password = password
            }, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return null;
            }

            var errorMessage = await ExtractErrorMessageAsync(response, cancellationToken);
            logger.LogWarning("Remote registration failed for {Email} with status code {StatusCode}.", email, response.StatusCode);
            return errorMessage;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Remote registration request failed for {Email}.", email);
            return "Server error.";
        }
    }

    private static async Task<string> ExtractErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return "Registration failed. Please try again.";
        }

        try
        {
            using var document = JsonDocument.Parse(content);

            if (document.RootElement.TryGetProperty("detail", out var detailElement) &&
                detailElement.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(detailElement.GetString()))
            {
                return detailElement.GetString()!;
            }

            if (document.RootElement.TryGetProperty("title", out var titleElement) &&
                titleElement.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(titleElement.GetString()))
            {
                return titleElement.GetString()!;
            }

            if (document.RootElement.TryGetProperty("errors", out var errorsElement) &&
                errorsElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var error in errorsElement.EnumerateObject())
                {
                    if (error.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var message in error.Value.EnumerateArray())
                        {
                            if (message.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(message.GetString()))
                            {
                                return message.GetString()!;
                            }
                        }
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Fall back to the raw response body when it is not JSON.
        }

        return content;
    }
}
