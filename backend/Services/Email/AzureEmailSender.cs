using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Options;

namespace backend.Services.Email;

public sealed class AzureEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendAsync(
        string toAddress,
        string subject,
        string htmlBody,
        string? plainTextBody = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsValid())
        {
            throw new InvalidOperationException(
                "Email sending is not configured. Set Email__ConnectionString and Email__SenderAddress.");
        }

        var client = new EmailClient(_options.ConnectionString);
        var content = new EmailContent(subject)
        {
            Html = htmlBody,
            PlainText = plainTextBody,
        };
        var recipients = new EmailRecipients([new EmailAddress(toAddress)]);
        var message = new EmailMessage(_options.SenderAddress, recipients, content);

        await client.SendAsync(WaitUntil.Completed, message, cancellationToken);
    }
}
