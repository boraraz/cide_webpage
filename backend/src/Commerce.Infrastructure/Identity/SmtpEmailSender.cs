using Commerce.Application.Identity;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Commerce.Infrastructure.Identity;

internal sealed partial class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var smtp = options.Value;
        if (string.IsNullOrWhiteSpace(smtp.Host))
        {
            LogMissingHost(logger);
            throw new InvalidOperationException("Smtp:Host is not configured.");
        }

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(smtp.FromName, smtp.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new TextPart("plain") { Text = message.Text };

        using var client = new SmtpClient();
        await client.ConnectAsync(smtp.Host, smtp.Port, MailKit.Security.SecureSocketOptions.None, cancellationToken);
        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Identity email was not sent because Smtp:Host is not configured.")]
    private static partial void LogMissingHost(ILogger logger);
}
