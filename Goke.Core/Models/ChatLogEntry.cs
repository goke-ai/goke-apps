using System;
using System.Collections.Generic;
using System.Text;

namespace Goke.Core.Models
{
    public class ChatLogEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Type { get; set; } = default!; // Public, Private, Group, System
        public string FromConnectionId { get; set; } = default!;
        public string? ToConnectionId { get; set; }
        public string? GroupName { get; set; }
        public string? DisplayName { get; set; }
        public string Message { get; set; } = default!;
        public DateTime SentUtc { get; set; }
    }
}
