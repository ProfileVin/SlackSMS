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
    IMediaReader mediaReader,
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

        sms = await InlineTextMediaAsync(sms, ct);

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

    /// <summary>
    /// Moves text/plain media parts into the body. Some phones and carriers send the message text that way,
    /// leaving the body empty. A part that can't be downloaded stays as a link.
    /// </summary>
    private async Task<InboundSms> InlineTextMediaAsync(InboundSms sms, CancellationToken ct)
    {
        if (!sms.Media.Any(m => m.IsText))
            return sms;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(sms.Body))
            parts.Add(sms.Body);

        var remaining = new List<InboundMedia>();
        foreach (var media in sms.Media)
        {
            var text = media.IsText ? await mediaReader.ReadTextAsync(media.Url, ct) : null;
            if (text is null)
                remaining.Add(media);
            else if (!string.IsNullOrWhiteSpace(text))
                parts.Add(text.Trim());
        }

        return sms with { Body = string.Join('\n', parts), Media = remaining };
    }
}
