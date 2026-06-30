using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using Goke.WebServer.Services;
using Goke.Core.Models;

namespace Goke.WebServer.Hubs;



public class ChatHub(IChatAuditService auditService) : Hub
{
    private static readonly ConcurrentDictionary<string, string> ConnectionAliases = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, string> AliasToConnection = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, HashSet<string>> GroupMembers = new();
    private static readonly ConcurrentDictionary<string, ConnectedClientInfo> ConnectedClients = new(StringComparer.Ordinal);

    public override async Task OnConnectedAsync()
    {
        var sender = GetSender();

        ConnectedClients[Context.ConnectionId] = CreateConnectedClientInfo(sender);

        await Clients.Caller.SendAsync("ConnectionIdAssigned", Context.ConnectionId);
        // log the connection event
        await auditService.AddAsync(new ChatLogEntry
        {
            Type = "System",
            FromConnectionId = Context.ConnectionId,
            FromUserId = sender.Id,
            FromIsAuthenticated = sender.IsAuthenticated,
            DisplayName = sender.DisplayName,
            Message = $"Sender '{sender.DisplayName}' connected.",
            SentUtc = DateTime.UtcNow
        });

        await Clients.Caller.SendAsync("SenderAssigned", sender.Id, sender.DisplayName, sender.IsAuthenticated);
        // log the sender assignment event
        await auditService.AddAsync(new ChatLogEntry
        {
            Type = "System",
            FromConnectionId = Context.ConnectionId,
            FromUserId = sender.Id,
            FromIsAuthenticated = sender.IsAuthenticated,
            DisplayName = sender.DisplayName,
            Message = $"Sender '{sender.DisplayName}' assigned.",
            SentUtc = DateTime.UtcNow
        });

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // log the disconnection event
        var sender = GetSender();

        ConnectedClients.TryRemove(Context.ConnectionId, out _);

        if (ConnectionAliases.TryRemove(Context.ConnectionId, out var alias))
        {
            AliasToConnection.TryRemove(alias, out _);
        }
        // log the alias removal event
        await auditService.AddAsync(new ChatLogEntry
        {
            Type = "System",
            FromConnectionId = Context.ConnectionId,
            FromUserId = sender.Id,
            FromIsAuthenticated = sender.IsAuthenticated,
            DisplayName = sender.DisplayName,
            Message = $"Alias '{alias}' removed for sender '{sender.DisplayName}'.",
            SentUtc = DateTime.UtcNow
        });

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SetAlias(string alias)
    {
        var normalizedAlias = alias?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedAlias))
        {
            throw new HubException("Alias is required.");
        }

        if (AliasToConnection.TryGetValue(normalizedAlias, out var existingConnectionId) &&
            !string.Equals(existingConnectionId, Context.ConnectionId, StringComparison.Ordinal))
        {
            throw new HubException("Alias is already in use.");
        }

        if (ConnectionAliases.TryGetValue(Context.ConnectionId, out var oldAlias))
        {
            AliasToConnection.TryRemove(oldAlias, out _);
        }

        ConnectionAliases[Context.ConnectionId] = normalizedAlias;
        AliasToConnection[normalizedAlias] = Context.ConnectionId;

        var sender = GetSender();
        ConnectedClients[Context.ConnectionId] = CreateConnectedClientInfo(sender);

        await auditService.AddAsync(new ChatLogEntry
        {
            Type = "System",
            FromConnectionId = Context.ConnectionId,
            FromUserId = sender.IsAuthenticated ? sender.Id : null,
            FromIsAuthenticated = sender.IsAuthenticated,
            DisplayName = sender.DisplayName,
            Message = $"Alias set to '{normalizedAlias}'.",
            SentUtc = DateTime.UtcNow
        });

