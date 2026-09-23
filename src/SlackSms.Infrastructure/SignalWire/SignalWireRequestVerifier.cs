using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace SlackSms.Infrastructure.SignalWire;

/// <summary>
/// Injectable wrapper around <see cref="SignalWireSignatureVerifier"/> that supplies the signing key
/// and the webhook URL, and adds the AccountSid check as defence in depth.
/// </summary>
public sealed class SignalWireRequestVerifier(IOptions<SignalWireOptions> options) : ISignalWireRequestVerifier
{
    public SignalWireRequestStatus Verify(IFormCollection form, string? signature)
    {
        var settings = options.Value;

        if (settings.ValidateSignature)
        {
            var formParams = form.Select(f =>
                new KeyValuePair<string, IEnumerable<string>>(f.Key, f.Value.Select(v => v ?? "")));

            if (!SignalWireSignatureVerifier.IsValid(settings.SigningKey, settings.WebhookPublicUrl, formParams, signature))
                return SignalWireRequestStatus.InvalidSignature;
        }

        // Defence in depth: the webhook must belong to our SignalWire project.
        return string.Equals(form["AccountSid"].ToString(), settings.ProjectId, StringComparison.Ordinal)
            ? SignalWireRequestStatus.Valid
            : SignalWireRequestStatus.WrongAccount;
    }
}
