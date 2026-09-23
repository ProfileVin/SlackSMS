using Microsoft.Extensions.Caching.Memory;
using SlackSms.Application.Abstractions;

namespace SlackSms.Infrastructure.Messaging;

/// <summary>Remembers event IDs in memory for an hour, long enough to outlast Slack's retries.</summary>
public sealed class MemoryCacheEventDeduplicator(IMemoryCache cache) : IEventDeduplicator
{
    private static readonly TimeSpan Retention = TimeSpan.FromHours(1);
    private readonly Lock _lock = new();

    public bool TryRegister(string eventId)
    {
        var key = "slack-event:" + eventId;
        lock (_lock)
        {
            if (cache.TryGetValue(key, out _))
                return false;

            cache.Set(key, true, Retention);
            return true;
        }
    }
}
