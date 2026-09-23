namespace SlackSms.Domain;

/// <summary>Rules for the body of an outgoing SMS.</summary>
public static class SmsBody
{
    /// <summary>Max body length accepted by SignalWire's messaging API.</summary>
    public const int MaxLength = 1600;

    /// <summary>Builds the SMS body for a channel message: optional sender prefix, attachment marker, length limit.</summary>
    public static string Compose(string plainText, bool hasAttachments, string? senderName)
    {
        var text = plainText.Trim();
        if (hasAttachments)
            text = string.IsNullOrEmpty(text) ? "[attachment]" : text + " [attachment]";

        if (!string.IsNullOrEmpty(senderName))
            text = $"{senderName}: {text}";

        return Truncate(text);
    }

    public static string Truncate(string text, int maxLength = MaxLength) =>
        text.Length <= maxLength ? text : text[..(maxLength - 1)] + "…";
}
