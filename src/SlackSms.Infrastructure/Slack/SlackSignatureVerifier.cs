using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SlackSms.Infrastructure.Slack;

/// <summary>
/// Verifies Slack request signatures:
/// https://api.slack.com/authentication/verifying-requests-from-slack
/// </summary>
public static class SlackSignatureVerifier
{
    public static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(5);

    public static bool IsValid(
        string signingSecret,
        string? timestampHeader,
        string? signatureHeader,
        string body,
        DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(timestampHeader) || string.IsNullOrEmpty(signatureHeader))
            return false;

        if (!long.TryParse(timestampHeader, NumberStyles.None, CultureInfo.InvariantCulture, out var unixSeconds))
            return false;

        var age = now - DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        if (age.Duration() > MaxClockSkew)
            return false;

        var baseString = $"v0:{timestampHeader}:{body}";
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingSecret), Encoding.UTF8.GetBytes(baseString));
        var expected = "v0=" + Convert.ToHexStringLower(hash);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(signatureHeader));
    }
}
