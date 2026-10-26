using System.ComponentModel.DataAnnotations;

namespace SlackSms.Infrastructure.Slack;

public sealed class SlackOptions
{
    public const string SectionName = "Slack";

    /// <summary>Bot User OAuth token (xoxb-...).</summary>
    [Required]
    public string BotToken { get; set; } = "";

    /// <summary>Signing secret from the Slack app's "Basic Information" page.</summary>
    [Required]
    public string SigningSecret { get; set; } = "";

    /// <summary>ID of the bridged channel (e.g. C0123456789), not its name.</summary>
    [Required]
    public string ChannelId { get; set; } = "";

    /// <summary>Prefix outgoing SMS with the Slack user's display name (default on). Requires the users:read scope.</summary>
    public bool IncludeSenderName { get; set; } = true;
}
