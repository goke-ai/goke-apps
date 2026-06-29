using Goke.Core.Models;

namespace Goke.WebServer.Services;



public interface IChatAuditService
{
    Task AddAsync(ChatLogEntry entry);
    Task<IReadOnlyList<ChatLogEntry>> GetAllAsync();
    event Action<ChatLogEntry>? MessageLogged;
}


public class InMemoryChatAuditService : IChatAuditService
{
    private readonly List<ChatLogEntry> _messages = [];
    private readonly object _lock = new();

    public event Action<ChatLogEntry>? MessageLogged;

    public Task AddAsync(ChatLogEntry entry)
    {
        lock (_lock)
        {
            _messages.Add(entry);
        }

        MessageLogged?.Invoke(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ChatLogEntry>> GetAllAsync()
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<ChatLogEntry>>(_messages.ToList());
        }
    }
}

