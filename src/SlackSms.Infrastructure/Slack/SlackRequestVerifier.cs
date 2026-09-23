using Microsoft.Extensions.Options;

namespace SlackSms.Infrastructure.Slack;

/// <summary>Injectable wrapper around <see cref="SlackSignatureVerifier"/> that supplies the secret and the clock.</summary>
public sealed class SlackRequestVerifier(IOptions<SlackOptions> options, TimeProvider time) : ISlackRequestVerifier
{
    public bool IsValid(string? timestampHeader, string? signatureHeader, string body) =>
        SlackSignatureVerifier.IsValid(
            options.Value.SigningSecret,
            timestampHeader,
            signatureHeader,
            body,
            time.GetUtcNow());
}
