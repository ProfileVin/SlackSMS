using Microsoft.Extensions.Caching.Memory;
using SlackSms.Application.Abstractions;

namespace SlackSms.Infrastructure.Slack;

/// <summary>
/// Renders Slack markup as SMS text, resolving mentions Slack sent as bare IDs to user, channel and
/// user group names. Lookups are cached for six hours, misses included, so a missing scope costs one call.
/// </summary>
public sealed class SlackTextRenderer(IUserDirectory users, ISlackNameLookup lookup, IMemoryCache cache)
    : IMessageTextRenderer
{
    private static readonly TimeSpan Retention = TimeSpan.FromHours(6);

    public async Task<string> ToPlainTextAsync(string text, CancellationToken ct)
    {
        var references = SlackTextFormatter.UnlabeledReferences(text);
        var resolved = await Task.WhenAll(references.Select(async r => (Reference: r, Name: await ResolveAsync(r, ct))));

        var names = resolved
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .ToDictionary(x => x.Reference, x => x.Name!);
        return SlackTextFormatter.ToPlainText(text, names);
    }

    private Task<string?> ResolveAsync(SlackReference reference, CancellationToken ct) => reference.Kind switch
    {
        SlackReferenceKind.User => users.GetDisplayNameAsync(reference.Id, ct),
        SlackReferenceKind.Channel => Cached("slack-channel:" + reference.Id, () => lookup.GetChannelNameAsync(reference.Id, ct)),
        SlackReferenceKind.UserGroup => Cached("slack-usergroup:" + reference.Id, () => lookup.GetUserGroupHandleAsync(reference.Id, ct)),
        _ => Task.FromResult<string?>(null),
    };

    private Task<string?> Cached(string key, Func<Task<string?>> lookupName) =>
        cache.GetOrCreateAsync(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Retention;
            return lookupName();
        });
}
