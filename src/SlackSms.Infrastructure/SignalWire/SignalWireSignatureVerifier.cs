using System.Security.Cryptography;
using System.Text;

namespace SlackSms.Infrastructure.SignalWire;

/// <summary>
/// Verifies the signature SignalWire sends on LaML/cXML webhooks (Twilio-compatible scheme):
/// Base64(HMAC-SHA1(signingKey, url + concat(sorted form params as key+value))).
/// </summary>
public static class SignalWireSignatureVerifier
{
    public static bool IsValid(
        string signingKey,
        string url,
        IEnumerable<KeyValuePair<string, IEnumerable<string>>> formParams,
        string? signature)
    {
        if (string.IsNullOrEmpty(signature))
            return false;

        var expected = ComputeSignature(signingKey, url, formParams);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(signature));
    }

    public static string ComputeSignature(
        string signingKey,
        string url,
        IEnumerable<KeyValuePair<string, IEnumerable<string>>> formParams)
    {
        var data = new StringBuilder(url);
        foreach (var (key, values) in formParams.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            foreach (var value in values.Order(StringComparer.Ordinal))
                data.Append(key).Append(value);
        }

        var hash = HMACSHA1.HashData(Encoding.UTF8.GetBytes(signingKey), Encoding.UTF8.GetBytes(data.ToString()));
        return Convert.ToBase64String(hash);
    }
}
