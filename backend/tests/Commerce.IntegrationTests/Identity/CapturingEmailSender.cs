using System.Collections.Concurrent;
using Commerce.Application.Identity;

namespace Commerce.IntegrationTests.Identity;

public sealed class CapturingEmailSender : IEmailSender
{
    public ConcurrentQueue<EmailMessage> Messages { get; } = new();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        Messages.Enqueue(message);
        return Task.CompletedTask;
    }

    public void Clear() => Messages.Clear();

    public string LatestToken()
    {
        var body = Messages.Last().Text;
        var token = body.Split("token=", StringSplitOptions.None).Last().Split([' ', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries).First();
        return Uri.UnescapeDataString(token);
    }
}
