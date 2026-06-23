namespace Goke.Web.Models
{
    public sealed class EmailSenderOptions
    {
        public const string SectionName = "EmailSender";

        public string? Host { get; set; }
        public int Port { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? FromAddress { get; set; }
        public string? FromName { get; set; }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(Host)
            && Port > 0
            && !string.IsNullOrWhiteSpace(FromAddress);
    }

}
