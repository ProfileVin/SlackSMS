using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SlackSms.Application.Abstractions;
using SlackSms.Domain;

namespace SlackSms.Application.UseCases;

public enum RelayResult
{
    Posted,
    IgnoredWrongNumber,
    Failed,
}

/// <summary>Posts an SMS received on the bridge number to the chat channel.</summary>
public sealed class RelayInboundSms(
    IChannelClient channel,
    IOptions<BridgeOptions> options,
    ILogger<RelayInboundSms> logger)
{
    public async Task<RelayResult> HandleAsync(InboundSms sms, CancellationToken ct)
    {
        var ownNumber = options.Value.OwnNumber;
        if (!string.Equals(sms.To, ownNumber, StringComparison.Ordinal))
        {
            logger.LogWarning("Ignoring SMS addressed to {To}, expected {FromNumber}", sms.To, ownNumber);
            return RelayResult.IgnoredWrongNumber;
        }

        try
        {
            await channel.PostAsync(sms.ToChannelText(), ct);
            logger.LogInformation("Posted SMS {MessageSid} to Slack", sms.MessageId);
            return RelayResult.Posted;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to post SMS {MessageSid} from {From} to Slack", sms.MessageId, sms.From);
            return RelayResult.Failed;
        }
    }
}
