using System.Threading.Channels;
using SlackSms.Application.Abstractions;
using SlackSms.Domain;

namespace SlackSms.Infrastructure.Messaging;

/// <summary>Unbounded in-process queue; messages still waiting are lost on restart.</summary>
public sealed class InMemoryOutboundQueue : IOutboundMessageQueue
{
    private readonly Channel<ChannelMessage> _channel =
        Channel.CreateUnbounded<ChannelMessage>(new UnboundedChannelOptions { SingleReader = true });

    public ValueTask EnqueueAsync(ChannelMessage message, CancellationToken ct) => _channel.Writer.WriteAsync(message, ct);

    public IAsyncEnumerable<ChannelMessage> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}
