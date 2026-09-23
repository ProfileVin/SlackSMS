using System.Text.Json.Serialization;

namespace SlackSms.Infrastructure.Slack;

/// <summary>Outer envelope of an Events API request.</summary>
public sealed record SlackEnvelope(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("challenge")] string? Challenge,
    [property: JsonPropertyName("event_id")] string? EventId,
    [property: JsonPropertyName("event")] SlackMessageEvent? Event);

public sealed record SlackMessageEvent(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("subtype")] string? Subtype,
    [property: JsonPropertyName("channel")] string? Channel,
    [property: JsonPropertyName("user")] string? User,
    [property: JsonPropertyName("bot_id")] string? BotId,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("ts")] string? Ts,
    [property: JsonPropertyName("files")] IReadOnlyList<object>? Files);
