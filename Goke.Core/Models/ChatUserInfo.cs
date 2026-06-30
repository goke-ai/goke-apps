using System;
using System.Collections.Generic;
using System.Text;

namespace Goke.Core.Models;

public sealed class ChatUserInfo 
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;     
}

public sealed class ConnectedClientInfo
{
    public string ConnectionId { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Alias { get; set; }
    public bool IsAuthenticated { get; set; }
}

public sealed class AliasInfo
{
    public string Alias { get; set; } = string.Empty;
    public string ConnectionId { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsAuthenticated { get; set; }
}