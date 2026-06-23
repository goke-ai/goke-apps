using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Goke.Web.Services;

public sealed class SeedConfirmationService
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<string, SeedConfirmationEntry> entries = new();

    public SeedConfirmationResult IssueCode(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var entry = new SeedConfirmationEntry(email, code, DateTimeOffset.UtcNow.Add(CodeLifetime));
        entries.AddOrUpdate(email, entry, static (_, newEntry) => newEntry);

        return new SeedConfirmationResult(code, entry.ExpiresAtUtc);
    }

    public SeedCodeValidationResult ValidateCode(string email, string? code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        if (string.IsNullOrWhiteSpace(code))
        {
            return SeedCodeValidationResult.Failure("Enter the confirmation code.");
        }

        if (!entries.TryGetValue(email, out var entry))
        {
            return SeedCodeValidationResult.Failure("Request a new confirmation code.");
        }

        if (!string.Equals(entry.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            entries.TryRemove(email, out _);
            return SeedCodeValidationResult.Failure("Request a new confirmation code.");
        }

        if (entry.ExpiresAtUtc < DateTimeOffset.UtcNow)
        {
            entries.TryRemove(email, out _);
            return SeedCodeValidationResult.Failure("The confirmation code has expired. Request a new one.");
        }

        if (!string.Equals(entry.Code, code.Trim(), StringComparison.Ordinal))
        {
            return SeedCodeValidationResult.Failure("The confirmation code is invalid.");
        }

        entries.TryRemove(email, out _);
        return SeedCodeValidationResult.Success();
    }

    private sealed record SeedConfirmationEntry(string Email, string Code, DateTimeOffset ExpiresAtUtc);
}

public sealed record SeedConfirmationResult(string Code, DateTimeOffset ExpiresAtUtc);

public sealed record SeedCodeValidationResult(bool Succeeded, string? ErrorMessage)
{
    public static SeedCodeValidationResult Success() => new(true, null);

    public static SeedCodeValidationResult Failure(string errorMessage) => new(false, errorMessage);
}