        await Clients.Caller.SendAsync("SenderAssigned", sender.Id, sender.DisplayName, sender.IsAuthenticated);
    }

    [Authorize]
    public Task<IReadOnlyList<ChatUserInfo>> GetAvailableUsers()
    {
        var users = ConnectedClients.Values
            .Where(x =>
                x.IsAuthenticated &&
                !string.Equals(x.SenderId, Context.UserIdentifier, StringComparison.Ordinal))
            .GroupBy(x => x.SenderId)
            .Select(group => group
                .OrderByDescending(x => !string.IsNullOrWhiteSpace(x.Alias))
                .ThenBy(x => x.DisplayName)
                .First())
            .OrderBy(x => x.DisplayName)
            .Select(x => new ChatUserInfo
            {
                UserId = x.SenderId,
                DisplayName = x.DisplayName
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<ChatUserInfo>>(users);
    }

    public Task<IReadOnlyList<ConnectedClientInfo>> GetConnectedClients()
    {
        var clients = ConnectedClients.Values
            .Where(x => !string.Equals(x.ConnectionId, Context.ConnectionId, StringComparison.Ordinal))
            .OrderBy(x => x.DisplayName)
            .ThenBy(x => x.ConnectionId)
            .ToList();

        return Task.FromResult<IReadOnlyList<ConnectedClientInfo>>(clients);
    }

    public Task<IReadOnlyList<AliasInfo>> GetAvailableAliases()
    {
        var aliases = ConnectedClients.Values
            .Where(x =>
                !string.Equals(x.ConnectionId, Context.ConnectionId, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(x.Alias))
            .OrderBy(x => x.Alias)
            .Select(x => new AliasInfo
            {
                Alias = x.Alias!,
                ConnectionId = x.ConnectionId,
                SenderId = x.SenderId,
                DisplayName = x.DisplayName,
                IsAuthenticated = x.IsAuthenticated
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<AliasInfo>>(aliases);
    }

    private ConnectedClientInfo CreateConnectedClientInfo(ChatSender sender)
    {
        ConnectionAliases.TryGetValue(Context.ConnectionId, out var alias);

        return new ConnectedClientInfo
        {
            ConnectionId = Context.ConnectionId,
            SenderId = sender.Id,
            DisplayName = sender.DisplayName,
            Alias = alias,
            IsAuthenticated = sender.IsAuthenticated
        };
    }

    public async Task<IReadOnlyList<ChatLogEntry>> GetRecentPublicMessages(int take = 50)
    {
        var entries = await auditService.GetRecentAsync(NormalizeTake(take));

        return [.. entries
            .Where(x => x.Type == "Public")
            .OrderBy(x => x.SentUtc)];
    }

    public async Task<IReadOnlyList<ChatLogEntry>> GetRecentGroupMessages(string groupName, int take = 50)
    {
        var normalizedGroupName = groupName?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedGroupName))
        {
            return [];
        }

        var entries = await auditService.GetRecentAsync(500);

        return entries
            .Where(x => x.Type == "Group" && string.Equals(x.GroupName, normalizedGroupName, StringComparison.Ordinal))
            .OrderByDescending(x => x.SentUtc)
            .Take(NormalizeTake(take))
            .OrderBy(x => x.SentUtc)
            .ToList();
    }

    [Authorize]
    public async Task<IReadOnlyList<ChatLogEntry>> GetRecentPrivateMessages(int take = 50)
    {
        var sender = RequireSender();
        var entries = await auditService.GetRecentAsync(500);

        return entries
            .Where(x =>
                x.Type == "Private" &&
                (string.Equals(x.FromUserId, sender.Id, StringComparison.Ordinal) ||
                 string.Equals(x.ToUserId, sender.Id, StringComparison.Ordinal)))
            .OrderByDescending(x => x.SentUtc)
            .Take(NormalizeTake(take))
            .OrderBy(x => x.SentUtc)
            .ToList();
    }

    public async Task<IReadOnlyList<ChatLogEntry>> GetRecentConnectionPrivateMessages(int take = 50)
    {
        var entries = await auditService.GetRecentAsync(500);

        return entries
            .Where(x =>
                x.Type == "Private" &&
                (string.Equals(x.FromConnectionId, Context.ConnectionId, StringComparison.Ordinal) ||
                 string.Equals(x.ToConnectionId, Context.ConnectionId, StringComparison.Ordinal)))
            .OrderByDescending(x => x.SentUtc)
            .Take(NormalizeTake(take))
            .OrderBy(x => x.SentUtc)
            .ToList();
    }

    [Authorize]
    public async Task<IReadOnlyList<ChatLogEntry>> GetRecentPrivateGroupMessages(string groupName, int take = 50)
    {
        var sender = RequireSender();
        var normalizedGroupName = groupName?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedGroupName))
        {
            return [];
        }

        if (!IsGroupMember(normalizedGroupName, sender.Id))
        {
            return [];
            //throw new HubException("Not allowed to read this private group history.");
        }

        var entries = await auditService.GetRecentAsync(500);

        return entries
            .Where(x =>
                x.Type == "Group" &&
                string.Equals(x.GroupName, normalizedGroupName, StringComparison.Ordinal))
            .OrderByDescending(x => x.SentUtc)
            .Take(NormalizeTake(take))
            .OrderBy(x => x.SentUtc)
            .ToList();
    }

    private static int NormalizeTake(int take) => Math.Clamp(take, 1, 100);
    private static string NormalizedMessage(string message)
    {
        var normalizedMessage = message?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedMessage))
        {
            throw new HubException("Message is required.");
        }

        return normalizedMessage;
    }

    public async Task SendPublicMessage(string message)
    {
        var sender = GetSender();
        var normalizedMessage = NormalizedMessage(message);

        await Clients.All.SendAsync(
            "ReceivePublicMessage",
            Context.ConnectionId,
            sender.Id,
            sender.DisplayName,
            normalizedMessage);

        await auditService.AddAsync(new ChatLogEntry
        {
            Type = "Public",
            FromConnectionId = Context.ConnectionId,
            FromUserId = sender.Id,
            FromIsAuthenticated = sender.IsAuthenticated,
            DisplayName = sender.DisplayName,
            Message = normalizedMessage,
            SentUtc = DateTime.UtcNow
        });
    }

    public async Task SendPrivateToAlias(string alias, string message)
    {
        var sender = GetSender();
        var normalizedAlias = alias?.Trim();
        var normalizedMessage = NormalizedMessage(message);

        if (string.IsNullOrWhiteSpace(normalizedAlias))
        {
            throw new HubException("Alias is required.");
        }

        if (!AliasToConnection.TryGetValue(normalizedAlias, out var targetConnectionId))
        {
            throw new HubException("Alias not found.");
        }

        await Clients.Client(targetConnectionId).SendAsync(
            "ReceivePrivateMessage",
            Context.ConnectionId,
            sender.Id,
            sender.DisplayName,
            normalizedMessage);

        await Clients.Caller.SendAsync(
            "ReceivePrivateMessage",
            Context.ConnectionId,
            sender.Id,
            sender.DisplayName,
            normalizedMessage);

        await auditService.AddAsync(new ChatLogEntry
        {
            Type = "Private",
            FromConnectionId = Context.ConnectionId,
            FromUserId = sender.IsAuthenticated ? sender.Id : null,
            FromIsAuthenticated = sender.IsAuthenticated,
            ToConnectionId = targetConnectionId,
            DisplayName = sender.DisplayName,
            Message = normalizedMessage,
            SentUtc = DateTime.UtcNow
        });
    }

    [Authorize]
    public async Task SendPrivateToUser(string toUserId, string message)
    {
        var sender = GetSender();
        string? normalizedMessage = NormalizedMessage(message);

        await Clients.User(toUserId).SendAsync(
            "ReceivePrivateMessage",
            Context.ConnectionId,
            sender.Id,
            sender.DisplayName,
            normalizedMessage);

        await Clients.Caller.SendAsync(
            "ReceivePrivateMessage",
            Context.ConnectionId,
            sender.Id,
            sender.DisplayName,
            normalizedMessage);

        await auditService.AddAsync(new ChatLogEntry
        {
            Type = "Private",
            FromConnectionId = Context.ConnectionId,
            FromUserId = sender.Id,
            FromIsAuthenticated = sender.IsAuthenticated,
            ToUserId = toUserId,
            DisplayName = sender.DisplayName,
            Message = normalizedMessage,
            SentUtc = DateTime.UtcNow
        });

    }


    public async Task SendPrivateToConnection(string toConnectionId, string message)
    {
        var sender = GetSender();
        var normalizedMessage = NormalizedMessage(message);

        await Clients.Client(toConnectionId).SendAsync(
            "ReceivePrivateMessage",
            Context.ConnectionId,
            sender.Id,
            sender.DisplayName,
            normalizedMessage);

        await Clients.Caller.SendAsync(
            "ReceivePrivateMessage",
            Context.ConnectionId,
            sender.Id,
            sender.DisplayName,
            normalizedMessage);

        await auditService.AddAsync(new ChatLogEntry
        {
            Type = "Private",
            FromConnectionId = Context.ConnectionId,
            FromUserId = sender.Id,
            FromIsAuthenticated = sender.IsAuthenticated,
            ToConnectionId = toConnectionId,
            DisplayName = sender.DisplayName,
            Message = normalizedMessage,
            SentUtc = DateTime.UtcNow
        });
    }


    public async Task JoinPublicGroup(string groupName)
    {
        var sender = GetSender();

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        await Clients.Group(groupName).SendAsync(
            "SystemMessage",
            $"{sender.DisplayName} joined '{groupName}'.");

        await auditService.AddAsync(new ChatLogEntry
        {
            Type = "System",
            FromConnectionId = Context.ConnectionId,
            FromUserId = sender.Id,
            FromIsAuthenticated = sender.IsAuthenticated,
            GroupName = groupName,
            DisplayName = sender.DisplayName,
            Message = $"Joined group '{groupName}'.",
            SentUtc = DateTime.UtcNow
        });
    }

    public async Task LeavePublicGroup(string groupName)
    {
        var sender = GetSender();

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        await Clients.Group(groupName).SendAsync(
            "SystemMessage",
            $"{sender.DisplayName} left '{groupName}'.");

        await auditService.AddAsync(new ChatLogEntry
        {
            Type = "System",
            FromConnectionId = Context.ConnectionId,
            FromUserId = sender.Id,
            FromIsAuthenticated = sender.IsAuthenticated,
            GroupName = groupName,
            DisplayName = sender.DisplayName,
            Message = $"Left group '{groupName}'.",
            SentUtc = DateTime.UtcNow
        });
    }

    public async Task SendGroupMessage(string groupName, string message)
    {
        var sender = GetSender();
        var normalizedMessage = NormalizedMessage(message);

        await Clients.Group(groupName).SendAsync(
            "ReceiveGroupMessage",
            groupName,
            Context.ConnectionId,
            sender.Id,
            sender.DisplayName,
            normalizedMessage);

        await auditService.AddAsync(new ChatLogEntry
        {
            Type = "Group",
            FromConnectionId = Context.ConnectionId,
            FromUserId = sender.Id,
            FromIsAuthenticated = sender.IsAuthenticated,
            GroupName = groupName,
            DisplayName = sender.DisplayName,
            Message = normalizedMessage,
            SentUtc = DateTime.UtcNow
        });
    }

    [Authorize]
    public async Task CreatePrivateGroup(string groupName)
    {
        var sender = RequireSender();

        var members = GroupMembers.GetOrAdd(groupName, _ => new HashSet<string>());
        lock (members)
        {
            members.Add(sender.Id);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        await Clients.Caller.SendAsync("PrivateGroupCreated", groupName);
        // log the private group creation event
        await auditService.AddAsync(new ChatLogEntry
        {
            Type = "System",
            FromConnectionId = Context.ConnectionId,
            FromUserId = sender.Id,
            FromIsAuthenticated = sender.IsAuthenticated,
            GroupName = groupName,
            DisplayName = sender.DisplayName,
            Message = $"Private group '{groupName}' created.",
            SentUtc = DateTime.UtcNow
        });
    }

    [Authorize]
    public async Task AddUserToPrivateGroup(string groupName, string targetUserId)
    {
        var sender = RequireSender();

        if (!IsGroupMember(groupName, sender.Id))
        {
            throw new HubException("Only group members can add users.");
        }

        var members = GroupMembers.GetOrAdd(groupName, _ => new HashSet<string>());
        lock (members)
        {
            members.Add(targetUserId);
        }

        await Clients.User(targetUserId).SendAsync(
            "AddedToPrivateGroup",
            groupName,
            sender.Id,
            sender.DisplayName);

        // log the add user to private group event
        await auditService.AddAsync(new ChatLogEntry
        {
            Type = "System",
            FromConnectionId = Context.ConnectionId,
            FromUserId = sender.Id,
            FromIsAuthenticated = sender.IsAuthenticated,
            GroupName = groupName,
            DisplayName = sender.DisplayName,
            Message = $"User '{targetUserId}' added to private group '{groupName}'.",
            SentUtc = DateTime.UtcNow
        });
    }

    [Authorize]
    public async Task JoinPrivateGroup(string groupName)
    {
        var sender = RequireSender();

        if (!IsGroupMember(groupName, sender.Id))
        {
            throw new HubException("Not allowed to join this private group.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        await Clients.Group(groupName).SendAsync(
            "SystemMessage",
            $"{sender.DisplayName} joined private group '{groupName}'.");

        // log the join private group event
        await auditService.AddAsync(new ChatLogEntry
        {
            Type = "System",
            FromConnectionId = Context.ConnectionId,
            FromUserId = sender.Id,
            FromIsAuthenticated = sender.IsAuthenticated,
            GroupName = groupName,
            DisplayName = sender.DisplayName,
            Message = $"Joined private group '{groupName}'.",
            SentUtc = DateTime.UtcNow
        });
    }

    [Authorize]
    public async Task SendPrivateGroupMessage(string groupName, string message)
    {
        var sender = RequireSender();
        var normalizedMessage = NormalizedMessage(message);

        if (!IsGroupMember(groupName, sender.Id))
        {
            throw new HubException("Not allowed to send to this private group.");
        }

        await Clients.Group(groupName).SendAsync(
            "ReceivePrivateGroupMessage",
            groupName,
            Context.ConnectionId,
            sender.Id,
            sender.DisplayName,
            normalizedMessage);

        await auditService.AddAsync(new ChatLogEntry
        {
            Type = "Group",
            FromConnectionId = Context.ConnectionId,
            FromUserId = sender.Id,
            FromIsAuthenticated = sender.IsAuthenticated,
            GroupName = groupName,
            DisplayName = sender.DisplayName,
            Message = normalizedMessage,
            SentUtc = DateTime.UtcNow
        });
    }

    private ChatSender GetSender()
    {
        var explicitAlias = ConnectionAliases.TryGetValue(Context.ConnectionId, out var alias)
            ? alias
            : null;

        var isAuthenticated = Context.User?.Identity?.IsAuthenticated == true;
        var userId = Context.UserIdentifier;

        if (isAuthenticated && !string.IsNullOrWhiteSpace(userId))
        {
            var displayName =
                explicitAlias
                ?? Context.User?.Identity?.Name
                ?? Context.User?.FindFirst("name")?.Value
                ?? userId;

            return new ChatSender(userId, displayName, true);
        }

        var guestId = $"guest:{Context.ConnectionId}";
        var guestName = explicitAlias ?? guestId;

        return new ChatSender(guestId, guestName, false);
    }

    private ChatSender RequireSender()
    {
        var sender = GetSender();

        if (!sender.IsAuthenticated)
        {
            throw new HubException("Authentication is required.");
        }

        return sender;
    }

    private static bool IsGroupMember(string groupName, string userId)
    {
        if (!GroupMembers.TryGetValue(groupName, out var members))
        {
            return false;
        }

        lock (members)
        {
            return members.Contains(userId);
        }
    }

    private sealed record ChatSender(string Id, string DisplayName, bool IsAuthenticated);





    //private static readonly ConcurrentDictionary<string, HashSet<string>> UserConnections = new();
    ////private static readonly ConcurrentDictionary<string, GroupInvite> PendingInvites = new();
    ////private static readonly ConcurrentDictionary<string, HashSet<string>> GroupMembers = new();

    //public override async Task OnConnectedAsync()
    //{
    //    var userId = Context.UserIdentifier;

    //    await RegisterUser(userId);

    //    await Clients.Caller.SendAsync("ConnectionIdAssigned", Context.ConnectionId);

    //    // log the connection event
    //    var logEntry = new ChatLogEntry
    //    {
    //        Type = "System",
    //        FromConnectionId = Context.ConnectionId,
    //        Message = $"User '{userId}' connected.",
    //        SentUtc = DateTime.UtcNow
    //    };
    //    await auditService.AddAsync(logEntry);

    //    await base.OnConnectedAsync();
    //}

    //public async Task RegisterUser(string? userId)
    //{
    //    if (!string.IsNullOrWhiteSpace(userId))
    //    {
    //        var connections = UserConnections.GetOrAdd(userId, _ => new HashSet<string>());
    //        lock (connections)
    //        {
    //            connections.Add(Context.ConnectionId);
    //        }

    //        await Clients.Caller.SendAsync("UserIdAssigned", userId, Context.ConnectionId);

    //        // log the registration event
    //        var logEntry = new ChatLogEntry
    //        {
    //            Type = "System",
    //            FromConnectionId = Context.ConnectionId,
    //            Message = $"User '{userId}' registered with connection ID '{Context.ConnectionId}'.",
    //            SentUtc = DateTime.UtcNow
    //        }; 
    //        await auditService.AddAsync(logEntry);
    //    }
    //}

    //public override async Task OnDisconnectedAsync(Exception? exception)
    //{
    //    var userId = Context.UserIdentifier;
    //    if (!string.IsNullOrWhiteSpace(userId) &&
    //        UserConnections.TryGetValue(userId, out var connections))
    //    {
    //        lock (connections)
    //        {
    //            connections.Remove(Context.ConnectionId);
    //            // message the user that they have been disconnected
    //            Clients.Caller.SendAsync("SystemMessage", $"You have been disconnected from connection ID '{Context.ConnectionId}'.");

    //            // log the disconnection event
    //            var logEntry = new ChatLogEntry
    //            {
    //                Type = "System",
    //                FromConnectionId = Context.ConnectionId,
    //                Message = $"User '{userId}' disconnected from connection ID '{Context.ConnectionId}'.",
    //                SentUtc = DateTime.UtcNow
    //            };
    //            auditService.AddAsync(logEntry).Wait();

    //            // If the user has no more active connections, remove them from the dictionary
    //            if (connections.Count == 0)
    //            {
    //                UserConnections.TryRemove(userId, out _);
    //            }
    //        }
    //    }

    //    // log the disconnection event
    //    await auditService.AddAsync(new ChatLogEntry
    //    {
    //        Type = "System",
    //        FromConnectionId = Context.ConnectionId,
    //        Message = $"User '{userId}' disconnected.",
    //        SentUtc = DateTime.UtcNow
    //    });

    //    await base.OnDisconnectedAsync(exception);
    //}


    //public async Task SendMessage(string user, string message)
    //{
    //    await Clients.All
    //        .SendAsync("ReceiveMessage", Context.ConnectionId, user, message);

    //    // log the public message event
    //    var logEntry = new ChatLogEntry
    //    {
    //        Type = "Public",
    //        FromConnectionId = Context.ConnectionId,
    //        DisplayName = user,
    //        Message = message,
    //        SentUtc = DateTime.UtcNow
    //    };
    //    await auditService.AddAsync(logEntry);
    //}

    //public async Task SendPrivateMessage(string user, string toUser, string message)
    //{
    //    if (UserConnections.TryGetValue(toUser, out var connections))
    //    {
    //        List<string> snapshot;
    //        lock (connections)
    //        {
    //            snapshot = connections.ToList();
    //        }
    //        // send message to all their active connections
    //        foreach (var connectionId in snapshot)
    //        {
    //            await Clients.Client(connectionId)
    //                .SendAsync("ReceivePrivateMessage", Context.ConnectionId, user, message);

    //            // log the private message event
    //            var logEntry = new ChatLogEntry
    //            {
    //                Type = "Private",
    //                FromConnectionId = Context.ConnectionId,
    //                ToConnectionId = connectionId,
    //                DisplayName = user,
    //                Message = message,
    //                SentUtc = DateTime.UtcNow
    //            };
    //            await auditService.AddAsync(logEntry);
    //        }
    //    }        
    //}

    //public async Task SendGroupMessage(string groupName, string? user, string message)
    //{
    //    await Clients.Group(groupName)
    //        .SendAsync("ReceiveGroupMessage", groupName, Context.ConnectionId, user, message);

    //    // log the group message event
    //    var logEntry = new ChatLogEntry
    //    {
    //        Type = "Group",
    //        FromConnectionId = Context.ConnectionId,
    //        GroupName = groupName,
    //        DisplayName = user,
    //        Message = message,
    //        SentUtc = DateTime.UtcNow
    //    };
    //    await auditService.AddAsync(logEntry);
    //}

    //public async Task JoinGroup(string user, string groupName)
    //{
    //    await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

    //    await Clients.Group(groupName)
    //        .SendAsync("SystemMessage", $"{Context.ConnectionId} - {user}: has joined the group {groupName}.");

    //    // log the join group event
    //    var logEntry = new ChatLogEntry
    //    {
    //        Type = "System",
    //        FromConnectionId = Context.ConnectionId,
    //        GroupName = groupName,
    //        DisplayName = user,
    //        Message = $"User '{user}' joined the group '{groupName}'.",
    //        SentUtc = DateTime.UtcNow
    //    };
    //    await auditService.AddAsync(logEntry);
    //}

    //public async Task LeaveGroup(string user, string groupName)
    //{
    //    await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

    //    await Clients.Group(groupName)
    //        .SendAsync("SystemMessage", $"{Context.ConnectionId} - {user}: has left the group {groupName}.");

    //    // log the leave group event
    //    var logEntry = new ChatLogEntry
    //    {
    //        Type = "System",
    //        FromConnectionId = Context.ConnectionId,
    //        GroupName = groupName,
    //        DisplayName = user,
    //        Message = $"User '{user}' left the group '{groupName}'.",
    //        SentUtc = DateTime.UtcNow
    //    };
    //    await auditService.AddAsync(logEntry);
    //}

    //public async Task AddUserToGroup(string user, string targetUserId, string groupName)
    //{
    //    if (UserConnections.TryGetValue(targetUserId, out var connections))
    //    {
    //        List<string> snapshot;
    //        lock (connections)
    //        {
    //            snapshot = connections.ToList();
    //        }
    //        // Add the target user to the group for all their active connections
    //        foreach (var connectionId in snapshot)
    //        {
    //            await Groups.AddToGroupAsync(connectionId, groupName);
    //        }
    //        await Clients.User(targetUserId)
    //            .SendAsync("SystemMessage", $"You were added to '{groupName}' by {Context.ConnectionId} - {user}.");

    //        // log the add user to group event
    //        var logEntry = new ChatLogEntry
    //        {
    //            Type = "System",
    //            FromConnectionId = Context.ConnectionId,
    //            GroupName = groupName,
    //            DisplayName = user,
    //            Message = $"User '{user}' added '{targetUserId}' to the group '{groupName}'.",
    //            SentUtc = DateTime.UtcNow
    //        };
    //        await auditService.AddAsync(logEntry);
    //    }
    //}



    //// Authenticated group management methods
    //public async Task CreateGroup(string groupName)
    //{
    //    var userId = RequireUserId();

    //    var members = GroupMembers.GetOrAdd(groupName, _ => new HashSet<string>());
    //    lock (members)
    //    {
    //        members.Add(userId);
    //    }

    //    await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

    //    await Clients.Caller.SendAsync("SystemMessage", $"Group '{groupName}' created.");
    //}

    //public async Task InviteUserToGroup(string groupName, string targetUserId)
    //{
    //    var inviterUserId = RequireUserId();

    //    if (!IsGroupMember(groupName, inviterUserId))
    //    {
    //        throw new HubException("Only group members can invite users.");
    //    }

    //    var invite = new GroupInvite
    //    {
    //        InviteId = Guid.NewGuid().ToString("N"),
    //        GroupName = groupName,
    //        InvitedByUserId = inviterUserId,
    //        InvitedUserId = targetUserId,
    //        CreatedUtc = DateTime.UtcNow
    //    };

    //    PendingInvites[invite.InviteId] = invite;

    //    await Clients.User(targetUserId).SendAsync(
    //        "GroupInviteReceived",
    //        invite.InviteId,
    //        invite.GroupName,
    //        invite.InvitedByUserId);

    //    await Clients.Caller.SendAsync("SystemMessage", $"Invite sent to '{targetUserId}'.");
    //}

    //public async Task AcceptGroupInvite(string inviteId)
    //{
    //    var currentUserId = RequireUserId();

    //    if (!PendingInvites.TryRemove(inviteId, out var invite))
    //    {
    //        throw new HubException("Invite not found or already handled.");
    //    }

    //    if (!string.Equals(invite.InvitedUserId, currentUserId, StringComparison.Ordinal))
    //    {
    //        throw new HubException("This invite does not belong to the current user.");
    //    }

    //    var members = GroupMembers.GetOrAdd(invite.GroupName, _ => new HashSet<string>());
    //    lock (members)
    //    {
    //        members.Add(currentUserId);
    //    }

    //    if (UserConnections.TryGetValue(currentUserId, out var connections))
    //    {
    //        List<string> snapshot;
    //        lock (connections)
    //        {
    //            snapshot = connections.ToList();
    //        }

    //        foreach (var connectionId in snapshot)
    //        {
    //            await Groups.AddToGroupAsync(connectionId, invite.GroupName);
    //        }
    //    }

    //    await Clients.Group(invite.GroupName).SendAsync(
    //        "SystemMessage",
    //        $"{currentUserId} joined '{invite.GroupName}'.");
    //}

    //public Task RejectGroupInvite(string inviteId)
    //{
    //    var currentUserId = RequireUserId();

    //    if (PendingInvites.TryGetValue(inviteId, out var invite) &&
    //        string.Equals(invite.InvitedUserId, currentUserId, StringComparison.Ordinal))
    //    {
    //        PendingInvites.TryRemove(inviteId, out _);
    //    }

    //    return Task.CompletedTask;
    //}

    //public async Task SendGroupMessage(string groupName, string message)
    //{
    //    var userId = RequireUserId();

    //    if (!IsGroupMember(groupName, userId))
    //    {
    //        throw new HubException("User is not a member of this group.");
    //    }

    //    await Clients.Group(groupName).SendAsync(
    //        "ReceiveGroupMessage",
    //        groupName,
    //        userId,
    //        message);
    //}

    //private string RequireUserId()
    //{
    //    return Context.UserIdentifier
    //        ?? throw new HubException("Authenticated user ID is missing.");
    //}

    //private static bool IsGroupMember(string groupName, string userId)
    //{
    //    if (!GroupMembers.TryGetValue(groupName, out var members))
    //    {
    //        return false;
    //    }

    //    lock (members)
    //    {
    //        return members.Contains(userId);
    //    }
    //}

    //private sealed class GroupInvite
    //{
    //    public string InviteId { get; set; } = default!;
    //    public string GroupName { get; set; } = default!;
    //    public string InvitedByUserId { get; set; } = default!;
    //    public string InvitedUserId { get; set; } = default!;
    //    public DateTime CreatedUtc { get; set; }
    //}
}





