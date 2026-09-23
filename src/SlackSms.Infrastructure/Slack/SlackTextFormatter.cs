using System.Text.RegularExpressions;

namespace SlackSms.Infrastructure.Slack;

/// <summary>Converts between Slack's mrkdwn escaping and plain SMS text.</summary>
public static partial class SlackTextFormatter
{
    [GeneratedRegex(@"<([^<>]+)>")]
    private static partial Regex AngleToken();

    /// <summary>Slack markup (links, mentions, entities) to plain text.</summary>
    public static string ToPlainText(string slackText)
    {
        var text = AngleToken().Replace(slackText, m =>
        {
            var inner = m.Groups[1].Value;
            var pipe = inner.IndexOf('|');
            var target = pipe >= 0 ? inner[..pipe] : inner;
            var label = pipe >= 0 ? inner[(pipe + 1)..] : null;

            return target[0] switch
            {
                '@' => label is not null ? "@" + label.TrimStart('@') : target,         // <@U123> / <@U123|bob>
                '#' => label is not null ? "#" + label : target,                        // <#C123|general>
                '!' => label ?? "@" + target[1..].Split('^')[0],                         // <!here>, <!subteam^ID|@team>
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
}
