using System.Text;

namespace SlackSms.Domain;

/// <summary>An SMS received on the bridge number.</summary>
public sealed record InboundSms(string MessageId, string From, string To, string Body, IReadOnlyList<string> MediaUrls)
{
    /// <summary>The text posted to the chat channel: sender, body, then one media URL per line.</summary>
    public string ToChannelText()
    {
        var text = new StringBuilder($"SMS from {From}: {Body}");
        foreach (var url in MediaUrls)
            text.Append('\n').Append(url);
        return text.ToString();
    }
}
