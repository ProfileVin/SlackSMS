namespace SlackSms.Infrastructure.Slack;

/// <summary>Best-effort Slack name lookups for rendering mentions; each returns null when unavailable.</summary>
public interface ISlackNameLookup
{
    /// <summary>Channel name without the '#'. Needs channels:read (groups:read and membership for private channels).</summary>
    Task<string?> GetChannelNameAsync(string channelId, CancellationToken ct);

    /// <summary>User group handle without the '@'. Needs usergroups:read.</summary>
    Task<string?> GetUserGroupHandleAsync(string groupId, CancellationToken ct);
}
