using System.Text.RegularExpressions;

namespace SlackSms.Infrastructure.Slack;

public enum SlackReferenceKind { User, Channel, UserGroup }

/// <summary>A user, channel or user group mentioned by ID in Slack markup.</summary>
public readonly record struct SlackReference(SlackReferenceKind Kind, string Id);

/// <summary>Converts between Slack's mrkdwn escaping and plain SMS text.</summary>
public static partial class SlackTextFormatter
{
    private const string SubteamPrefix = "!subteam^";

    [GeneratedRegex(@"<([^<>]+)>")]
    private static partial Regex AngleToken();

    /// <summary>Mentions Slack sent without a name label (e.g. <c>&lt;@U123&gt;</c>), which need a lookup to render.</summary>
    public static IReadOnlySet<SlackReference> UnlabeledReferences(string slackText)
    {
        var references = new HashSet<SlackReference>();
        foreach (Match m in AngleToken().Matches(slackText))
        {
            var (target, label) = Split(m.Groups[1].Value);
            if (label is null && Classify(target) is { } reference)
                references.Add(reference);
        }
        return references;
    }

    /// <summary>
    /// Slack markup (links, mentions, entities) to plain text. Mentions use Slack's own label first, then
    /// <paramref name="names"/>, then fall back to the raw ID.
    /// </summary>
    public static string ToPlainText(string slackText, IReadOnlyDictionary<SlackReference, string>? names = null)
    {
        var text = AngleToken().Replace(slackText, m =>
        {
            var (target, label) = Split(m.Groups[1].Value);
            if (target.Length == 0)
                return label ?? "";

            var reference = Classify(target);
            var name = label ?? (reference is { } r && names?.GetValueOrDefault(r) is { } resolved ? resolved : null);
            var id = reference?.Id;

            return reference?.Kind switch
            {
                SlackReferenceKind.User => "@" + (name?.TrimStart('@') ?? id),          // <@U123> / <@U123|bob>
                SlackReferenceKind.Channel => "#" + (name?.TrimStart('#') ?? "channel"), // <#C123|general> / <#C123|>
                SlackReferenceKind.UserGroup => "@" + (name?.TrimStart('@') ?? id),     // <!subteam^S123> / <!subteam^S123|@devs>
                _ when target[0] == '!' => label ?? "@" + target[1..].Split('^')[0],                   // <!here>, <!date^…|fallback>
                _ when target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                    => label ?? target["mailto:".Length..],
                _ => label is not null && label != target ? $"{label} ({target})" : target,
            };
        });

        return text.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&");
    }

    /// <summary>Plain text to Slack-safe text, so an SMS can't inject @channel pings or fake links.</summary>
    public static string EscapeForSlack(string plainText) =>
        plainText.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    /// <summary>Splits "target|label"; an empty label (Slack sends <c>&lt;#C123|&gt;</c>) counts as none.</summary>
    private static (string Target, string? Label) Split(string inner)
    {
        var pipe = inner.IndexOf('|');
        if (pipe < 0)
            return (inner, null);
        var label = inner[(pipe + 1)..];
        return (inner[..pipe], label.Length > 0 ? label : null);
    }

    private static SlackReference? Classify(string target) => target switch
    {
        ['@', _, ..] => new SlackReference(SlackReferenceKind.User, target[1..]),
        ['#', _, ..] => new SlackReference(SlackReferenceKind.Channel, target[1..]),
        _ when target.StartsWith(SubteamPrefix, StringComparison.Ordinal) && target.Length > SubteamPrefix.Length
            => new SlackReference(SlackReferenceKind.UserGroup, target[SubteamPrefix.Length..]),
        _ => null,
    };
}
