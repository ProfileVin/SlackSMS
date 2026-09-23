using SlackSms.Domain;

namespace SlackSms.Application.Abstractions;

/// <summary>Channel messages waiting to be sent as SMS.</summary>
public interface IOutboundMessageQueue
{
    ValueTask EnqueueAsync(ChannelMessage message, CancellationToken ct);

    IAsyncEnumerable<ChannelMessage> ReadAllAsync(CancellationToken ct);
}
