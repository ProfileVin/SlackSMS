using System.Text;

namespace SlackSms.Domain;

/// <summary>An SMS received on the bridge number.</summary>
public sealed record InboundSms(string MessageId, string From, string To, string Body, IReadOnlyList<InboundMedia> Media)
{
    // Right-to-left isolate / pop directional isolate: the wrapped line is laid out right to left
    // instead of inheriting left-to-right from the "SMS from" prefix.
    private const char Rli = '\u2067';
    private const char Pdi = '\u2069';

    /// <summary>
    /// The text posted to the chat channel: sender, body, then one media URL per line.
    /// A body with Hebrew or Arabic letters starts on its own line, and each such line is wrapped in a
    /// right-to-left isolate. The body's characters are never reordered.
    /// </summary>
    public string ToChannelText()
    {
        var text = new StringBuilder();
        if (ContainsRtl(Body))
        {
            text.Append($"SMS from {From}:");
            foreach (var line in Body.ReplaceLineEndings("\n").Split('\n'))
                text.Append('\n').Append(ContainsRtl(line) ? $"{Rli}{line}{Pdi}" : line);
        }
        else
        {
            text.Append($"SMS from {From}: {Body}");
        }

        foreach (var media in Media)
            text.Append('\n').Append(media.Url);
        return text.ToString();
    }

    private static bool ContainsRtl(string text) => text.Any(c =>
        c is (>= '\u0590' and <= '\u05FF')     // Hebrew
          or (>= '\u0600' and <= '\u06FF')     // Arabic
          or (>= '\u0750' and <= '\u077F')     // Arabic Supplement
          or (>= '\uFB1D' and <= '\uFDFF')     // Hebrew and Arabic presentation forms A
          or (>= '\uFE70' and <= '\uFEFC'));   // Arabic presentation forms B
}

/// <summary>A media part of an inbound MMS.</summary>
public sealed record InboundMedia(string Url, string ContentType)
{
    /// <summary>
    /// True for a plain-text part. Some phones and carriers put the message text there instead of the body.
    /// </summary>
    public bool IsText => ContentType.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase);
}
