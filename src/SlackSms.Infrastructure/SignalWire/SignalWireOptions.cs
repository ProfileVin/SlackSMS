using System.ComponentModel.DataAnnotations;

namespace SlackSms.Infrastructure.SignalWire;

public sealed class SignalWireOptions
{
    public const string SectionName = "SignalWire";

    /// <summary>Space name, e.g. "example" for example.signalwire.com (the full host is also accepted).</summary>
    [Required]
    public string Space { get; set; } = "";

    [Required]
    public string ProjectId { get; set; } = "";

    [Required]
    public string ApiToken { get; set; } = "";

    /// <summary>Signing Key from the SignalWire API page, used to validate incoming webhooks.</summary>
    [Required]
    public string SigningKey { get; set; } = "";

    /// <summary>The purchased SignalWire number in E.164 format (+15551234567).</summary>
    [Required]
    public string FromNumber { get; set; } = "";

    /// <summary>The phone number that receives SMS for every Slack message, in E.164 format.</summary>
    [Required]
    public string DestinationNumber { get; set; } = "";

    /// <summary>
    /// The exact public URL configured as the messaging webhook in SignalWire
    /// (e.g. https://bridge.example.com/signalwire/sms). The signature is computed over this URL.
    /// </summary>
    [Required, Url]
    public string WebhookPublicUrl { get; set; } = "";

    /// <summary>Only for troubleshooting. Never disable in production.</summary>
    public bool ValidateSignature { get; set; } = true;
}
