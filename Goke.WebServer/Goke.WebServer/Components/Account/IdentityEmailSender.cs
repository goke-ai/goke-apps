using Goke.WebServer.Data;
using Goke.WebServer.Services;
using Microsoft.AspNetCore.Identity;

namespace Goke.WebServer.Components.Account
{
    public sealed class IdentityEmailSender(ApplicationEmailSender appEmailSender) : IEmailSender<ApplicationUser>
    {
        public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
            SendEmailAsync(email, "Confirm your email", $"Please confirm your account by <a href='{confirmationLink}'>clicking here</a>.");

        public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
            SendEmailAsync(email, "Reset your password", $"Please reset your password by <a href='{resetLink}'>clicking here</a>.");

        public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
            SendEmailAsync(email, "Reset your password", $"Please reset your password using the following code: {resetCode}");

        private async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            await appEmailSender.SendEmailAsync(email, subject, htmlMessage);
        }
    }
}
