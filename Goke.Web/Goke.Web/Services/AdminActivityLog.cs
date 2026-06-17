using System.Collections.Concurrent;

namespace Goke.Web.Services;

public sealed class AdminActivityLog
{
    private readonly ConcurrentQueue<AdminActivityEntry> entries = new();
    private const int MaxEntries = 100;

    public IReadOnlyList<AdminActivityEntry> GetRecentEntries() => entries.ToArray();

    public void Add(string action, string details)
    {
        entries.Enqueue(new AdminActivityEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Action = action,
            Details = details
        });

        while (entries.Count > MaxEntries && entries.TryDequeue(out _))
        {
        }
    }
}

public sealed class AdminActivityEntry
{
    public required DateTimeOffset Timestamp { get; init; }

    public required string Action { get; init; }

    public required string Details { get; init; }
}
