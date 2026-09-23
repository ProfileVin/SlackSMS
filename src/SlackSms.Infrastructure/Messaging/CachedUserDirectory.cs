using Microsoft.Extensions.Caching.Memory;
using SlackSms.Application.Abstractions;

namespace SlackSms.Infrastructure.Messaging;

/// <summary>Caches display names so each Slack user is looked up at most every six hours.</summary>
public sealed class CachedUserDirectory(IUserDirectory inner, IMemoryCache cache) : IUserDirectory
{
    private static readonly TimeSpan Retention = TimeSpan.FromHours(6);

    public Task<string?> GetDisplayNameAsync(string userId, CancellationToken ct) =>
        cache.GetOrCreateAsync("slack-user:" + userId, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Retention;
            return inner.GetDisplayNameAsync(userId, ct);
        });
}
