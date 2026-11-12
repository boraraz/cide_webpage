using Commerce.Application.Identity;

namespace Commerce.IntegrationTests.Orders;

public sealed class ScriptedEmailSender : IEmailSender
{
    private readonly object _gate = new();

    public int FailuresRemaining { get; set; }

    public List<EmailMessage> Sent { get; } = [];

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (FailuresRemaining > 0)
            {
                FailuresRemaining--;
                throw new InvalidOperationException("smtp-421");
            }

            Sent.Add(message);
        }

        return Task.CompletedTask;
    }
}
