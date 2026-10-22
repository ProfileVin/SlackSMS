using System.Text;

namespace SlackSms.Domain;

/// <summary>An SMS received on the bridge number.</summary>
public sealed record InboundSms(string MessageId, string From, string To, string Body, IReadOnlyList<InboundMedia> Media)
{
    /// <summary>The text posted to the chat channel: sender, body, then one media URL per line.</summary>
    public string ToChannelText()
    {
        var text = new StringBuilder($"SMS from {From}: {Body}");
        foreach (var media in Media)
            text.Append('\n').Append(media.Url);
        return text.ToString();
    }
}

/// <summary>A media part of an inbound MMS.</summary>
public sealed record InboundMedia(string Url, string ContentType)
{
    /// <summary>
    /// True for a plain-text part. Some phones and carriers put the message text there instead of the body.
    /// </summary>
    public bool IsText => ContentType.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase);
}
