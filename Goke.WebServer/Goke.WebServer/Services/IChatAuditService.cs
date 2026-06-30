using Goke.Core.Models;
using System.Collections.Concurrent;

namespace Goke.WebServer.Services;



public interface IChatAuditService
{
    event Action<ChatLogEntry>? MessageLogged;

    Task AddAsync(ChatLogEntry entry, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatLogEntry>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatLogEntry>> GetRecentAsync(int take, CancellationToken cancellationToken = default);

}


public sealed class InMemoryChatAuditService : IChatAuditService
{
    private const int MaxEntries = 1000;
    private readonly ConcurrentQueue<ChatLogEntry> entries = new();

    public event Action<ChatLogEntry>? MessageLogged;

    public Task AddAsync(ChatLogEntry entry, CancellationToken cancellationToken = default)
    {
        entries.Enqueue(entry);

        while (entries.Count > MaxEntries && entries.TryDequeue(out _))
        {
        }

        MessageLogged?.Invoke(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ChatLogEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ChatLogEntry> snapshot = entries.ToArray();
        return Task.FromResult(snapshot);
    }

    public Task<IReadOnlyList<ChatLogEntry>> GetRecentAsync(int take, CancellationToken cancellationToken = default)
    {
        var normalizedTake = Math.Clamp(take, 1, 500);

        IReadOnlyList<ChatLogEntry> snapshot = entries
            .Reverse()
            .Take(normalizedTake)
            .Reverse()
            .ToArray();

        return Task.FromResult(snapshot);
    }
}

