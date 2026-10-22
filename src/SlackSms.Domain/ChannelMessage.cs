namespace SlackSms.Domain;

/// <summary>
/// A human-authored chat channel message awaiting delivery as SMS. <see cref="Text"/> is still in the chat
/// platform's markup; mentions are rendered to names when the SMS is composed.
/// </summary>
public sealed record ChannelMessage(string Id, string? SenderId, string Text, bool HasAttachments);
