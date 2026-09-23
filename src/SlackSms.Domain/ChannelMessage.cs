namespace SlackSms.Domain;

/// <summary>A human-authored chat channel message, already converted to plain text, awaiting delivery as SMS.</summary>
public sealed record ChannelMessage(string Id, string? SenderId, string PlainText, bool HasAttachments);
