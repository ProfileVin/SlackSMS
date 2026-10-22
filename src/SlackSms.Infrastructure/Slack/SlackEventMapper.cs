using System.Diagnostics.CodeAnalysis;
using SlackSms.Domain;

namespace SlackSms.Infrastructure.Slack;

/// <summary>Turns Slack message events into channel messages for the bridge.</summary>
public static class SlackEventMapper
{
    public static bool TryMap(
        SlackMessageEvent evt,
        string eventId,
        string channelId,
        [NotNullWhen(true)] out ChannelMessage? message)
    {
        message = ShouldForward(evt, channelId)
            ? new ChannelMessage(eventId, evt.User, evt.Text ?? "", evt.Files is { Count: > 0 })
            : null;
        return message is not null;
    }

    /// <summary>
    /// Only new, human-authored messages in the bridged channel. Skipping bot messages also prevents
    /// SMS posted into Slack by this app from being sent back out as SMS.
    /// </summary>
    internal static bool ShouldForward(SlackMessageEvent evt, string channelId) =>
        evt.Type == "message"
        && evt.Channel == channelId
        && evt.Subtype is null or "file_share" or "thread_broadcast"
        && evt.BotId is null
        && !string.IsNullOrEmpty(evt.User)
        && (!string.IsNullOrWhiteSpace(evt.Text) || evt.Files is { Count: > 0 });
}
