using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SlackSms.Application.Abstractions;
using SlackSms.Domain;

namespace SlackSms.Application.UseCases;

/// <summary>Delivers a channel message as SMS, with retries for transient failures.</summary>
public sealed class ForwardMessageToSms(
    ISmsSender sms,
    IUserDirectory users,
    IOptions<BridgeOptions> options,
    TimeProvider time,
    ILogger<ForwardMessageToSms> logger)
{
    public async Task HandleAsync(ChannelMessage message, CancellationToken ct)
    {
        var settings = options.Value;
        var retryDelays = settings.SmsRetryDelays;

        string? senderName = null;
        if (settings.IncludeSenderName && message.SenderId is { } senderId)
            senderName = await users.GetDisplayNameAsync(senderId, ct);

        var body = SmsBody.Compose(message.PlainText, message.HasAttachments, senderName);

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await sms.SendSmsAsync(settings.DestinationNumber, body, ct);
                return;
            }
            catch (SmsSendException ex) when (ex.IsTransient && attempt < retryDelays.Count)
            {
                logger.LogWarning(ex, "Transient SMS failure for {EventId}, retry {Attempt} in {Delay}",
                    message.Id, attempt + 1, retryDelays[attempt]);
                await Task.Delay(retryDelays[attempt], time, ct);
            }
        }
    }
}
