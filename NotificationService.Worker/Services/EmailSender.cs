using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;
using NotificationService.Worker.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotificationService.Worker.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly SmtpOptions _options;
        private readonly ILogger<EmailSender> _logger;

        public EmailSender(IOptions<SmtpOptions> options, ILogger<EmailSender> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Order System", _options.Username));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            //message.Body = new TextPart("plain") { Text = body };

            message.Body = new TextPart(TextFormat.Plain)
            {
                Text = body,
                ContentTransferEncoding = ContentEncoding.Base64
            };
            using var client = new SmtpClient();

            await client.ConnectAsync(_options.Host, _options.Port, _options.EnableSsl, cancellationToken);
            await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Email sent to {Email} with subject {Subject}", toEmail, subject);
        }
    }
}
