namespace Goke.Web.Models
{
    public class SeedUserOptions
    {
        public required string UserName { get; set; }
        public required string Email { get; set; }
        public string? Password { get; set; }
        public string? Role { get; set; }
    }
}