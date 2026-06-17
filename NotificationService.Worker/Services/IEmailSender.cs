namespace NotificationService.Worker.Services
{
    public interface IEmailSender
    {
        Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken);
    }
}
