using Microsoft.Extensions.Logging;
using SlackSms.Application.Abstractions;
using SlackSms.Domain;

namespace SlackSms.Application.UseCases;

/// <summary>Accepts a channel message for SMS delivery, ignoring events already seen.</summary>
public sealed class QueueChannelMessage(
    IEventDeduplicator deduplicator,
    IOutboundMessageQueue queue,
    ILogger<QueueChannelMessage> logger)
{
    public async Task HandleAsync(ChannelMessage message, CancellationToken ct)
    {
        if (!deduplicator.TryRegister(message.Id))
            return;

        await queue.EnqueueAsync(message, ct);
        logger.LogInformation("Queued Slack event {EventId} for SMS", message.Id);
    }
}
