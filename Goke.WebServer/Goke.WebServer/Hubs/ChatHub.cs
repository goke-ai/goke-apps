using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using Goke.WebServer.Services;
using Goke.Core.Models;

namespace Goke.WebServer.Hubs;

public class ChatHub(IChatAuditService auditService) : Hub
{
    private static readonly ConcurrentDictionary<string, HashSet<string>> UserConnections = new();
    //private static readonly ConcurrentDictionary<string, GroupInvite> PendingInvites = new();
    //private static readonly ConcurrentDictionary<string, HashSet<string>> GroupMembers = new();

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;

        await RegisterUser(userId);

        await Clients.Caller.SendAsync("ConnectionIdAssigned", Context.ConnectionId);

        // log the connection event
        var logEntry = new ChatLogEntry
        {
            Type = "System",
            FromConnectionId = Context.ConnectionId,
            Message = $"User '{userId}' connected.",
            SentUtc = DateTime.UtcNow
        };
        await auditService.AddAsync(logEntry);

        await base.OnConnectedAsync();
    }

    public async Task RegisterUser(string? userId)
    {
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var connections = UserConnections.GetOrAdd(userId, _ => new HashSet<string>());
            lock (connections)
            {
                connections.Add(Context.ConnectionId);
            }

            await Clients.Caller.SendAsync("UserIdAssigned", userId, Context.ConnectionId);

            // log the registration event
            var logEntry = new ChatLogEntry
            {
                Type = "System",
                FromConnectionId = Context.ConnectionId,
                Message = $"User '{userId}' registered with connection ID '{Context.ConnectionId}'.",
                SentUtc = DateTime.UtcNow
            }; 
            await auditService.AddAsync(logEntry);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrWhiteSpace(userId) &&
            UserConnections.TryGetValue(userId, out var connections))
        {
            lock (connections)
            {
                connections.Remove(Context.ConnectionId);
                // message the user that they have been disconnected
                Clients.Caller.SendAsync("SystemMessage", $"You have been disconnected from connection ID '{Context.ConnectionId}'.");

                // log the disconnection event
                var logEntry = new ChatLogEntry
                {
                    Type = "System",
                    FromConnectionId = Context.ConnectionId,
                    Message = $"User '{userId}' disconnected from connection ID '{Context.ConnectionId}'.",
                    SentUtc = DateTime.UtcNow
                };
                auditService.AddAsync(logEntry).Wait();

                // If the user has no more active connections, remove them from the dictionary
                if (connections.Count == 0)
                {
                    UserConnections.TryRemove(userId, out _);
                }
            }
        }

        // log the disconnection event
        await auditService.AddAsync(new ChatLogEntry
        {
            Type = "System",
            FromConnectionId = Context.ConnectionId,
            Message = $"User '{userId}' disconnected.",
            SentUtc = DateTime.UtcNow
        });

        await base.OnDisconnectedAsync(exception);
    }


    public async Task SendMessage(string user, string message)
    {
        await Clients.All
            .SendAsync("ReceiveMessage", Context.ConnectionId, user, message);

        // log the public message event
        var logEntry = new ChatLogEntry
        {
            Type = "Public",
            FromConnectionId = Context.ConnectionId,
            DisplayName = user,
            Message = message,
            SentUtc = DateTime.UtcNow
        };
        await auditService.AddAsync(logEntry);
    }

    public async Task SendPrivateMessage(string user, string toUser, string message)
    {
        if (UserConnections.TryGetValue(toUser, out var connections))
        {
            List<string> snapshot;
            lock (connections)
            {
                snapshot = connections.ToList();
            }
            // send message to all their active connections
            foreach (var connectionId in snapshot)
            {
                await Clients.Client(connectionId)
                    .SendAsync("ReceivePrivateMessage", Context.ConnectionId, user, message);

                // log the private message event
                var logEntry = new ChatLogEntry
                {
                    Type = "Private",
                    FromConnectionId = Context.ConnectionId,
                    ToConnectionId = connectionId,
                    DisplayName = user,
                    Message = message,
                    SentUtc = DateTime.UtcNow
                };
                await auditService.AddAsync(logEntry);
            }
        }        
    }

    public async Task SendGroupMessage(string groupName, string? user, string message)
    {
        await Clients.Group(groupName)
            .SendAsync("ReceiveGroupMessage", groupName, Context.ConnectionId, user, message);

        // log the group message event
        var logEntry = new ChatLogEntry
        {
            Type = "Group",
            FromConnectionId = Context.ConnectionId,
            GroupName = groupName,
            DisplayName = user,
            Message = message,
            SentUtc = DateTime.UtcNow
        };
        await auditService.AddAsync(logEntry);
    }

    public async Task JoinGroup(string user, string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        await Clients.Group(groupName)
            .SendAsync("SystemMessage", $"{Context.ConnectionId} - {user}: has joined the group {groupName}.");

        // log the join group event
        var logEntry = new ChatLogEntry
        {
            Type = "System",
            FromConnectionId = Context.ConnectionId,
            GroupName = groupName,
            DisplayName = user,
            Message = $"User '{user}' joined the group '{groupName}'.",
            SentUtc = DateTime.UtcNow
        };
        await auditService.AddAsync(logEntry);
    }

    public async Task LeaveGroup(string user, string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        await Clients.Group(groupName)
            .SendAsync("SystemMessage", $"{Context.ConnectionId} - {user}: has left the group {groupName}.");

        // log the leave group event
        var logEntry = new ChatLogEntry
        {
            Type = "System",
            FromConnectionId = Context.ConnectionId,
            GroupName = groupName,
            DisplayName = user,
            Message = $"User '{user}' left the group '{groupName}'.",
            SentUtc = DateTime.UtcNow
        };
        await auditService.AddAsync(logEntry);
    }

    public async Task AddUserToGroup(string user, string targetUserId, string groupName)
    {
        if (UserConnections.TryGetValue(targetUserId, out var connections))
        {
            List<string> snapshot;
            lock (connections)
            {
                snapshot = connections.ToList();
            }
            // Add the target user to the group for all their active connections
            foreach (var connectionId in snapshot)
            {
                await Groups.AddToGroupAsync(connectionId, groupName);
            }
            await Clients.User(targetUserId)
                .SendAsync("SystemMessage", $"You were added to '{groupName}' by {Context.ConnectionId} - {user}.");

            // log the add user to group event
            var logEntry = new ChatLogEntry
            {
                Type = "System",
                FromConnectionId = Context.ConnectionId,
                GroupName = groupName,
                DisplayName = user,
                Message = $"User '{user}' added '{targetUserId}' to the group '{groupName}'.",
                SentUtc = DateTime.UtcNow
            };
            await auditService.AddAsync(logEntry);
        }
    }

  

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





