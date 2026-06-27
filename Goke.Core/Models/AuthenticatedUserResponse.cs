using Goke.Core.Models.Goke.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Goke.Core.Models
{

    namespace Goke.Core.Models
    {
        public class AuthenticatedUserClaimResponse
        {
            [JsonPropertyName("type")]
            public required string Type { get; set; }

            [JsonPropertyName("value")]
            public required string Value { get; set; }
        }
    }

    public class AuthenticatedUserResponse
    {
        [JsonPropertyName("userId")]
        public required string UserId { get; set; }

        [JsonPropertyName("email")]
        public required string Email { get; set; }

        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("roles")]
        public List<string> Roles { get; set; } = [];

        [JsonPropertyName("claims")]
        public List<AuthenticatedUserClaimResponse> Claims { get; set; } = [];
    }
}
