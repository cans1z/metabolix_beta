using Google.Apis.Auth;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Metabolix.Api.Services;

public record GoogleUser(string Subject, string Email, string? Name, bool EmailVerified);

public interface IGoogleTokenValidator
{
    bool IsConfigured { get; }

    /// <returns>null, если токен поддельный, просрочен или выдан не нашему приложению.</returns>
    Task<GoogleUser?> ValidateAsync(string idToken);
}

/// <summary>Проверяет подпись id_token по ключам Google и что токен выдан нашему client id.</summary>
public class GoogleTokenValidator(IConfiguration config) : IGoogleTokenValidator
{
    // Пустая переменная окружения превращается в пустую строку, такие значения отбрасываем.
    private readonly string[] _clientIds = (config.GetSection("Google:ClientIds").Get<string[]>() ?? [])
        .Where(id => !string.IsNullOrWhiteSpace(id)).ToArray();

    public bool IsConfigured => _clientIds.Length > 0;

    public async Task<GoogleUser?> ValidateAsync(string idToken)
    {
        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken,
                new GoogleJsonWebSignature.ValidationSettings { Audience = _clientIds });
            return new GoogleUser(payload.Subject, payload.Email, payload.Name, payload.EmailVerified);
        }
        catch (InvalidJwtException)
        {
            return null;
        }
    }
}

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body);
}

public class SmtpEmailSender(IConfiguration config) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string body)
    {
        var smtp = config.GetSection("Smtp");
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(smtp["From"]!));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync(smtp["Host"]!, smtp.GetValue("Port", 1025), SecureSocketOptions.Auto);
        if (!string.IsNullOrEmpty(smtp["User"]))
            await client.AuthenticateAsync(smtp["User"]!, smtp["Password"] ?? "");
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}

/// <summary>Без SMTP письмо пишется в лог, чтобы можно было проверить сброс пароля локально.</summary>
public class LogEmailSender(ILogger<LogEmailSender> log) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body)
    {
        log.LogInformation("Письмо для {To}: {Subject}\n{Body}", to, subject, body);
        return Task.CompletedTask;
    }
}
