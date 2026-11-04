using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;

namespace Modulus.Flow.Connectors;

/// <summary>One email a workflow sends.</summary>
public sealed record EmailMessage(string To, string Subject, string Body);

/// <summary>Sends email for the email node. An app registers its own implementation, or uses <see cref="SmtpEmailSender"/>.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>The SMTP server and sender an app configures. Keep <see cref="Password"/> in the app's secret store, not in a workflow.</summary>
public sealed record SmtpSettings(string Host, string From)
{
    public int Port { get; init; } = 587;

    public bool UseSsl { get; init; } = true;

    public string? UserName { get; init; }

    public string? Password { get; init; }
}

/// <summary>Email over SMTP (the in-box <c>System.Net.Mail</c> client). Not run against a live server in this repository.</summary>
public sealed class SmtpEmailSender(SmtpSettings settings) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.UseSsl,
            Credentials = settings.UserName is null ? null : new NetworkCredential(settings.UserName, settings.Password),
        };
        using var mail = new MailMessage(settings.From, message.To, message.Subject, message.Body);
        await client.SendMailAsync(mail, cancellationToken);
    }
}

/// <summary>A Slack or Teams incoming webhook. The URL is the app's configuration; the channel is fixed by the webhook.</summary>
public sealed class SlackWebhook(HttpClient http, Uri webhook)
{
    public async Task PostAsync(string text, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync(webhook, JsonContent.Create(new { text }), cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"The chat webhook answered {(int)response.StatusCode}.", null, response.StatusCode);
    }
}
