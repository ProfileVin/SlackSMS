namespace SlackSms.Application;

/// <summary>Bridge settings the use cases need, mapped from the Slack and SignalWire configuration sections.</summary>
public sealed class BridgeOptions
{
    /// <summary>The bridge's own SMS number (E.164); inbound SMS to any other number is ignored.</summary>
    public string OwnNumber { get; set; } = "";

    /// <summary>The phone number that receives SMS for every channel message (E.164).</summary>
    public string DestinationNumber { get; set; } = "";

    /// <summary>Prefix outgoing SMS with the sender's display name.</summary>
    public bool IncludeSenderName { get; set; }

    /// <summary>How long to wait before each retry of a transient SMS failure. Its length is the retry count.</summary>
    public IReadOnlyList<TimeSpan> SmsRetryDelays { get; set; } =
        [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)];
}
