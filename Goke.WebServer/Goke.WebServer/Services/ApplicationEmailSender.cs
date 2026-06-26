using System.Net;
using System.Net.Mail;
using Goke.Core.Models;
using Microsoft.Extensions.Options;

namespace Goke.WebServer.Services
{
   
    public sealed class ApplicationEmailSender(
        IOptions<EmailSenderOptions> options,
        IHostEnvironment environment,
        ILogger<ApplicationEmailSender> logger)
    {
        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var emailOptions = options.Value;
            if (!emailOptions.IsConfigured)
            {
                LogDevelopmentEmail(email, subject, htmlMessage, "Email delivery is not configured.");
                return;
            }

            try
            {
                using var client = new SmtpClient(emailOptions.Host!, emailOptions.Port)
                {
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    EnableSsl = emailOptions.EnableSsl,
                    UseDefaultCredentials = false
                };

                if (!string.IsNullOrWhiteSpace(emailOptions.Username))
                {
                    client.Credentials = new NetworkCredential(emailOptions.Username, emailOptions.Password);
                }

                using var message = new MailMessage
                {
                    From = new MailAddress(emailOptions.FromAddress!, emailOptions.FromName),
                    Subject = subject,
                    Body = htmlMessage,
                    IsBodyHtml = true
                };

                message.To.Add(email);
                await client.SendMailAsync(message);
            }
            catch (SmtpException ex) when (environment.IsDevelopment())
            {
                LogDevelopmentEmail(email, subject, htmlMessage, $"SMTP delivery failed: {ex.Message}");
            }
        }

        private void LogDevelopmentEmail(string email, string subject, string htmlMessage, string reason)
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(reason);
            }

            logger.LogWarning("{Reason} Development email to {Email} with subject {Subject}: {HtmlMessage}", reason, email, subject, htmlMessage);
        }
    }

}
